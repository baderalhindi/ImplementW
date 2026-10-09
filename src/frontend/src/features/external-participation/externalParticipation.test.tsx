import { cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { type Session } from '@/features/identity-access/session/sessionApi.ts';
import { type Language, translate } from '@/shared/i18n/i18n.ts';
import { concernProject, managerSession } from '@/test/concernFixtures.ts';
import {
  ATTEMPT_2_ID,
  attempt,
  BASE_COURSE_TASK_ID,
  DRAFT_REQUEST_ID,
  type ExternalParticipationState,
  externalView,
  FORMAL_PROJECT_ID,
  INFO_REQUEST_ID,
  infoRequest,
  INTERNAL_NOTE,
  NEW_REQUEST_ID,
  NEW_REVISION_ID,
  PARTICIPATION_VERSION_ID,
  PROGRESS_REQUEST_ID,
  progressRequest,
  REQUEST_ETAG,
  returnedThenAccepted,
  REVISION_1_ID,
  REVISION_2_ID,
  REVISION_ETAG,
  revision,
  TASK_PROGRESS_TYPE_ID,
  TASK_VERSION_ANSWERED,
  TASK_VERSION_CHANGED,
  withExternalParticipation,
} from '@/test/externalParticipationFixtures.ts';
import { type MockApi, mockApi, problem } from '@/test/mockApi.ts';
import {
  ENTITY_ID,
  entitySession,
  OTHER_PERSON_ID,
  PROJECT_ID,
  reviewerSession,
  withProject,
  withProjectLookups,
} from '@/test/projectFixtures.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';

// SCR-160 to SCR-167 (TASK-067) against the TASK-066 API. Acceptance criteria: (1) the external-facing view of SCR-162
// never renders internal-only fields present in the internal view — verified by comparing what is rendered for an R08
// entity user and for an internal role; (2) Source Application Monitoring (SCR-166) surfaces conflict and retry states
// explicitly, not as a generic error. Gate decision: no origination and no escalation controls in the external UI.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);
/** Text with the bidi isolates `translate` puts around parameters removed, as a person reads it. */
const plain = (text: string | null | undefined) => (text ?? '').replace(/[⁨⁩]/g, '');

const PROGRESS_PATH = `/external-requests/${PROGRESS_REQUEST_ID}`;
const INFO_PATH = `/external-requests/${INFO_REQUEST_ID}`;

function open(
  path: string,
  state: ExternalParticipationState = {},
  {
    session = managerSession(),
    language = 'en',
    catalogues = true,
  }: { session?: Session; language?: Language; catalogues?: boolean } = {},
): MockApi {
  const external = session.user.userType === 'EXTERNAL';
  const api = withExternalParticipation(
    withProject(withProjectLookups(mockApi(), { catalogues: false }), concernProject()),
    { audience: external ? 'EXTERNAL' : 'INTERNAL', ...state },
  );
  if (!catalogues) {
    api.on('GET', /^\/master-data-catalogues$/, problem(403, 'PERMISSION_DENIED'));
  }
  renderApp({ path, session, language });
  return api;
}

/** The progress request answered, returned, corrected and accepted, with its internal handling filled in. */
function answeredProgress(): ExternalParticipationState {
  return {
    requests: [progressRequest({ status: 'RESPONDED' })],
    revisions: { [PROGRESS_REQUEST_ID]: returnedThenAccepted() },
    attempts: { [REVISION_2_ID]: [] },
  };
}

async function settled(): Promise<HTMLElement> {
  const main = document.querySelector<HTMLElement>('#main');
  if (main === null) {
    throw new Error('No main region.');
  }
  await waitFor(() => {
    expect(main.querySelector('.state--loading')).toBeNull();
    expect(main.querySelector('h1')).not.toBeNull();
  });
  return main;
}

/** Every rendered internal-only marker and value the internal view of the answered progress request shows. */
const INTERNAL_ONLY_TEXT = [
  en('externalParticipation.detail.handlingTitle'),
  en('externalParticipation.fields.reviewer'),
  en('externalParticipation.fields.issuedBy'),
  en('externalParticipation.fields.configuration'),
  PARTICIPATION_VERSION_ID,
  en('externalParticipation.revision.internalNote'),
  INTERNAL_NOTE,
  'Second return would escalate to the sponsor.',
  en('externalParticipation.revision.answeredAgainst'),
  en('externalParticipation.revision.reviewedBy'),
  'Nora Manager',
  String(TASK_VERSION_ANSWERED),
  en('externalParticipation.contributionStatus.ACCEPTED_PENDING_APPLICATION'),
  en('externalParticipation.detail.applicationTitle'),
];

const INTERNAL_ONLY_MARKERS = [
  '[data-field="internalHandling"]',
  '[data-field="reviewer"]',
  '[data-field="issuedBy"]',
  '[data-field="configurationVersion"]',
  '[data-field="internalNote"]',
  '[data-field="targetVersion"]',
  '[data-field="reviewedBy"]',
  '[data-field="reviewStartedAt"]',
];

describe('SCR-162: the external view never renders an internal-only field (acceptance criterion 1)', () => {
  test('the same request, read by an internal role and by the R08 responder: every internal-only field rendered for AHDA is absent for the entity', async () => {
    open(PROGRESS_PATH, answeredProgress(), { session: reviewerSession() });
    const internal = await settled();
    await screen.findByText(INTERNAL_NOTE);
    await screen.findAllByText('Nora Manager');
    const internalText = plain(internal.textContent);
    expect(internal.querySelector('[data-projection="INTERNAL"]')).not.toBeNull();
    // The control: the internal view does render each of them, or the comparison proves nothing.
    for (const text of INTERNAL_ONLY_TEXT) {
      expect(internalText, text).toContain(text);
    }
    for (const marker of INTERNAL_ONLY_MARKERS) {
      expect(internal.querySelector(marker), marker).not.toBeNull();
    }

    cleanup();
    const api = open(PROGRESS_PATH, answeredProgress(), { session: entitySession() });
    const external = await settled();
    await screen.findByText(en('externalParticipation.response.historyTitle'));
    await screen.findByText('The diary shows 42 %, not 45 %.');
    const externalText = plain(external.textContent);

    expect(external.querySelector('[data-projection="EXTERNAL"]')).not.toBeNull();
    for (const text of INTERNAL_ONLY_TEXT) {
      expect(externalText, text).not.toContain(text);
    }
    for (const marker of INTERNAL_ONLY_MARKERS) {
      expect(external.querySelector(marker), marker).toBeNull();
    }
    // What the entity may read is there: the safe context, the outcome in its words and AHDA's reason.
    expect(externalText).toContain(FORMAL_PROJECT_ID);
    expect(externalText).toContain('Lay the base course');
    expect(externalText).toContain('Report the base course progress as of Thursday.');
    expect(externalText).toContain(en('externalParticipation.outcome.ACCEPTED'));
    expect(externalText).toContain(en('externalParticipation.outcome.RETURNED'));
    // Nothing internal was asked for: no application attempt, and no read of AHDA's reviewer.
    expect(api.requestsTo('GET', /^\/source-applications/)).toHaveLength(0);
    expect(api.requestsTo('GET', new RegExp(`^/users/${OTHER_PERSON_ID}$`))).toHaveLength(0);
    expect(api.requestsTo('GET', /^\/external-entities$/)).toHaveLength(0);
  });

  test('a field the server should have withheld is still not rendered on an external view', async () => {
    const api = open(PROGRESS_PATH, answeredProgress(), { session: entitySession() });
    // A projection that leaks: EXTERNAL, but every internal-only field still present with a value.
    const leaking = { ...progressRequest({ status: 'RESPONDED' }), projection: 'EXTERNAL' };
    const [accepted, returned] = returnedThenAccepted();
    api
      .on('GET', new RegExp(`^/external-update-requests/${PROGRESS_REQUEST_ID}$`), {
        body: leaking,
        headers: { ETag: REQUEST_ETAG },
      })
      .on('GET', /^\/external-contributions$/, {
        body: {
          items: [accepted, returned].map((item) => ({ ...item, projection: 'EXTERNAL' })),
          page: 1,
          pageSize: 200,
          totalCount: 2,
        },
      });
    const main = await settled();
    await screen.findByText('The diary shows 42 %, not 45 %.');
    const text = plain(main.textContent);

    expect(text).not.toContain(INTERNAL_NOTE);
    expect(text).not.toContain(PARTICIPATION_VERSION_ID);
    expect(text).not.toContain(String(TASK_VERSION_ANSWERED));
    for (const marker of INTERNAL_ONLY_MARKERS) {
      expect(main.querySelector(marker), marker).toBeNull();
    }
  });

  test('the entity reads an accepted answer as Accepted: AHDA’s application states are not shown to it', async () => {
    open(PROGRESS_PATH, answeredProgress(), { session: entitySession() });
    await settled();
    await screen.findByText(en('externalParticipation.response.historyTitle'));

    const outcomes = [...document.querySelectorAll('[data-badge="outcome"]')].map((badge) =>
      plain(badge.textContent),
    );
    expect(outcomes).toEqual([
      en('externalParticipation.outcome.ACCEPTED'),
      en('externalParticipation.outcome.RETURNED'),
    ]);
    expect(document.querySelector('[data-badge="contribution-status"]')).toBeNull();
    // The request is still with AHDA until its answer is applied: no form, and it says so.
    expect(screen.getByText(en('externalParticipation.response.withAhda'))).toBeTruthy();
  });

  test('a draft request does not exist for the entity (404), and SCR-162 says so without detail', async () => {
    open(`/external-requests/${DRAFT_REQUEST_ID}`, {}, { session: entitySession() });

    expect(await screen.findByText(en('externalParticipation.detail.notFound'))).toBeTruthy();
  });

  test('accessibility: the internal and the external views have no axe violations', async () => {
    open(PROGRESS_PATH, answeredProgress(), { session: reviewerSession() });
    await settled();
    await screen.findByText(INTERNAL_NOTE);
    let violations = await accessibilityViolations();
    expect(violations, describeViolations(violations)).toEqual([]);

    cleanup();
    open(PROGRESS_PATH, answeredProgress(), { session: entitySession() });
    await settled();
    await screen.findByText(en('externalParticipation.response.historyTitle'));
    violations = await accessibilityViolations();
    expect(violations, describeViolations(violations)).toEqual([]);
  });
});

/** The buttons and button-styled links a screen offers. */
function controls(): string[] {
  const main = document.querySelector<HTMLElement>('#main');
  return main === null
    ? []
    : [
        ...within(main).queryAllByRole('button'),
        ...main.querySelectorAll<HTMLElement>('a.button'),
      ].map((element) => plain(element.textContent).trim());
}

const AHDA_ACTIONS = [
  'create',
  'issue',
  'delete',
  'edit',
  'cancelRequest',
  'changeResponder',
  'changeReviewer',
  'startReview',
  'accept',
  'return',
  'reject',
  'apply',
  'applyAgain',
  'revalidate',
].map((action) => en(`externalParticipation.actions.${action}`));

describe('gate decision: no origination and no escalation controls in the external UI', () => {
  test.each([
    ['SCR-163 My External Requests', '/my-external-requests'],
    ['SCR-164 My Contributions', '/my-contributions'],
    ['SCR-162 an issued request', INFO_PATH],
    ['SCR-162 a request with AHDA', PROGRESS_PATH],
    ['the project workspace tab', `/projects/${PROJECT_ID}/external-requests`],
  ])('%s offers an entity user none of AHDA’s actions', async (_name, path) => {
    open(
      path,
      {
        ...answeredProgress(),
        requests: [progressRequest({ status: 'RESPONDED' }), infoRequest()],
      },
      {
        session: entitySession(),
      },
    );
    await settled();
    await waitFor(() => {
      expect(document.querySelector('#main .state--loading')).toBeNull();
    });

    const offered = controls();
    for (const action of AHDA_ACTIONS) {
      expect(offered, action).not.toContain(action);
    }
    expect(offered.some((label) => /escalat/i.test(label))).toBe(false);
  });

  test.each([
    ['SCR-160', '/external-requests'],
    ['SCR-161', `/external-requests/new?projectId=${PROJECT_ID}`],
    ['SCR-165', `/external-contributions/${REVISION_1_ID}`],
    ['SCR-166', '/external-requests/applications'],
    ['SCR-167', '/external-requests/monitor'],
  ])(
    '%s, AHDA’s, reached by address by an entity user, reads nothing and points to their own list',
    async (_name, path) => {
      const api = open(path, {}, { session: entitySession() });

      expect(
        await screen.findByText(en('externalParticipation.internalOnly.description')),
      ).toBeTruthy();
      const main = document.querySelector<HTMLElement>('#main');
      expect(
        main === null
          ? null
          : within(main).getByRole('link', { name: en('externalParticipation.nav.mine') }),
      ).toBeTruthy();
      expect(api.requests.filter((request) => !request.path.startsWith('/notifications'))).toEqual(
        [],
      );
    },
  );

  test('the sidebar offers an entity user its own requests and contributions, and AHDA its register and monitors', async () => {
    open('/my-external-requests', {}, { session: entitySession() });
    const externalNav = await screen.findByRole('navigation', {
      name: en('externalParticipation.nav.label'),
    });
    expect(
      within(externalNav)
        .getAllByRole('link')
        .map((link) => link.textContent),
    ).toEqual([
      en('externalParticipation.nav.mine'),
      en('externalParticipation.nav.contributions'),
    ]);

    cleanup();
    open('/external-requests', {}, { session: managerSession() });
    const internalNav = await screen.findByRole('navigation', {
      name: en('externalParticipation.nav.label'),
    });
    expect(
      within(internalNav)
        .getAllByRole('link')
        .map((link) => link.textContent),
    ).toEqual([
      en('externalParticipation.nav.register'),
      en('externalParticipation.nav.applications'),
      en('externalParticipation.nav.monitor'),
    ]);
  });
});

/** Three accepted answers, each in a state SCR-166 must tell apart. */
function threeOutcomes(): ExternalParticipationState {
  const conflictRevision = revision({ id: REVISION_2_ID, status: 'ACCEPTED_PENDING_APPLICATION' });
  const failedRevision = revision({
    id: 'c1300000-0000-4000-8000-000000000211',
    externalUpdateRequestId: 'e1300000-0000-4000-8000-000000000111',
    status: 'APPLICATION_FAILED',
  });
  const retryRevision = revision({
    id: 'c1300000-0000-4000-8000-000000000212',
    externalUpdateRequestId: 'e1300000-0000-4000-8000-000000000112',
    status: 'ACCEPTED_PENDING_APPLICATION',
  });
  return {
    requests: [
      progressRequest({ status: 'RESPONDED' }),
      progressRequest({
        id: 'e1300000-0000-4000-8000-000000000111',
        status: 'CLOSED',
        targetLabel: { text: 'Remove the old kerbs', language: 'EN' },
      }),
      progressRequest({
        id: 'e1300000-0000-4000-8000-000000000112',
        status: 'RESPONDED',
        targetLabel: { text: 'Paint the road markings', language: 'EN' },
      }),
    ],
    revisions: {
      [PROGRESS_REQUEST_ID]: [conflictRevision],
      'e1300000-0000-4000-8000-000000000111': [failedRevision],
      'e1300000-0000-4000-8000-000000000112': [retryRevision],
    },
    attempts: {
      [REVISION_2_ID]: [
        attempt({
          status: 'CONFLICT',
          actualTargetRevisionNo: TASK_VERSION_CHANGED,
        }),
      ],
      [failedRevision.id]: [
        attempt({
          id: ATTEMPT_2_ID,
          externalContributionId: failedRevision.id,
          status: 'FAILED',
          failureCode: 'SOURCE_RECORD_TERMINAL',
        }),
      ],
      [retryRevision.id]: [
        attempt({
          id: 'a1300000-0000-4000-8000-000000000303',
          externalContributionId: retryRevision.id,
          status: 'FAILED',
          failureCode: 'PROJECT_STATE_NOT_PERMITTED',
        }),
      ],
    },
  };
}

function caseCard(state: string): HTMLElement {
  const card = document.querySelector<HTMLElement>(`[data-application-case="${state}"]`);
  if (card === null) {
    throw new Error(`No ${state} case.`);
  }
  return card;
}

describe('SCR-166: conflict and retry are explicit states, never a generic error (acceptance criterion 2)', () => {
  test('a conflict, a retryable refusal and a final failure each have their own words, mark, style and explanation', async () => {
    open('/external-requests/applications', threeOutcomes());
    await screen.findAllByRole('heading', { level: 2 });

    const conflict = caseCard('CONFLICT');
    const retry = caseCard('RETRYABLE');
    const failed = caseCard('FAILED');

    // No error state anywhere: these are results.
    expect(document.querySelector('#main [role="alert"]')).toBeNull();
    expect(document.querySelector('#main .state--error')).toBeNull();

    const badge = (card: HTMLElement) =>
      card.querySelector<HTMLElement>('[data-application-state]');
    expect(plain(badge(conflict)?.textContent)).toBe(
      en('externalParticipation.applicationState.CONFLICT'),
    );
    expect(plain(badge(retry)?.textContent)).toBe(
      en('externalParticipation.applicationState.RETRYABLE'),
    );
    expect(plain(badge(failed)?.textContent)).toBe(
      en('externalParticipation.applicationState.FAILED'),
    );
    // Three styles, three marks: none drawn like another.
    const classes = [conflict, retry, failed].map((card) => badge(card)?.className);
    expect(new Set(classes).size).toBe(3);
    expect(badge(conflict)?.className).toContain('badge--application-conflict');
    expect(badge(retry)?.className).toContain('badge--application-retry');
    expect(badge(failed)?.className).toContain('badge--application-failed');
    const marks = [conflict, retry, failed].map((card) =>
      badge(card)?.querySelector('path')?.getAttribute('d'),
    );
    expect(new Set(marks).size).toBe(3);

    // Each says why, with the versions or the safe failure.
    expect(plain(conflict.querySelector('[data-field="stateExplanation"]')?.textContent)).toBe(
      plain(
        en('externalParticipation.applicationExplain.CONFLICT_VERSIONS', {
          expected: TASK_VERSION_ANSWERED,
          found: TASK_VERSION_CHANGED,
        }),
      ),
    );
    expect(plain(retry.querySelector('[data-field="stateExplanation"]')?.textContent)).toContain(
      en('externalParticipation.failure.PROJECT_STATE_NOT_PERMITTED'),
    );
    expect(plain(failed.querySelector('[data-field="stateExplanation"]')?.textContent)).toContain(
      en('externalParticipation.failure.SOURCE_RECORD_TERMINAL'),
    );

    // And the one action each takes: revalidate a conflict, apply again after a refusal, nothing after a failure.
    expect(
      within(conflict).getByRole('button', {
        name: en('externalParticipation.actions.revalidate'),
      }),
    ).toBeTruthy();
    expect(
      within(retry).getByRole('button', { name: en('externalParticipation.actions.applyAgain') }),
    ).toBeTruthy();
    expect(within(failed).queryAllByRole('button')).toEqual([]);
    // Ordered by what needs action: the conflict first.
    expect(
      [...document.querySelectorAll('[data-application-case]')].map((card) =>
        card.getAttribute('data-application-case'),
      ),
    ).toEqual(['CONFLICT', 'RETRYABLE', 'FAILED']);
  });

  test('an attempt that finds a conflict is told as a conflict, then revalidated and applied', async () => {
    const api = open('/external-requests/applications', {
      requests: [progressRequest({ status: 'RESPONDED' })],
      revisions: {
        [PROGRESS_REQUEST_ID]: [
          revision({ id: REVISION_2_ID, status: 'ACCEPTED_PENDING_APPLICATION' }),
        ],
      },
      attempts: {},
      applyOutcome: 'CONFLICT',
      taskVersion: TASK_VERSION_CHANGED,
    });
    const user = userEvent.setup();
    await user.click(
      await screen.findByRole('button', { name: en('externalParticipation.actions.apply') }),
    );
    const dialog = await screen.findByRole('dialog');
    await user.click(
      within(dialog).getByRole('button', { name: en('externalParticipation.actions.apply') }),
    );

    // 201 CONFLICT: a warning notice naming it, never an error; the card is now the conflict.
    const notice = await screen.findByText(en('externalParticipation.done.conflictRecorded'));
    expect(notice.closest('[role="status"]')?.className).toContain('notice--warning');
    await waitFor(() => {
      expect(caseCard('CONFLICT')).toBeTruthy();
    });
    expect(document.querySelector('#main [role="alert"]')).toBeNull();

    await user.click(
      screen.getByRole('button', { name: en('externalParticipation.actions.revalidate') }),
    );
    const confirm = await screen.findByRole('dialog');
    await user.click(
      within(confirm).getByRole('button', { name: en('externalParticipation.actions.revalidate') }),
    );
    await screen.findByText(en('externalParticipation.done.revalidated'));
    await waitFor(() => {
      expect(caseCard('REVALIDATED')).toBeTruthy();
    });
    // The fixture numbers a new attempt …0310: the first made on this revision.
    expect(api.requestsTo('POST', /\/revalidate$/)[0]?.path).toBe(
      '/source-applications/a1300000-0000-4000-8000-000000000310/revalidate',
    );
  });

  test('the attempt keeps its Idempotency-Key when sent again after a lost answer, so nothing applies twice', async () => {
    const api = open('/external-requests/applications', {
      requests: [progressRequest({ status: 'RESPONDED' })],
      revisions: {
        [PROGRESS_REQUEST_ID]: [
          revision({ id: REVISION_2_ID, status: 'ACCEPTED_PENDING_APPLICATION' }),
        ],
      },
      attempts: {},
    });
    let first = true;
    api.on('POST', /^\/source-applications$/, () => {
      if (first) {
        first = false;
        return problem(503, 'UNAVAILABLE');
      }
      return { status: 201, body: attempt({ externalContributionId: REVISION_2_ID }) };
    });
    const user = userEvent.setup();
    await user.click(
      await screen.findByRole('button', { name: en('externalParticipation.actions.apply') }),
    );
    const dialog = await screen.findByRole('dialog');
    const send = within(dialog).getByRole('button', {
      name: en('externalParticipation.actions.apply'),
    });
    await user.click(send);
    expect(await within(dialog).findByText(en('common.problems.unavailable'))).toBeTruthy();
    await user.click(send);
    await screen.findByText(en('externalParticipation.done.applied'));

    const keys = api
      .requestsTo('POST', /^\/source-applications$/)
      .map((request) => request.headers.get('Idempotency-Key'));
    expect(keys).toHaveLength(2);
    expect(keys[0]).not.toBeNull();
    expect(keys[1]).toBe(keys[0]);
  });

  test('every attempt’s lineage is kept with its answer: versions, refusal and correlation id', async () => {
    open('/external-requests/applications', threeOutcomes());
    const conflict = await waitFor(() => caseCard('CONFLICT'));
    await userEvent
      .setup()
      .click(within(conflict).getByText(en('externalParticipation.applications.lineage')));

    const row = conflict.querySelector<HTMLElement>('tr[data-attempt="1"]');
    expect(row).not.toBeNull();
    const cells = [...(row?.querySelectorAll('td') ?? [])].map((cell) => plain(cell.textContent));
    expect(cells[1]).toBe(en('externalParticipation.attemptStatus.CONFLICT'));
    expect(cells[2]).toBe(String(TASK_VERSION_ANSWERED));
    expect(cells[3]).toBe(String(TASK_VERSION_CHANGED));
    expect(cells[7]).toBe(attempt().correlationId);
  });

  test('accessibility: SCR-166 has no axe violations, and states are words, not colour alone', async () => {
    open('/external-requests/applications', threeOutcomes());
    await waitFor(() => caseCard('FAILED'));
    const violations = await accessibilityViolations();
    expect(violations, describeViolations(violations)).toEqual([]);
  });
});

describe('SCR-162 for the entity’s responder: answering a request in its typed schema', () => {
  test('the responder starts an answer with the schema’s fields only, saves a draft, then submits it once confirmed', async () => {
    const api = open(INFO_PATH, { requests: [infoRequest()] }, { session: entitySession() });
    const user = userEvent.setup();

    const response = await screen.findByRole('textbox', { name: /^Response/ });
    expect(screen.getByLabelText(/^As of/).getAttribute('type')).toBe('date');
    await user.type(response, 'The survey is two-thirds done.');
    await user.click(
      screen.getByRole('button', { name: en('externalParticipation.actions.saveDraft') }),
    );
    await screen.findByText(en('externalParticipation.done.draftSaved'));

    expect(api.requestsTo('POST', /^\/external-contributions$/)[0]?.body).toEqual({
      externalUpdateRequestId: INFO_REQUEST_ID,
      fields: [{ fieldCode: 'response', value: 'The survey is two-thirds done.', language: 'en' }],
    });

    await user.click(
      await screen.findByRole('button', {
        name: en('externalParticipation.actions.submitResponse'),
      }),
    );
    const dialog = await screen.findByRole('dialog');
    expect(
      within(dialog).getByText(en('externalParticipation.command.submit.consequence')),
    ).toBeTruthy();
    await user.click(
      within(dialog).getByRole('button', {
        name: en('externalParticipation.actions.submitResponse'),
      }),
    );
    await screen.findByText(en('externalParticipation.done.submitted'));

    const [saved] = api.requestsTo('PUT', /^\/external-contributions\//);
    expect(saved?.headers.get('If-Match')).toBe(REVISION_ETAG);
    const [submitted] = api.requestsTo('POST', /\/submit$/);
    expect(submitted?.path).toBe(`/external-contributions/${NEW_REVISION_ID}/submit`);
    expect(submitted?.headers.get('If-Match')).toBe(REVISION_ETAG);
  });

  test('a required field must be answered and a number stay in its range before anything is sent', async () => {
    const api = open(
      PROGRESS_PATH,
      { requests: [progressRequest()] },
      { session: entitySession() },
    );
    const user = userEvent.setup();
    const percent = await screen.findByRole('textbox', { name: /^Actual percent complete/ });

    await user.click(
      screen.getByRole('button', { name: en('externalParticipation.actions.submitResponse') }),
    );
    expect(screen.getByText(en('externalParticipation.fieldErrors.answerRequired'))).toBeTruthy();
    expect(document.activeElement).toBe(percent);

    await user.type(percent, '120');
    expect(
      screen.getByText(
        en('externalParticipation.fieldErrors.answerOutOfRange', { min: 0, max: 100 }),
      ),
    ).toBeTruthy();
    await user.clear(percent);
    await user.type(percent, '42.12345');
    expect(screen.getByText(en('externalParticipation.fieldErrors.answerTooPrecise'))).toBeTruthy();
    expect(api.requestsTo('POST', /^\/external-contributions/)).toHaveLength(0);
  });

  test('the API’s refusal of a value lands on the field it names, though the API names it by the index sent', async () => {
    const api = open(
      PROGRESS_PATH,
      { requests: [progressRequest()] },
      { session: entitySession() },
    );
    api.on(
      'POST',
      /^\/external-contributions$/,
      problem(422, 'CONTRIBUTION_VALIDATION_FAILED', [
        { field: 'fields[1].value', code: 'NOT_ALLOWED' },
      ]),
    );
    const user = userEvent.setup();
    await user.type(await screen.findByRole('textbox', { name: /^Actual percent complete/ }), '42');
    await user.type(screen.getByRole('textbox', { name: /^Progress note/ }), 'Laid.');
    await user.click(
      screen.getByRole('button', { name: en('externalParticipation.actions.saveDraft') }),
    );

    const note = screen.getByRole('textbox', { name: /^Progress note/ });
    await waitFor(() => {
      expect(note.getAttribute('aria-invalid')).toBe('true');
    });
    expect(
      screen
        .getByRole('textbox', { name: /^Actual percent complete/ })
        .getAttribute('aria-invalid'),
    ).toBeNull();
  });

  test('a returned answer comes back as the next revision’s draft, with AHDA’s reason above the form', async () => {
    const returned = revision({
      status: 'RETURNED',
      reviewReason: { text: 'The diary shows 42 %, not 45 %.', language: 'EN' },
    });
    const draft = revision({
      id: REVISION_2_ID,
      revisionNo: 2,
      previousRevisionId: REVISION_1_ID,
      status: 'DRAFT',
      submittedAt: null,
    });
    open(
      PROGRESS_PATH,
      {
        requests: [progressRequest({ status: 'IN_PROGRESS' })],
        revisions: { [PROGRESS_REQUEST_ID]: [draft, returned] },
      },
      { session: entitySession() },
    );

    expect(
      await screen.findByText(en('externalParticipation.response.returnedTitle')),
    ).toBeTruthy();
    expect(screen.getAllByText('The diary shows 42 %, not 45 %.').length).toBeGreaterThan(0);
    const percent = await screen.findByRole('textbox', { name: /^Actual percent complete/ });
    expect((percent as HTMLInputElement).value).toBe('45');
  });

  test('another entity user sees the request but no form: only the named responder answers', async () => {
    const other = entitySession();
    other.user = {
      ...other.user,
      id: '1a1a1a1a-0000-4000-8000-000000000999',
      displayName: 'Omar Contractor',
    };
    open(INFO_PATH, { requests: [infoRequest()] }, { session: other });

    expect(
      await screen.findByText(en('externalParticipation.response.otherResponder')),
    ).toBeTruthy();
    expect(
      screen.queryByRole('button', { name: en('externalParticipation.actions.submitResponse') }),
    ).toBeNull();
  });
});

describe('SCR-165 Contribution Review: decide, never edit', () => {
  test('the reviewer sees the values read-only beside the source as it is now, and that the source changed since', async () => {
    open(`/external-contributions/${REVISION_1_ID}`, {
      requests: [progressRequest({ status: 'RESPONDED' })],
      revisions: { [PROGRESS_REQUEST_ID]: [revision()] },
      taskVersion: TASK_VERSION_CHANGED,
    });

    const changed = await waitFor(() => {
      const found = document.querySelector<HTMLElement>('[data-field="sourceChanged"]');
      if (found === null) {
        throw new Error('No source indicator yet.');
      }
      return found;
    });
    expect(changed.getAttribute('data-changed')).toBe('true');
    expect(plain(changed.textContent)).toBe(
      plain(
        en('externalParticipation.review.sourceChanged', {
          version: TASK_VERSION_CHANGED,
          state: 'IN_PROGRESS',
        }),
      ),
    );
    const percentRow = document.querySelector<HTMLElement>(
      'tr[data-field-code="actualPercentComplete"]',
    );
    expect([...(percentRow?.querySelectorAll('td') ?? [])].map((cell) => cell.textContent)).toEqual(
      ['45', '40'],
    );
    // No input of any kind: a submitted value is never edited (TASK-066 D-5).
    expect(document.querySelectorAll('#main input, #main textarea, #main select')).toHaveLength(0);
  });

  test('the assigned reviewer starts the review, then accepts with an internal note and sends no value', async () => {
    const api = open(`/external-contributions/${REVISION_1_ID}`, {
      requests: [progressRequest({ status: 'RESPONDED' })],
      revisions: { [PROGRESS_REQUEST_ID]: [revision()] },
    });
    const user = userEvent.setup();
    expect(
      screen.queryByRole('button', { name: en('externalParticipation.actions.accept') }),
    ).toBeNull();
    await user.click(
      await screen.findByRole('button', { name: en('externalParticipation.actions.startReview') }),
    );
    await user.click(
      within(await screen.findByRole('dialog')).getByRole('button', {
        name: en('externalParticipation.actions.startReview'),
      }),
    );

    await user.click(
      await screen.findByRole('button', { name: en('externalParticipation.actions.accept') }),
    );
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).queryByRole('textbox', { name: /^Reason/ })).toBeNull();
    await user.type(within(dialog).getByRole('textbox', { name: /^Internal note/ }), INTERNAL_NOTE);
    await user.click(
      within(dialog).getByRole('button', { name: en('externalParticipation.actions.accept') }),
    );
    await screen.findByText(en('externalParticipation.done.accepted'));

    const [accepted] = api.requestsTo('POST', /\/accept$/);
    expect(accepted?.body).toEqual({ internalNote: { text: INTERNAL_NOTE, language: 'en' } });
    expect(accepted?.headers.get('If-Match')).toBe(REVISION_ETAG);
    await screen.findByText(en('externalParticipation.review.pendingApplication'));
  });

  test('a return needs the reason the entity reads', async () => {
    const api = open(`/external-contributions/${REVISION_1_ID}`, {
      requests: [progressRequest({ status: 'RESPONDED' })],
      revisions: { [PROGRESS_REQUEST_ID]: [revision({ status: 'UNDER_REVIEW' })] },
    });
    const user = userEvent.setup();
    await user.click(
      await screen.findByRole('button', { name: en('externalParticipation.actions.return') }),
    );
    const dialog = await screen.findByRole('dialog');
    await user.click(
      within(dialog).getByRole('button', { name: en('externalParticipation.actions.return') }),
    );

    expect(within(dialog).getByText(en('common.fieldErrors.required'))).toBeTruthy();
    expect(api.requestsTo('POST', /\/return$/)).toHaveLength(0);
    await user.type(
      within(dialog).getByRole('textbox', { name: /^Reason/ }),
      'Use the diary figure.',
    );
    await user.click(
      within(dialog).getByRole('button', { name: en('externalParticipation.actions.return') }),
    );
    await screen.findByText(en('externalParticipation.done.returned'));
    expect(api.requestsTo('POST', /\/return$/)[0]?.body).toEqual({
      reason: { text: 'Use the diary figure.', language: 'en' },
      internalNote: null,
    });
  });

  test('someone who is not the request’s reviewer is told who decides, and offered nothing', async () => {
    open(
      `/external-contributions/${REVISION_1_ID}`,
      {
        requests: [progressRequest({ status: 'RESPONDED' })],
        revisions: { [PROGRESS_REQUEST_ID]: [revision()] },
      },
      { session: reviewerSession() },
    );

    expect(
      await screen.findByText(
        en('externalParticipation.review.notReviewer', { name: 'Nora Manager' }),
      ),
    ).toBeTruthy();
    expect(controls()).toEqual([]);
  });
});

describe('SCR-160 and SCR-161: AHDA’s register and request form', () => {
  test('the register lists drafts and issued requests with status, due condition and people, filtered in the address', async () => {
    open('/external-requests', {}, { session: reviewerSession() });
    const table = await screen.findByRole('table');
    expect(within(table).getAllByRole('row')).toHaveLength(4);
    expect(within(table).getByText(en('externalParticipation.dueCondition.OVERDUE'))).toBeTruthy();

    await userEvent
      .setup()
      .selectOptions(
        screen.getByRole('combobox', { name: en('externalParticipation.filter.due') }),
        'OVERDUE',
      );
    expect(within(screen.getByRole('table')).getAllByRole('row')).toHaveLength(2);
  });

  test('SCR-161 from the workspace: a task-progress request names its task, is saved and issued at once', async () => {
    const api = open(`/projects/${PROJECT_ID}/external-requests`, { requests: [] });
    const user = userEvent.setup();
    await user.click(
      await screen.findByRole('link', { name: en('externalParticipation.actions.create') }),
    );

    await user.selectOptions(
      await screen.findByRole('combobox', { name: /^Contribution type/ }),
      TASK_PROGRESS_TYPE_ID,
    );
    await user.selectOptions(
      await screen.findByRole('combobox', { name: /^Source record/ }),
      BASE_COURSE_TASK_ID,
    );
    await user.type(
      screen.getByRole('textbox', { name: /^Instructions/ }),
      'Report the base course progress.',
    );
    await user.type(screen.getByLabelText(/^Response due/), '2099-10-15');
    const responder = screen.getByRole('group', { name: /^Responder/ });
    await user.click(
      within(responder).getByRole('radio', { name: en('externalParticipation.people.other') }),
    );
    await user.type(
      within(responder).getByRole('textbox', { name: /^User ID/ }),
      '1A1A1A1A-0000-4000-8000-000000000101',
    );
    await user.click(
      screen.getByRole('button', { name: en('externalParticipation.actions.saveAndIssue') }),
    );

    await screen.findByText(en('externalParticipation.done.issued'));
    expect(api.requestsTo('POST', /^\/external-update-requests$/)[0]?.body).toEqual({
      projectId: PROJECT_ID,
      externalEntityId: ENTITY_ID,
      contributionTypeItemId: TASK_PROGRESS_TYPE_ID,
      targetId: BASE_COURSE_TASK_ID,
      instructions: { text: 'Report the base course progress.', language: 'en' },
      responsibleUserId: '1a1a1a1a-0000-4000-8000-000000000101',
      reviewerUserId: OTHER_PERSON_ID,
      dueDate: '2099-10-15',
    });
    expect(api.requestsTo('POST', /\/issue$/)[0]?.path).toBe(
      `/external-update-requests/${NEW_REQUEST_ID}/issue`,
    );
  });

  test('issuing needs the responder and a due date; a draft does not', async () => {
    const api = open(`/external-requests/new?projectId=${PROJECT_ID}`, { requests: [] });
    const user = userEvent.setup();
    await user.selectOptions(
      await screen.findByRole('combobox', { name: /^Contribution type/ }),
      'd1300000-0000-4000-8000-000000000502',
    );
    expect(screen.queryByRole('combobox', { name: /^Source record/ })).toBeNull();
    await user.type(
      screen.getByRole('textbox', { name: /^Instructions/ }),
      'Where is the drainage survey?',
    );

    await user.click(
      screen.getByRole('button', { name: en('externalParticipation.actions.saveAndIssue') }),
    );
    expect(
      screen.getByText(en('externalParticipation.fieldErrors.responderRequired')),
    ).toBeTruthy();
    expect(api.requestsTo('POST', /^\/external-update-requests/)).toHaveLength(0);

    await user.click(
      screen.getByRole('button', { name: en('externalParticipation.actions.saveDraft') }),
    );
    await screen.findByText(en('externalParticipation.done.saved'));
    expect(api.requestsTo('POST', /\/issue$/)).toHaveLength(0);
  });

  test('without MASTER_DATA_VIEW the contribution type is given by its id, and the source by the task list', async () => {
    open(`/external-requests/new?projectId=${PROJECT_ID}`, { requests: [] }, { catalogues: false });

    const type = await screen.findByRole('textbox', { name: /^Contribution type/ });
    expect(type).toBeTruthy();
    expect(
      screen.getByText(en('externalParticipation.form.typeIdHint'), { exact: false }),
    ).toBeTruthy();
  });

  test('accessibility: SCR-160 and SCR-161 have no axe violations', async () => {
    open('/external-requests', {}, { session: reviewerSession() });
    await screen.findByRole('table');
    let violations = await accessibilityViolations();
    expect(violations, describeViolations(violations)).toEqual([]);

    cleanup();
    open(`/external-requests/new?projectId=${PROJECT_ID}`, { requests: [] });
    await screen.findByRole('combobox', { name: /^Contribution type/ });
    violations = await accessibilityViolations();
    expect(violations, describeViolations(violations)).toEqual([]);
  });
});

describe('SCR-163, SCR-164 and SCR-167', () => {
  test('SCR-163 lists the entity’s requests, never a draft, and counts only those', async () => {
    open('/my-external-requests', {}, { session: entitySession() });
    const table = await screen.findByRole('table');

    expect(within(table).getAllByRole('row')).toHaveLength(3);
    expect(within(table).queryByText(en('externalParticipation.requestStatus.DRAFT'))).toBeNull();
    expect(plain(screen.getByText(/^Showing/).textContent)).toBe(
      plain(en('externalParticipation.filter.showing', { shown: 2, total: 2 })),
    );
    expect(
      within(table).queryByRole('columnheader', {
        name: en('externalParticipation.table.reviewer'),
      }),
    ).toBeNull();
  });

  test('SCR-164 lists the person’s revisions in the entity’s words with AHDA’s reason, and what waits for them', async () => {
    open(
      '/my-contributions',
      {
        ...answeredProgress(),
        requests: [progressRequest({ status: 'RESPONDED' }), infoRequest()],
      },
      { session: entitySession() },
    );
    const table = await screen.findByRole('table');

    expect(
      screen.getByRole('link', {
        name: en('externalParticipation.contributions.start', {
          purpose: en('externalParticipation.schema.PROJECT_INFORMATION'),
        }),
      }),
    ).toBeTruthy();
    const outcomes = within(table)
      .getAllByRole('row')
      .slice(1)
      .map((row) => plain(row.querySelector('[data-badge="outcome"]')?.textContent));
    expect(outcomes).toEqual([
      en('externalParticipation.outcome.ACCEPTED'),
      en('externalParticipation.outcome.RETURNED'),
    ]);
    expect(within(table).getByText('The diary shows 42 %, not 45 %.')).toBeTruthy();
    expect(plain(table.textContent)).not.toContain(INTERNAL_NOTE);
    const violations = await accessibilityViolations();
    expect(violations, describeViolations(violations)).toEqual([]);
  });

  test('SCR-167 counts each project and entity’s requests by where they stand and says access expiry is not available', async () => {
    open('/external-requests/monitor', {
      requests: [
        progressRequest({ status: 'RESPONDED' }),
        infoRequest(),
        progressRequest({ id: DRAFT_REQUEST_ID, status: 'DRAFT', dueCondition: 'NOT_APPLICABLE' }),
      ],
    });
    const table = await screen.findByRole('table');
    const cells = [...(within(table).getAllByRole('row')[1]?.querySelectorAll('td') ?? [])].map(
      (cell) => plain(cell.textContent),
    );

    expect(cells[0]).toBe(FORMAL_PROJECT_ID);
    // Not started 1 (the issued survey), drafting 0, with AHDA 1, one overdue, one responder, one AHDA draft.
    expect(cells.slice(2, 9)).toEqual([
      '1',
      '0',
      '1',
      plain(en('externalParticipation.monitor.overdueCount', { count: 1 })),
      '1',
      '1',
      '0 / 0',
    ]);
    expect(document.querySelector('[data-field="accessNotAvailable"]')).not.toBeNull();
    expect(within(table).getByRole('link').getAttribute('href')).toBe(
      `/external-requests?projectId=${PROJECT_ID}&externalEntityId=${ENTITY_ID}`,
    );
    const violations = await accessibilityViolations();
    expect(violations, describeViolations(violations)).toEqual([]);
  });

  test('in Arabic, SCR-166 reads its states in Arabic, right to left', async () => {
    open('/external-requests/applications', threeOutcomes(), { language: 'ar' });
    await waitFor(() => caseCard('CONFLICT'));

    expect(document.documentElement.getAttribute('dir')).toBe('rtl');
    expect(plain(caseCard('CONFLICT').querySelector('[data-application-state]')?.textContent)).toBe(
      translate('ar', 'externalParticipation.applicationState.CONFLICT'),
    );
  });
});

/** The fixture's external view is the server's: the same keys as the internal one, less exactly the masked ones. */
test('fixture check: the external projection differs from the internal one by exactly the masked fields', () => {
  const internal = progressRequest();
  const external = externalView(internal);
  const removed = Object.keys(internal).filter((key) => !(key in external));
  expect(removed.sort()).toEqual([...external.maskedFields].sort());
});
