import {
  type ContributionFieldValue,
  type ExternalContributionDetail,
  type ExternalUpdateRequestDetail,
  type SourceApplicationDetail,
  type SourceApplicationStatus,
} from '@/features/external-participation/api/types.ts';
import {
  CONTRIBUTION_INTERNAL_ONLY_FIELDS,
  REQUEST_INTERNAL_ONLY_FIELDS,
} from '@/features/external-participation/projection.ts';
import { type ProjectTaskDetail } from '@/features/tasks/api/types.ts';

import { type MockApi, type MockReply, page, problem } from './mockApi.ts';
import { ENTITY_ID, ENTITY_USER_ID, OTHER_PERSON_ID, PROJECT_ID } from './projectFixtures.ts';

// Test data only: invented names and fixed ids, each starting differently so a shortened id is recognisable.
//
// The Coastal road upgrade (PROJECT_ID, formal ID PRJ-2026-0042), ACTIVE, managed by Nora (internal R04,
// OTHER_PERSON_ID), delivered by Contractor A (ENTITY_ID). AHDA asks Contractor A's Huda (R08, ENTITY_USER_ID):
//   "Base course progress" — TASK_PROGRESS on the task "Lay the base course" (40 % now), reviewed by Nora.
//   "Drainage survey status" — PROJECT_INFORMATION, issued, not yet answered.
//   "Lighting handover" — an AHDA DRAFT the entity cannot see.

export const PROGRESS_REQUEST_ID = 'e1300000-0000-4000-8000-000000000101';
export const INFO_REQUEST_ID = 'e1300000-0000-4000-8000-000000000102';
export const DRAFT_REQUEST_ID = 'e1300000-0000-4000-8000-000000000103';
export const NEW_REQUEST_ID = 'e1300000-0000-4000-8000-000000000104';
export const REVISION_1_ID = 'c1300000-0000-4000-8000-000000000201';
export const REVISION_2_ID = 'c1300000-0000-4000-8000-000000000202';
export const NEW_REVISION_ID = 'c1300000-0000-4000-8000-000000000203';
export const ATTEMPT_1_ID = 'a1300000-0000-4000-8000-000000000301';
export const ATTEMPT_2_ID = 'a1300000-0000-4000-8000-000000000302';
export const BASE_COURSE_TASK_ID = 'b1300000-0000-4000-8000-000000000401';
export const TASK_PROGRESS_TYPE_ID = 'd1300000-0000-4000-8000-000000000501';
export const PROJECT_INFORMATION_TYPE_ID = 'd1300000-0000-4000-8000-000000000502';
export const PARTICIPATION_VERSION_ID = 'f1300000-0000-4000-8000-000000000601';
export const CONTRIBUTION_TYPE_CATALOGUE_ID = 'c0000000-0000-4000-8000-000000000081';
export const FORMAL_PROJECT_ID = 'PRJ-2026-0042';
export const REQUEST_ETAG = '"501"';
export const REVISION_ETAG = '"601"';
/** The task's row version when Huda answered, and after Nora changed it. */
export const TASK_VERSION_ANSWERED = 7001;
export const TASK_VERSION_CHANGED = 7002;
export const INTERNAL_NOTE = 'Checked against the site diary of 6 October.';

export function progressRequest(
  overrides: Partial<ExternalUpdateRequestDetail> = {},
): ExternalUpdateRequestDetail {
  return {
    id: PROGRESS_REQUEST_ID,
    projectId: PROJECT_ID,
    formalProjectId: FORMAL_PROJECT_ID,
    externalEntityId: ENTITY_ID,
    origin: 'AHDA_ISSUED',
    contributionTypeItemId: TASK_PROGRESS_TYPE_ID,
    contributionSchemaCode: 'TASK_PROGRESS',
    applicationMode: 'UPDATE_ALLOWED_SOURCE_FIELDS',
    targetModule: 'ProjectTask',
    targetType: 'ProjectTask',
    targetId: BASE_COURSE_TASK_ID,
    targetLabel: { text: 'Lay the base course', language: 'EN' },
    responseFields: [
      {
        fieldCode: 'actualPercentComplete',
        fieldType: 'NUMBER',
        required: true,
        minimum: 0,
        maximum: 100,
      },
      {
        fieldCode: 'progressNote',
        fieldType: 'NARRATIVE',
        required: false,
        minimum: null,
        maximum: null,
      },
    ],
    instructions: { text: 'Report the base course progress as of Thursday.', language: 'EN' },
    responsibleUserId: ENTITY_USER_ID,
    reviewerUserId: OTHER_PERSON_ID,
    dueDate: '2099-10-15',
    dueCondition: 'NOT_DUE',
    status: 'ISSUED',
    participationConfigurationVersionId: PARTICIPATION_VERSION_ID,
    issuedByUserId: OTHER_PERSON_ID,
    issuedAt: '2026-10-05T09:00:00Z',
    cancelledAt: null,
    cancellationReason: null,
    closedAt: null,
    createdAt: '2026-10-04T09:00:00Z',
    createdBy: OTHER_PERSON_ID,
    updatedAt: '2026-10-05T09:00:00Z',
    updatedBy: OTHER_PERSON_ID,
    projection: 'INTERNAL',
    maskedFields: [],
    ...overrides,
  };
}

export function infoRequest(
  overrides: Partial<ExternalUpdateRequestDetail> = {},
): ExternalUpdateRequestDetail {
  return progressRequest({
    id: INFO_REQUEST_ID,
    contributionTypeItemId: PROJECT_INFORMATION_TYPE_ID,
    contributionSchemaCode: 'PROJECT_INFORMATION',
    applicationMode: 'REFERENCE_ONLY',
    targetModule: null,
    targetType: null,
    targetId: null,
    targetLabel: null,
    responseFields: [
      {
        fieldCode: 'response',
        fieldType: 'NARRATIVE',
        required: true,
        minimum: null,
        maximum: null,
      },
      { fieldCode: 'asOfDate', fieldType: 'DATE', required: false, minimum: null, maximum: null },
    ],
    instructions: { text: 'Tell us where the drainage survey stands.', language: 'EN' },
    dueDate: '2026-10-01',
    dueCondition: 'OVERDUE',
    updatedAt: '2026-10-06T09:00:00Z',
    ...overrides,
  });
}

export function draftRequest(
  overrides: Partial<ExternalUpdateRequestDetail> = {},
): ExternalUpdateRequestDetail {
  return infoRequest({
    id: DRAFT_REQUEST_ID,
    instructions: { text: 'Confirm the lighting handover date.', language: 'EN' },
    status: 'DRAFT',
    dueDate: null,
    dueCondition: 'NOT_APPLICABLE',
    responsibleUserId: null,
    participationConfigurationVersionId: null,
    issuedByUserId: null,
    issuedAt: null,
    updatedAt: '2026-10-07T09:00:00Z',
    ...overrides,
  });
}

function values(percent: string, note: string | null): ContributionFieldValue[] {
  return [
    { fieldCode: 'actualPercentComplete', value: percent, language: null },
    ...(note === null ? [] : [{ fieldCode: 'progressNote', value: note, language: 'EN' as const }]),
  ];
}

export function revision(
  overrides: Partial<ExternalContributionDetail> = {},
): ExternalContributionDetail {
  return {
    id: REVISION_1_ID,
    externalUpdateRequestId: PROGRESS_REQUEST_ID,
    projectId: PROJECT_ID,
    externalEntityId: ENTITY_ID,
    revisionNo: 1,
    previousRevisionId: null,
    status: 'SUBMITTED',
    contributorUserId: ENTITY_USER_ID,
    fields: values('45', 'Base course laid on chainage 0 to 900.'),
    submittedAt: '2026-10-06T10:00:00Z',
    targetVersion: TASK_VERSION_ANSWERED,
    targetState: 'IN_PROGRESS',
    reviewedByUserId: null,
    reviewStartedAt: null,
    reviewedAt: null,
    reviewReason: null,
    reviewInternalNote: null,
    createdAt: '2026-10-06T09:30:00Z',
    createdBy: ENTITY_USER_ID,
    updatedAt: '2026-10-06T10:00:00Z',
    updatedBy: ENTITY_USER_ID,
    projection: 'INTERNAL',
    maskedFields: [],
    ...overrides,
  };
}

/** Revision 1 returned with a reason and an internal note; revision 2 accepted, pending application. */
export function returnedThenAccepted(): ExternalContributionDetail[] {
  return [
    revision({
      id: REVISION_2_ID,
      revisionNo: 2,
      previousRevisionId: REVISION_1_ID,
      status: 'ACCEPTED_PENDING_APPLICATION',
      fields: values('42', 'Corrected after the site diary check.'),
      submittedAt: '2026-10-07T10:00:00Z',
      reviewedByUserId: OTHER_PERSON_ID,
      reviewStartedAt: '2026-10-07T11:00:00Z',
      reviewedAt: '2026-10-07T11:30:00Z',
      reviewInternalNote: { text: INTERNAL_NOTE, language: 'EN' },
      updatedAt: '2026-10-07T11:30:00Z',
    }),
    revision({
      status: 'RETURNED',
      reviewedByUserId: OTHER_PERSON_ID,
      reviewStartedAt: '2026-10-06T11:00:00Z',
      reviewedAt: '2026-10-06T11:30:00Z',
      reviewReason: { text: 'The diary shows 42 %, not 45 %.', language: 'EN' },
      reviewInternalNote: { text: 'Second return would escalate to the sponsor.', language: 'EN' },
    }),
  ];
}

export function attempt(overrides: Partial<SourceApplicationDetail> = {}): SourceApplicationDetail {
  return {
    id: ATTEMPT_1_ID,
    externalContributionId: REVISION_2_ID,
    externalUpdateRequestId: PROGRESS_REQUEST_ID,
    attemptNo: 1,
    status: 'APPLIED',
    applicationMode: 'UPDATE_ALLOWED_SOURCE_FIELDS',
    targetModule: 'ProjectTask',
    targetType: 'ProjectTask',
    targetId: BASE_COURSE_TASK_ID,
    expectedTargetRevisionNo: TASK_VERSION_ANSWERED,
    actualTargetRevisionNo: TASK_VERSION_ANSWERED,
    failureCode: null,
    attemptedByUserId: OTHER_PERSON_ID,
    attemptedAt: '2026-10-08T09:00:00Z',
    completedAt: '2026-10-08T09:00:01Z',
    revalidatedAt: null,
    revalidatedByUserId: null,
    revalidatedTargetRevisionNo: null,
    correlationId: '0a0a0a0a-0000-4000-8000-000000000901',
    ...overrides,
  };
}

export function baseCourseTask(overrides: Partial<ProjectTaskDetail> = {}): ProjectTaskDetail {
  return {
    id: BASE_COURSE_TASK_ID,
    projectId: PROJECT_ID,
    scheduleActivityId: null,
    parentTaskId: null,
    title: { text: 'Lay the base course', language: 'EN' },
    description: null,
    assigneeUserId: null,
    priorityItemId: null,
    status: 'IN_PROGRESS',
    plannedStartDate: '2026-10-01',
    plannedFinishDate: '2026-11-30',
    plannedDurationDays: 61,
    actualStartDate: '2026-10-01',
    actualFinishDate: null,
    actualPercentComplete: '40',
    percentComplete: '40',
    subtaskCount: 0,
    blockedReason: null,
    completedAt: null,
    reopenedCount: 0,
    createdAt: '2026-09-30T09:00:00Z',
    createdBy: OTHER_PERSON_ID,
    updatedAt: '2026-10-02T09:00:00Z',
    updatedBy: OTHER_PERSON_ID,
    ...overrides,
  };
}

/** As the server writes an external caller's view (TASK-066 D-6): internal-only fields named and absent. */
export function externalView<T extends ExternalUpdateRequestDetail | ExternalContributionDetail>(
  record: T,
): T {
  const internalOnly: readonly string[] =
    'responseFields' in record ? REQUEST_INTERNAL_ONLY_FIELDS : CONTRIBUTION_INTERNAL_ONLY_FIELDS;
  const kept = Object.entries(record).filter(([field]) => !internalOnly.includes(field));
  return {
    ...Object.fromEntries(kept),
    projection: 'EXTERNAL',
    maskedFields: [...internalOnly],
  } as T;
}

export interface ExternalParticipationState {
  requests?: ExternalUpdateRequestDetail[];
  /** By request id, newest first. */
  revisions?: Record<string, ExternalContributionDetail[]>;
  /** By revision id, newest first. */
  attempts?: Record<string, SourceApplicationDetail[]>;
  /** Which representation the caller gets, as the API decides by the caller's user type. */
  audience?: 'INTERNAL' | 'EXTERNAL';
  /** What the next application attempt finds. */
  applyOutcome?: SourceApplicationStatus;
  /** The failure code of a FAILED attempt; a retryable one leaves the revision pending. */
  applyFailure?: string;
  /** The source task as it stands now, and its row version (its ETag); null when the reviewer may not read it. */
  task?: ProjectTaskDetail | null;
  taskVersion?: number;
  /**
   * Answer the CONTRIBUTION_TYPE catalogue (MASTER_DATA_VIEW). Off where other modules' catalogues are answered by
   * `withProjectLookups`, whose master-data routes these would replace.
   */
  contributionTypes?: boolean;
}

const REQUEST_PATH = /^\/external-update-requests\/([^/]+)$/;
const REQUEST_COMMAND = /^\/external-update-requests\/([^/]+)\/([a-z-]+)$/;
const REVISION_PATH = /^\/external-contributions\/([^/]+)$/;
const REVISION_COMMAND = /^\/external-contributions\/([^/]+)\/([a-z-]+)$/;

/**
 * WF-13's 22 operations over a little state that commands change, so a screen read again shows the effect. The
 * caller's audience decides the representation as the API's does: an external caller gets the projection, never sees a
 * DRAFT request (404) and reads no application attempt (404).
 */
export function withExternalParticipation(
  api: MockApi,
  state: ExternalParticipationState = {},
): MockApi {
  const requests = [...(state.requests ?? [progressRequest(), infoRequest(), draftRequest()])];
  const revisions: Record<string, ExternalContributionDetail[]> = { ...(state.revisions ?? {}) };
  const attempts: Record<string, SourceApplicationDetail[]> = { ...(state.attempts ?? {}) };
  const external = state.audience === 'EXTERNAL';
  const keys = new Map<string, SourceApplicationDetail>();
  const task = state.task === undefined ? baseCourseTask() : state.task;
  const taskVersion = state.taskVersion ?? TASK_VERSION_ANSWERED;

  const viewRequest = (request: ExternalUpdateRequestDetail) =>
    external ? externalView(request) : request;
  const viewRevision = (contribution: ExternalContributionDetail) =>
    external ? externalView(contribution) : contribution;
  const visible = (request: ExternalUpdateRequestDetail) => !external || request.status !== 'DRAFT';
  const findRequest = (id: string | undefined) =>
    requests.find((request) => request.id === id && visible(request));
  const findRevision = (id: string | undefined) =>
    Object.values(revisions)
      .flat()
      .find((contribution) => contribution.id === id);
  const replaceRequest = (updated: ExternalUpdateRequestDetail): MockReply => {
    requests.splice(
      requests.findIndex((request) => request.id === updated.id),
      1,
      updated,
    );
    return { body: viewRequest(updated), headers: { ETag: REQUEST_ETAG } };
  };
  const replaceRevision = (updated: ExternalContributionDetail): MockReply => {
    const list = revisions[updated.externalUpdateRequestId] ?? [];
    revisions[updated.externalUpdateRequestId] = list.map((contribution) =>
      contribution.id === updated.id ? updated : contribution,
    );
    return { body: viewRevision(updated), headers: { ETag: REVISION_ETAG } };
  };

  api
    .on('GET', /^\/external-update-requests$/, (request) => {
      const projectId = request.query.get('projectId');
      const statuses = request.query.get('status')?.split(',') ?? null;
      return {
        body: page(
          requests
            .filter(visible)
            .filter((candidate) => projectId === null || candidate.projectId === projectId)
            .filter((candidate) => statuses === null || statuses.includes(candidate.status))
            .map(viewRequest),
          200,
        ),
      };
    })
    .on('GET', REQUEST_PATH, (request) => {
      const found = findRequest(REQUEST_PATH.exec(request.path)?.[1]);
      return found === undefined
        ? problem(404, 'NOT_FOUND')
        : { body: viewRequest(found), headers: { ETag: REQUEST_ETAG } };
    })
    .on('POST', /^\/external-update-requests$/, (request) => {
      const body = request.body as Partial<ExternalUpdateRequestDetail> & {
        instructions: { text: string };
      };
      const created = draftRequest({
        id: NEW_REQUEST_ID,
        projectId: body.projectId ?? PROJECT_ID,
        externalEntityId: body.externalEntityId ?? ENTITY_ID,
        contributionTypeItemId: body.contributionTypeItemId ?? TASK_PROGRESS_TYPE_ID,
        targetId: body.targetId ?? null,
        instructions: { text: body.instructions.text, language: 'EN' },
        responsibleUserId: body.responsibleUserId ?? null,
        reviewerUserId: body.reviewerUserId ?? null,
        dueDate: body.dueDate ?? null,
      });
      requests.unshift(created);
      return { status: 201, body: created, headers: { ETag: REQUEST_ETAG } };
    })
    .on('PUT', REQUEST_PATH, (request) => {
      const found = findRequest(REQUEST_PATH.exec(request.path)?.[1]);
      return found === undefined
        ? problem(404, 'NOT_FOUND')
        : replaceRequest({ ...found, ...(request.body as Partial<ExternalUpdateRequestDetail>) });
    })
    .on('DELETE', REQUEST_PATH, (request) => {
      const id = REQUEST_PATH.exec(request.path)?.[1];
      requests.splice(
        requests.findIndex((candidate) => candidate.id === id),
        1,
      );
      return { status: 204 };
    })
    .on('POST', REQUEST_COMMAND, (request) => {
      const [, id, command] = REQUEST_COMMAND.exec(request.path) ?? [];
      const found = findRequest(id);
      if (found === undefined) {
        return problem(404, 'NOT_FOUND');
      }
      const body = request.body as Record<string, unknown> | undefined;
      switch (command) {
        case 'issue':
          return replaceRequest({ ...found, status: 'ISSUED', issuedAt: '2026-10-09T09:00:00Z' });
        case 'cancel':
          return replaceRequest({
            ...found,
            status: 'CANCELLED',
            cancelledAt: '2026-10-09T09:00:00Z',
            cancellationReason: {
              text: (body?.reason as { text: string }).text,
              language: 'EN',
            },
          });
        case 'assign-responder':
          return replaceRequest({ ...found, responsibleUserId: String(body?.responsibleUserId) });
        case 'assign-reviewer':
          return replaceRequest({ ...found, reviewerUserId: String(body?.reviewerUserId) });
        default:
          return problem(404, 'NOT_FOUND');
      }
    })
    .on('GET', /^\/external-contributions$/, (request) => {
      const found = findRequest(request.query.get('externalUpdateRequestId') ?? undefined);
      return {
        body: page(found === undefined ? [] : (revisions[found.id] ?? []).map(viewRevision), 200),
      };
    })
    .on('GET', REVISION_PATH, (request) => {
      const found = findRevision(REVISION_PATH.exec(request.path)?.[1]);
      return found === undefined
        ? problem(404, 'NOT_FOUND')
        : { body: viewRevision(found), headers: { ETag: REVISION_ETAG } };
    })
    .on('POST', /^\/external-contributions$/, (request) => {
      const body = request.body as {
        externalUpdateRequestId: string;
        fields: ContributionFieldValue[];
      };
      const found = findRequest(body.externalUpdateRequestId);
      if (found === undefined) {
        return problem(404, 'NOT_FOUND');
      }
      const created = revision({
        id: NEW_REVISION_ID,
        externalUpdateRequestId: found.id,
        status: 'DRAFT',
        fields: body.fields.map((field) => ({
          ...field,
          language: field.language === null ? null : 'EN',
        })),
        submittedAt: null,
        targetVersion: null,
        targetState: null,
      });
      revisions[found.id] = [created, ...(revisions[found.id] ?? [])];
      replaceRequest({ ...found, status: 'IN_PROGRESS' });
      return { status: 201, body: viewRevision(created), headers: { ETag: REVISION_ETAG } };
    })
    .on('PUT', REVISION_PATH, (request) => {
      const found = findRevision(REVISION_PATH.exec(request.path)?.[1]);
      if (found === undefined) {
        return problem(404, 'NOT_FOUND');
      }
      if (found.status !== 'DRAFT') {
        return problem(409, 'CONTRIBUTION_NOT_EDITABLE');
      }
      const body = request.body as { fields: ContributionFieldValue[] };
      return replaceRevision({
        ...found,
        fields: body.fields.map((field) => ({
          ...field,
          language: field.language === null ? null : 'EN',
        })),
      });
    })
    .on('POST', REVISION_COMMAND, (request) => {
      const [, id, command] = REVISION_COMMAND.exec(request.path) ?? [];
      const found = findRevision(id);
      const owner = requests.find((candidate) => candidate.id === found?.externalUpdateRequestId);
      if (found === undefined || owner === undefined) {
        return problem(404, 'NOT_FOUND');
      }
      const words = request.body as
        | {
            reason?: { text: string } | null;
            internalNote?: { text: string } | null;
          }
        | undefined;
      const decided = {
        reviewedByUserId: OTHER_PERSON_ID,
        reviewedAt: '2026-10-09T10:00:00Z',
        reviewInternalNote:
          words?.internalNote == null ? null : { text: words.internalNote.text, language: 'EN' },
      };
      switch (command) {
        case 'submit':
          replaceRequest({ ...owner, status: 'RESPONDED' });
          return replaceRevision({
            ...found,
            status: 'SUBMITTED',
            submittedAt: '2026-10-09T09:30:00Z',
            targetVersion: taskVersion,
            targetState: 'IN_PROGRESS',
          });
        case 'start-review':
          return replaceRevision({
            ...found,
            status: 'UNDER_REVIEW',
            reviewStartedAt: '2026-10-09T09:45:00Z',
          });
        case 'accept':
          return replaceRevision({
            ...found,
            ...decided,
            status:
              owner.applicationMode === 'REFERENCE_ONLY'
                ? 'APPLIED'
                : 'ACCEPTED_PENDING_APPLICATION',
          });
        case 'return': {
          const next = {
            ...found,
            id: NEW_REVISION_ID,
            revisionNo: found.revisionNo + 1,
            previousRevisionId: found.id,
            status: 'DRAFT' as const,
            submittedAt: null,
          };
          revisions[owner.id] = [next, ...(revisions[owner.id] ?? [])];
          replaceRequest({ ...owner, status: 'IN_PROGRESS' });
          return replaceRevision({
            ...found,
            ...decided,
            status: 'RETURNED',
            reviewReason: { text: words?.reason?.text ?? '', language: 'EN' },
          });
        }
        case 'reject':
          replaceRequest({ ...owner, status: 'CLOSED', closedAt: '2026-10-09T10:00:00Z' });
          return replaceRevision({
            ...found,
            ...decided,
            status: 'REJECTED',
            reviewReason: { text: words?.reason?.text ?? '', language: 'EN' },
          });
        default:
          return problem(404, 'NOT_FOUND');
      }
    })
    .on('GET', /^\/source-applications$/, (request) =>
      external
        ? problem(404, 'NOT_FOUND')
        : { body: page(attempts[request.query.get('externalContributionId') ?? ''] ?? [], 200) },
    )
    .on('POST', /^\/source-applications$/, (request) => {
      const key = request.headers.get('Idempotency-Key') ?? '';
      const replayed = keys.get(key);
      if (replayed !== undefined) {
        return { status: 201, body: replayed, headers: { 'Idempotent-Replayed': 'true' } };
      }
      const { externalContributionId } = request.body as { externalContributionId: string };
      const found = findRevision(externalContributionId);
      if (found === undefined) {
        return problem(404, 'NOT_FOUND');
      }
      const earlier = attempts[found.id] ?? [];
      const latest = earlier[0];
      if (latest?.status === 'CONFLICT' && latest.revalidatedAt === null) {
        return problem(409, 'SOURCE_APPLICATION_CONFLICT');
      }
      const outcome = state.applyOutcome ?? 'APPLIED';
      const made = attempt({
        id: `a1300000-0000-4000-8000-0000000003${String(earlier.length + 10)}`,
        externalContributionId: found.id,
        attemptNo: earlier.length + 1,
        status: outcome,
        expectedTargetRevisionNo:
          latest?.revalidatedTargetRevisionNo ?? found.targetVersion ?? null,
        actualTargetRevisionNo: taskVersion,
        failureCode:
          outcome === 'FAILED' ? (state.applyFailure ?? 'SOURCE_RECORD_STATE_INVALID') : null,
      });
      attempts[found.id] = [made, ...earlier];
      keys.set(key, made);
      const terminal =
        state.applyFailure === 'SOURCE_RECORD_TERMINAL' ||
        state.applyFailure === 'SOURCE_RECORD_NOT_FOUND';
      if (outcome === 'APPLIED' || (outcome === 'FAILED' && terminal)) {
        replaceRevision({
          ...found,
          status: outcome === 'APPLIED' ? 'APPLIED' : 'APPLICATION_FAILED',
        });
      }
      return { status: 201, body: made };
    })
    .on('POST', /^\/source-applications\/([^/]+)\/revalidate$/, (request) => {
      const id = /^\/source-applications\/([^/]+)\/revalidate$/.exec(request.path)?.[1];
      for (const [revisionId, list] of Object.entries(attempts)) {
        const index = list.findIndex((candidate) => candidate.id === id);
        const found = list[index];
        if (found !== undefined) {
          const revalidated = {
            ...found,
            revalidatedAt: '2026-10-09T11:00:00Z',
            revalidatedByUserId: OTHER_PERSON_ID,
            revalidatedTargetRevisionNo: found.actualTargetRevisionNo,
          };
          attempts[revisionId] = list.map((candidate, at) =>
            at === index ? revalidated : candidate,
          );
          return { body: revalidated };
        }
      }
      return problem(404, 'NOT_FOUND');
    })
    .on('GET', /^\/project-tasks\/[^/]+$/, () =>
      task === null
        ? problem(403, 'PERMISSION_DENIED')
        : { body: task, headers: { ETag: `"${String(taskVersion)}"` } },
    )
    .on('GET', /^\/project-tasks$/, () => ({ body: page(task === null ? [] : [task], 200) }));
  if (state.contributionTypes === false) {
    return api;
  }
  api
    .on('GET', /^\/master-data-catalogues$/, {
      body: [{ id: CONTRIBUTION_TYPE_CATALOGUE_ID, code: 'CONTRIBUTION_TYPE' }],
    })
    .on('GET', /^\/master-data-items$/, (request) => ({
      body: page(
        request.query.get('catalogueId') === CONTRIBUTION_TYPE_CATALOGUE_ID
          ? [
              contributionType(TASK_PROGRESS_TYPE_ID, 'TASK_PROGRESS', 'Task progress'),
              contributionType(
                PROJECT_INFORMATION_TYPE_ID,
                'PROJECT_INFORMATION',
                'Project information',
              ),
            ]
          : [],
        200,
      ),
    }));
  return api;
}

function contributionType(id: string, code: string, en: string) {
  return {
    id,
    catalogueId: CONTRIBUTION_TYPE_CATALOGUE_ID,
    code,
    label: { en, ar: en },
    parentItemId: null,
    sortOrder: 1,
    lifecycleState: 'PUBLISHED',
    isSystem: false,
  };
}
