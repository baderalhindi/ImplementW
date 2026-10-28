import { screen, within } from '@testing-library/react';
import { describe, expect, test } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import { inboxItem } from '@/test/approvalFixtures.ts';
import { catalogueFor, dashboardView, widget, withDashboards } from '@/test/dashboardFixtures.ts';
import { sessionFor } from '@/test/identityAccessFixtures.ts';
import { type MockApi, mockApi, page } from '@/test/mockApi.ts';
import { notification } from '@/test/notificationFixtures.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';

// The Home (the 24 Oct 2026 review, H2 and M-1; the 27 Oct 2026 review): a welcome banner with the ways in, the Portfolio Dashboard's
// headline figures for its audience, and the person's own work, each card offered on the permission its list reads under; the
// platform's health for the administrator.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);

/** What the Home's cards read, answered empty unless a test says otherwise; no dashboard is offered. */
function homeApi(): MockApi {
  return mockApi()
    .on('GET', /^\/dashboards$/, { body: page([]) })
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

/** The banner's ways in. */
function actions(): HTMLElement {
  return screen.getByRole('navigation', { name: en('home.actions.title') });
}

describe('the Home', () => {
  test('a Portfolio Manager sees what waits for its decision and its own requests, and is offered the portfolio analysis', async () => {
    homeApi();
    renderApp({ path: '/', session: sessionFor(['R02']) });

    expect(
      await screen.findByRole('heading', {
        level: 1,
        name: en('home.title', { name: 'Test Administrator' }),
      }),
    ).toBeTruthy();
    const approvals = await screen.findByRole('region', { name: en('home.approvals.title') });
    expect(await within(approvals).findByText('2')).toBeTruthy();
    expect(card(en('home.requests.title'))).toBeTruthy();
    expect(card(en('home.notifications.title'))).toBeTruthy();
    // No task of its own to update: the tasks card is the doers'.
    expect(screen.queryByRole('region', { name: en('home.tasks.title') })).toBeNull();
    expect(
      within(actions())
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
      within(actions()).queryByRole('link', {
        name: en('common.menu.portfolioDashboard'),
      }),
    ).toBeNull();
    expect(within(actions()).getByRole('link', { name: en('projects.nav.create') })).toBeTruthy();
    // No Portfolio Dashboard for its roles: no portfolio snapshot.
    expect(screen.queryByRole('heading', { name: en('home.portfolio.title') })).toBeNull();
  });

  test("the Portfolio Dashboard's audience sees its headline figures, health in words, and its projects by location", async () => {
    withDashboards(homeApi(), {
      catalogue: catalogueFor(['R02']),
      views: {
        PORTFOLIO: dashboardView('PORTFOLIO', {
          widgets: [
            widget({
              code: 'PROJECTS_BY_LIFECYCLE',
              widgetType: 'STATUS_DISTRIBUTION',
              projectionCode: 'PROJECT.LIFECYCLE_STATE',
              data: {
                state: null,
                figures: [],
                distribution: [
                  { key: 'ACTIVE', label: null, count: 7 },
                  { key: 'SUSPENDED', label: null, count: 1 },
                ],
                series: [],
              },
            }),
            widget({
              code: 'PUBLISHED_HEALTH',
              widgetType: 'STATUS_DISTRIBUTION',
              projectionCode: 'PROGRESS.PUBLISHED_PROGRESS_SNAPSHOT',
              data: {
                state: null,
                figures: [],
                distribution: [
                  { key: 'GREEN', label: null, count: 4 },
                  { key: 'RED', label: null, count: 2 },
                ],
                series: [],
              },
            }),
          ],
          locations: [{ regionItemId: 'region-east', cityItemId: null, projectCount: 8 }],
          regionOptions: ['region-east'],
          places: [
            {
              id: 'region-east',
              label: { en: 'Eastern region', ar: 'المنطقة الشرقية' },
              parentItemId: null,
            },
          ],
        }),
      },
    });
    renderApp({ path: '/', session: sessionFor(['R02']) });

    const snapshot = await screen.findByRole('region', { name: en('home.portfolio.title') });
    const projects = await within(snapshot).findByText(en('home.portfolio.projects'));
    expect(projects.parentElement?.textContent).toBe(`8${en('home.portfolio.projects')}`);
    expect(within(snapshot).getByText(en('progress.health.GREEN'))).toBeTruthy();
    expect(within(snapshot).getByText(en('progress.health.RED'))).toBeTruthy();
    expect(
      within(snapshot).getByRole('link', { name: 'Eastern region' }).getAttribute('href'),
    ).toBe('/dashboards?dashboard=PORTFOLIO&region=region-east');
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
