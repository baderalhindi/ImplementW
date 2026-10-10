import { screen, within } from '@testing-library/react';
import { describe, expect, test } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import { APPROVER_ID, sessionAs, withApprovalNames } from '@/test/approvalFixtures.ts';
import { catalogueFor, withDashboards } from '@/test/dashboardFixtures.ts';
import { mockApi, page } from '@/test/mockApi.ts';
import { renderApp } from '@/test/renderApp.tsx';

import { approvalNavigation } from './routes.tsx';

// The WF-11 screens are for anyone signed in; what each person may see or decide is the API's answer (TASK-035).

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

  test('any signed-in role gets the approvals navigation, and only R01 the administration one', async () => {
    withApprovalNames(mockApi()).on('GET', /^\/approval-tasks$/, { body: page([]) });
    renderApp({ path: '/approvals', session: sessionAs(APPROVER_ID, ['R05']) });

    expect(
      await screen.findByRole('heading', { level: 1, name: en('approvals.inbox.title') }),
    ).toBeTruthy();
    const navigation = screen.getByRole('navigation', { name: en('approvals.nav.label') });
    expect(
      within(navigation)
        .getAllByRole('link')
        .map((link) => link.getAttribute('href')),
    ).toEqual(approvalNavigation.map((item) => item.to));
    expect(screen.queryByRole('navigation', { name: en('identityAccess.nav.label') })).toBeNull();
  });

  test('from the home page, the shell links to the inbox for everyone', async () => {
    withDashboards(mockApi(), { catalogue: catalogueFor(['R05']) });
    renderApp({ path: '/', session: sessionAs(APPROVER_ID, ['R05']) });

    const navigation = await screen.findByRole('navigation', { name: en('approvals.nav.label') });
    const link = within(navigation).getByRole('link', { name: en('approvals.nav.inbox') });
    expect(link.getAttribute('href')).toBe('/approvals/inbox');
    expect(screen.queryByRole('navigation', { name: en('identityAccess.nav.label') })).toBeNull();
  });
});
