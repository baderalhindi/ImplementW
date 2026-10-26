import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, test } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import {
  APPROVER_ID,
  DELEGATOR_ID,
  inboxItem,
  RUN_ID,
  sessionAs,
  withApprovalNames,
} from '@/test/approvalFixtures.ts';
import { type MockReply, mockApi, page, problem } from '@/test/mockApi.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';

// SCR-100 Approval Inbox: acceptance criteria 1 (only the approver's tasks) and 3 (empty state apart from loading),
// and the workbook's check that a delegate sees the delegated item.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);

function openInbox(
  inbox: MockReply | (() => MockReply | Promise<MockReply>),
  namesReadable = true,
) {
  const api = withApprovalNames(mockApi(), { namesReadable }).on(
    'GET',
    /^\/approval-tasks$/,
    inbox,
  );
  renderApp({ path: '/approvals/inbox', session: sessionAs(APPROVER_ID) });
  return api;
}

describe('SCR-100 Approval Inbox', () => {
  test('shows a loading state, then a distinct "no pending approvals" empty state', async () => {
    let answer: (reply: MockReply) => void = () => undefined;
    openInbox(
      () =>
        new Promise<MockReply>((resolve) => {
          answer = resolve;
        }),
    );

    const loading = await screen.findByText(en('approvals.inbox.loading'));
    expect(loading.getAttribute('role')).toBe('status');
    expect(screen.queryByText(en('approvals.inbox.empty'))).toBeNull();

    answer({ body: page([]) });

    const empty = await screen.findByText(en('approvals.inbox.empty'));
    expect(empty.textContent).toBe('No pending approvals');
    expect(screen.queryByText(en('approvals.inbox.loading'))).toBeNull();
    expect(screen.queryByRole('table')).toBeNull();
    expect(describeViolations(await accessibilityViolations())).toBe('');
  });

  test('lists exactly the tasks the API returns for the caller, read from the inbox endpoint only', async () => {
    const api = openInbox({
      body: page([
        inboxItem(),
        inboxItem({ taskId: '17171717-0000-4000-8000-000000000b02', sequenceNo: 2 }),
      ]),
    });

    const table = await screen.findByRole('table', { name: en('approvals.inbox.caption') });
    expect(within(table).getAllByRole('row')).toHaveLength(3);
    expect(within(table).getAllByText('ChangeRequest · CHANGE_REQUEST')).toHaveLength(2);
    expect(await within(table).findAllByText('Faisal Requester')).toHaveLength(2);
    expect(await within(table).findAllByText('Portfolio Manager (R02)')).toHaveLength(2);

    // The scope is the server's (TASK-035 D-4): the screen asks one question and adds nothing of its own.
    const [request] = api.requestsTo('GET', /^\/approval-tasks$/);
    expect(request?.query.get('page')).toBe('1');
    expect(request?.query.get('pageSize')).toBe('25');
    expect(api.requestsTo('GET', /^\/approval-instances/)).toHaveLength(0);
    expect(
      within(table)
        .getAllByRole('link', { name: en('approvals.viewHistory') })[0]
        ?.getAttribute('href'),
    ).toBe(`/approvals/instances/${RUN_ID}`);
    expect(describeViolations(await accessibilityViolations())).toBe('');
  });

  test('a task decided under a delegation is listed with the delegator it is decided for', async () => {
    openInbox({ body: page([inboxItem({ onBehalfOfUserId: DELEGATOR_ID })]) });

    const table = await screen.findByRole('table');
    expect(
      await within(table).findByText(en('approvals.inbox.onBehalfOf', { name: 'Huda Delegator' })),
    ).toBeTruthy();
    expect(within(table).queryByText(en('approvals.inbox.ownAuthority'))).toBeNull();
  });

  test('without USER_VIEW and ROLE_VIEW, people and roles are shown by a shortened id', async () => {
    openInbox({ body: page([inboxItem({ onBehalfOfUserId: DELEGATOR_ID })]) }, false);

    const table = await screen.findByRole('table');
    expect(
      within(table).getByText(en('common.people.unknownUser', { id: 'b2b2b2b2' })),
    ).toBeTruthy();
    expect(
      within(table).getByText(en('approvals.people.unknownRole', { id: 'd4d4d4d4' })),
    ).toBeTruthy();
    expect(
      within(table).getByText(
        en('approvals.inbox.onBehalfOf', {
          name: en('common.people.unknownUser', { id: 'c3c3c3c3' }),
        }),
      ),
    ).toBeTruthy();
  });

  test('an overdue task is marked overdue', async () => {
    openInbox({ body: page([inboxItem({ dueAt: '2026-01-01T00:00:00Z' })]) });

    const table = await screen.findByRole('table');
    expect(within(table).getByText(en('approvals.overdue'))).toBeTruthy();
  });

  test('without approval authority the refusal is shown as an error, not as an empty inbox', async () => {
    openInbox(problem(403, 'PERMISSION_DENIED'));

    const alert = await screen.findByRole('alert');
    expect(alert.textContent).toContain(en('approvals.problems.permissionDenied'));
    expect(screen.queryByText(en('approvals.inbox.empty'))).toBeNull();
  });

  test('a failed read can be retried', async () => {
    let attempts = 0;
    openInbox(() => {
      attempts += 1;
      return attempts === 1 ? problem(503, 'UNAVAILABLE') : { body: page([inboxItem()]) };
    });

    const alert = await screen.findByRole('alert');
    within(alert)
      .getByRole('button', { name: en('common.actions.retry') })
      .click();

    await waitFor(() => {
      expect(screen.getByRole('table')).toBeTruthy();
    });
  });
});
