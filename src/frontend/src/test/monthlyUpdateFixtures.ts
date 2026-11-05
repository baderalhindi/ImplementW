import {
  type MyUpdateRequest,
  type MyUpdateRequestPage,
  type UpdateRequestBatchDetail,
  type UpdateRequestBatchRow,
} from '@/features/external-participation/api/monthlyUpdateTypes.ts';

import { type MockApi } from './mockApi.ts';
import { ENTITY_ID, OTHER_PERSON_ID, PROJECT_ID, REVIEWER_ID } from './projectFixtures.ts';

// Test data only. Three projects waiting on one responder, a batch of three, with invented names and fixed ids.

export const REQUEST_A = 'e2000000-0000-4000-8000-000000000001';
export const REQUEST_B = 'e2000000-0000-4000-8000-000000000002';
export const REQUEST_C = 'e2000000-0000-4000-8000-000000000003';
export const CONTRIBUTION_ID = 'c2000000-0000-4000-8000-000000000001';
export const BATCH_ID = 'b2000000-0000-4000-8000-000000000001';

export function myRequest(overrides: Partial<MyUpdateRequest> = {}): MyUpdateRequest {
  return {
    requestId: REQUEST_A,
    requestVersion: 11,
    projectId: PROJECT_ID,
    formalProjectId: 'PRJ-2026-0042',
    legacyReference: null,
    projectTitle: { text: 'Coastal road upgrade', language: 'EN' },
    dueDate: '2026-11-10',
    overdue: false,
    status: 'ISSUED',
    instructions: { text: 'Report the month as of its last day.', language: 'EN' },
    contributionId: null,
    contributionVersion: null,
    contributionStatus: null,
    returnReason: null,
    previouslyReturned: false,
    plannedProgressPercent: 50,
    prefill: { actualProgressPercent: '40', currentObstacle: 'Permit delay' },
    prefillLanguages: { currentObstacle: 'AR' },
    ...overrides,
  };
}

export function threeRequests(): MyUpdateRequest[] {
  return [
    myRequest(),
    myRequest({
      requestId: REQUEST_B,
      projectId: 'p2',
      projectTitle: { text: 'Harbour dredging', language: 'EN' },
    }),
    myRequest({
      requestId: REQUEST_C,
      projectId: 'p3',
      projectTitle: { text: 'Airport lighting', language: 'EN' },
    }),
  ];
}

export function myUpdatePage(items: MyUpdateRequest[]): MyUpdateRequestPage {
  return {
    items,
    page: 1,
    pageSize: 200,
    totalCount: items.length,
    overdueCount: items.filter((item) => item.overdue).length,
    earliestDueDate: items[0]?.dueDate ?? null,
  };
}

/** The responder's side, over a list that a submit shortens, so what is read after it shows the effect. */
export function withMyUpdates(api: MockApi, initial: MyUpdateRequest[]): MockApi {
  let items = [...initial];
  const contributions = new Map<string, string>();
  return api
    .on('GET', /^\/my-update-requests$/, () => ({ body: myUpdatePage(items) }))
    .on('POST', /^\/my-update-requests\/[^/]+\/mark-opened$/, { status: 204 })
    .on('POST', /^\/external-contributions$/, (request) => {
      const { externalUpdateRequestId } = request.body as { externalUpdateRequestId: string };
      contributions.set(CONTRIBUTION_ID, externalUpdateRequestId);
      return {
        status: 201,
        body: { id: CONTRIBUTION_ID, externalUpdateRequestId, status: 'DRAFT' },
        headers: { ETag: '"701"' },
      };
    })
    .on('PUT', /^\/external-contributions\/[^/]+$/, () => ({
      body: { id: CONTRIBUTION_ID, status: 'DRAFT' },
      headers: { ETag: '"702"' },
    }))
    .on('POST', /^\/external-contributions\/[^/]+\/submit$/, () => {
      const answered = contributions.get(CONTRIBUTION_ID);
      items = items.filter((item) => item.requestId !== answered);
      return { body: { id: CONTRIBUTION_ID, status: 'SUBMITTED' }, headers: { ETag: '"703"' } };
    });
}

function batchRow(overrides: Partial<UpdateRequestBatchRow>): UpdateRequestBatchRow {
  return {
    requestId: REQUEST_A,
    projectId: PROJECT_ID,
    formalProjectId: 'PRJ-2026-0042',
    legacyReference: null,
    projectTitle: { text: 'Coastal road upgrade', language: 'EN' },
    externalEntityId: ENTITY_ID,
    responsibleUserId: OTHER_PERSON_ID,
    state: 'SENT',
    overdue: false,
    requestVersion: 11,
    ...overrides,
  };
}

export function batchDetail(
  overrides: Partial<UpdateRequestBatchDetail> = {},
): UpdateRequestBatchDetail {
  return {
    batch: {
      id: BATCH_ID,
      seriesId: BATCH_ID,
      dueDate: '2026-11-10',
      repeatMonthly: true,
      repeatDay: 1,
      nextRunOn: '2026-12-01',
      withdrawnAt: null,
      reviewerUserId: REVIEWER_ID,
      createdBy: REVIEWER_ID,
      createdAt: '2026-11-01T08:00:00Z',
      projectCount: 3,
      entityCount: 1,
      openCount: 2,
    },
    instructions: { text: 'Report the month.', language: 'AR' },
    rows: [
      batchRow({ state: 'SENT', overdue: true }),
      batchRow({
        requestId: REQUEST_B,
        projectId: 'p2',
        projectTitle: { text: 'Harbour dredging', language: 'EN' },
        state: 'DRAFT',
      }),
      batchRow({
        requestId: REQUEST_C,
        projectId: 'p3',
        projectTitle: { text: 'Airport lighting', language: 'EN' },
        state: 'ACCEPTED',
      }),
    ],
    ...overrides,
  };
}
