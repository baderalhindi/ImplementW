import { screen, within } from '@testing-library/react';
import { describe, expect, test } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import { inboxItem } from '@/test/approvalFixtures.ts';
import { sessionFor } from '@/test/identityAccessFixtures.ts';
import { type MockApi, mockApi, page } from '@/test/mockApi.ts';
import { notification } from '@/test/notificationFixtures.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';

// The Home (the 24 Oct 2026 review, H2 and M-1): the person's own work, each card offered on the permission its list reads under,
// and the platform's health for the administrator — never the portfolio's counts, which are the Portfolio menu's.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);

/** What the Home's cards read, answered empty unless a test says otherwise. */
function homeApi(): MockApi {
  return mockApi()
    .on('GET', /^\/approval-tasks$/, {
      body: page([inboxItem(), inboxItem({ taskId: 'second-task' })]),
    })
    .on('GET', /^\/approval-instances$/, { body: page([]) })
    .on('GET', /^\/projects$/, { body: page([]) })
    .on('GET', /^\/notifications$/, { body: page([notification()]) })
    .on('GET', /^\/external-update-requests$/, { body: page([]) })
    .on('GET', /^\/integrations$/, { body: { environment: 'test', items: [] } })
    .on('GET', /^\/audit-ingestion-status$/, {
      body: {
        pendingMessages: 2,
        failingMessages: 0,
        oldestPendingOccurredAt: null,
        lastError: null,
        eventsStored: 10,
        storeHeadRecordedAt: null,
        projectionLag: 1,
        projectionCheckpointRecordedAt: null,
      },
    })
    .on('GET', /^\/users$/, { body: { items: [], page: 1, pageSize: 1, totalCount: 4 } });
}

function card(title: string): HTMLElement {
  return screen.getByRole('region', { name: title });
}

describe('the Home', () => {
  test('a Portfolio Manager sees what waits for its decision and its own requests, and is offered the portfolio analysis', async () => {
    homeApi();
    renderApp({ path: '/', session: sessionFor(['R02']) });

    expect(await screen.findByRole('heading', { level: 1, name: en('home.title') })).toBeTruthy();
    const approvals = await screen.findByRole('region', { name: en('home.approvals.title') });
    expect(await within(approvals).findByText('2')).toBeTruthy();
    expect(card(en('home.requests.title'))).toBeTruthy();
    expect(card(en('home.notifications.title'))).toBeTruthy();
    // No task of its own to update: the tasks card is the doers'.
    expect(screen.queryByRole('region', { name: en('home.tasks.title') })).toBeNull();
    expect(
      within(card(en('home.actions.title')))
        .getByRole('link', { name: en('common.menu.portfolioDashboard') })
        .getAttribute('href'),
    ).toBe('/dashboards?dashboard=PORTFOLIO');
    // The Home is not a dashboard: it reads none.
    expect(
      screen.queryByRole('heading', { level: 1, name: en('dashboards.landing.R02') }),
    ).toBeNull();
  });

  test('a Project Manager sees its tasks and its requests, and nothing waits for a decision it cannot take', async () => {
    homeApi();
    renderApp({ path: '/', session: sessionFor(['R04']) });

    expect(await screen.findByRole('region', { name: en('home.tasks.title') })).toBeTruthy();
    expect(
      await within(card(en('home.tasks.title'))).findByText(en('home.tasks.none')),
    ).toBeTruthy();
    expect(card(en('home.requests.title'))).toBeTruthy();
    expect(screen.queryByRole('region', { name: en('home.approvals.title') })).toBeNull();
    expect(
      within(card(en('home.actions.title'))).queryByRole('link', {
        name: en('common.menu.portfolioDashboard'),
      }),
    ).toBeNull();
    expect(
      within(card(en('home.actions.title'))).getByRole('link', { name: en('projects.nav.create') }),
    ).toBeTruthy();
  });

  test('the System Administrator sees the platform’s health and no business work (the review, M-1)', async () => {
    homeApi();
    renderApp({ path: '/', session: sessionFor(['R01']) });

    expect(
      await screen.findByRole('heading', { level: 1, name: en('home.health.title') }),
    ).toBeTruthy();
    expect(
      await screen.findByRole('region', { name: en('home.health.integrations.title') }),
    ).toBeTruthy();
    expect(card(en('home.health.audit.title'))).toBeTruthy();
    expect(await within(card(en('home.health.people.title'))).findByText('8')).toBeTruthy();
    expect(screen.queryByRole('region', { name: en('home.approvals.title') })).toBeNull();
    expect(screen.queryByRole('region', { name: en('home.notifications.title') })).toBeNull();
  });

  test('has no axe violation', async () => {
    homeApi();
    renderApp({ path: '/', session: sessionFor(['R02']) });
    await screen.findByRole('region', { name: en('home.approvals.title') });
    expect(describeViolations(await accessibilityViolations())).toBe('');
  });
});
