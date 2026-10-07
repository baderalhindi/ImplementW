import {
  type ApprovalInboxItem,
  type ApprovalInstanceSummary,
} from '@/features/approvals/api/types.ts';
import {
  type ChangeAuthorizationDetail,
  type ChangeRequestCreateRequest,
  type ChangeRequestDetail,
  type ChangeRequestRequest,
  type MaterialityAssessment,
} from '@/features/change-requests/api/types.ts';
import { type ProjectSummary } from '@/features/projects/api/types.ts';

import { concernProjects } from './concernFixtures.ts';
import { type MockApi, type MockReply, page, problem } from './mockApi.ts';
import { OTHER_PERSON_ID, PROJECT_ID, REVIEWER_ID } from './projectFixtures.ts';
import { OTHER_PROJECT_ID } from './taskFixtures.ts';

// Test data only: fixed ids, each starting differently so a shortened id is recognisable.
//
// The Coastal road upgrade (PROJECT_ID), ACTIVE, managed by Nora (internal, R04, OTHER_PERSON_ID); Faisal (R03,
// REVIEWER_ID) manages its department and reviews its change requests.
//   "Extend the paving season" — a schedule change of 12 days and SAR 60,000.00, raised by Nora; DRAFT by default.
// The Harbour bridge repair (OTHER_PROJECT_ID): "Replace the bearing pads" — a cost change, SUBMITTED.

export const PAVING_ID = 'c8000000-0000-4000-8000-000000000201';
export const NEW_CHANGE_REQUEST_ID = 'c8000000-0000-4000-8000-000000000202';
export const BEARING_ID = 'c8000000-0000-4000-8000-000000000203';
export const MATERIALITY_VERSION_ID = 'd8000000-0000-4000-8000-000000000301';
export const EVALUATION_ID = 'd8000000-0000-4000-8000-000000000302';
export const BASELINE_ID = 'd8000000-0000-4000-8000-000000000303';
export const BUDGET_ID = 'd8000000-0000-4000-8000-000000000304';
export const REBASELINE_AUTHORIZATION_ID = 'e8000000-0000-4000-8000-000000000401';
export const BUDGET_AUTHORIZATION_ID = 'e8000000-0000-4000-8000-000000000402';
export const CHANGE_RUN_ID = 'f8000000-0000-4000-8000-000000000501';
export const CHANGE_TASK_ID = 'f8000000-0000-4000-8000-000000000502';
export const CHANGE_REQUEST_ETAG = '"42"';

export function changeRequest(overrides: Partial<ChangeRequestDetail> = {}): ChangeRequestDetail {
  return {
    id: PAVING_ID,
    projectId: PROJECT_ID,
    title: { text: 'Extend the paving season', language: 'EN' },
    justification: { text: 'The asphalt plant opens six weeks late.', language: 'EN' },
    changeType: 'SCHEDULE',
    status: 'DRAFT',
    revisionNo: 1,
    requestedByUserId: OTHER_PERSON_ID,
    submittedAt: null,
    costImpactSar: '60000.00',
    scheduleImpactDays: 12,
    scopeImpact: null,
    isContractualObligation: false,
    requestedGovernanceProfileItemId: null,
    materiality: null,
    authorizations: [],
    implementedAt: null,
    closedAt: null,
    createdAt: '2026-10-05T09:00:00Z',
    createdBy: OTHER_PERSON_ID,
    updatedAt: '2026-10-05T09:00:00Z',
    updatedBy: OTHER_PERSON_ID,
    ...overrides,
  };
}

/** Band 2: cost 6% of the budget triggers band 2, 12 days band 1; no scope impact. A preview: no evaluation id. */
export function previewAssessment(
  overrides: Partial<MaterialityAssessment> = {},
): MaterialityAssessment {
  return {
    evaluationId: null,
    revisionNo: 1,
    evaluatedAt: '2026-10-06T10:00:00Z',
    materialityConfigurationVersionId: MATERIALITY_VERSION_ID,
    projectBaselineId: BASELINE_ID,
    financialCommitmentId: BUDGET_ID,
    cumulativeCostImpactSar: '60000.00',
    cumulativeScheduleImpactDays: 12,
    costBandNo: 2,
    scheduleBandNo: 1,
    scopeBandNo: null,
    resultingBandNo: 2,
    ...overrides,
  };
}

/** The same classification, recorded when the review started. */
export function recordedAssessment(): MaterialityAssessment {
  return previewAssessment({ evaluationId: EVALUATION_ID, evaluatedAt: '2026-10-06T12:00:00Z' });
}

export function authorization(
  overrides: Partial<ChangeAuthorizationDetail> = {},
): ChangeAuthorizationDetail {
  return {
    id: REBASELINE_AUTHORIZATION_ID,
    changeRequestId: PAVING_ID,
    approvalInstanceId: CHANGE_RUN_ID,
    authorizationScope: 'REBASELINE',
    targetModule: 'Schedule',
    targetType: 'ProjectBaseline',
    targetId: BASELINE_ID,
    targetRevisionNo: 1,
    status: 'ISSUED',
    issuedAt: '2026-10-07T09:00:00Z',
    expiresAt: null,
    appliedAt: null,
    appliedByUserId: null,
    appliedReference: null,
    ...overrides,
  };
}

const APPLIED = {
  status: 'APPLIED',
  appliedAt: '2026-10-08T09:00:00Z',
  appliedByUserId: OTHER_PERSON_ID,
} as const;

/** The paving change at a later point of its lifecycle, with what that state carries. */
export function pavingAt(status: ChangeRequestDetail['status']): ChangeRequestDetail {
  const reviewed = { submittedAt: '2026-10-06T09:00:00Z', materiality: recordedAssessment() };
  const issued = [
    authorization(),
    authorization({
      id: BUDGET_AUTHORIZATION_ID,
      authorizationScope: 'COMMITMENT_CHANGE',
      targetModule: 'FinancialKpi',
      targetType: 'FinancialCommitment',
      targetId: BUDGET_ID,
    }),
  ];
  switch (status) {
    case 'DRAFT':
      return changeRequest();
    case 'SUBMITTED':
      return changeRequest({ status, submittedAt: '2026-10-06T09:00:00Z' });
    case 'UNDER_REVIEW':
    case 'RETURNED':
    case 'REJECTED':
    case 'WITHDRAWN':
      return changeRequest({ status, ...reviewed });
    case 'APPROVED':
      return changeRequest({ status, ...reviewed, authorizations: issued });
    case 'IMPLEMENTATION':
      return changeRequest({
        status,
        ...reviewed,
        authorizations: [
          authorization({
            ...APPLIED,
            appliedReference: `Schedule.ProjectBaseline:${BASELINE_ID}`,
          }),
          issued[1] ?? authorization(),
        ],
      });
    case 'IMPLEMENTED':
    case 'CLOSED':
      return changeRequest({
        status,
        ...reviewed,
        authorizations: issued.map((entry) => ({ ...entry, ...APPLIED })),
        implementedAt: '2026-10-09T09:00:00Z',
        closedAt: status === 'CLOSED' ? '2026-10-10T09:00:00Z' : null,
      });
  }
}

export function bearingRequest(): ChangeRequestDetail {
  return changeRequest({
    id: BEARING_ID,
    projectId: OTHER_PROJECT_ID,
    title: { text: 'Replace the bearing pads', language: 'EN' },
    changeType: 'COST',
    status: 'SUBMITTED',
    scheduleImpactDays: null,
    costImpactSar: '-15000.00',
    requestedByUserId: REVIEWER_ID,
    updatedAt: '2026-10-06T09:00:00Z',
  });
}

/** The WF-11 run reviewing a revision of the paving change, started with the originator, Nora, as its requester. */
export function changeRun(
  overrides: Partial<ApprovalInstanceSummary> = {},
): ApprovalInstanceSummary {
  return {
    id: CHANGE_RUN_ID,
    subject: { module: 'ChangeRequest', type: 'ChangeRequest', id: PAVING_ID, revisionNo: 1 },
    routingKey: 'CHANGE_REQUEST',
    requestedByUserId: OTHER_PERSON_ID,
    requestedAt: '2026-10-06T12:00:00Z',
    status: 'PENDING',
    completedAt: null,
    ...overrides,
  };
}

export function changeInboxItem(overrides: Partial<ApprovalInboxItem> = {}): ApprovalInboxItem {
  return {
    taskId: CHANGE_TASK_ID,
    sequenceNo: 1,
    assignedRoleId: 'd4d4d4d4-0000-4000-8000-0000000000b2',
    dueAt: '2099-01-10T09:00:00Z',
    onBehalfOfUserId: null,
    instance: changeRun(),
    ...overrides,
  };
}

export interface ChangeRequestState {
  requests?: ChangeRequestDetail[];
  projects?: ProjectSummary[];
  /** The runs the caller may read, or 403 without APPROVAL_VIEW (TASK-060 F-12). */
  runs?: ApprovalInstanceSummary[] | 'forbidden';
  inbox?: ApprovalInboxItem[];
  /** What `preview-materiality` answers. */
  preview?: MockReply;
}

function narrative(text: { text: string; language: string } | null) {
  return text === null ? null : { text: text.text, language: text.language.toUpperCase() };
}

function applyFields(
  request: ChangeRequestDetail,
  body: ChangeRequestRequest,
): ChangeRequestDetail {
  return {
    ...request,
    title: narrative(body.title) ?? request.title,
    justification: narrative(body.justification) ?? request.justification,
    costImpactSar: body.costImpactSar,
    scheduleImpactDays: body.scheduleImpactDays,
    scopeImpact: narrative(body.scopeImpact),
    isContractualObligation: body.isContractualObligation,
    requestedGovernanceProfileItemId: body.requestedGovernanceProfileItemId,
  };
}

/**
 * The WF-08 API over an in-memory set of requests — create, read, update, preview, the lifecycle commands — and the
 * WF-11 reads SCR-107 makes. Layer over withProjectLookups (names) and withProject (the request's project).
 */
export function withChangeRequests(
  api: MockApi,
  {
    requests = [changeRequest(), bearingRequest()],
    projects = concernProjects(),
    runs = [],
    inbox = [],
    preview = { body: previewAssessment() },
  }: ChangeRequestState = {},
): MockApi {
  const store = [...requests];
  const reply = (request: ChangeRequestDetail, status = 200): MockReply => ({
    status,
    body: request,
    headers: { ETag: CHANGE_REQUEST_ETAG },
  });
  const replace = (id: string, change: (request: ChangeRequestDetail) => ChangeRequestDetail) => {
    const index = store.findIndex((item) => item.id === id);
    const found = store[index];
    if (found === undefined) {
      return problem(404, 'NOT_FOUND');
    }
    const changed = change(found);
    store[index] = changed;
    return reply(changed);
  };
  const idOf = (path: string) => path.split('/')[2] ?? '';

  return api
    .on('GET', /^\/projects$/, { body: page(projects, 200) })
    .on('GET', /^\/change-requests$/, (request) => ({
      body: page(
        store.filter((item) => item.projectId === request.query.get('projectId')),
        200,
      ),
    }))
    .on('GET', /^\/change-requests\/[^/]+$/, (request) => {
      const found = store.find((item) => item.id === idOf(request.path));
      return found === undefined ? problem(404, 'NOT_FOUND') : reply(found);
    })
    .on('POST', /^\/change-requests$/, (request) => {
      const body = request.body as ChangeRequestCreateRequest;
      const created = applyFields(
        changeRequest({
          id: NEW_CHANGE_REQUEST_ID,
          projectId: body.projectId,
          changeType: body.changeType,
        }),
        body,
      );
      store.push(created);
      return reply(created, 201);
    })
    .on('PUT', /^\/change-requests\/[^/]+$/, (request) =>
      replace(idOf(request.path), (found) =>
        applyFields(found, request.body as ChangeRequestRequest),
      ),
    )
    .on('DELETE', /^\/change-requests\/[^/]+$/, { status: 204 })
    .on('POST', /^\/change-requests\/[^/]+\/preview-materiality$/, preview)
    .on('POST', /^\/change-requests\/[^/]+\/submit$/, (request) =>
      replace(idOf(request.path), (found) => ({
        ...found,
        status: 'SUBMITTED',
        submittedAt: '2026-10-06T09:00:00Z',
      })),
    )
    .on('POST', /^\/change-requests\/[^/]+\/start-review$/, (request) =>
      replace(idOf(request.path), (found) => ({
        ...found,
        status: 'UNDER_REVIEW',
        materiality: recordedAssessment(),
      })),
    )
    .on('GET', /^\/approval-instances$/, (request) =>
      runs === 'forbidden'
        ? problem(403, 'PERMISSION_DENIED')
        : {
            body: page(
              runs.filter((run) => run.subject.id === request.query.get('subjectId')),
              200,
            ),
          },
    )
    .on('GET', /^\/approval-tasks$/, { body: page(inbox, 200) });
}
