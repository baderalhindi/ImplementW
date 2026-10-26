import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { type NotificationHistoryItem } from '@/features/notifications/api/types.ts';
import { translate } from '@/shared/i18n/i18n.ts';
import { shownAt } from '@/test/documentFixtures.ts';
import { historyItem, notificationSession, withInbox } from '@/test/notificationFixtures.ts';
import { type MockApi, mockApi, page } from '@/test/mockApi.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';

// SCR-153 Notification History.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);

const HISTORY: NotificationHistoryItem[] = [
  historyItem({
    id: '3c3c3c3c-0000-4000-8000-000000000201',
    channel: 'IN_APP',
    status: 'READ',
    readAt: '2026-10-01T09:30:00Z',
  }),
  historyItem({
    id: '3c3c3c3c-0000-4000-8000-000000000202',
    channel: 'EMAIL',
    status: 'DEAD_LETTER',
  }),
  historyItem({
    id: '3c3c3c3c-0000-4000-8000-000000000203',
    channel: 'SMS',
    status: 'SUPPRESSED',
    suppressionReason: 'RECIPIENT_OPTED_OUT',
    sentAt: null,
  }),
  historyItem({
    id: '3c3c3c3c-0000-4000-8000-000000000204',
    channel: 'SMS',
    status: 'SUPPRESSED',
    suppressionReason: 'SOMETHING_NEW',
    subject: null,
    sentAt: null,
  }),
];

function openHistory(items = HISTORY, path = '/notifications/history'): MockApi {
  const api = withInbox(mockApi()).on('GET', /^\/notifications\/history$/, { body: page(items) });
  renderApp({ path, session: notificationSession() });
  return api;
}

describe('SCR-153 Notification History', () => {
  test('lists every delivery on every channel with what became of it and why', async () => {
    openHistory();
    const table = await screen.findByRole('table', { name: en('notifications.history.title') });
    const rows = within(table).getAllByRole('row').slice(1);
    expect(rows).toHaveLength(4);

    const [read, deadLetter, optedOut, unknown] = rows as [
      HTMLElement,
      HTMLElement,
      HTMLElement,
      HTMLElement,
    ];
    expect(within(read).getByText(en('notifications.channel.IN_APP'))).toBeTruthy();
    expect(
      within(read).getByText(
        en('notifications.values.readAt', { at: shownAt('2026-10-01T09:30:00Z') }),
      ),
    ).toBeTruthy();
    expect(within(deadLetter).getByText(en('notifications.status.DEAD_LETTER')).className).toBe(
      'badge badge--negative',
    );
    expect(
      within(optedOut).getByText(en('notifications.suppressionReason.RECIPIENT_OPTED_OUT')),
    ).toBeTruthy();
    expect(within(optedOut).getByText('—')).toBeTruthy();
    // A reason this screen does not know yet is still shown, by its code; a delivery without a subject by its event.
    expect(
      within(unknown).getByText(
        en('notifications.suppressionReason.other', { reason: 'SOMETHING_NEW' }),
      ),
    ).toBeTruthy();
    expect(within(unknown).getByText('Approval.ApprovalTaskAssigned')).toBeTruthy();
    expect(await within(read).findByText('Approval tasks')).toBeTruthy();
    expect(describeViolations(await accessibilityViolations())).toBe('');
  });

  test('channel and status filters are sent to the API and kept in the URL', async () => {
    const api = openHistory();
    const user = userEvent.setup();
    await screen.findByRole('table');

    await user.selectOptions(
      screen.getByRole('combobox', { name: en('notifications.filters.channel') }),
      'SMS',
    );
    await user.selectOptions(
      screen.getByRole('combobox', { name: en('notifications.fields.status') }),
      'SUPPRESSED',
    );

    await waitFor(() => {
      const last = api.requestsTo('GET', /^\/notifications\/history$/).at(-1);
      expect(last?.query.get('channel')).toBe('SMS');
      expect(last?.query.get('status')).toBe('SUPPRESSED');
    });
  });

  test('empty and filtered-empty are distinct', async () => {
    openHistory([]);
    expect(await screen.findByText(en('notifications.history.empty'))).toBeTruthy();
  });

  test('a filter that matches nothing says so', async () => {
    openHistory([], '/notifications/history?channel=EMAIL');
    expect(await screen.findByText(en('notifications.history.emptyFiltered'))).toBeTruthy();
  });
});
