import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { type ApprovalInstanceDetail } from '@/features/approvals/api/types.ts';
import { translate } from '@/shared/i18n/i18n.ts';
import {
  APPROVER_ID,
  approvalRun,
  DELEGATOR_ID,
  ESCALATION_ROLE_ID,
  REQUESTER_ID,
  RUN_ID,
  sessionAs,
  task,
  withApprovalNames,
} from '@/test/approvalFixtures.ts';
import { mockApi, problem } from '@/test/mockApi.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';

// SCR-115 Workflow/Approval History, MOD-044 Withdraw and MOD-045 Escalate.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);

const OVERDUE = '2026-01-01T00:00:00Z';
const SECOND_TASK_ID = '17171717-0000-4000-8000-000000000b02';

/** Stage 1 approved under a delegation; stage 2 overdue and current; returned with a reason on another run. */
const twoStages = approvalRun({
  tasks: [
    task({
      id: SECOND_TASK_ID,
      sequenceNo: 2,
      dueAt: OVERDUE,
    }),
    task({
      status: 'APPROVED',
      assignedUserId: DELEGATOR_ID,
      actingUserId: APPROVER_ID,
      approvalDelegationId: '4a4a4a4a-0000-4000-8000-000000000e01',
      decidedAt: '2026-09-28T12:00:00Z',
      decisionReason: { text: 'Within the approved budget.', language: 'EN' },
    }),
  ],
});

function openHistory(run: ApprovalInstanceDetail, viewer = REQUESTER_ID) {
  const api = withApprovalNames(mockApi()).on('GET', /^\/approval-instances\/[^/]+$/, {
    body: run,
  });
  renderApp({ path: `/approvals/instances/${run.id}`, session: sessionAs(viewer) });
  return api;
}

describe('SCR-115 approval history', () => {
  test('shows every stage in order with who decided, on whose authority, when and why', async () => {
    openHistory(twoStages);

    const stageHeadings = await screen.findAllByRole('heading', { level: 2 });
    expect(stageHeadings.map((h) => h.textContent)).toEqual([
      en('approvals.stage', { stage: 1 }),
      en('approvals.stage', { stage: 2 }),
    ]);
    const stageOne = screen.getByRole('table', {
      name: en('approvals.history.stageCaption', { stage: 1 }),
    });
    expect(within(stageOne).getByText(en('approvals.taskStatus.APPROVED')).className).toBe(
      'badge badge--positive',
    );
    expect(await within(stageOne).findByText('Noura Approver')).toBeTruthy();
    expect(
      within(stageOne).getByText(en('approvals.history.onBehalfOf', { name: 'Huda Delegator' })),
    ).toBeTruthy();
    expect(within(stageOne).getByText('Within the approved budget.').getAttribute('lang')).toBe(
      'en',
    );

    const stageTwo = screen.getByRole('table', {
      name: en('approvals.history.stageCaption', { stage: 2 }),
    });
    expect(within(stageTwo).getByText(en('approvals.taskStatus.PENDING')).className).toBe(
      'badge badge--info',
    );
    expect(within(stageTwo).getByText(en('approvals.overdue'))).toBeTruthy();
    expect(screen.getByText(en('approvals.history.currentStage'))).toBeTruthy();
    expect(describeViolations(await accessibilityViolations())).toBe('');
  });

  test('a returned run shows its reason and links back from a resubmission', async () => {
    const returned = approvalRun({
      status: 'RETURNED',
      completedAt: '2026-09-29T09:00:00Z',
      outcomeDeliveredAt: '2026-09-29T09:00:05Z',
      previousInstanceId: 'f6f6f6f6-0000-4000-8000-000000000a00',
      tasks: [
        task({
          status: 'RETURNED',
          actingUserId: APPROVER_ID,
          assignedUserId: APPROVER_ID,
          decidedAt: '2026-09-29T09:00:00Z',
          decisionReason: { text: 'يلزم تحديث الجدول الزمني.', language: 'AR' },
        }),
      ],
    });
    openHistory(returned);

    expect(
      (await screen.findAllByText(en('approvals.instanceStatus.RETURNED')))[0]?.className,
    ).toBe('badge badge--warning');
    expect(screen.getByText('يلزم تحديث الجدول الزمني.').getAttribute('lang')).toBe('ar');
    expect(
      screen.getByRole('link', { name: en('approvals.history.previousLink') }).getAttribute('href'),
    ).toBe('/approvals/instances/f6f6f6f6-0000-4000-8000-000000000a00');
    expect(screen.queryByRole('button', { name: en('approvals.withdraw.action') })).toBeNull();
  });

  test('the requester escalates an overdue task of the current stage, with an optional reason', async () => {
    const api = openHistory(twoStages);
    const escalated = approvalRun({
      tasks: [task({ id: SECOND_TASK_ID, sequenceNo: 2, status: 'ESCALATED', dueAt: OVERDUE })],
    });
    api.on('POST', /\/escalate$/, { body: escalated });
    const user = userEvent.setup();

    // One overdue task of the current stage: exactly one Escalate button (getByRole throws on more).
    await user.click(
      await screen.findByRole('button', { name: en('approvals.decision.escalate.action') }),
    );
    const dialog = screen.getByRole('dialog', { name: en('approvals.decision.escalate.title') });
    expect(
      within(dialog)
        .getByRole('textbox', { name: /Reason/ })
        .hasAttribute('required'),
    ).toBe(false);
    api.on('GET', /^\/approval-instances\/[^/]+$/, { body: escalated });
    await user.click(
      within(dialog).getByRole('button', { name: en('approvals.decision.escalate.confirm') }),
    );

    expect(await screen.findByText(en('approvals.done.escalate'))).toBeTruthy();
    expect(api.requestsTo('POST', /\/escalate$/)[0]?.path).toBe(
      `/approval-tasks/${SECOND_TASK_ID}/escalate`,
    );
    expect(api.requestsTo('POST', /\/escalate$/)[0]?.body).toEqual({});
    expect(await screen.findByText(en('approvals.taskStatus.ESCALATED'))).toBeTruthy();
  });

  test('an escalation the API refuses is explained', async () => {
    const api = openHistory(twoStages);
    api.on('POST', /\/escalate$/, problem(422, 'APPROVAL_ESCALATION_NOT_ALLOWED'));
    const user = userEvent.setup();

    await user.click(
      await screen.findByRole('button', { name: en('approvals.decision.escalate.action') }),
    );
    const dialog = screen.getByRole('dialog');
    await user.click(
      within(dialog).getByRole('button', { name: en('approvals.decision.escalate.confirm') }),
    );

    expect((await within(dialog).findByRole('alert')).textContent).toBe(
      en('approvals.problems.escalationNotAllowed'),
    );
  });

  test('someone other than the requester can neither escalate nor withdraw', async () => {
    openHistory(twoStages, APPROVER_ID);

    await screen.findAllByRole('heading', { level: 2 });
    expect(
      screen.queryByRole('button', { name: en('approvals.decision.escalate.action') }),
    ).toBeNull();
    expect(screen.queryByRole('button', { name: en('approvals.withdraw.action') })).toBeNull();
  });

  test('a task not yet due or of a later stage offers no escalation', async () => {
    openHistory(
      approvalRun({
        tasks: [
          task(),
          task({
            id: SECOND_TASK_ID,
            sequenceNo: 2,
            dueAt: null,
            assignedRoleId: ESCALATION_ROLE_ID,
          }),
        ],
      }),
    );

    await screen.findAllByRole('heading', { level: 2 });
    expect(screen.getByText(en('approvals.history.notReached'))).toBeTruthy();
    expect(
      screen.queryByRole('button', { name: en('approvals.decision.escalate.action') }),
    ).toBeNull();
  });

  test('the requester withdraws a pending run from its history', async () => {
    const api = openHistory(twoStages);
    api.on('POST', /\/withdraw$/, { body: approvalRun({ status: 'WITHDRAWN' }) });
    const user = userEvent.setup();

    await user.click(await screen.findByRole('button', { name: en('approvals.withdraw.action') }));
    api.on('GET', /^\/approval-instances\/[^/]+$/, {
      body: approvalRun({ status: 'WITHDRAWN', tasks: [task({ status: 'CANCELLED' })] }),
    });
    await user.click(
      within(screen.getByRole('dialog')).getByRole('button', {
        name: en('approvals.withdraw.confirm'),
      }),
    );

    expect(await screen.findByText(en('approvals.done.withdraw'))).toBeTruthy();
    expect(api.requestsTo('POST', /\/withdraw$/)[0]?.path).toBe(
      `/approval-instances/${RUN_ID}/withdraw`,
    );
    expect(await screen.findByText(en('approvals.taskStatus.CANCELLED'))).toBeTruthy();
    expect(screen.queryByRole('button', { name: en('approvals.withdraw.action') })).toBeNull();
  });

  test('a run the caller may not see is answered as not found', async () => {
    withApprovalNames(mockApi()).on(
      'GET',
      /^\/approval-instances\/[^/]+$/,
      problem(404, 'NOT_FOUND'),
    );
    renderApp({ path: `/approvals/instances/${RUN_ID}`, session: sessionAs(APPROVER_ID) });

    expect((await screen.findByRole('alert')).textContent).toContain(
      en('common.problems.notFound'),
    );
  });
});
