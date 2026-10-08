import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { type Session } from '@/features/identity-access/session/sessionApi.ts';
import { type ProjectDetail } from '@/features/projects/api/types.ts';
import { type Language, translate } from '@/shared/i18n/i18n.ts';
import { APPROVER_ID, sessionAs, withApprovalNames } from '@/test/approvalFixtures.ts';
import { concernProject, managerSession } from '@/test/concernFixtures.ts';
import { type MockApi, mockApi, problem } from '@/test/mockApi.ts';
import {
  PROJECT_ID,
  reviewerSession,
  withProject,
  withProjectLookups,
} from '@/test/projectFixtures.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';
import {
  CASE_ETAG,
  closeoutInboxItem,
  closeoutRun,
  COMPLETION_ID,
  completionCase,
  completionEffected,
  NEW_CASE_ID,
  NEW_SUSPENSION_ID,
  notReady,
  obligation,
  SUSPENSION_ETAG,
  SUSPENSION_ID,
  type SuspensionClosureState,
  suspensionInEffect,
  suspensionPeriod,
  suspensionRequest,
  withSuspensionClosure,
} from '@/test/suspensionClosureFixtures.ts';

// SCR-108 Suspension Requests, SCR-109 Create Suspension Request, SCR-110 Suspension Request Detail and the reused
// Closure screen family SCR-111–113 for the two-stage Completion/Closure closeout (TASK-064), against the TASK-062 and
// TASK-063 APIs. Acceptance criterion 1: the Closure UI distinguishes the Completion stage from the Closure stage as two
// sequential steps, not one combined action. Criterion 2 (a closed project's workspace is read-only) is in
// closedWorkspace.test.tsx.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);

function open(
  path: string,
  state: SuspensionClosureState = {},
  {
    project = concernProject(),
    session = managerSession(),
    language = 'en',
  }: { project?: ProjectDetail; session?: Session; language?: Language } = {},
): MockApi {
  const api = withSuspensionClosure(
    withApprovalNames(withProject(withProjectLookups(mockApi()), project)),
    state,
  );
  renderApp({ path, session, language });
  return api;
}

async function closeoutStages(): Promise<[HTMLElement, HTMLElement]> {
  await screen.findByRole('heading', { name: en('suspensionClosure.stages.title') });
  const items = [...document.querySelectorAll<HTMLElement>('.closeout-stage')];
  expect(items).toHaveLength(2);
  const [completion, closure] = items;
  if (completion === undefined || closure === undefined) {
    throw new Error('Two stages expected');
  }
  return [completion, closure];
}

describe('SCR-111 the closeout as two sequential stages (acceptance criterion 1)', () => {
  test('an ACTIVE project shows Stage 1 Completion ready and Stage 2 Closure waiting, each its own step with its own action', async () => {
    open(`/projects/${PROJECT_ID}/closeout`);

    const [completion, closure] = await closeoutStages();
    expect(
      within(completion).getByRole('heading', {
        name: `${en('suspensionClosure.stages.number', { number: 1, total: 2 })} ${en('suspensionClosure.stages.name.completion')}`,
      }),
    ).toBeTruthy();
    expect(
      within(closure).getByRole('heading', {
        name: `${en('suspensionClosure.stages.number', { number: 2, total: 2 })} ${en('suspensionClosure.stages.name.closure')}`,
      }),
    ).toBeTruthy();
    expect(within(completion).getByText(en('suspensionClosure.stages.state.READY'))).toBeTruthy();
    expect(within(closure).getByText(en('suspensionClosure.stages.state.LOCKED'))).toBeTruthy();
    expect(completion.getAttribute('aria-current')).toBe('step');
    expect(closure.getAttribute('aria-current')).toBeNull();

    // One action, on Stage 1 only: there is no combined "complete and close", and Stage 2 offers nothing yet.
    const raise = within(completion).getByRole('link', {
      name: en('suspensionClosure.actions.raise.completion'),
    });
    expect(raise.getAttribute('href')).toBe(
      `/closure-requests/new?projectId=${PROJECT_ID}&stage=completion`,
    );
    expect(within(closure).queryByRole('link')).toBeNull();
    expect(within(closure).queryByRole('button')).toBeNull();
    expect(
      screen.queryByRole('link', { name: en('suspensionClosure.actions.raise.closure') }),
    ).toBeNull();
  });

  test('once Stage 1 is in effect the project is COMPLETED: Stage 1 is done and Stage 2 offers its own, separate request', async () => {
    open(
      `/projects/${PROJECT_ID}/closeout`,
      { completions: [completionEffected()], obligations: [obligation()] },
      { project: concernProject({ status: 'COMPLETED' }) },
    );

    const [completion, closure] = await closeoutStages();
    expect(within(completion).getByText(en('suspensionClosure.stages.state.DONE'))).toBeTruthy();
    expect(within(completion).queryByRole('link', { name: /raise/i })).toBeNull();
    expect(
      within(completion)
        .getByRole('link', {
          name: en('suspensionClosure.stages.caseLink.completion', { revision: 1 }),
        })
        .getAttribute('href'),
    ).toBe(`/closure-requests/completion/${COMPLETION_ID}`);
    expect(closure.getAttribute('aria-current')).toBe('step');
    expect(
      within(closure)
        .getByRole('link', { name: en('suspensionClosure.actions.raise.closure') })
        .getAttribute('href'),
    ).toBe(`/closure-requests/new?projectId=${PROJECT_ID}&stage=closure`);

    // The obligation outlives Completion and is still worked on (TASK-063 D-9).
    const obligations = screen.getByRole('table', {
      name: en('suspensionClosure.obligations.caption'),
    });
    expect(within(obligations).getByText('Defect liability period')).toBeTruthy();
    expect(
      within(obligations).getByRole('button', {
        name: en('suspensionClosure.obligations.command.satisfy.named', {
          name: 'Defect liability period',
        }),
      }),
    ).toBeTruthy();
  });

  test('the Project Manager records an obligation against the completion case, naming themself as its owner', async () => {
    const user = userEvent.setup();
    const nora = managerSession();
    const api = open(
      `/projects/${PROJECT_ID}/closeout`,
      { completions: [completionEffected()] },
      { project: concernProject({ status: 'COMPLETED' }), session: nora },
    );

    await user.click(
      await screen.findByRole('button', { name: en('suspensionClosure.obligations.add') }),
    );
    const dialog = await screen.findByRole('dialog');
    await user.type(
      within(dialog).getByLabelText(en('suspensionClosure.obligations.fields.title'), {
        exact: false,
      }),
      'Defect liability period',
    );
    await user.click(
      within(dialog).getByLabelText(en('tasks.owner.self', { name: nora.user.displayName })),
    );
    await user.type(
      within(dialog).getByLabelText(en('suspensionClosure.obligations.fields.dueDate')),
      '2027-10-01',
    );
    await user.click(
      within(dialog).getByRole('button', { name: en('suspensionClosure.obligations.save') }),
    );

    await screen.findByText(en('suspensionClosure.done.obligationAdded'));
    expect(api.requestsTo('POST', /^\/post-project-obligations$/)[0]?.body).toEqual({
      completionCaseId: COMPLETION_ID,
      closureCaseId: null,
      title: { text: 'Defect liability period', language: 'en' },
      description: null,
      ownerUserId: nora.user.id,
      dueDate: '2027-10-01',
    });
  });

  test('a SUSPENDED project holds Stage 1 and offers a closure without completion in Stage 2', async () => {
    open(
      `/projects/${PROJECT_ID}/closeout`,
      {},
      { project: concernProject({ status: 'SUSPENDED' }) },
    );

    const [completion, closure] = await closeoutStages();
    expect(within(completion).getByText(en('suspensionClosure.stages.state.ON_HOLD'))).toBeTruthy();
    expect(within(completion).queryByRole('link')).toBeNull();
    expect(
      within(closure).getByRole('link', {
        name: en('suspensionClosure.actions.raise.terminalClosure'),
      }),
    ).toBeTruthy();
  });

  test('SCR-112 raises Stage 1 with exactly the fields typed, and the case is evaluated and submitted from its own page', async () => {
    const user = userEvent.setup();
    const api = open(`/closure-requests/new?projectId=${PROJECT_ID}&stage=completion`, {
      evaluated: notReady(),
    });

    await screen.findByRole('heading', {
      level: 1,
      name: en('suspensionClosure.closeout.form.createTitle.completion'),
    });
    expect(screen.getByText(en('suspensionClosure.closeout.form.intro.completion'))).toBeTruthy();
    await user.type(
      screen.getByLabelText(en('suspensionClosure.closeout.fields.actualProjectCompletionDate'), {
        exact: false,
      }),
      '2026-10-06',
    );
    await user.type(
      screen.getByLabelText(en('suspensionClosure.closeout.fields.narrative.completion'), {
        exact: false,
      }),
      'Road surfaced',
    );
    await user.click(
      screen.getByRole('button', { name: en('suspensionClosure.actions.saveDraft') }),
    );

    await screen.findByText(en('suspensionClosure.done.saved'));
    expect(api.requestsTo('POST', /^\/completion-cases$/)[0]?.body).toEqual({
      projectId: PROJECT_ID,
      actualProjectCompletionDate: '2026-10-06',
      completionNarrative: { text: 'Road surfaced', language: 'en' },
    });
    expect(api.requestsTo('POST', /^\/closure-cases$/)).toHaveLength(0);
    expect(
      screen.getByRole('heading', {
        level: 1,
        name: en('suspensionClosure.closeout.detail.heading.completion'),
      }),
    ).toBeTruthy();

    await user.click(
      screen.getByRole('button', { name: en('suspensionClosure.actions.evaluateReadiness') }),
    );
    await screen.findByText(en('suspensionClosure.done.evaluated'));
    const evaluation = api.requestsTo(
      'POST',
      new RegExp(`^/completion-cases/${NEW_CASE_ID}/evaluate-readiness$`),
    );
    expect(evaluation[0]?.headers.get('If-Match')).toBe(CASE_ETAG);
    const checks = screen.getByRole('table', { name: en('suspensionClosure.readiness.caption') });
    expect(
      within(checks).getByRole('rowheader', {
        name: new RegExp(en('suspensionClosure.readiness.checks.PROGRESS_REPORTED')),
      }),
    ).toBeTruthy();
    expect(
      screen.getAllByText(en('suspensionClosure.readiness.status.NOT_READY')).length,
    ).toBeGreaterThan(0);
  });

  test('a submission refused for blockers names each criterion that still fails', async () => {
    const user = userEvent.setup();
    open(`/closure-requests/completion/${COMPLETION_ID}`, {
      completions: [completionCase({ readiness: notReady() })],
      submitReply: problem(422, 'CLOSURE_BLOCKER_EXISTS', [
        { field: 'PROGRESS_REPORTED', code: 'NOT_ALLOWED' },
        { field: 'OBLIGATIONS_OWNED', code: 'NOT_ALLOWED' },
      ]),
    });

    await user.click(
      await screen.findByRole('button', { name: en('suspensionClosure.command.submit.title') }),
    );
    const dialog = await screen.findByRole('dialog');
    await user.click(
      within(dialog).getByRole('button', { name: en('suspensionClosure.command.submit.title') }),
    );
    expect(
      await within(dialog).findByText(
        en('suspensionClosure.problems.blockersNamed', {
          criteria: `${en('suspensionClosure.readiness.checks.PROGRESS_REPORTED')}; ${en('suspensionClosure.readiness.checks.OBLIGATIONS_OWNED')}`,
        }),
      ),
    ).toBeTruthy();
  });

  test("AHDA's reviewer waives a failed criterion with a reason; a criterion settled where it lives offers no waiver", async () => {
    const user = userEvent.setup();
    const api = open(
      `/closure-requests/completion/${COMPLETION_ID}`,
      { completions: [completionCase({ readiness: notReady() })] },
      { session: reviewerSession() },
    );

    const checks = await screen.findByRole('table', {
      name: en('suspensionClosure.readiness.caption'),
    });
    const owned = within(checks).getByRole('row', {
      name: new RegExp(en('suspensionClosure.readiness.checks.OBLIGATIONS_OWNED')),
    });
    expect(within(owned).queryByRole('button')).toBeNull();
    expect(within(owned).getByText(en('suspensionClosure.readiness.notWaivable'))).toBeTruthy();

    await user.click(
      within(checks).getByRole('button', {
        name: en('suspensionClosure.waive.named', {
          criterion: en('suspensionClosure.readiness.checks.PROGRESS_REPORTED'),
        }),
      }),
    );
    const dialog = await screen.findByRole('dialog');
    await user.type(
      within(dialog).getByLabelText(en('suspensionClosure.waive.reason'), { exact: false }),
      'Final report filed on paper',
    );
    await user.click(
      within(dialog).getByRole('button', { name: en('suspensionClosure.waive.action') }),
    );

    await screen.findByText(en('suspensionClosure.done.waived'));
    const waiver = api.requestsTo('POST', /\/waive-check$/)[0];
    expect(waiver?.body).toEqual({
      checkCode: 'PROGRESS_REPORTED',
      reason: { text: 'Final report filed on paper', language: 'en' },
    });
    expect(waiver?.headers.get('If-Match')).toBe(CASE_ETAG);
    // The reviewer neither edits nor submits the Project Manager's case.
    expect(
      screen.queryByRole('button', { name: en('suspensionClosure.command.submit.title') }),
    ).toBeNull();
  });

  test('an approved completion reads Approved for its approval and not yet in effect for the project, Stage 1 marked as shown', async () => {
    open(
      `/closure-requests/completion/${COMPLETION_ID}`,
      {
        completions: [completionCase({ status: 'APPROVED', submittedAt: '2026-10-08T10:00:00Z' })],
      },
      { session: reviewerSession() },
    );

    await screen.findByRole('heading', { name: en('suspensionClosure.state.title') });
    expect(document.querySelector('[data-badge="approval"]')?.textContent).toBe(
      en('suspensionClosure.approval.APPROVED'),
    );
    expect(document.querySelector('[data-badge="activation"]')?.textContent).toBe(
      en('suspensionClosure.activation.PENDING'),
    );
    expect(
      screen.getByText(en('suspensionClosure.closeout.explain.completion.APPROVED')),
    ).toBeTruthy();
    const [completion, closure] = await closeoutStages();
    expect(completion.classList.contains('closeout-stage--shown')).toBe(true);
    expect(
      within(completion).getByText(en('suspensionClosure.stages.state.IN_PROGRESS')),
    ).toBeTruthy();
    expect(within(closure).getByText(en('suspensionClosure.stages.state.LOCKED'))).toBeTruthy();
    expect(
      screen.getByRole('button', { name: en('suspensionClosure.command.activate.title') }),
    ).toBeTruthy();
  });

  test("an approver decides the case through WF-11's own dialog; the originator does not", async () => {
    const underReview = completionCase({
      status: 'UNDER_REVIEW',
      submittedAt: '2026-10-08T10:00:00Z',
    });
    open(
      `/closure-requests/completion/${COMPLETION_ID}`,
      { completions: [underReview], runs: [closeoutRun()], inbox: [closeoutInboxItem()] },
      { session: sessionAs(APPROVER_ID) },
    );

    expect(
      await screen.findByRole('button', { name: en('approvals.decision.approve.action') }),
    ).toBeTruthy();
    expect(
      screen.getByRole('button', { name: en('approvals.decision.reject.action') }),
    ).toBeTruthy();
    expect(screen.getByText(en('suspensionClosure.review.youDecide'))).toBeTruthy();
  });

  test('the closeout tab and a case detail have no accessibility violations, in Arabic too', async () => {
    open(
      `/projects/${PROJECT_ID}/closeout`,
      { completions: [completionEffected()], obligations: [obligation()] },
      { project: concernProject({ status: 'COMPLETED' }), language: 'ar' },
    );
    await screen.findByRole('heading', { name: translate('ar', 'suspensionClosure.stages.title') });
    const violations = await accessibilityViolations();
    expect(violations, describeViolations(violations)).toEqual([]);
  });

  test('the case detail with readiness and review has no accessibility violations', async () => {
    open(`/closure-requests/completion/${COMPLETION_ID}`, {
      completions: [completionCase({ readiness: notReady() })],
      obligations: [obligation()],
    });
    await screen.findByRole('table', { name: en('suspensionClosure.readiness.caption') });
    await screen.findByRole('heading', { name: en('suspensionClosure.stages.title') });
    const violations = await accessibilityViolations();
    expect(violations, describeViolations(violations)).toEqual([]);
  });
});

describe('SCR-108–110 suspension and resumption', () => {
  test("SCR-108 in an ACTIVE project's workspace offers its Project Manager a suspension request, not a resumption", async () => {
    open(`/projects/${PROJECT_ID}/suspension`, { requests: [] });

    expect(
      (
        await screen.findByRole('link', { name: en('suspensionClosure.actions.requestSuspension') })
      ).getAttribute('href'),
    ).toBe(`/suspension-requests/new?projectId=${PROJECT_ID}&type=SUSPEND`);
    expect(
      screen.queryByRole('link', { name: en('suspensionClosure.actions.requestResumption') }),
    ).toBeNull();
    expect(screen.getByText(en('suspensionClosure.suspension.notSuspended'))).toBeTruthy();
  });

  test('a second suspension is not offered while one is open', async () => {
    open(`/projects/${PROJECT_ID}/suspension`, {
      requests: [suspensionRequest({ status: 'APPROVED' })],
    });
    await screen.findByRole('table', { name: en('suspensionClosure.suspension.projectCaption') });
    expect(
      screen.queryByRole('link', { name: en('suspensionClosure.actions.requestSuspension') }),
    ).toBeNull();
  });

  test('SCR-109 saves and submits a suspension with exactly the fields typed', async () => {
    const user = userEvent.setup();
    const api = open(`/suspension-requests/new?projectId=${PROJECT_ID}&type=SUSPEND`, {
      requests: [],
    });

    await screen.findByRole('heading', {
      level: 1,
      name: en('suspensionClosure.suspension.form.createTitle.SUSPEND'),
    });
    await user.type(
      screen.getByLabelText(en('suspensionClosure.suspension.fields.reason'), { exact: false }),
      'Land handover dispute',
    );
    await user.type(
      screen.getByLabelText(en('suspensionClosure.suspension.fields.requestedEffectiveDate'), {
        exact: false,
      }),
      '2099-01-15',
    );
    await user.type(
      screen.getByLabelText(en('suspensionClosure.suspension.fields.plannedResumptionDate'), {
        exact: false,
      }),
      '2099-04-01',
    );
    await user.click(screen.getByRole('button', { name: en('suspensionClosure.actions.submit') }));

    await screen.findByText(en('suspensionClosure.done.submitted'));
    expect(api.requestsTo('POST', /^\/suspension-requests$/)[0]?.body).toEqual({
      projectId: PROJECT_ID,
      requestType: 'SUSPEND',
      reason: { text: 'Land handover dispute', language: 'en' },
      requestedEffectiveDate: '2099-01-15',
      plannedResumptionDate: '2099-04-01',
    });
    const submit = api.requestsTo(
      'POST',
      new RegExp(`^/suspension-requests/${NEW_SUSPENSION_ID}/submit$`),
    );
    expect(submit[0]?.headers.get('If-Match')).toBe(SUSPENSION_ETAG);
  });

  test('submission without an effective date is stopped on the field, before any request', async () => {
    const user = userEvent.setup();
    const api = open(`/suspension-requests/new?projectId=${PROJECT_ID}&type=SUSPEND`, {
      requests: [],
    });
    await user.type(
      await screen.findByLabelText(en('suspensionClosure.suspension.fields.reason'), {
        exact: false,
      }),
      'Dispute',
    );
    await user.click(screen.getByRole('button', { name: en('suspensionClosure.actions.submit') }));
    expect(
      await screen.findByText(en('suspensionClosure.fieldErrors.effectiveDateRequired')),
    ).toBeTruthy();
    expect(api.requestsTo('POST', /^\/suspension-requests/)).toHaveLength(0);
  });

  test('SCR-110: an approved suspension has not suspended the project; the explanation says it waits for its date', async () => {
    open(
      `/suspension-requests/${SUSPENSION_ID}`,
      {
        requests: [suspensionRequest({ status: 'APPROVED', submittedAt: '2026-10-05T10:00:00Z' })],
      },
      { session: reviewerSession() },
    );

    await screen.findByRole('heading', { name: en('suspensionClosure.state.title') });
    expect(document.querySelector('[data-badge="approval"]')?.textContent).toBe(
      en('suspensionClosure.approval.APPROVED'),
    );
    expect(document.querySelector('[data-badge="activation"]')?.textContent).toBe(
      en('suspensionClosure.activation.PENDING'),
    );
    expect(
      screen.getByText(en('suspensionClosure.suspension.explain.SUSPEND.APPROVED')),
    ).toBeTruthy();
    expect(await screen.findByText(en('projects.status.ACTIVE'))).toBeTruthy();
  });

  test('SCR-110 of a suspension in effect shows its period and starts the resumption from it', async () => {
    open(
      `/suspension-requests/${SUSPENSION_ID}`,
      { requests: [suspensionInEffect()], periods: [suspensionPeriod()] },
      { project: concernProject({ status: 'SUSPENDED' }) },
    );

    expect(
      await screen.findByRole('heading', { name: en('suspensionClosure.suspension.periodTitle') }),
    ).toBeTruthy();
    expect(document.querySelector('[data-badge="activation"]')?.textContent).toBe(
      en('suspensionClosure.activation.EFFECTED'),
    );
    expect(
      (
        await screen.findByRole('link', { name: en('suspensionClosure.actions.requestResumption') })
      ).getAttribute('href'),
    ).toBe(`/suspension-requests/new?projectId=${PROJECT_ID}&type=RESUME`);
  });

  test('the suspended workspace carries its banner on every tab, linking to the suspension', async () => {
    open(
      `/projects/${PROJECT_ID}`,
      { requests: [suspensionInEffect()], periods: [suspensionPeriod()] },
      { project: concernProject({ status: 'SUSPENDED' }) },
    );
    const banner = await screen.findByRole('note');
    expect(within(banner).getByText(en('suspensionClosure.banner.SUSPENDED.title'))).toBeTruthy();
    expect(
      within(banner)
        .getByRole('link', { name: en('suspensionClosure.banner.SUSPENDED.link') })
        .getAttribute('href'),
    ).toBe(`/projects/${PROJECT_ID}/suspension`);
  });

  test("SCR-108 across projects names each request's project", async () => {
    open('/suspension-requests');
    const table = await screen.findByRole('table', {
      name: en('suspensionClosure.suspension.caption'),
    });
    await waitFor(() => {
      expect(within(table).getByText('Coastal road upgrade')).toBeTruthy();
    });
  });
});
