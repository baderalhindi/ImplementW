import { cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import { catalogueFor, withDashboards } from '@/test/dashboardFixtures.ts';
import { sessionFor } from '@/test/identityAccessFixtures.ts';
import { mockApi, SHELL_INBOX_COUNT } from '@/test/mockApi.ts';
import { notificationSession, withInbox } from '@/test/notificationFixtures.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';
import { findMainMenu, mainMenu, menuLink, openMenuTargets } from '@/test/shell.ts';

// The master navigation of Blueprint §20.1 (the AHDA Global Navigation design): eleven menus in order, the open menu's
// screens only, counts on Requests and Approvals and on Notifications only, Administration hidden from a role without it.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);

const MENUS_IN_ORDER = [
  'common.menu.home',
  'common.menu.portfolio',
  'common.menu.projects',
  'common.menu.myWork',
  'common.menu.risksIssues',
  'common.menu.requestsApprovals',
  'common.menu.documents',
  'common.menu.dashboardsReports',
  'common.menu.notifications',
  'common.menu.administration',
];

describe('the master navigation', () => {
  test('lists the menus in the blueprint’s order, Administration included for R01', async () => {
    withDashboards(mockApi(), { catalogue: catalogueFor(['R01']) });
    renderApp({ path: '/', session: sessionFor(['R01']) });

    await findMainMenu();
    // On Home no menu is open, so every link of the rail is a menu.
    expect(
      within(mainMenu())
        .getAllByRole('link')
        .map((link) => link.textContent),
    ).toEqual(MENUS_IN_ORDER.map((key) => en(key)));
    expect(menuLink('common.menu.home')?.getAttribute('aria-current')).toBe('page');
  });

  test('a role without administration screens is not shown Administration, and is told so', async () => {
    withDashboards(mockApi(), { catalogue: catalogueFor(['R02']) });
    renderApp({ path: '/', session: sessionFor(['R02']) });

    await findMainMenu();
    expect(menuLink('common.menu.administration')).toBeNull();
    expect(screen.getByText(en('common.menu.administrationHidden'))).toBeTruthy();
  });

  test('only the open menu lists its screens, and the breadcrumb names the menu and the screen', async () => {
    withInbox(mockApi());
    renderApp({ path: '/notifications', session: notificationSession() });

    await findMainMenu();
    expect(within(mainMenu()).getAllByRole('list', { name: /.+/ })).toHaveLength(1);
    expect(openMenuTargets('common.menu.notifications')[0]).toBe('/notifications');

    const breadcrumb = screen.getByRole('navigation', { name: en('common.shell.breadcrumb') });
    const crumbs = within(breadcrumb).getAllByRole('listitem');
    expect(crumbs.map((crumb) => crumb.textContent)).toEqual([
      en('common.menu.notifications'),
      en('notifications.nav.all'),
    ]);
    expect(
      within(breadcrumb).getByText(en('notifications.nav.all')).getAttribute('aria-current'),
    ).toBe('page');
  });

  test('Requests and Approvals carries the number of approvals awaiting the person, read from the inbox total', async () => {
    const api = withDashboards(mockApi(), { catalogue: catalogueFor(['R02']) }).on(
      'GET',
      SHELL_INBOX_COUNT,
      { body: { items: [], page: 1, pageSize: 1, totalCount: 6 } },
    );
    renderApp({ path: '/', session: sessionFor(['R02']) });

    await findMainMenu();
    await waitFor(() => {
      expect(menuLink('common.menu.requestsApprovals')?.textContent).toContain(
        en('common.menu.awaitingDecision', { count: 6 }),
      );
    });
    expect(
      menuLink('common.menu.requestsApprovals')?.querySelector('.rail__count [aria-hidden="true"]')
        ?.textContent,
    ).toBe('6');
    expect(api.shellInboxCountReads[0]?.query.get('page')).toBe('1');
  });

  test('collapsing keeps every menu’s name for screen readers, hides the screens, and is remembered', async () => {
    withInbox(mockApi());
    renderApp({ path: '/notifications', session: notificationSession() });
    const user = userEvent.setup();

    await findMainMenu();
    await user.click(screen.getByRole('button', { name: en('common.menu.collapse') }));
    expect(
      screen.getByRole('button', { name: en('common.menu.expand') }).getAttribute('aria-pressed'),
    ).toBe('true');
    expect(menuLink('common.menu.notifications')).toBeTruthy();
    expect(within(mainMenu()).queryAllByRole('list', { name: /.+/ })).toHaveLength(0);
    cleanup();

    withInbox(mockApi());
    renderApp({ path: '/notifications', session: notificationSession() });
    expect(await screen.findByRole('button', { name: en('common.menu.expand') })).toBeTruthy();
  });

  test('has no axe violations with a menu open', async () => {
    withInbox(mockApi());
    renderApp({ path: '/notifications', session: notificationSession() });
    await findMainMenu();
    const violations = await accessibilityViolations();
    expect(violations, describeViolations(violations)).toHaveLength(0);
  });
});
