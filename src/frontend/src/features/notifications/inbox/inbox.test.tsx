import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import {
  notification,
  NOTIFICATION_ID,
  notificationSession,
  READ_NOTIFICATION_ID,
  readNotification,
  withInbox,
} from '@/test/notificationFixtures.ts';
import { withDashboards } from '@/test/dashboardFixtures.ts';
import { mockApi, problem } from '@/test/mockApi.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';

// SCR-150 All Notifications, SCR-151 Alerts, SCR-152 Escalations and MOD-070 Notification Detail.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);

describe('SCR-150 All Notifications', () => {
  test('shows exactly the notifications the API returns, unread ones marked, with family labels', async () => {
    const api = withInbox(mockApi());
    renderApp({ path: '/notifications', session: notificationSession() });

    const table = await screen.findByRole('table', { name: en('notifications.all.title') });
    expect(within(table).getAllByRole('row')).toHaveLength(3);
    const unreadRow = within(table)
      .getByRole('button', { name: 'Approval task assigned' })
      .closest('tr');
    expect(unreadRow?.className).toBe('row--unread');
    expect(
      within(unreadRow as HTMLElement).getByText(en('notifications.readState.unread')).className,
    ).toBe('badge badge--info');
    expect(within(unreadRow as HTMLElement).getByText('Approval tasks')).toBeTruthy();
    const readRow = within(table).getByRole('button', { name: 'Report ready' }).closest('tr');
    expect(readRow?.className).toBe('');
    // A family the routing in force does not have is shown by its code.
    expect(within(readRow as HTMLElement).getByText('REPORT_READY')).toBeTruthy();
    expect(api.requestsTo('GET', /^\/notifications$/)).toHaveLength(1);
    expect(describeViolations(await accessibilityViolations())).toBe('');
  });

  test('read state and family filters are sent to the API and kept in the URL', async () => {
    const api = withInbox(mockApi());
    const { router } = renderApp({ path: '/notifications', session: notificationSession() });
    const user = userEvent.setup();
    await screen.findByRole('table');

    await user.selectOptions(
      screen.getByRole('combobox', { name: en('notifications.filters.readState') }),
      'unread',
    );
    await user.selectOptions(
      screen.getByRole('combobox', { name: en('notifications.filters.family') }),
      'APPROVAL_TASK',
    );

    await waitFor(() => {
      const last = api.requestsTo('GET', /^\/notifications$/).at(-1);
      expect(last?.query.get('unread')).toBe('true');
      expect(last?.query.get('eventFamilyCode')).toBe('APPROVAL_TASK');
    });
    expect(router.state.location.search).toBe('?state=unread&family=APPROVAL_TASK');
    const families = within(
      screen.getByRole('combobox', { name: en('notifications.filters.family') }),
    )
      .getAllByRole('option')
      .map((option) => option.textContent);
    expect(families).toEqual([
      en('common.filters.any'),
      'Approval tasks',
      'Security alerts',
      'Budget alerts',
      'Concern escalations',
    ]);
  });

  test('an empty inbox says so', async () => {
    withInbox(mockApi(), { items: [] });
    renderApp({ path: '/notifications', session: notificationSession() });
    expect(await screen.findByText(en('notifications.list.empty'))).toBeTruthy();
  });

  test('a filter that matches nothing says so', async () => {
    withInbox(mockApi(), { items: [] });
    renderApp({ path: '/notifications?state=read', session: notificationSession() });
    expect(await screen.findByText(en('notifications.list.emptyFiltered'))).toBeTruthy();
  });

  test('an unreadable inbox is an error with a retry', async () => {
    withInbox(mockApi()).on('GET', /^\/notifications$/, problem(503, 'UNAVAILABLE'));
    renderApp({ path: '/notifications', session: notificationSession() });
    const alert = await screen.findByRole('alert');
    expect(alert.textContent).toContain(en('common.problems.unavailable'));
    expect(within(alert).getByRole('button', { name: en('common.actions.retry') })).toBeTruthy();
    expect(screen.queryByText(en('notifications.list.empty'))).toBeNull();
  });

  test('without a routing configuration the inbox still lists, by family code and with no family filter', async () => {
    withInbox(mockApi()).on(
      'GET',
      /^\/notification-preferences$/,
      problem(422, 'CONFIGURATION_MISSING'),
    );
    renderApp({ path: '/notifications', session: notificationSession() });
    const table = await screen.findByRole('table');
    expect(await within(table).findByText('APPROVAL_TASK')).toBeTruthy();
    expect(screen.queryByRole('combobox', { name: en('notifications.filters.family') })).toBeNull();
  });

  test('is reached from the shell navigation, on the home page too', async () => {
    withDashboards(withInbox(mockApi()));
    renderApp({ path: '/', session: notificationSession() });
    const navigation = await screen.findByRole('navigation', {
      name: en('notifications.nav.label'),
    });
    expect(
      within(navigation)
        .getAllByRole('link')
        .map((link) => link.getAttribute('href')),
    ).toEqual([
      '/notifications',
      '/notifications/alerts',
      '/notifications/escalations',
      '/notifications/history',
      '/notifications/preferences',
    ]);
  });
});

describe('SCR-151 Alerts and SCR-152 Escalations', () => {
  test('Alerts offers only alert families and lists the first one by default', async () => {
    const api = withInbox(mockApi(), {
      items: [
        notification({ eventFamilyCode: 'SECURITY_ALERT', subject: 'Sign-in from a new device' }),
      ],
    });
    renderApp({ path: '/notifications/alerts', session: notificationSession() });

    expect(
      await screen.findByRole('heading', { level: 1, name: en('notifications.alert.title') }),
    ).toBeTruthy();
    expect(await screen.findByRole('button', { name: 'Sign-in from a new device' })).toBeTruthy();
    const options = within(
      screen.getByRole('combobox', { name: en('notifications.filters.family') }),
    )
      .getAllByRole('option')
      .map((option) => option.textContent);
    expect(options).toEqual(['Security alerts', 'Budget alerts']);
    expect(
      api
        .requestsTo('GET', /^\/notifications$/)
        .at(-1)
        ?.query.get('eventFamilyCode'),
    ).toBe('SECURITY_ALERT');
    // Mark-all-read is SCR-150's: it marks every family.
    expect(screen.queryByRole('button', { name: en('notifications.markAll.action') })).toBeNull();
  });

  test('Escalations lists the escalation family the URL names', async () => {
    const api = withInbox(mockApi());
    renderApp({
      path: '/notifications/escalations?family=CONCERN_ESCALATION',
      session: notificationSession(),
    });
    await screen.findByText(en('notifications.list.empty'));
    const options = within(
      screen.getByRole('combobox', { name: en('notifications.filters.family') }),
    )
      .getAllByRole('option')
      .map((option) => option.textContent);
    expect(options).toEqual(['Concern escalations']);
    expect(
      api
        .requestsTo('GET', /^\/notifications$/)
        .at(-1)
        ?.query.get('eventFamilyCode'),
    ).toBe('CONCERN_ESCALATION');
  });

  test('with no family of its kind the screen says so and asks for no notifications', async () => {
    const api = withInbox(mockApi(), { preferences: { families: [] } });
    renderApp({ path: '/notifications/escalations', session: notificationSession() });
    expect(await screen.findByText(en('notifications.escalation.noFamilies'))).toBeTruthy();
    expect(api.requestsTo('GET', /^\/notifications$/)).toHaveLength(0);
  });

  test('with no routing configuration the screen says no family is configured', async () => {
    const api = withInbox(mockApi()).on(
      'GET',
      /^\/notification-preferences$/,
      problem(422, 'CONFIGURATION_MISSING'),
    );
    renderApp({ path: '/notifications/alerts', session: notificationSession() });
    expect(await screen.findByText(en('notifications.alert.noFamilies'))).toBeTruthy();
    expect(api.requestsTo('GET', /^\/notifications$/)).toHaveLength(0);
  });
});

describe('MOD-070 Notification Detail', () => {
  test('opening an unread notification shows its text and link, reads it, and the badge follows', async () => {
    let unreadCount = 1;
    const api = withInbox(mockApi())
      .on('GET', /^\/notifications\/unread-count$/, () => ({ body: { unreadCount } }))
      .on('GET', new RegExp(`^/notifications/${NOTIFICATION_ID}$`), { body: notification() })
      .on('POST', new RegExp(`^/notifications/${NOTIFICATION_ID}/read$`), () => {
        unreadCount = 0;
        return { body: notification({ status: 'READ', readAt: '2026-10-02T10:00:00Z' }) };
      });
    renderApp({ path: '/notifications', session: notificationSession() });
    const user = userEvent.setup();

    await user.click(await screen.findByRole('button', { name: 'Approval task assigned' }));
    const dialog = screen.getByRole('dialog', { name: en('notifications.detail.title') });
    expect(
      await within(dialog).findByRole('heading', { name: 'Approval task assigned' }),
    ).toBeTruthy();
    const body = within(dialog).getByText(/waiting for your decision/);
    expect(body.textContent).toBe(notification().body);
    expect(body.closest('article')?.getAttribute('lang')).toBe('en');
    expect(within(dialog).getByText('Approval tasks')).toBeTruthy();
    expect(within(dialog).getByText(en('notifications.fields.readAt'))).toBeTruthy();
    expect(
      within(dialog)
        .getByRole('link', { name: en('notifications.detail.openRecord') })
        .getAttribute('href'),
    ).toBe('/approvals/inbox');
    expect(api.requestsTo('POST', /\/read$/)).toHaveLength(1);
    expect(describeViolations(await accessibilityViolations())).toBe('');

    await user.click(
      within(dialog).getByRole('button', { name: en('notifications.detail.close') }),
    );
    await waitFor(() => {
      expect(
        screen.getByRole('link', { name: /^Notifications/ }).querySelector('.bell__count'),
      ).toBeNull();
    });
  });

  test('opening a read notification only shows it', async () => {
    const api = withInbox(mockApi()).on(
      'GET',
      new RegExp(`^/notifications/${READ_NOTIFICATION_ID}$`),
      { body: readNotification({ deepLink: null }) },
    );
    renderApp({ path: '/notifications', session: notificationSession() });
    const user = userEvent.setup();

    await user.click(await screen.findByRole('button', { name: 'Report ready' }));
    const dialog = screen.getByRole('dialog');
    expect(await within(dialog).findByRole('heading', { name: 'Report ready' })).toBeTruthy();
    expect(within(dialog).queryByRole('link')).toBeNull();
    expect(api.requestsTo('POST', /\/read$/)).toHaveLength(0);
  });

  test('a link that is not an application route is not offered', async () => {
    withInbox(mockApi()).on('GET', new RegExp(`^/notifications/${READ_NOTIFICATION_ID}$`), {
      body: readNotification({ deepLink: '//evil.example/x' }),
    });
    renderApp({ path: '/notifications', session: notificationSession() });
    const user = userEvent.setup();
    await user.click(await screen.findByRole('button', { name: 'Report ready' }));
    const dialog = screen.getByRole('dialog');
    await within(dialog).findByRole('heading', { name: 'Report ready' });
    expect(within(dialog).queryByRole('link')).toBeNull();
  });

  test('an Arabic notification keeps its language, and a notification no longer there is an error', async () => {
    withInbox(mockApi(), {
      items: [
        notification({ subject: 'مهمة موافقة جديدة', body: 'بانتظار قرارك.', language: 'ar' }),
      ],
    }).on('GET', new RegExp(`^/notifications/${NOTIFICATION_ID}$`), problem(404, 'NOT_FOUND'));
    renderApp({ path: '/notifications', session: notificationSession() });
    const user = userEvent.setup();

    const open = await screen.findByRole('button', { name: 'مهمة موافقة جديدة' });
    expect(within(open).getByText('مهمة موافقة جديدة').getAttribute('lang')).toBe('ar');
    await user.click(open);
    expect((await within(screen.getByRole('dialog')).findByRole('alert')).textContent).toContain(
      en('notifications.problems.notFound'),
    );
  });
});
