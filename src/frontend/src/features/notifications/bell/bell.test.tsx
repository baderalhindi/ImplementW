import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import { notification, notificationSession } from '@/test/notificationFixtures.ts';
import { mockApi, page } from '@/test/mockApi.ts';
import { renderApp } from '@/test/renderApp.tsx';

// The top bar's bell (20 Nov 2026): only the unread notifications; every one is in All notifications.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);

/** The bell's open panel. */
async function panel() {
  const menu = await waitFor(() => {
    const found = document.querySelector<HTMLElement>('.notification-menu');
    if (found === null) {
      throw new Error('The bell’s panel is not open');
    }
    return found;
  });
  return within(menu);
}

const bell = () =>
  screen.getByRole('button', { name: new RegExp(`^${en('notifications.bell.label')}`) });

describe('the bell', () => {
  test('lists only the unread notifications, asking the API for those alone', async () => {
    const api = mockApi()
      .on('GET', /^\/notifications\/unread-count$/, { body: { unreadCount: 1 } })
      .on('GET', /^\/notifications$/, { body: page([notification()]) });
    renderApp({ path: '/', session: notificationSession() });
    const user = userEvent.setup();

    await user.click(await waitFor(bell));

    const menu = await panel();
    expect(menu.getByText(en('notifications.bell.title'))).toBeTruthy();
    await waitFor(() => {
      expect(
        api
          .requestsTo('GET', /^\/notifications$/)
          .at(-1)
          ?.query.get('unread'),
      ).toBe('true');
    });
    expect(menu.getByRole('link', { name: en('notifications.bell.all') })).toBeTruthy();
  });

  test('says so when nothing is unread, and still offers All notifications', async () => {
    mockApi()
      .on('GET', /^\/notifications\/unread-count$/, { body: { unreadCount: 0 } })
      .on('GET', /^\/notifications$/, { body: page([]) });
    renderApp({ path: '/', session: notificationSession() });
    const user = userEvent.setup();

    await user.click(await waitFor(bell));

    const menu = await panel();
    expect(await menu.findByText(en('notifications.bell.noneUnread'))).toBeTruthy();
    expect(menu.getByRole('link', { name: en('notifications.bell.all') })).toBeTruthy();
  });
});
