import { cleanup, screen, waitFor } from '@testing-library/react';
import { describe, expect, test } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import { APPROVER_ID, sessionAs, withApprovalNames } from '@/test/approvalFixtures.ts';
import { catalogueFor, withDashboards } from '@/test/dashboardFixtures.ts';
import { mockApi, page } from '@/test/mockApi.ts';
import { renderApp } from '@/test/renderApp.tsx';
import { findMainMenu, menuLink, openMenuTargets } from '@/test/shell.ts';

import { approvalNavigation } from './routes.tsx';

// The WF-11 screens are offered on the approval permissions — the inbox and delegations to a decider, the requests to whoever
// views approvals (the 24 Oct 2026 review, C1 and C3); what each person may see or decide is the API's answer (TASK-035).

const en = (key: string) => translate('en', key);

describe('approval routes', () => {
  test.each(approvalNavigation.map((item) => item.to))(
    '%s without a session goes to sign-in and calls no API',
    async (path) => {
      const api = mockApi();
      const { router } = renderApp({ path });

      expect(
        await screen.findByRole('heading', { level: 1, name: en('identityAccess.signIn.title') }),
      ).toBeTruthy();
      expect(router.state.location.pathname).toBe('/sign-in');
      expect(api.requests).toHaveLength(0);
    },
  );

  test('a decider gets the whole approvals navigation, and no administration one', async () => {
    withApprovalNames(mockApi()).on('GET', /^\/approval-tasks$/, { body: page([]) });
    renderApp({ path: '/approvals', session: sessionAs(APPROVER_ID, ['R02']) });

    expect(
      await screen.findByRole('heading', { level: 1, name: en('approvals.inbox.title') }),
    ).toBeTruthy();
    expect(openMenuTargets('common.menu.requestsApprovals')).toEqual(
      expect.arrayContaining(approvalNavigation.map((item) => item.to)),
    );
    expect(menuLink('common.menu.administration')).toBeNull();
  });

  test('from the home page, the shell links a decider to the inbox, and a Project Manager to its requests', async () => {
    withDashboards(mockApi(), { catalogue: catalogueFor(['R02']) });
    const decider = renderApp({ path: '/', session: sessionAs(APPROVER_ID, ['R02']) });
    await findMainMenu();
    await waitFor(() => {
      expect(menuLink('common.menu.requestsApprovals')?.getAttribute('href')).toBe('/approvals/inbox');
    });
    decider.unmount();
    cleanup();

    withDashboards(mockApi(), { catalogue: catalogueFor(['R04']) });
    renderApp({ path: '/', session: sessionAs(APPROVER_ID, ['R04']) });
    await findMainMenu();
    await waitFor(() => {
      expect(menuLink('common.menu.requestsApprovals')?.getAttribute('href')).toBe('/approvals/requests');
    });
  });

  test('a role with no approval permission is offered none of the approval screens (the review, C3)', async () => {
    withDashboards(mockApi(), { catalogue: catalogueFor(['R06']) });
    renderApp({ path: '/', session: sessionAs(APPROVER_ID, ['R06']) });

    await findMainMenu();
    await waitFor(() => {
      expect(menuLink('common.menu.projects')).toBeTruthy();
    });
    expect(menuLink('common.menu.requestsApprovals')?.getAttribute('href')).toBe('/change-requests');
  });
});
