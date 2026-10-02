import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest';

import { type NotificationSummary } from '@/features/notifications/api/types.ts';
import { UNREAD_POLL_INTERVAL_MS } from '@/features/notifications/unreadCount.ts';
import { translate } from '@/shared/i18n/i18n.ts';
import {
  notification,
  notificationSession,
  readNotification,
  withInbox,
} from '@/test/notificationFixtures.ts';
import { mockApi, type MockReply, page, problem } from '@/test/mockApi.ts';
import { renderApp } from '@/test/renderApp.tsx';

// The unread badge and MOD-071 Mark All Read, against TASK-040's first two acceptance criteria.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);

function bell() {
  return screen.getByRole('link', { name: new RegExp(`^${en('notifications.bell.text')}`) });
}

async function shownCount(expected: number | null) {
  await waitFor(() => {
    const link = bell();
    if (expected === null) {
      expect(link.querySelector('.bell__count')).toBeNull();
    } else {
      expect(link.querySelector('.bell__count [aria-hidden="true"]')?.textContent).toBe(
        String(expected),
      );
      expect(link.textContent).toContain(en('notifications.bell.unread', { count: expected }));
    }
  });
}

beforeEach(() => {
  // Only the poll's interval is faked; promises, fetch and the DOM run as usual.
  vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval'] });
});

afterEach(() => {
  vi.useRealTimers();
});

describe('unread badge', () => {
  test('shows the count the API returns, and the API’s new count after one poll interval', async () => {
    let unreadCount = 3;
    const api = mockApi().on('GET', /^\/notifications\/unread-count$/, () => ({
      body: { unreadCount },
    }));
    renderApp({ path: '/', session: notificationSession() });

    await shownCount(3);
    expect(bell().getAttribute('href')).toBe('/notifications');

    unreadCount = 41;
    await vi.advanceTimersByTimeAsync(UNREAD_POLL_INTERVAL_MS - 1);
    await shownCount(3);
    await vi.advanceTimersByTimeAsync(1);
    await shownCount(41);

    unreadCount = 0;
    await vi.advanceTimersByTimeAsync(UNREAD_POLL_INTERVAL_MS);
    await shownCount(null);
    expect(api.requestsTo('GET', /^\/notifications\/unread-count$/)).toHaveLength(3);
  });

  test('a failed poll keeps the last count, and the next poll corrects it', async () => {
    let reply: MockReply = { body: { unreadCount: 2 } };
    mockApi().on('GET', /^\/notifications\/unread-count$/, () => reply);
    renderApp({ path: '/', session: notificationSession() });
    await shownCount(2);

    reply = problem(503, 'UNAVAILABLE');
    await vi.advanceTimersByTimeAsync(UNREAD_POLL_INTERVAL_MS);
    await shownCount(2);

    reply = { body: { unreadCount: 5 } };
    await vi.advanceTimersByTimeAsync(UNREAD_POLL_INTERVAL_MS);
    await shownCount(5);
  });

  test('is read again when the tab becomes visible, and not while it is hidden', async () => {
    let unreadCount = 1;
    const api = mockApi().on('GET', /^\/notifications\/unread-count$/, () => ({
      body: { unreadCount },
    }));
    renderApp({ path: '/', session: notificationSession() });
    await shownCount(1);

    const visibility = vi.spyOn(document, 'visibilityState', 'get').mockReturnValue('hidden');
    unreadCount = 4;
    await vi.advanceTimersByTimeAsync(UNREAD_POLL_INTERVAL_MS);
    expect(api.requestsTo('GET', /^\/notifications\/unread-count$/)).toHaveLength(1);

    visibility.mockReturnValue('visible');
    document.dispatchEvent(new Event('visibilitychange'));
    await shownCount(4);
    visibility.mockRestore();
  });

  test('without a session there is no badge and no poll', async () => {
    const api = mockApi();
    renderApp({ path: '/sign-in' });
    await screen.findByRole('heading', { level: 1 });
    await vi.advanceTimersByTimeAsync(UNREAD_POLL_INTERVAL_MS);
    expect(screen.queryByRole('link', { name: /Notifications/ })).toBeNull();
    expect(api.requestsTo('GET', /^\/notifications/)).toHaveLength(0);
  });
});

describe('MOD-071 Mark All Read', () => {
  test('marks everything read, the badge follows the API, and only a new notification counts again', async () => {
    let items: NotificationSummary[] = [
      notification(),
      notification({ id: '1a1a1a1a-0000-4000-8000-000000000199', subject: 'Second task' }),
      readNotification(),
    ];
    const unread = () => items.filter((item) => item.status === 'SENT').length;
    const api = withInbox(mockApi())
      .on('GET', /^\/notifications$/, () => ({ body: page(items) }))
      .on('GET', /^\/notifications\/unread-count$/, () => ({ body: { unreadCount: unread() } }))
      .on('POST', /^\/notifications\/read-all$/, () => {
        const markedCount = unread();
        items = items.map((item) =>
          item.status === 'SENT'
            ? { ...item, status: 'READ', readAt: '2026-10-02T10:00:00Z' }
            : item,
        );
        return { body: { markedCount } };
      });
    renderApp({ path: '/notifications', session: notificationSession() });
    const user = userEvent.setup();

    await shownCount(2);
    const table = await screen.findByRole('table', { name: en('notifications.all.title') });
    expect(within(table).getAllByText(en('notifications.readState.unread'))).toHaveLength(2);

    await user.click(screen.getByRole('button', { name: en('notifications.markAll.action') }));
    const dialog = screen.getByRole('dialog', { name: en('notifications.markAll.title') });
    expect(within(dialog).getByText(en('notifications.markAll.body'))).toBeTruthy();
    await user.click(
      within(dialog).getByRole('button', { name: en('notifications.markAll.confirm') }),
    );

    expect(await screen.findByText(en('notifications.markAll.done', { count: 2 }))).toBeTruthy();
    await shownCount(null);
    await waitFor(() => {
      expect(
        within(screen.getByRole('table')).queryByText(en('notifications.readState.unread')),
      ).toBeNull();
    });
    expect(api.requestsTo('POST', /^\/notifications\/read-all$/)).toHaveLength(1);
    // Nothing on the screen can make a read notification unread again.
    expect(screen.queryByRole('button', { name: /unread/i })).toBeNull();
    expect(screen.getByRole('button', { name: en('notifications.markAll.action') })).toHaveProperty(
      'disabled',
      true,
    );

    // A new notification arrives: the next poll counts it and the open page shows it, the old ones stay read.
    items = [
      notification({ id: '1a1a1a1a-0000-4000-8000-000000000200', subject: 'New task' }),
      ...items,
    ];
    await vi.advanceTimersByTimeAsync(UNREAD_POLL_INTERVAL_MS);
    await shownCount(1);
    const row = (await screen.findByRole('button', { name: 'New task' })).closest('tr');
    expect(row?.textContent).toContain(en('notifications.readState.unread'));
    expect(
      within(screen.getByRole('table')).getAllByText(en('notifications.readState.unread')),
    ).toHaveLength(1);
  });

  test('a refusal stays in the dialog and changes nothing', async () => {
    withInbox(mockApi()).on('POST', /^\/notifications\/read-all$/, problem(503, 'UNAVAILABLE'));
    renderApp({ path: '/notifications', session: notificationSession() });
    const user = userEvent.setup();
    await screen.findByRole('table');
    await shownCount(1);

    await user.click(screen.getByRole('button', { name: en('notifications.markAll.action') }));
    const dialog = screen.getByRole('dialog');
    await user.click(
      within(dialog).getByRole('button', { name: en('notifications.markAll.confirm') }),
    );

    expect((await within(dialog).findByRole('alert')).textContent).toBe(
      en('common.problems.unavailable'),
    );
    await shownCount(1);
  });
});
