import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { type ApprovalInstanceStatus } from '@/features/approvals/api/types.ts';
import { translate } from '@/shared/i18n/i18n.ts';
import {
  approvalRun,
  REQUESTER_ID,
  sessionAs,
  withApprovalNames,
} from '@/test/approvalFixtures.ts';
import { mockApi, page, problem } from '@/test/mockApi.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';

// SCR-101 My Requests and MOD-044 Withdraw.

const en = (key: string) => translate('en', key);

function summary(status: ApprovalInstanceStatus, index: number) {
  const { tasks: _tasks, ...run } = approvalRun({
    id: `f6f6f6f6-0000-4000-8000-00000000000${String(index)}`,
    status,
    completedAt: status === 'PENDING' ? null : '2026-09-29T09:00:00Z',
  });
  return run;
}

const ALL: ApprovalInstanceStatus[] = ['PENDING', 'APPROVED', 'REJECTED', 'RETURNED', 'WITHDRAWN'];

function openRequests(items = ALL.map(summary), path = '/approvals/requests') {
  const api = withApprovalNames(mockApi()).on('GET', /^\/approval-instances$/, (request) => {
    const status = request.query.get('status');
    return { body: page(items.filter((item) => status === null || item.status === status)) };
  });
  renderApp({ path, session: sessionAs(REQUESTER_ID, ['R06']) });
  return api;
}

describe('SCR-101 My Requests', () => {
  test('asks for the caller’s own runs and shows each status with its own indicator', async () => {
    const api = openRequests();

    const table = await screen.findByRole('table', { name: en('approvals.requests.caption') });
    expect(api.requestsTo('GET', /^\/approval-instances$/)[0]?.query.get('requestedBy')).toBe('me');

    const badge = (status: ApprovalInstanceStatus) =>
      within(table).getByText(en(`approvals.instanceStatus.${status}`)).className;
    expect(badge('PENDING')).toBe('badge badge--info');
    expect(badge('APPROVED')).toBe('badge badge--positive');
    expect(badge('REJECTED')).toBe('badge badge--negative');
    expect(badge('RETURNED')).toBe('badge badge--warning');
    expect(
      new Set(
        ['PENDING', 'APPROVED', 'REJECTED', 'RETURNED'].map((s) =>
          badge(s as ApprovalInstanceStatus),
        ),
      ).size,
    ).toBe(4);

    // Only a PENDING request can be withdrawn.
    expect(
      within(table).getAllByRole('button', { name: en('approvals.withdraw.action') }),
    ).toHaveLength(1);
    expect(describeViolations(await accessibilityViolations())).toBe('');
  });

  test('the status filter is sent to the API and kept in the URL', async () => {
    const api = openRequests();
    const user = userEvent.setup();
    await screen.findByRole('table');

    await user.selectOptions(
      screen.getByRole('combobox', { name: en('approvals.fields.status') }),
      'RETURNED',
    );

    await screen.findByText(en('approvals.instanceStatus.RETURNED'), { selector: '.badge' });
    expect(
      api
        .requestsTo('GET', /^\/approval-instances$/)
        .at(-1)
        ?.query.get('status'),
    ).toBe('RETURNED');
    expect(screen.getAllByRole('row')).toHaveLength(2);
  });

  test('no requests at all has its own empty state', async () => {
    openRequests([]);
    expect(await screen.findByText(en('approvals.requests.empty'))).toBeTruthy();
    expect(screen.queryByText(en('approvals.requests.emptyFiltered'))).toBeNull();
  });

  test('a status filter that matches nothing says so', async () => {
    openRequests([summary('PENDING', 1)], '/approvals/requests?status=REJECTED');
    expect(await screen.findByText(en('approvals.requests.emptyFiltered'))).toBeTruthy();
    expect(screen.queryByText(en('approvals.requests.empty'))).toBeNull();
  });

  test('withdrawing a pending request posts to the run and reads the list again', async () => {
    const api = openRequests();
    api.on('POST', /\/withdraw$/, { body: approvalRun({ status: 'WITHDRAWN' }) });
    const user = userEvent.setup();

    await user.click(await screen.findByRole('button', { name: en('approvals.withdraw.action') }));
    const dialog = screen.getByRole('dialog', { name: en('approvals.withdraw.title') });
    await user.click(
      within(dialog).getByRole('button', { name: en('approvals.withdraw.confirm') }),
    );

    expect(await screen.findByText(en('approvals.done.withdraw'))).toBeTruthy();
    expect(api.requestsTo('POST', /\/withdraw$/)[0]?.path).toBe(
      `/approval-instances/${summary('PENDING', 0).id}/withdraw`,
    );
    expect(api.requestsTo('GET', /^\/approval-instances$/)).toHaveLength(2);
  });

  test('a request already decided cannot be withdrawn, and the dialog says why', async () => {
    const api = openRequests([summary('PENDING', 1)]);
    api.on('POST', /\/withdraw$/, problem(409, 'TERMINAL_STATE'));
    const user = userEvent.setup();

    await user.click(await screen.findByRole('button', { name: en('approvals.withdraw.action') }));
    const dialog = screen.getByRole('dialog');
    await user.click(
      within(dialog).getByRole('button', { name: en('approvals.withdraw.confirm') }),
    );

    expect((await within(dialog).findByRole('alert')).textContent).toBe(
      en('approvals.problems.terminalState'),
    );
  });

  test('each request links to its history', async () => {
    const approved = summary('APPROVED', 1);
    openRequests([approved]);
    const link = await screen.findByRole('link', { name: en('approvals.viewHistory') });
    expect(link.getAttribute('href')).toBe(`/approvals/instances/${approved.id}`);
  });
});
