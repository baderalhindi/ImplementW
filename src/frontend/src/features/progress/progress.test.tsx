import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { type Session } from '@/features/identity-access/session/sessionApi.ts';
import { type ProjectDetail } from '@/features/projects/api/types.ts';
import { translate } from '@/shared/i18n/i18n.ts';
import { type MockApi, mockApi, problem } from '@/test/mockApi.ts';
import {
  CYCLE_ID,
  INTAKE_ID,
  liveHealth,
  PUBLISHED_SUBMISSION_ID,
  RETURNED_SUBMISSION_ID,
  snapshot,
  submission,
  SUBMISSION_ETAG,
  SUBMISSION_ID,
  submitted,
  withProgress,
} from '@/test/progressFixtures.ts';
import {
  ENTITY_USER_ID,
  entitySession,
  projectDetail,
  PROJECT_ID,
  reviewerSession,
  withProject,
  withProjectLookups,
} from '@/test/projectFixtures.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';

import { type ProgressSubmissionDetail } from './api/types.ts';

// SCR-048 Project Progress tab, SCR-070 Progress Update History and MOD-020 to MOD-022 (TASK-045), against the WF-02
// API (TASK-044). Acceptance criteria: (1) an out-of-range percentage is refused client- and server-side with a
// field-level error; (2) the history paginates and shows Published and current/live values, each with its own badge.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);

/** Interpolated values are bidi-isolated, so expected texts are built the same way. */
const points = (value: string) => en('progress.figures.points', { value });
const range = (start: string, end: string) => en('progress.period.range', { start, end });

const PROGRESS_PATH = `/projects/${PROJECT_ID}/progress`;
const HISTORY_PATH = `${PROGRESS_PATH}/history`;
const NEW_ETAG = '"22"';

/** An ACTIVE project managed by the entity's Project Manager (ADR-013). */
function activeProject(overrides: Partial<ProjectDetail> = {}): ProjectDetail {
  return projectDetail({
    status: 'ACTIVE',
    projectManagerUserId: ENTITY_USER_ID,
    activatedAt: '2026-08-01T00:00:00Z',
    ...overrides,
  });
}

function open(
  session: Session,
  state: Parameters<typeof withProgress>[1] = {},
  { project = activeProject(), path = PROGRESS_PATH } = {},
): MockApi {
  const api = withProgress(withProject(withProjectLookups(mockApi()), project), state);
  renderApp({ path, session });
  return api;
}

const pressButton = async (key: string) => {
  await userEvent.click(await screen.findByRole('button', { name: en(key) }));
};

async function openDialog(actionKey: string, titleKey: string) {
  await pressButton(actionKey);
  return screen.findByRole('dialog', { name: en(titleKey) });
}

const percentInput = (dialog: HTMLElement) =>
  within(dialog).getByLabelText(new RegExp(`^${escape(en('progress.update.overridePercent'))}`));
const reasonInput = (dialog: HTMLElement) =>
  within(dialog).getByLabelText(new RegExp(`^${escape(en('progress.update.overrideReason'))}`));

/** The table's row at `index` (0 is the header), to query within. */
function rowAt(table: HTMLElement, index: number) {
  const row = within(table).getAllByRole('row')[index];
  if (row === undefined) {
    throw new Error(`The table has no row ${String(index)}.`);
  }
  return within(row);
}

function escape(text: string): string {
  return text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

describe('SCR-048 shows the official and the live value side by side, each badged (M-12)', () => {
  test('Published/official and current/live carry distinct badges, and planned and actual stand side by side (ADR-009)', async () => {
    open(entitySession(), { snapshots: [snapshot()], live: [liveHealth()] });

    const official = await screen.findByRole('region', { name: /Official progress/ });
    const live = screen.getByRole('region', { name: /Current progress/ });

    const officialBadge = within(official).getByText(en('progress.semanticState.official'));
    const liveBadge = within(live).getByText(en('progress.semanticState.live'));
    expect(officialBadge.className).toBe('badge badge--official');
    expect(liveBadge.className).toBe('badge badge--live');

    // The official record: 30% reported as an override against 35% planned, AMBER.
    expect(within(official).getByText(en('progress.health.AMBER'))).toBeTruthy();
    expect(within(official).getByText('35%')).toBeTruthy();
    expect(within(official).getByText('30%')).toBeTruthy();
    expect(within(official).getByText(en('progress.figures.overridden'))).toBeTruthy();
    expect(within(official).getByText(points('\u22125'))).toBeTruthy();
    // The live value: derived figures only, never an override.
    expect(within(live).getByText(en('progress.health.RED'))).toBeTruthy();
    expect(within(live).getByText('55%')).toBeTruthy();
    expect(within(live).getByText(points('\u221215'))).toBeTruthy();
    expect(within(live).queryByText(en('progress.figures.overridden'))).toBeNull();
  });

  test('UNKNOWN health is a word in grey, never a colour (TASK-044 D-4)', async () => {
    open(entitySession(), {
      live: [liveHealth({ overallHealth: 'UNKNOWN', plannedPercent: null })],
    });

    const live = await screen.findByRole('region', { name: /Current progress/ });
    expect(within(live).getByText(en('progress.health.UNKNOWN')).className).toBe(
      'badge badge--neutral',
    );
    expect(within(live).getByText(en('progress.figures.noPlan'))).toBeTruthy();
    expect(within(live).getByText(en('progress.figures.noVariance'))).toBeTruthy();
  });

  test('before anything is reported, each state says it has no value yet', async () => {
    open(entitySession());

    expect(await screen.findByText(en('progress.official.none'))).toBeTruthy();
    expect(screen.getByText(en('progress.live.none'))).toBeTruthy();
    expect(screen.getByText(en('progress.current.none'))).toBeTruthy();
  });

  test('without PROGRESS_VIEW the tab says so instead of failing', async () => {
    const api = withProgress(withProject(withProjectLookups(mockApi()), activeProject()));
    api.on('GET', /^\/published-progress-snapshots$/, problem(403, 'PERMISSION_DENIED'));
    renderApp({ path: PROGRESS_PATH, session: entitySession() });

    expect(await screen.findByText(en('progress.forbidden'))).toBeTruthy();
  });
});

describe('SCR-048 error state', () => {
  test('a failed read shows an error and reads again on retry', async () => {
    const api = withProgress(withProject(withProjectLookups(mockApi()), activeProject()));
    api.on('GET', /^\/project-health-statuses$/, problem(500, 'INTERNAL_ERROR'));
    renderApp({ path: PROGRESS_PATH, session: entitySession() });

    const alert = await screen.findByRole('alert');
    expect(alert.textContent).toContain(
      en('common.problems.unexpectedWithReference', { reference: 'test-correlation-id' }),
    );
    api.on('GET', /^\/project-health-statuses$/, {
      body: { items: [], page: 1, pageSize: 25, totalCount: 0 },
    });
    await userEvent.click(within(alert).getByRole('button', { name: en('common.actions.retry') }));
    expect(await screen.findByText(en('progress.current.none'))).toBeTruthy();
  });
});

describe('starting an update (TASK-044 D-5, ADR-017)', () => {
  test('the Project Manager starts the period’s update and lands in MOD-020 with the pre-filled draft', async () => {
    const submissions: ProgressSubmissionDetail[] = [];
    const api = open(entitySession(), { submissions });
    api
      .on('POST', /^\/progress-submissions$/, () => {
        submissions.push(submission());
        return { status: 201, body: submission(), headers: { ETag: SUBMISSION_ETAG } };
      })
      .on('GET', new RegExp(`^/progress-submissions/${SUBMISSION_ID}$`), {
        body: submission(),
        headers: { ETag: SUBMISSION_ETAG },
      });

    const dialog = await openDialog('progress.actions.start', 'progress.update.submitTitle');

    expect(api.requestsTo('POST', /^\/progress-submissions$/)[0]?.body).toEqual({
      projectId: PROJECT_ID,
    });
    expect(within(dialog).getByText(en('progress.update.prefilled'))).toBeTruthy();
    expect(
      within(dialog).getByText(
        en('progress.update.period', { period: range('2026-09-01', '2026-09-30'), revision: 1 }),
      ),
    ).toBeTruthy();
    // Planned and the calculated actual are shown, never typed (ADR-009).
    expect(within(dialog).getByText('50%')).toBeTruthy();
    expect(within(dialog).getByText('40%')).toBeTruthy();
  });

  test('an update already in progress (a retried start, F-17) is shown, not reported as a failure', async () => {
    const api = open(entitySession());
    api.on('POST', /^\/progress-submissions$/, problem(409, 'PROGRESS_SUBMISSION_EXISTS'));

    await pressButton('progress.actions.start');

    expect(await screen.findByText(en('progress.problems.submissionExists'))).toBeTruthy();
    expect(screen.queryByRole('alert')).toBeNull();
  });

  test('a start the API refuses says why beside the button', async () => {
    const api = open(entitySession());
    api.on('POST', /^\/progress-submissions$/, problem(422, 'PROGRESS_ROLLUP_UNAVAILABLE'));

    await pressButton('progress.actions.start');

    expect((await screen.findByRole('alert')).textContent).toBe(
      en('progress.problems.rollupUnavailable'),
    );
  });

  test('nothing is offered on a project that is not ACTIVE, nor to someone who is not its Project Manager', async () => {
    open(entitySession(), {}, { project: activeProject({ status: 'APPROVED_PLANNED' }) });
    expect(await screen.findByText(en('progress.current.notActive'))).toBeTruthy();
    expect(screen.queryByRole('button', { name: en('progress.actions.start') })).toBeNull();
  });

  test('an AHDA reviewer is not offered the Project Manager’s start', async () => {
    open(reviewerSession());
    expect(await screen.findByText(en('progress.current.none'))).toBeTruthy();
    expect(screen.queryByRole('button', { name: en('progress.actions.start') })).toBeNull();
  });
});

describe('acceptance criterion 1: an out-of-range percentage is refused with a field-level error', () => {
  async function overrideDialog() {
    const dialog = await openDialog('progress.actions.submit', 'progress.update.submitTitle');
    await userEvent.click(
      within(dialog).getByRole('radio', { name: en('progress.update.useOverride') }),
    );
    return dialog;
  }

  test.each(['120', '-1', '100.5', '42.12345'])(
    'client-side: %s is flagged on the field as it is typed, and nothing is sent',
    async (value) => {
      const api = open(entitySession(), { submissions: [submission()] });
      const dialog = await overrideDialog();

      await userEvent.type(percentInput(dialog), value);
      await userEvent.type(reasonInput(dialog), 'Survey of the northern section');

      const input = percentInput(dialog);
      expect(input.getAttribute('aria-invalid')).toBe('true');
      const errorId = input.getAttribute('aria-describedby')?.split(' ').at(-1) ?? '';
      expect(document.getElementById(errorId)?.textContent).toBe(
        en('progress.fieldErrors.percentOutOfRange'),
      );

      await userEvent.click(
        within(dialog).getByRole('button', { name: en('progress.update.confirmSubmit') }),
      );
      expect(within(dialog).getByRole('alert').textContent).toBe(
        en('common.form.fixErrors', { count: 1 }),
      );
      expect(document.activeElement).toBe(input);
      expect(api.requestsTo('PUT', /^\/progress-submissions\//)).toHaveLength(0);
      expect(api.requestsTo('POST', /\/submit$/)).toHaveLength(0);
    },
  );

  test('server-side: the API’s OUT_OF_RANGE lands on the percentage field, and the update is not submitted', async () => {
    const api = open(entitySession(), { submissions: [submission()] });
    api.on(
      'PUT',
      new RegExp(`^/progress-submissions/${SUBMISSION_ID}$`),
      problem(400, 'VALIDATION_FAILED', [
        { field: 'override.actualPercent', code: 'OUT_OF_RANGE' },
      ]),
    );
    const dialog = await overrideDialog();

    // A value the client accepts but the server refuses (e.g. a tolerance the API applies, UGV-02).
    await userEvent.type(percentInput(dialog), '99');
    await userEvent.type(reasonInput(dialog), 'Survey of the northern section');
    await userEvent.click(
      within(dialog).getByRole('button', { name: en('progress.update.confirmSubmit') }),
    );

    const input = percentInput(dialog);
    await screen.findByText(en('progress.fieldErrors.percentOutOfRange'));
    expect(input.getAttribute('aria-invalid')).toBe('true');
    expect(within(dialog).getByRole('alert').textContent).toBe(
      en('common.problems.validationFailed'),
    );
    expect(api.requestsTo('PUT', /^\/progress-submissions\//)).toHaveLength(1);
    expect(api.requestsTo('POST', /\/submit$/)).toHaveLength(0);
    // Typing again clears the server's verdict.
    await userEvent.type(input, '.5');
    expect(input.getAttribute('aria-invalid')).toBeNull();
  });

  test('an override without its reason is refused on the reason field (ADR-009)', async () => {
    const api = open(entitySession(), { submissions: [submission()] });
    const dialog = await overrideDialog();

    await userEvent.type(percentInput(dialog), '45');
    await userEvent.click(
      within(dialog).getByRole('button', { name: en('progress.update.confirmSubmit') }),
    );

    expect(reasonInput(dialog).getAttribute('aria-invalid')).toBe('true');
    expect(within(dialog).getByText(en('common.fieldErrors.required'))).toBeTruthy();
    expect(api.requestsTo('PUT', /^\/progress-submissions\//)).toHaveLength(0);
  });
});

describe('MOD-020 and MOD-021', () => {
  test('MOD-020 saves the override with the ETag it was shown, then submits with the new one', async () => {
    const api = open(entitySession(), { submissions: [submission()] });
    api
      .on('PUT', new RegExp(`^/progress-submissions/${SUBMISSION_ID}$`), {
        body: submission({ isOverridden: true, actualPercentOverride: 45, actualPercent: 45 }),
        headers: { ETag: NEW_ETAG },
      })
      .on('POST', /\/submit$/, { body: submitted() });

    const dialog = await openDialog('progress.actions.submit', 'progress.update.submitTitle');
    await userEvent.click(
      within(dialog).getByRole('radio', { name: en('progress.update.useOverride') }),
    );
    await userEvent.type(percentInput(dialog), '45.25');
    await userEvent.type(reasonInput(dialog), 'Survey of the northern section');
    await userEvent.click(
      within(dialog).getByRole('button', { name: en('progress.update.confirmSubmit') }),
    );

    expect(await screen.findByText(en('progress.done.submitted'))).toBeTruthy();
    const [put] = api.requestsTo('PUT', /^\/progress-submissions\//);
    expect(put?.headers.get('If-Match')).toBe(SUBMISSION_ETAG);
    expect(put?.headers.get('Idempotency-Key')).toBeTruthy();
    expect(put?.body).toEqual({
      narrative: { text: 'Earthworks on the northern section.', language: 'en' },
      override: {
        actualPercent: 45.25,
        reason: { text: 'Survey of the northern section', language: 'en' },
      },
    });
    const [submit] = api.requestsTo('POST', /\/submit$/);
    expect(submit?.path).toBe(`/progress-submissions/${SUBMISSION_ID}/submit`);
    expect(submit?.headers.get('If-Match')).toBe(NEW_ETAG);
  });

  test('confirming a pre-filled draft as it is submits it without an edit (ADR-017)', async () => {
    const api = open(entitySession(), { submissions: [submission()] });
    api.on('POST', /\/submit$/, { body: submitted() });

    const dialog = await openDialog('progress.actions.submit', 'progress.update.submitTitle');
    await userEvent.click(
      within(dialog).getByRole('button', { name: en('progress.update.confirmSubmit') }),
    );

    expect(await screen.findByText(en('progress.done.submitted'))).toBeTruthy();
    expect(api.requestsTo('PUT', /^\/progress-submissions\//)).toHaveLength(0);
    expect(api.requestsTo('POST', /\/submit$/)[0]?.headers.get('If-Match')).toBe(SUBMISSION_ETAG);
  });

  test('MOD-021 saves the draft and does not submit it', async () => {
    const api = open(entitySession(), { submissions: [submission()] });
    api.on('PUT', new RegExp(`^/progress-submissions/${SUBMISSION_ID}$`), {
      body: submission(),
      headers: { ETag: NEW_ETAG },
    });

    const dialog = await openDialog('progress.actions.edit', 'progress.update.editTitle');
    const narrative = within(dialog).getByLabelText(en('progress.update.narrative'));
    await userEvent.clear(narrative);
    await userEvent.type(narrative, 'Earthworks complete.');
    await userEvent.click(
      within(dialog).getByRole('button', { name: en('progress.update.confirmSave') }),
    );

    expect(await screen.findByText(en('progress.done.saved'))).toBeTruthy();
    expect(api.requestsTo('PUT', /^\/progress-submissions\//)[0]?.body).toEqual({
      narrative: { text: 'Earthworks complete.', language: 'en' },
      override: null,
    });
    expect(api.requestsTo('POST', /\/submit$/)).toHaveLength(0);
  });

  test('a draft changed meanwhile (412) closes the dialog, says so and shows the latest version', async () => {
    const api = open(entitySession(), { submissions: [submission()] });
    api.on('POST', /\/submit$/, problem(412, 'PRECONDITION_FAILED'));

    const dialog = await openDialog('progress.actions.submit', 'progress.update.submitTitle');
    await userEvent.click(
      within(dialog).getByRole('button', { name: en('progress.update.confirmSubmit') }),
    );

    expect(await screen.findByText(en('progress.done.stale'))).toBeTruthy();
    expect(screen.queryByRole('dialog')).toBeNull();
  });

  test('a returned revision’s reason is shown on the draft that follows it', async () => {
    open(entitySession(), {
      submissions: [
        submission({ revisionNo: 2 }),
        submitted({
          id: RETURNED_SUBMISSION_ID,
          status: 'RETURNED',
          returnReason: { text: 'Attach the survey.', language: 'EN' },
        }),
      ],
    });

    expect(await screen.findByText(en('progress.current.returned'))).toBeTruthy();
    expect(screen.getByText('Attach the survey.')).toBeTruthy();
  });
});

describe('MOD-022 review is AHDA’s gate (ADR-013, TASK-044 D-9)', () => {
  test('the submitter is not offered the review of their own update', async () => {
    open(entitySession(), { submissions: [submitted()] });

    expect(await screen.findByText(en('progress.current.next.SUBMITTED'))).toBeTruthy();
    expect(screen.queryByRole('button', { name: en('progress.actions.review') })).toBeNull();
    expect(screen.queryByRole('button', { name: en('progress.actions.submit') })).toBeNull();
  });

  test('an AHDA reviewer takes a submitted update into review', async () => {
    const api = open(reviewerSession(), { submissions: [submitted()] });
    api.on('POST', /\/start-review$/, { body: submitted({ status: 'UNDER_REVIEW' }) });

    const dialog = await openDialog('progress.actions.review', 'progress.review.title');
    await userEvent.click(
      within(dialog).getByRole('button', { name: en('progress.review.start') }),
    );

    expect(await screen.findByText(en('progress.done.reviewStarted'))).toBeTruthy();
    expect(api.requestsTo('POST', /\/start-review$/)[0]?.headers.get('If-Match')).toBe(
      SUBMISSION_ETAG,
    );
  });

  test('returning needs a reason, which is sent in the language it was typed in', async () => {
    const underReview = submitted({ status: 'UNDER_REVIEW' });
    const api = open(reviewerSession(), { submissions: [underReview] });
    api.on('POST', /\/return$/, { body: { ...underReview, status: 'RETURNED' } });

    const dialog = await openDialog('progress.actions.review', 'progress.review.title');
    await userEvent.click(
      within(dialog).getByRole('radio', { name: en('progress.review.return') }),
    );
    await userEvent.click(
      within(dialog).getByRole('button', { name: en('progress.review.confirm') }),
    );
    expect(within(dialog).getByText(en('common.fieldErrors.required'))).toBeTruthy();
    expect(api.requestsTo('POST', /\/return$/)).toHaveLength(0);

    await userEvent.type(
      within(dialog).getByLabelText(new RegExp(`^${escape(en('progress.review.reason'))}`)),
      'Attach the survey.',
    );
    await userEvent.click(
      within(dialog).getByRole('button', { name: en('progress.review.confirm') }),
    );

    expect(await screen.findByText(en('progress.done.returned'))).toBeTruthy();
    expect(api.requestsTo('POST', /\/return$/)[0]?.body).toEqual({
      reason: { text: 'Attach the survey.', language: 'en' },
    });
  });

  test('publishing says it is final, and the override is shown to the reviewer with its reason', async () => {
    const underReview = submitted({
      status: 'UNDER_REVIEW',
      isOverridden: true,
      actualPercent: 45,
      actualPercentOverride: 45,
      overrideReason: { text: 'Survey of the northern section', language: 'EN' },
    });
    const api = open(reviewerSession(), { submissions: [underReview] });
    api.on('POST', /\/publish$/, { body: { ...underReview, status: 'PUBLISHED' } });

    const dialog = await openDialog('progress.actions.review', 'progress.review.title');
    expect(within(dialog).getByText(en('progress.figures.overridden'))).toBeTruthy();
    expect(
      within(dialog).getByText(en('progress.figures.calculated', { value: '40%' })),
    ).toBeTruthy();
    expect(within(dialog).getByText('Survey of the northern section')).toBeTruthy();

    await userEvent.click(
      within(dialog).getByRole('radio', { name: en('progress.review.publish') }),
    );
    expect(within(dialog).getByText(en('progress.review.publishConsequence'))).toBeTruthy();
    expect(await accessibilityViolations().then(describeViolations)).toBe('');
    await userEvent.click(
      within(dialog).getByRole('button', { name: en('progress.review.confirm') }),
    );

    expect(await screen.findByText(en('progress.done.published'))).toBeTruthy();
    expect(api.requestsTo('POST', /\/publish$/)).toHaveLength(1);
  });
});

describe('acceptance criterion 2: SCR-070 paginates and badges each semantic state', () => {
  function revisions(count: number): ProgressSubmissionDetail[] {
    return Array.from({ length: count }, (_, index) =>
      submitted({
        id: `9b9b9b9b-0000-4000-8000-${String(index).padStart(12, '0')}`,
        revisionNo: count - index,
        status: index === 0 ? 'SUBMITTED' : 'RETURNED',
      }),
    );
  }

  test('the history is read one page at a time, the page kept in the address', async () => {
    const api = open(
      entitySession(),
      { submissions: revisions(30), snapshots: [snapshot()], live: [liveHealth()] },
      { path: HISTORY_PATH },
    );

    const table = await screen.findByRole('table', { name: en('progress.history.caption') });
    expect(within(table).getAllByRole('row')).toHaveLength(26);
    expect(
      screen.getByText(en('common.pagination.status', { page: 1, pageCount: 2, totalCount: 30 })),
    ).toBeTruthy();

    await userEvent.click(screen.getByRole('button', { name: en('common.pagination.next') }));

    const second = await screen.findByRole('table', { name: en('progress.history.caption') });
    expect(within(second).getAllByRole('row')).toHaveLength(6);
    const reads = api.requestsTo('GET', /^\/progress-submissions$/);
    expect(reads.at(-1)?.query.get('page')).toBe('2');
    expect(reads.at(-1)?.query.get('pageSize')).toBe('25');
    expect(reads.at(-1)?.query.get('projectId')).toBe(PROJECT_ID);
  });

  test('Published/official and current/live head the history with distinct badges, and a published row is badged official', async () => {
    open(
      entitySession(),
      {
        submissions: [
          submitted({ id: SUBMISSION_ID, revisionNo: 1 }),
          submitted({
            id: PUBLISHED_SUBMISSION_ID,
            reportingCycleId: '9a9a9a9a-0000-4000-8000-000000000402',
            status: 'PUBLISHED',
            isOverridden: true,
            actualPercent: '30.0000',
            actualPercentCalculated: '25.0000',
            actualPercentOverride: '30.0000',
            plannedPercent: '35.0000',
          }),
        ],
        snapshots: [snapshot()],
        live: [liveHealth()],
      },
      { path: HISTORY_PATH },
    );

    const official = await screen.findByRole('region', { name: /Official progress/ });
    const live = screen.getByRole('region', { name: /Current progress/ });
    expect(within(official).getByText(en('progress.semanticState.official')).className).toBe(
      'badge badge--official',
    );
    expect(within(live).getByText(en('progress.semanticState.live')).className).toBe(
      'badge badge--live',
    );

    const table = screen.getByRole('table', { name: en('progress.history.caption') });
    const current = rowAt(table, 1);
    const row = rowAt(table, 2);
    expect(current.queryByText(en('progress.semanticState.official'))).toBeNull();
    expect(current.getByText(range('2026-09-01', '2026-09-30'))).toBeTruthy();
    expect(row.getByText(en('progress.semanticState.official'))).toBeTruthy();
    expect(row.getByText(en('progress.figures.overridden'))).toBeTruthy();
    expect(row.getByText(en('progress.figures.calculated', { value: '25%' }))).toBeTruthy();
    expect(row.getByText(points('\u22125'))).toBeTruthy();
    expect(row.getByText(range('2026-08-01', '2026-08-31'))).toBeTruthy();

    expect(await accessibilityViolations().then(describeViolations)).toBe('');
  });

  test('a project with no update says so', async () => {
    open(entitySession(), {}, { path: HISTORY_PATH });
    expect(await screen.findByText(en('progress.history.empty'))).toBeTruthy();
  });

  test('a returned revision says why', async () => {
    open(
      entitySession(),
      {
        submissions: [
          submitted({
            status: 'RETURNED',
            returnReason: { text: 'Attach the survey.', language: 'EN' },
          }),
        ],
      },
      { path: HISTORY_PATH },
    );
    expect(
      await screen.findByText(
        en('progress.history.returnReason', { reason: 'Attach the survey.' }),
      ),
    ).toBeTruthy();
  });
});

describe('ADR-014: a legacy-intake project carries its permanent intake marker', () => {
  const intakeProject = activeProject({ legacyIntakeDate: '2026-07-15' });
  const openingPosition = submitted({
    id: PUBLISHED_SUBMISSION_ID,
    reportingCycleId: CYCLE_ID,
    status: 'PUBLISHED',
    actualPercent: 62,
    actualPercentCalculated: 62,
    plannedPercent: null,
    projectIntakeId: INTAKE_ID,
  });

  test('SCR-070 marks the project, its opening position has no variance, and later variance runs from intake', async () => {
    open(
      entitySession(),
      {
        submissions: [
          submitted({ revisionNo: 1, actualPercent: 70, plannedPercent: 72 }),
          openingPosition,
        ],
        snapshots: [snapshot({ actualPercent: 62, plannedPercent: null, isOverridden: false })],
      },
      { project: intakeProject, path: HISTORY_PATH },
    );

    expect(
      await screen.findByText(en('progress.intake.marker', { date: '2026-07-15' })),
    ).toBeTruthy();
    const table = screen.getByRole('table', { name: en('progress.history.caption') });
    const later = rowAt(table, 1);
    const opening = rowAt(table, 2);
    expect(opening.getAllByText(en('progress.intake.openingPosition'))).toHaveLength(2);
    expect(opening.queryByText(/ pp$/)).toBeNull();
    expect(later.getByText(points('\u22122'))).toBeTruthy();
    expect(later.getByText(en('progress.intake.since', { date: '2026-07-15' }))).toBeTruthy();
  });

  test('SCR-048 shows the marker too, and a project without intake shows none', async () => {
    open(entitySession(), {}, { project: intakeProject });
    expect(
      await screen.findByText(en('progress.intake.marker', { date: '2026-07-15' })),
    ).toBeTruthy();
    expect(await accessibilityViolations().then(describeViolations)).toBe('');
  });

  test('a project registered before it started has no intake marker', async () => {
    open(entitySession());
    await screen.findByText(en('progress.current.none'));
    expect(screen.queryByText(/Legacy intake/)).toBeNull();
  });
});
