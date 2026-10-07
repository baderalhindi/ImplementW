import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { type Session } from '@/features/identity-access/session/sessionApi.ts';
import { type Language, translate } from '@/shared/i18n/i18n.ts';
import { APPROVER_ID, approvalRun, sessionAs, withApprovalNames } from '@/test/approvalFixtures.ts';
import {
  BEARING_ID,
  CHANGE_REQUEST_ETAG,
  CHANGE_RUN_ID,
  CHANGE_TASK_ID,
  changeInboxItem,
  changeRequest,
  type ChangeRequestState,
  changeRun,
  NEW_CHANGE_REQUEST_ID,
  PAVING_ID,
  pavingAt,
  withChangeRequests,
} from '@/test/changeRequestFixtures.ts';
import { concernProject, managerSession } from '@/test/concernFixtures.ts';
import { type MockApi, mockApi, page, problem } from '@/test/mockApi.ts';
import {
  PROJECT_ID,
  reviewerSession,
  withProject,
  withProjectLookups,
} from '@/test/projectFixtures.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';

// SCR-105 Change Requests, SCR-106 Create Change Request and SCR-107 Change Request Detail (TASK-061) against the
// TASK-060 API, with WF-11's MOD-040–042 and MOD-044 reused. Acceptance criteria: (1) the form surfaces the computed
// materiality classification before final submission, so the requester understands the approval path they are entering;
// (2) the Detail screen separates "Approved" from "Implemented" as distinct, non-interchangeable badges. The workbook's
// validation checks: the classification is visible before submit is enabled; Approved and Implemented render as visually
// distinct statuses.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);

const NEW_PATH = `/change-requests/new?projectId=${PROJECT_ID}`;
const PAVING_PATH = `/change-requests/${PAVING_ID}`;

function open(
  path: string,
  state: ChangeRequestState = {},
  {
    session = managerSession(),
    language = 'en',
    projectHidden = false,
  }: { session?: Session; language?: Language; projectHidden?: boolean } = {},
): MockApi {
  const api = withChangeRequests(
    withProject(withProjectLookups(mockApi()), concernProject()),
    state,
  );
  if (projectHidden) {
    // R-47: a project the caller may not see is 404 — local.r04's own project on the shipped grants (F-1).
    api.on('GET', /^\/projects\/[^/]+$/, problem(404, 'NOT_FOUND'));
  }
  renderApp({ path, session, language });
  return api;
}

const submitButton = () =>
  screen.getByRole('button', { name: en('changeRequests.actions.submit') });
const classifyButton = () =>
  screen.getByRole('button', { name: en('changeRequests.actions.classify') });

/** Fills SCR-106 for a schedule change of 12 days and SAR 60,000. */
async function fillScheduleChange(user: ReturnType<typeof userEvent.setup>) {
  await user.selectOptions(
    await screen.findByRole('combobox', { name: /Change type/ }),
    'SCHEDULE',
  );
  await user.type(screen.getByRole('textbox', { name: /^Title/ }), 'Extend the paving season');
  await user.type(
    screen.getByRole('textbox', { name: /^Justification/ }),
    'The asphalt plant opens six weeks late.',
  );
  await user.type(screen.getByRole('textbox', { name: /^Cost impact/ }), '60000');
  await user.type(screen.getByRole('textbox', { name: /^Schedule impact/ }), '12');
}

/** The badge a state dimension shows ("status", "approval", "implementation"). */
function badgeOf(name: string): HTMLElement {
  const badge = document.querySelector<HTMLElement>(`[data-badge="${name}"] .badge`);
  if (badge === null) {
    throw new Error(`No ${name} badge.`);
  }
  return badge;
}

describe('SCR-106: the materiality classification is shown before submission (acceptance criterion 1)', () => {
  test('submit stays disabled until the server’s classification of the saved request, with its approval path, is shown', async () => {
    const api = open(NEW_PATH);
    const user = userEvent.setup();
    await fillScheduleChange(user);

    // The workbook's check: nothing can be submitted before the classification is visible.
    expect(submitButton()).toHaveProperty('disabled', true);
    expect(screen.getByText(en('changeRequests.form.classifyFirst'))).toBeTruthy();
    expect(
      screen.queryByRole('heading', { name: en('changeRequests.materiality.previewTitle') }),
    ).toBeNull();

    await user.click(classifyButton());

    const panel = (
      await screen.findByRole('heading', { name: en('changeRequests.materiality.previewTitle') })
    ).closest('section');
    if (panel === null) {
      throw new Error('No materiality panel.');
    }
    expect(
      within(panel).getByText(en('changeRequests.materiality.band', { band: 2 }), {
        selector: '[data-field="resultingBand"] .badge',
      }),
    ).toBeTruthy();
    expect(within(panel).getByText(en('changeRequests.materiality.path.band2'))).toBeTruthy();
    expect(submitButton()).toHaveProperty('disabled', false);
    expect(
      screen.getByText(en('changeRequests.form.readyToSubmit', { band: 2, revision: 1 })),
    ).toBeTruthy();

    // Classifying saved the draft first, and nothing the SPA sent names a band or a materiality.
    const [created] = api.requestsTo('POST', /^\/change-requests$/);
    expect(created?.body).toEqual({
      projectId: PROJECT_ID,
      changeType: 'SCHEDULE',
      title: { text: 'Extend the paving season', language: 'en' },
      justification: { text: 'The asphalt plant opens six weeks late.', language: 'en' },
      costImpactSar: '60000.00',
      scheduleImpactDays: 12,
      scopeImpact: null,
      isContractualObligation: false,
      requestedGovernanceProfileItemId: null,
    });
    expect(api.requestsTo('POST', /\/preview-materiality$/)[0]?.path).toBe(
      `/change-requests/${NEW_CHANGE_REQUEST_ID}/preview-materiality`,
    );

    await user.click(submitButton());

    expect(await screen.findByText(en('changeRequests.done.submitted'))).toBeTruthy();
    const [submitted] = api.requestsTo('POST', /\/submit$/);
    expect(submitted?.path).toBe(`/change-requests/${NEW_CHANGE_REQUEST_ID}/submit`);
    expect(submitted?.headers.get('If-Match')).toBe(CHANGE_REQUEST_ETAG);
  });

  test('a change after classification disables submission until the request is classified again', async () => {
    const api = open(NEW_PATH);
    const user = userEvent.setup();
    await fillScheduleChange(user);
    await user.click(classifyButton());
    await screen.findByRole('heading', { name: en('changeRequests.materiality.previewTitle') });

    const days = screen.getByRole('textbox', { name: /^Schedule impact/ });
    await user.clear(days);
    await user.type(days, '20');

    expect(submitButton()).toHaveProperty('disabled', true);
    expect(screen.getByText(en('changeRequests.form.materialityStale'))).toBeTruthy();
    expect(
      screen.queryByRole('heading', { name: en('changeRequests.materiality.previewTitle') }),
    ).toBeNull();

    await user.click(classifyButton());
    await screen.findByRole('heading', { name: en('changeRequests.materiality.previewTitle') });

    const [updated] = api.requestsTo('PUT', /^\/change-requests\//);
    expect(updated?.headers.get('If-Match')).toBe(CHANGE_REQUEST_ETAG);
    expect((updated?.body as { scheduleImpactDays: number }).scheduleImpactDays).toBe(20);
    expect(api.requestsTo('POST', /\/preview-materiality$/)).toHaveLength(2);
    expect(api.requestsTo('POST', /^\/change-requests$/)).toHaveLength(1);
    expect(submitButton()).toHaveProperty('disabled', false);
  });

  test('a schedule change is not classified without its days, though its draft may be saved without them', async () => {
    const api = open(NEW_PATH);
    const user = userEvent.setup();
    await fillScheduleChange(user);
    await user.clear(screen.getByRole('textbox', { name: /^Schedule impact/ }));

    await user.click(classifyButton());

    expect(screen.getByText(en('changeRequests.fieldErrors.daysRequired'))).toBeTruthy();
    expect(document.activeElement).toBe(screen.getByRole('textbox', { name: /^Schedule impact/ }));
    expect(api.requestsTo('POST', /^\/change-requests/)).toHaveLength(0);
    expect(submitButton()).toHaveProperty('disabled', true);

    await user.click(screen.getByRole('button', { name: en('changeRequests.actions.saveDraft') }));

    expect(await screen.findByText(en('changeRequests.done.saved'))).toBeTruthy();
    expect(
      (api.requestsTo('POST', /^\/change-requests$/)[0]?.body as { scheduleImpactDays: null })
        .scheduleImpactDays,
    ).toBeNull();
  });

  test('a zero or malformed impact is flagged as it is typed', async () => {
    open(NEW_PATH);
    const user = userEvent.setup();
    await user.type(await screen.findByRole('textbox', { name: /^Schedule impact/ }), '0');
    await user.type(screen.getByRole('textbox', { name: /^Cost impact/ }), '12.345');

    expect(screen.getByText(en('changeRequests.fieldErrors.daysOutOfRange'))).toBeTruthy();
    expect(screen.getByText(en('changeRequests.fieldErrors.costMalformed'))).toBeTruthy();
  });

  test('a Project Manager who cannot read the project still raises and classifies it; the API decides', async () => {
    const api = open(NEW_PATH, {}, { projectHidden: true });
    const user = userEvent.setup();

    expect(await screen.findByText(en('changeRequests.detail.projectUnreadable'))).toBeTruthy();
    expect(screen.queryByText(en('changeRequests.form.notRaiser'))).toBeNull();
    await fillScheduleChange(user);
    await user.click(classifyButton());

    await screen.findByRole('heading', { name: en('changeRequests.materiality.previewTitle') });
    expect(
      (api.requestsTo('POST', /^\/change-requests$/)[0]?.body as { projectId: string }).projectId,
    ).toBe(PROJECT_ID);
    expect(submitButton()).toHaveProperty('disabled', false);
  });

  test('when no classification can be computed, the reason is shown and submission stays disabled', async () => {
    const api = open(NEW_PATH, { preview: problem(422, 'CONFIGURATION_MISSING') });
    const user = userEvent.setup();
    await fillScheduleChange(user);

    await user.click(classifyButton());

    await waitFor(() => {
      expect(screen.getByRole('alert').textContent).toBe(
        en('changeRequests.problems.configurationMissing'),
      );
    });
    expect(submitButton()).toHaveProperty('disabled', true);
    expect(api.requestsTo('POST', /^\/change-requests$/)).toHaveLength(1);
    expect(api.requestsTo('POST', /\/submit$/)).toHaveLength(0);
  });

  test('the classified form has no accessibility violations, in Arabic too', async () => {
    open(NEW_PATH, {}, { language: 'ar' });
    const user = userEvent.setup();
    const ar = (key: string) => translate('ar', key);
    await user.selectOptions(
      await screen.findByRole('combobox', {
        name: new RegExp(ar('changeRequests.fields.changeType')),
      }),
      'COST',
    );
    await user.type(
      screen.getByRole('textbox', { name: new RegExp(`^${ar('changeRequests.fields.title')}`) }),
      'تمديد موسم الرصف',
    );
    await user.type(
      screen.getByRole('textbox', {
        name: new RegExp(`^${ar('changeRequests.fields.justification')}`),
      }),
      'يتأخر مصنع الإسفلت ستة أسابيع.',
    );
    await user.type(
      screen.getByRole('textbox', {
        name: new RegExp(`^${ar('changeRequests.fields.costImpactSar')}`),
      }),
      '60000',
    );
    await user.click(screen.getByRole('button', { name: ar('changeRequests.actions.classify') }));

    await screen.findByRole('heading', { name: ar('changeRequests.materiality.previewTitle') });
    expect(describeViolations(await accessibilityViolations())).toBe('');
  });
});

describe('SCR-107: Approved and Implemented are distinct, non-interchangeable badges (acceptance criterion 2)', () => {
  test('an approved request reads Approved for its approval and Not started for its implementation', async () => {
    open(PAVING_PATH, {
      requests: [pavingAt('APPROVED')],
      runs: [changeRun({ status: 'APPROVED' })],
    });

    await screen.findByRole('heading', { level: 1, name: 'Extend the paving season' });
    expect(badgeOf('approval').textContent).toBe(en('changeRequests.approvalState.APPROVED'));
    expect(badgeOf('approval').className).toContain('badge--approved');
    expect(badgeOf('implementation').textContent).toBe(
      en('changeRequests.implementationState.NOT_STARTED'),
    );
    expect(badgeOf('implementation').className).not.toContain('badge--implemented');
    expect(
      screen.getByText(en('changeRequests.detail.explain.APPROVED', { total: 2 })),
    ).toBeTruthy();

    // Both authorisations issued, neither applied: approval changed nothing in the schedule or the budget.
    const table = screen.getByRole('table', {
      name: en('changeRequests.detail.authorizationsCaption'),
    });
    expect(
      within(table).getAllByText(en('changeRequests.authorization.statuses.ISSUED')),
    ).toHaveLength(2);
    expect(
      screen.getByRole('button', { name: en('changeRequests.command.startImplementation.title') }),
    ).toBeTruthy();
    expect(
      screen.queryByRole('button', { name: en('changeRequests.command.markImplemented.title') }),
    ).toBeNull();
  });

  test('an implemented request shows Approved and Implemented in two different styles and words', async () => {
    open(PAVING_PATH, { requests: [pavingAt('IMPLEMENTED')] });

    await screen.findByRole('heading', { level: 1, name: 'Extend the paving season' });
    const approval = badgeOf('approval');
    const implementation = badgeOf('implementation');
    expect(approval.textContent).toBe(en('changeRequests.approvalState.APPROVED'));
    expect(implementation.textContent).toBe(en('changeRequests.implementationState.IMPLEMENTED'));
    expect(approval.className).toBe('badge badge--approved');
    expect(implementation.className).toBe('badge badge--implemented');
    // Each carries its own icon: a tick for approved, a double tick for implemented.
    expect(approval.querySelector('path')?.getAttribute('d')).not.toBe(
      implementation.querySelector('path')?.getAttribute('d'),
    );
    expect(badgeOf('status').className).toBe('badge badge--implemented');
    expect(describeViolations(await accessibilityViolations())).toBe('');
  });

  test('during implementation it says how many authorisations are applied and waits for the rest', async () => {
    open(PAVING_PATH, { requests: [pavingAt('IMPLEMENTATION')] });

    await screen.findByRole('heading', { level: 1, name: 'Extend the paving season' });
    expect(badgeOf('approval').textContent).toBe(en('changeRequests.approvalState.APPROVED'));
    expect(badgeOf('implementation').textContent).toBe(
      en('changeRequests.implementationState.IN_PROGRESS'),
    );
    expect(
      screen.getByText(en('changeRequests.detail.appliedCount', { applied: 1, total: 2 })),
    ).toBeTruthy();
    expect(screen.getByText(en('changeRequests.detail.waitingForApplication'))).toBeTruthy();
    expect(
      screen.queryByRole('button', { name: en('changeRequests.command.markImplemented.title') }),
    ).toBeNull();
  });

  test('in the list, Approved and Implemented requests read and look different', async () => {
    open(`/projects/${PROJECT_ID}/change-requests`, {
      requests: [
        changeRequest({ id: 'c8000000-0000-4000-8000-0000000002a1', status: 'APPROVED' }),
        changeRequest({
          id: 'c8000000-0000-4000-8000-0000000002a2',
          title: { text: 'Resurface the shoulder', language: 'EN' },
          status: 'IMPLEMENTED',
        }),
      ],
    });

    const table = await screen.findByRole('table', {
      name: en('changeRequests.list.projectCaption'),
    });
    const approved = within(table).getByText(en('changeRequests.status.APPROVED'));
    const implemented = within(table).getByText(en('changeRequests.status.IMPLEMENTED'));
    expect(approved.className).toBe('badge badge--approved');
    expect(implemented.className).toBe('badge badge--implemented');
  });
});

describe('SCR-107 reuses the WF-11 approval modals', () => {
  test('an approver decides the revision under review through MOD-041, with the reason it requires', async () => {
    const api = open(
      PAVING_PATH,
      { requests: [pavingAt('UNDER_REVIEW')], inbox: [changeInboxItem()], runs: 'forbidden' },
      { session: sessionAs(APPROVER_ID) },
    );
    api.on('POST', /^\/approval-tasks\/[^/]+\/reject$/, {
      body: approvalRun({ id: CHANGE_RUN_ID, status: 'REJECTED' }),
    });
    const user = userEvent.setup();

    await user.click(
      await screen.findByRole('button', { name: en('approvals.decision.reject.action') }),
    );
    const dialog = screen.getByRole('dialog', { name: en('approvals.decision.reject.title') });
    await user.click(
      within(dialog).getByRole('button', { name: en('approvals.decision.reject.confirm') }),
    );
    expect(within(dialog).getByText(en('common.fieldErrors.required'))).toBeTruthy();
    expect(api.requestsTo('POST', /^\/approval-tasks\//)).toHaveLength(0);

    await user.type(within(dialog).getByRole('textbox', { name: /Reason/ }), 'Not funded.');
    await user.click(
      within(dialog).getByRole('button', { name: en('approvals.decision.reject.confirm') }),
    );

    expect(await screen.findByText(en('changeRequests.done.reject'))).toBeTruthy();
    const [sent] = api.requestsTo('POST', /^\/approval-tasks\//);
    expect(sent?.path).toBe(`/approval-tasks/${CHANGE_TASK_ID}/reject`);
    expect(sent?.body).toEqual({ reason: { text: 'Not funded.', language: 'en' } });
    // The run's history needs APPROVAL_VIEW, which this approver lacks.
    expect(screen.getByText(en('changeRequests.detail.reviewUnreadable'))).toBeTruthy();
  });

  test('the originator is never offered a decision on their own change', async () => {
    open(PAVING_PATH, {
      requests: [pavingAt('UNDER_REVIEW')],
      inbox: [changeInboxItem()],
      runs: [changeRun()],
    });

    await screen.findByRole('heading', { level: 1, name: 'Extend the paving season' });
    await screen.findByRole('link', {
      name: en('changeRequests.detail.reviewOfRevision', { revision: 1 }),
    });
    expect(
      screen.queryByRole('button', { name: en('approvals.decision.approve.action') }),
    ).toBeNull();
  });

  test('the originator withdraws a review in progress through MOD-044, which ends the run', async () => {
    const api = open(PAVING_PATH, { requests: [pavingAt('UNDER_REVIEW')], runs: [changeRun()] });
    api.on('POST', /^\/approval-instances\/[^/]+\/withdraw$/, {
      body: approvalRun({ id: CHANGE_RUN_ID, status: 'WITHDRAWN' }),
    });
    const user = userEvent.setup();

    await user.click(
      await screen.findByRole('button', { name: en('changeRequests.actions.withdrawReview') }),
    );
    const dialog = screen.getByRole('dialog', { name: en('approvals.withdraw.title') });
    await user.click(
      within(dialog).getByRole('button', { name: en('approvals.withdraw.confirm') }),
    );

    expect(await screen.findByText(en('changeRequests.done.reviewWithdrawn'))).toBeTruthy();
    expect(api.requestsTo('POST', /^\/approval-instances\//)[0]?.path).toBe(
      `/approval-instances/${CHANGE_RUN_ID}/withdraw`,
    );
    // The request's own withdraw is for before the review; under review the run is withdrawn instead.
    expect(api.requestsTo('POST', /^\/change-requests\//)).toHaveLength(0);
  });

  test('the Department Manager starts the review, which records the classification; the raiser is not offered it', async () => {
    const api = open(
      PAVING_PATH,
      { requests: [pavingAt('SUBMITTED')] },
      { session: reviewerSession() },
    );
    const user = userEvent.setup();

    await user.click(
      await screen.findByRole('button', { name: en('changeRequests.command.startReview.title') }),
    );
    const dialog = screen.getByRole('dialog', {
      name: en('changeRequests.command.startReview.title'),
    });
    expect(
      within(dialog).getByText(en('changeRequests.command.startReview.consequence')),
    ).toBeTruthy();
    await user.click(
      within(dialog).getByRole('button', { name: en('changeRequests.command.startReview.title') }),
    );

    expect(await screen.findByText(en('changeRequests.done.reviewStarted'))).toBeTruthy();
    expect(api.requestsTo('POST', /\/start-review$/)[0]?.headers.get('If-Match')).toBe(
      CHANGE_REQUEST_ETAG,
    );
    expect(
      await screen.findByRole('heading', { name: en('changeRequests.materiality.recordedTitle') }),
    ).toBeTruthy();
  });

  test('the raiser of a submitted request may withdraw it but not start its review', async () => {
    open(PAVING_PATH, { requests: [pavingAt('SUBMITTED')] });

    expect(
      await screen.findByRole('button', { name: en('changeRequests.command.withdraw.title') }),
    ).toBeTruthy();
    expect(
      screen.queryByRole('button', { name: en('changeRequests.command.startReview.title') }),
    ).toBeNull();
  });

  test('the approval inbox links a change request to its detail', async () => {
    withApprovalNames(mockApi()).on('GET', /^\/approval-tasks$/, {
      body: page([changeInboxItem()]),
    });
    renderApp({ path: '/approvals/inbox', session: sessionAs(APPROVER_ID) });

    const link = await screen.findByRole('link', { name: 'ChangeRequest · ChangeRequest' });
    expect(link.getAttribute('href')).toBe(PAVING_PATH);
  });
});

describe('SCR-105 Change Requests', () => {
  test('the project’s list offers raising to its Project Manager and filters by status', async () => {
    open(`/projects/${PROJECT_ID}/change-requests`, {
      requests: [
        changeRequest(),
        changeRequest({
          id: 'c8000000-0000-4000-8000-0000000002a3',
          title: { text: 'Close lane 2', language: 'EN' },
          status: 'UNDER_REVIEW',
          materiality: pavingAt('UNDER_REVIEW').materiality,
        }),
      ],
    });
    const user = userEvent.setup();

    const table = await screen.findByRole('table', {
      name: en('changeRequests.list.projectCaption'),
    });
    expect(within(table).getAllByRole('row')).toHaveLength(3);
    expect(
      within(table).getByRole('link', { name: 'Extend the paving season' }).getAttribute('href'),
    ).toBe(PAVING_PATH);
    expect(within(table).getByText(en('changeRequests.table.notRecorded'))).toBeTruthy();
    expect(
      within(table).getByText(en('changeRequests.materiality.band', { band: 2 })),
    ).toBeTruthy();
    expect(
      screen.getByRole('link', { name: en('changeRequests.actions.raise') }).getAttribute('href'),
    ).toBe(NEW_PATH);

    await user.selectOptions(
      screen.getByRole('combobox', { name: en('changeRequests.filter.status') }),
      'UNDER_REVIEW',
    );
    expect(within(table).getAllByRole('row')).toHaveLength(2);
    expect(
      screen.getByText(en('changeRequests.filter.showing', { shown: 1, total: 2 })),
    ).toBeTruthy();
  });

  test('someone who does not manage the project is not offered raising', async () => {
    open(`/projects/${PROJECT_ID}/change-requests`, {}, { session: reviewerSession() });

    await screen.findByRole('table', { name: en('changeRequests.list.projectCaption') });
    expect(screen.queryByRole('link', { name: en('changeRequests.actions.raise') })).toBeNull();
  });

  test('across projects, each request names its project, most recently changed first', async () => {
    const api = open('/change-requests');

    const table = await screen.findByRole('table', { name: en('changeRequests.list.caption') });
    const rows = within(table).getAllByRole('row').slice(1);
    expect(rows.map((row) => within(row).getAllByRole('cell')[1]?.textContent)).toEqual([
      'Harbour bridge repair',
      'Coastal road upgrade',
    ]);
    expect(
      within(rows[0] ?? table)
        .getByRole('link', { name: 'Replace the bearing pads' })
        .getAttribute('href'),
    ).toBe(`/change-requests/${BEARING_ID}`);
    expect(api.requestsTo('GET', /^\/change-requests$/)).toHaveLength(2);
  });
});
