import { cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import { catalogueFor, withDashboards } from '@/test/dashboardFixtures.ts';
import { sessionFor } from '@/test/identityAccessFixtures.ts';
import { mockApi, SHELL_INBOX_COUNT } from '@/test/mockApi.ts';
import { notificationSession, withInbox } from '@/test/notificationFixtures.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';
import { findMainMenu, findOpenMenu, mainMenu, menuLink, openMenuTargets } from '@/test/shell.ts';

// The master navigation of Blueprint §20.1 (the AHDA Global Navigation design): eleven menus in order, the open menu's
// screens only, counts on Requests and Approvals and on Notifications only, and every screen offered on the permission it
// needs, so a role sees the menus its permissions open and no other (the 24 Oct 2026 review, C3).

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
  'common.menu.help',
];

describe('the master navigation', () => {
  test('lists the menus in the blueprint’s order for a session whose roles open every one', async () => {
    withDashboards(mockApi(), { catalogue: catalogueFor(['R02']) });
    renderApp({ path: '/', session: sessionFor(['R01', 'R02', 'R04']) });

    await findMainMenu();
    // On Home no menu is open, so every link of the rail is a menu.
    expect(
      within(mainMenu())
        .getAllByRole('link')
        .map((link) => link.textContent),
    ).toEqual(MENUS_IN_ORDER.map((key) => en(key)));
    expect(menuLink('common.menu.home')?.getAttribute('aria-current')).toBe('page');
  });

  test.each([
    ['R01', ['home', 'notifications', 'administration', 'help']],
    [
      'R02',
      [
        'home',
        'portfolio',
        'projects',
        'risksIssues',
        'requestsApprovals',
        'documents',
        'dashboardsReports',
        'notifications',
        'help',
      ],
    ],
    [
      'R04',
      [
        'home',
        'projects',
        'myWork',
        'risksIssues',
        'requestsApprovals',
        'documents',
        'dashboardsReports',
        'notifications',
        'help',
      ],
    ],
    [
      'R05',
      [
        'home',
        'projects',
        'risksIssues',
        'requestsApprovals',
        'documents',
        'dashboardsReports',
        'notifications',
        'help',
      ],
    ],
    [
      'R06',
      [
        'home',
        'portfolio',
        'projects',
        'risksIssues',
        'requestsApprovals',
        'documents',
        'dashboardsReports',
        'notifications',
        'help',
      ],
    ],
    [
      'R07',
      [
        'home',
        'portfolio',
        'projects',
        'risksIssues',
        'requestsApprovals',
        'documents',
        'dashboardsReports',
        'notifications',
        'help',
      ],
    ],
  ])(
    '%s is shown the menus its shipped permissions open, and no other (review: “What each role should see”)',
    async (role, menus) => {
      withDashboards(mockApi(), { catalogue: catalogueFor([role]) });
      renderApp({ path: '/', session: sessionFor([role]) });

      await findMainMenu();
      await waitFor(() => {
        expect(
          within(mainMenu())
            .getAllByRole('link')
            .map((link) => link.textContent),
        ).toEqual(menus.map((menu) => en(`common.menu.${menu}`)));
      });
    },
  );

  test('an external entity user is shown its projects, its tasks, its requests and nothing of AHDA’s portfolio, approvals or reports', async () => {
    withDashboards(mockApi(), { catalogue: catalogueFor(['R08']) });
    renderApp({
      path: '/',
      session: {
        ...sessionFor(['R08']),
        user: { ...sessionFor(['R08']).user, userType: 'EXTERNAL' },
      },
    });

    await findMainMenu();
    await waitFor(() => {
      expect(
        within(mainMenu())
          .getAllByRole('link')
          .map((link) => link.textContent),
      ).toEqual(
        [
          'home',
          'projects',
          'myWork',
          'risksIssues',
          'requestsApprovals',
          'notifications',
          'help',
        ].map((menu) => en(`common.menu.${menu}`)),
      );
    });
    expect(screen.queryByRole('button', { name: en('common.shell.create') })).toBeTruthy();
  });

  test('Create offers only what the person may start, and is not there for someone who may start nothing', async () => {
    withDashboards(mockApi(), { catalogue: catalogueFor(['R06']) });
    const viewer = renderApp({ path: '/', session: sessionFor(['R06']) });
    await findMainMenu();
    expect(screen.queryByRole('button', { name: en('common.shell.create') })).toBeNull();
    viewer.unmount();
    cleanup();

    withDashboards(mockApi(), { catalogue: catalogueFor(['R04']) });
    renderApp({ path: '/', session: sessionFor(['R04']) });
    const user = userEvent.setup();
    await user.click(await screen.findByRole('button', { name: en('common.shell.create') }));
    expect(screen.getByRole('link', { name: en('projects.nav.create') })).toBeTruthy();
    expect(screen.getByRole('link', { name: en('common.shell.createChangeRequest') })).toBeTruthy();
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

  test('a Portfolio Manager’s Portfolio menu leads to the Portfolio Dashboard, the portfolio reports and the KPI register', async () => {
    withDashboards(mockApi(), { catalogue: catalogueFor(['R02']) });
    renderApp({ path: '/?dashboard=PORTFOLIO', session: sessionFor(['R02']) });

    await findOpenMenu('common.menu.portfolio');
    expect(openMenuTargets('common.menu.portfolio')).toEqual([
      '/?dashboard=PORTFOLIO',
      '/reports/portfolio',
      '/kpis',
    ]);
    // The dashboard is the Home's rendering, but its link belongs to Portfolio: Home is not the current page.
    expect(menuLink('common.menu.home')?.getAttribute('aria-current')).toBeNull();
    const breadcrumb = screen.getByRole('navigation', { name: en('common.shell.breadcrumb') });
    expect(
      within(breadcrumb)
        .getAllByRole('listitem')
        .map((crumb) => crumb.textContent),
    ).toEqual([en('common.menu.portfolio'), en('common.menu.portfolioDashboard')]);
    expect(
      within(breadcrumb)
        .getByText(en('common.menu.portfolioDashboard'))
        .getAttribute('aria-current'),
    ).toBe('page');
  });

  test('a Project Manager is given no Portfolio menu: its KPIs are its projects’ (the review, finding 1b)', async () => {
    withDashboards(mockApi(), { catalogue: catalogueFor(['R04']) });
    renderApp({ path: '/', session: sessionFor(['R04']) });

    await findMainMenu();
    await waitFor(() => {
      expect(menuLink('common.menu.projects')).toBeTruthy();
    });
    expect(menuLink('common.menu.portfolio')).toBeNull();
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
