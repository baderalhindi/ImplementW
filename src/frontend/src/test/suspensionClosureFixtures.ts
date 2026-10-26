import {
  type ApprovalInboxItem,
  type ApprovalInstanceSummary,
} from '@/features/approvals/api/types.ts';
import { type ProjectSummary } from '@/features/projects/api/types.ts';
import {
  type ActiveSuspensionDetail,
  type ClosureCaseDetail,
  type CompletionCaseDetail,
  type PostProjectObligationDetail,
  type ReadinessCheckCode,
  type ReadinessCheckDetail,
  type ReadinessDetail,
  type ReadinessResult,
  type SuspensionRequestDetail,
} from '@/features/suspension-closure/api/types.ts';

import { concernProjects } from './concernFixtures.ts';
import { type MockApi, type MockReply, page, problem } from './mockApi.ts';
import { OTHER_PERSON_ID, PROJECT_ID } from './projectFixtures.ts';

// Test data only: fixed ids, each starting differently so a shortened id is recognisable.
//
// The Coastal road upgrade (PROJECT_ID), managed by Nora (internal, R04, OTHER_PERSON_ID); Faisal (R03, REVIEWER_ID)
// manages its department, reviews and waives.
//   WF-09: "Land handover dispute" — a suspension raised by Nora, DRAFT by default.
//   WF-10: a completion case raised by Nora, DRAFT by default, never evaluated; a closure case after it; an obligation
//   "Defect liability period" recorded against the completion case, OPEN with an owner and a due date.

export const SUSPENSION_ID = 'a9000000-0000-4000-8000-000000000101';
export const RESUMPTION_ID = 'a9000000-0000-4000-8000-000000000102';
export const NEW_SUSPENSION_ID = 'a9000000-0000-4000-8000-000000000103';
export const PERIOD_ID = 'aa000000-0000-4000-8000-000000000104';
export const COMPLETION_ID = 'ab000000-0000-4000-8000-000000000201';
export const CLOSURE_ID = 'ab000000-0000-4000-8000-000000000202';
export const NEW_CASE_ID = 'ab000000-0000-4000-8000-000000000203';
export const OBLIGATION_ID = 'ac000000-0000-4000-8000-000000000301';
export const NEW_OBLIGATION_ID = 'ac000000-0000-4000-8000-000000000302';
export const CLOSEOUT_RUN_ID = 'ad000000-0000-4000-8000-000000000401';
export const CLOSEOUT_TASK_ID = 'ad000000-0000-4000-8000-000000000402';
export const SUSPENSION_ETAG = '"61"';
export const CASE_ETAG = '"62"';

export function suspensionRequest(
  overrides: Partial<SuspensionRequestDetail> = {},
): SuspensionRequestDetail {
  return {
    id: SUSPENSION_ID,
    projectId: PROJECT_ID,
    requestType: 'SUSPEND',
    status: 'DRAFT',
    revisionNo: 1,
    reason: { text: 'Land handover dispute', language: 'EN' },
    requestedByUserId: OTHER_PERSON_ID,
    submittedAt: null,
    requestedEffectiveDate: '2099-01-15',
    plannedResumptionDate: '2099-04-01',
    effectedAt: null,
    suspension: null,
    createdAt: '2026-10-05T09:00:00Z',
    createdBy: OTHER_PERSON_ID,
    updatedAt: '2026-10-05T09:00:00Z',
    updatedBy: OTHER_PERSON_ID,
    ...overrides,
  };
}

export function suspensionPeriod(
  overrides: Partial<ActiveSuspensionDetail> = {},
): ActiveSuspensionDetail {
  return {
    id: PERIOD_ID,
    projectId: PROJECT_ID,
    suspensionRequestId: SUSPENSION_ID,
    startedAt: '2026-10-07T00:00:10Z',
    endedAt: null,
    endReason: null,
    resumptionRequestId: null,
    ...overrides,
  };
}

/** The suspension, in effect since its activation: the project SUSPENDED and its period open. */
export function suspensionInEffect(): SuspensionRequestDetail {
  return suspensionRequest({
    status: 'EFFECTED',
    submittedAt: '2026-10-05T10:00:00Z',
    requestedEffectiveDate: '2026-10-07',
    effectedAt: '2026-10-07T00:00:10Z',
    suspension: suspensionPeriod(),
  });
}

export function check(
  checkCode: ReadinessCheckCode,
  result: ReadinessResult = 'PASS',
  overrides: Partial<ReadinessCheckDetail> = {},
): ReadinessCheckDetail {
  const unwaivable: ReadinessCheckCode[] = [
    'DECISIONS_SETTLED',
    'SUSPENSION_REQUESTS_SETTLED',
    'OBLIGATIONS_OWNED',
    'OBLIGATIONS_SATISFIED',
  ];
  return {
    checkCode,
    result,
    blockingCount: result === 'PASS' ? 0 : 2,
    waivable: !unwaivable.includes(checkCode),
    waivedByUserId: null,
    waiverReason: null,
    ...overrides,
  };
}

export function readiness(overrides: Partial<ReadinessDetail> = {}): ReadinessDetail {
  return { status: 'INCOMPLETE', evaluatedAt: null, checks: [], ...overrides };
}

/** The latest evaluation of a completion case: progress not reported (waivable) and an obligation without an owner. */
export function notReady(): ReadinessDetail {
  return readiness({
    status: 'NOT_READY',
    evaluatedAt: '2026-10-08T09:00:00Z',
    checks: [
      check('DECISIONS_SETTLED'),
      check('TASKS_DISPOSITIONED'),
      check('PROGRESS_REPORTED', 'FAIL', { blockingCount: 1 }),
      check('OBLIGATIONS_OWNED', 'FAIL', { blockingCount: 1 }),
    ],
  });
}

export function completionCase(
  overrides: Partial<CompletionCaseDetail> = {},
): CompletionCaseDetail {
  return {
    id: COMPLETION_ID,
    projectId: PROJECT_ID,
    status: 'DRAFT',
    revisionNo: 1,
    requestedByUserId: OTHER_PERSON_ID,
    submittedAt: null,
    actualProjectCompletionDate: '2026-10-06',
    completionNarrative: { text: 'Road surfaced and handed to the municipality.', language: 'EN' },
    effectedAt: null,
    readiness: readiness(),
    createdAt: '2026-10-08T08:00:00Z',
    createdBy: OTHER_PERSON_ID,
    updatedAt: '2026-10-08T08:00:00Z',
    updatedBy: OTHER_PERSON_ID,
    ...overrides,
  };
}

/** Stage 1 in effect: the project COMPLETED. */
export function completionEffected(): CompletionCaseDetail {
  return completionCase({
    status: 'EFFECTED',
    submittedAt: '2026-10-08T10:00:00Z',
    effectedAt: '2026-10-09T09:00:00Z',
    readiness: readiness({ status: 'READY', evaluatedAt: '2026-10-09T09:00:00Z' }),
    updatedAt: '2026-10-09T09:00:00Z',
  });
}

export function closureCase(overrides: Partial<ClosureCaseDetail> = {}): ClosureCaseDetail {
  return {
    id: CLOSURE_ID,
    projectId: PROJECT_ID,
    completionCaseId: COMPLETION_ID,
    outcome: 'COMPLETED',
    status: 'DRAFT',
    revisionNo: 1,
    requestedByUserId: OTHER_PERSON_ID,
    submittedAt: null,
    closureNarrative: { text: 'Every obligation settled.', language: 'EN' },
    effectedAt: null,
    readiness: readiness(),
    createdAt: '2026-10-10T08:00:00Z',
    createdBy: OTHER_PERSON_ID,
    updatedAt: '2026-10-10T08:00:00Z',
    updatedBy: OTHER_PERSON_ID,
    ...overrides,
  };
}

export function obligation(
  overrides: Partial<PostProjectObligationDetail> = {},
): PostProjectObligationDetail {
  return {
    id: OBLIGATION_ID,
    projectId: PROJECT_ID,
    completionCaseId: COMPLETION_ID,
    closureCaseId: null,
    title: { text: 'Defect liability period', language: 'EN' },
    description: null,
    ownerUserId: OTHER_PERSON_ID,
    dueDate: '2027-10-01',
    status: 'OPEN',
    satisfiedAt: null,
    createdAt: '2026-10-08T08:30:00Z',
    createdBy: OTHER_PERSON_ID,
    updatedAt: '2026-10-08T08:30:00Z',
    updatedBy: OTHER_PERSON_ID,
    ...overrides,
  };
}

/** The WF-11 run reviewing a closeout case, started with its originator, Nora, as its requester. */
export function closeoutRun(
  overrides: Partial<ApprovalInstanceSummary> = {},
): ApprovalInstanceSummary {
  return {
    id: CLOSEOUT_RUN_ID,
    subject: { module: 'Closure', type: 'CompletionCase', id: COMPLETION_ID, revisionNo: 1 },
    routingKey: 'COMPLETION',
    requestedByUserId: OTHER_PERSON_ID,
    requestedAt: '2026-10-08T12:00:00Z',
    status: 'PENDING',
    completedAt: null,
    ...overrides,
  };
}

export function closeoutInboxItem(overrides: Partial<ApprovalInboxItem> = {}): ApprovalInboxItem {
  return {
    taskId: CLOSEOUT_TASK_ID,
    sequenceNo: 1,
    assignedRoleId: 'd4d4d4d4-0000-4000-8000-0000000000b2',
    dueAt: '2099-01-10T09:00:00Z',
    onBehalfOfUserId: null,
    instance: closeoutRun(),
    ...overrides,
  };
}

export interface SuspensionClosureState {
  projects?: ProjectSummary[];
  requests?: SuspensionRequestDetail[];
  periods?: ActiveSuspensionDetail[];
  completions?: CompletionCaseDetail[];
  closures?: ClosureCaseDetail[];
  obligations?: PostProjectObligationDetail[];
  runs?: ApprovalInstanceSummary[];
  inbox?: ApprovalInboxItem[];
  /** What a case's `submit` answers; by default the case SUBMITTED. */
  submitReply?: MockReply;
  /** The readiness a case's `evaluate-readiness` records; by default `notReady()`. */
  evaluated?: ReadinessDetail;
}

type Store<T extends { id: string }> = T[];

function replaceIn<T extends { id: string }>(
  store: Store<T>,
  id: string,
  change: (found: T) => T,
  etag: string,
): MockReply {
  const index = store.findIndex((item) => item.id === id);
  const found = store[index];
  if (found === undefined) {
    return problem(404, 'NOT_FOUND');
  }
  const changed = change(found);
  store[index] = changed;
  return { body: changed, headers: { ETag: etag } };
}

function narrative(text: { text: string; language: string } | null) {
  return text === null ? null : { text: text.text, language: text.language.toUpperCase() };
}

/**
 * The WF-09 and WF-10 APIs over in-memory records — create, read, update, the lifecycle commands, readiness and
 * waivers, obligations — and the WF-11 reads SCR-110 and SCR-113 make. Layer over withProjectLookups (names) and
 * withProject (the record's project).
 */
export function withSuspensionClosure(
  api: MockApi,
  {
    projects = concernProjects(),
    requests = [suspensionRequest()],
    periods = [],
    completions = [],
    closures = [],
    obligations = [],
    runs = [],
    inbox = [],
    submitReply,
    evaluated = notReady(),
  }: SuspensionClosureState = {},
): MockApi {
  const requestStore = [...requests];
  const completionStore = [...completions];
  const closureStore = [...closures];
  const obligationStore = [...obligations];
  const idOf = (path: string) => path.split('/')[2] ?? '';
  const byProject = <T extends { projectId: string }>(items: T[], query: URLSearchParams) => ({
    body: page(
      items.filter((item) => item.projectId === query.get('projectId')),
      200,
    ),
  });

  api
    .on('GET', /^\/projects$/, { body: page(projects, 200) })
    // WF-09
    .on('GET', /^\/suspension-requests$/, (request) => byProject(requestStore, request.query))
    .on('GET', /^\/suspension-requests\/[^/]+$/, (request) => {
      const found = requestStore.find((item) => item.id === idOf(request.path));
      return found === undefined
        ? problem(404, 'NOT_FOUND')
        : { body: found, headers: { ETag: SUSPENSION_ETAG } };
    })
    .on('POST', /^\/suspension-requests$/, (request) => {
      const body = request.body as SuspensionRequestDetail;
      const created = suspensionRequest({
        id: NEW_SUSPENSION_ID,
        projectId: body.projectId,
        requestType: body.requestType,
        reason: narrative(body.reason) ?? suspensionRequest().reason,
        requestedEffectiveDate: body.requestedEffectiveDate,
        plannedResumptionDate: body.plannedResumptionDate,
      });
      requestStore.push(created);
      return { status: 201, body: created, headers: { ETag: SUSPENSION_ETAG } };
    })
    .on('PUT', /^\/suspension-requests\/[^/]+$/, (request) =>
      replaceIn(
        requestStore,
        idOf(request.path),
        (found) => {
          const body = request.body as SuspensionRequestDetail;
          return {
            ...found,
            reason: narrative(body.reason) ?? found.reason,
            requestedEffectiveDate: body.requestedEffectiveDate,
            plannedResumptionDate: body.plannedResumptionDate,
          };
        },
        SUSPENSION_ETAG,
      ),
    )
    .on('POST', /^\/suspension-requests\/[^/]+\/submit$/, (request) =>
      replaceIn(
        requestStore,
        idOf(request.path),
        (found) => ({
          ...found,
          status: 'SUBMITTED' as const,
          submittedAt: '2026-10-08T10:00:00Z',
        }),
        SUSPENSION_ETAG,
      ),
    )
    .on('POST', /^\/suspension-requests\/[^/]+\/start-review$/, (request) =>
      replaceIn(
        requestStore,
        idOf(request.path),
        (found) => ({ ...found, status: 'UNDER_REVIEW' as const }),
        SUSPENSION_ETAG,
      ),
    )
    .on('GET', /^\/active-suspensions$/, (request) => byProject(periods, request.query))
    // WF-10
    .on('GET', /^\/completion-cases$/, (request) => byProject(completionStore, request.query))
    .on('GET', /^\/closure-cases$/, (request) => byProject(closureStore, request.query))
    .on('GET', /^\/(completion|closure)-cases\/[^/]+$/, (request) => {
      const store: { id: string }[] = request.path.startsWith('/completion')
        ? completionStore
        : closureStore;
      const found = store.find((item) => item.id === idOf(request.path));
      return found === undefined
        ? problem(404, 'NOT_FOUND')
        : { body: found, headers: { ETag: CASE_ETAG } };
    })
    .on('POST', /^\/completion-cases$/, (request) => {
      const body = request.body as CompletionCaseDetail;
      const created = completionCase({
        id: NEW_CASE_ID,
        projectId: body.projectId,
        actualProjectCompletionDate: body.actualProjectCompletionDate,
        completionNarrative: narrative(body.completionNarrative),
      });
      completionStore.push(created);
      return { status: 201, body: created, headers: { ETag: CASE_ETAG } };
    })
    .on('POST', /^\/closure-cases$/, (request) => {
      const body = request.body as ClosureCaseDetail;
      const created = closureCase({
        id: NEW_CASE_ID,
        projectId: body.projectId,
        closureNarrative: narrative(body.closureNarrative),
      });
      closureStore.push(created);
      return { status: 201, body: created, headers: { ETag: CASE_ETAG } };
    })
    .on('POST', /^\/completion-cases\/[^/]+\/evaluate-readiness$/, (request) =>
      replaceIn(
        completionStore,
        idOf(request.path),
        (found) => ({ ...found, readiness: evaluated }),
        CASE_ETAG,
      ),
    )
    .on('POST', /^\/completion-cases\/[^/]+\/waive-check$/, (request) =>
      replaceIn(
        completionStore,
        idOf(request.path),
        (found) => {
          const body = request.body as { checkCode: ReadinessCheckCode };
          return {
            ...found,
            readiness: {
              ...found.readiness,
              checks: found.readiness.checks.map((item) =>
                item.checkCode === body.checkCode ? { ...item, result: 'WAIVED' as const } : item,
              ),
            },
          };
        },
        CASE_ETAG,
      ),
    )
    .on(
      'POST',
      /^\/completion-cases\/[^/]+\/submit$/,
      (request) =>
        submitReply ??
        replaceIn(
          completionStore,
          idOf(request.path),
          (found) => ({
            ...found,
            status: 'SUBMITTED' as const,
            submittedAt: '2026-10-08T10:00:00Z',
          }),
          CASE_ETAG,
        ),
    )
    // Obligations
    .on('GET', /^\/post-project-obligations$/, (request) =>
      byProject(obligationStore, request.query),
    )
    .on('GET', /^\/post-project-obligations\/[^/]+$/, (request) => {
      const found = obligationStore.find((item) => item.id === idOf(request.path));
      return found === undefined
        ? problem(404, 'NOT_FOUND')
        : { body: found, headers: { ETag: CASE_ETAG } };
    })
    .on('POST', /^\/post-project-obligations$/, (request) => {
      const body = request.body as PostProjectObligationDetail;
      const created = obligation({
        id: NEW_OBLIGATION_ID,
        completionCaseId: body.completionCaseId,
        closureCaseId: body.closureCaseId,
        title: narrative(body.title) ?? obligation().title,
        ownerUserId: body.ownerUserId,
        dueDate: body.dueDate,
      });
      obligationStore.push(created);
      return { status: 201, body: created, headers: { ETag: CASE_ETAG } };
    })
    .on('POST', /^\/post-project-obligations\/[^/]+\/satisfy$/, (request) =>
      replaceIn(
        obligationStore,
        idOf(request.path),
        (found) => ({
          ...found,
          status: 'SATISFIED' as const,
          satisfiedAt: '2026-10-09T09:00:00Z',
        }),
        CASE_ETAG,
      ),
    )
    // WF-11
    .on('GET', /^\/approval-instances$/, (request) => ({
      body: page(
        runs.filter((run) => run.subject.id === request.query.get('subjectId')),
        200,
      ),
    }))
    .on('GET', /^\/approval-tasks$/, { body: page(inbox, 200) });
  return api;
}
