import { cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import { catalogueFor, withDashboards } from '@/test/dashboardFixtures.ts';
import { sessionFor } from '@/test/identityAccessFixtures.ts';
import { mockApi, SHELL_INBOX_COUNT, SHELL_PERMISSIONS } from '@/test/mockApi.ts';
import { notificationSession, withInbox } from '@/test/notificationFixtures.ts';
import { shippedPermissions } from '@/test/permissionFixtures.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';
import {
  findMainMenu,
  findOpenMenu,
  mainMenu,
  menuLink,
  openMenuTargets,
  sectionTabs,
} from '@/test/shell.ts';

// The master navigation of Blueprint §20.1 (the AHDA Global Navigation design): eleven menus in order, the open menu's
// screens as tabs over the page, counts on Requests and Approvals and on Notifications only, and every screen offered on the permission it
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
    withInbox(withDashboards(mockApi(), { catalogue: catalogueFor(['R06']) }));
    // On a screen of its own, so the Home's quick actions do not stand beside the menu's.
    const viewer = renderApp({ path: '/notifications', session: sessionFor(['R06']) });
    await findMainMenu();
    expect(screen.queryByRole('button', { name: en('common.shell.create') })).toBeNull();
    viewer.unmount();
    cleanup();

    withInbox(withDashboards(mockApi(), { catalogue: catalogueFor(['R04']) }));
    renderApp({ path: '/notifications', session: sessionFor(['R04']) });
    const user = userEvent.setup();
    await user.click(await screen.findByRole('button', { name: en('common.shell.create') }));
    expect(screen.getByRole('link', { name: en('projects.nav.create') })).toBeTruthy();
    expect(screen.getByRole('link', { name: en('common.shell.createChangeRequest') })).toBeTruthy();
  });

  test('the rail names the menus only; the open menu’s screens are tabs over the page, and the breadcrumb names both', async () => {
    withInbox(mockApi());
    renderApp({ path: '/notifications', session: notificationSession() });

    await findMainMenu();
    expect(within(mainMenu()).queryAllByRole('list', { name: /.+/ })).toHaveLength(0);
    expect(sectionTabs()).toBeTruthy();
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

  test('a role whose setting is off has no Create in the top bar, and keeps it in the menu (review round 5, item 14)', async () => {
    const api = mockApi();
    api.on('GET', SHELL_PERMISSIONS, {
      body: {
        permissions: [
          { code: 'PROJECT_REGISTER', scopes: ['ALL'] },
          { code: 'PROJECT_VIEW', scopes: ['ALL'] },
        ],
        showCreateInHeader: false,
      },
    });
    renderApp({ path: '/projects', session: sessionFor(['R02']) });

    await screen.findByRole('heading', { level: 1 });
    expect(screen.queryByRole('button', { name: en('common.shell.create') })).toBeNull();
    expect(
      (await screen.findAllByRole('link', { name: en('projects.nav.create') })).length,
    ).toBeGreaterThan(0);
  });

  test('a Portfolio Manager’s Portfolio menu leads to the Portfolio Dashboard, the portfolio reports and the KPI register', async () => {
    withDashboards(mockApi(), { catalogue: catalogueFor(['R02']) });
    renderApp({ path: '/dashboards?dashboard=PORTFOLIO', session: sessionFor(['R02']) });

    await findOpenMenu('common.menu.portfolio');
    expect(openMenuTargets('common.menu.portfolio')).toEqual([
      '/dashboards?dashboard=PORTFOLIO',
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

  test('a created role the catalogue offers the Portfolio Dashboard is given the Portfolio menu, as R06 is', async () => {
    withDashboards(mockApi(), { catalogue: catalogueFor(['R06']) }).on('GET', SHELL_PERMISSIONS, {
      body: shippedPermissions(sessionFor(['R06']).user),
    });
    renderApp({ path: '/', session: sessionFor(['R09']) });

    await waitFor(() => {
      expect(menuLink('common.menu.portfolio')).toBeTruthy();
    });
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

  test('someone who may not decide approvals gets no count, and the shell never asks the inbox for one', async () => {
    const api = mockApi();
    renderApp({ path: '/', session: sessionFor(['R04']) });

    await findMainMenu();
    await waitFor(() => {
      expect(menuLink('common.menu.requestsApprovals')).toBeTruthy();
    });
    expect(menuLink('common.menu.requestsApprovals')?.querySelector('.rail__count')).toBeNull();
    expect(api.shellInboxCountReads).toHaveLength(0);
  });

  test('a page below a menu’s screens shows no section tabs: its breadcrumb leads back', async () => {
    withInbox(mockApi());
    renderApp({ path: '/notifications/no-such-screen', session: notificationSession() });

    await findMainMenu();
    expect(sectionTabs()).toBeNull();
  });

  test('the Home’s cards offer every screen of the rail’s menus that this person may open, and no other', async () => {
    withInbox(withDashboards(mockApi(), { catalogue: catalogueFor(['R04']) }));
    renderApp({ path: '/', session: sessionFor(['R04']) });

    const overview = await screen.findByRole('region', { name: en('home.menus.title') });
    expect(sectionTabs()).toBeNull();
    const projects = await within(overview).findByRole('list', {
      name: en('common.menu.projects'),
    });
    expect(
      within(projects)
        .getByRole('link', { name: en('projects.nav.register') })
        .getAttribute('href'),
    ).toBe('/projects');
    const cards = within(overview)
      .getAllByRole('heading', { level: 3 })
      .map((heading) => heading.textContent);
    const menus = Array.from(mainMenu().querySelectorAll('.rail__label'))
      .map((label) => label.textContent)
      .filter((name) => name !== en('common.menu.home'));
    expect(cards).toEqual(menus);
    // The Project Manager administers nothing (the review, C3).
    expect(within(overview).queryByText(en('identityAccess.nav.users'))).toBeNull();
  });

  test('collapsing keeps every menu’s name for screen readers and is remembered', async () => {
    withInbox(mockApi());
    renderApp({ path: '/notifications', session: notificationSession() });
    const user = userEvent.setup();

    await findMainMenu();
    await user.click(screen.getByRole('button', { name: en('common.menu.collapse') }));
    expect(
      screen.getByRole('button', { name: en('common.menu.expand') }).getAttribute('aria-pressed'),
    ).toBe('true');
    expect(menuLink('common.menu.notifications')).toBeTruthy();
    cleanup();

    withInbox(mockApi());
    renderApp({ path: '/notifications', session: notificationSession() });
    expect(await screen.findByRole('button', { name: en('common.menu.expand') })).toBeTruthy();
  });

  test('the dark mode switch turns the whole page dark, and the choice is remembered', async () => {
    withInbox(mockApi());
    renderApp({ path: '/notifications', session: notificationSession() });
    const user = userEvent.setup();

    await findMainMenu();
    const darkMode = screen.getByRole('button', { name: en('common.darkMode') });
    expect(darkMode.getAttribute('aria-pressed')).toBe('false');
    await user.click(darkMode);
    expect(darkMode.getAttribute('aria-pressed')).toBe('true');
    expect(document.documentElement.dataset.theme).toBe('dark');
    cleanup();
    // As on a fresh page load: the theme comes back from the stored choice alone.
    delete document.documentElement.dataset.theme;

    withInbox(mockApi());
    renderApp({ path: '/notifications', session: notificationSession() });
    await findMainMenu();
    expect(
      screen.getByRole('button', { name: en('common.darkMode') }).getAttribute('aria-pressed'),
    ).toBe('true');
    await user.click(screen.getByRole('button', { name: en('common.darkMode') }));
    expect(document.documentElement.dataset.theme).toBe('light');
  });

  test('has no axe violations with a menu open', async () => {
    withInbox(mockApi());
    renderApp({ path: '/notifications', session: notificationSession() });
    await findMainMenu();
    const violations = await accessibilityViolations();
    expect(violations, describeViolations(violations)).toHaveLength(0);
  });
});
