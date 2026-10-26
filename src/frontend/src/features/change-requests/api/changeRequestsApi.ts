import { type ApprovalInstanceSummary } from '@/features/approvals/api/types.ts';
import { apiRequest } from '@/shared/api/httpClient.ts';
import { readAllPages } from '@/shared/api/paging.ts';

import {
  type ChangeRequestCreateRequest,
  type ChangeRequestDetail,
  type ChangeRequestRequest,
  type MaterialityAssessment,
} from './types.ts';

// WF-08's change requests (`/change-requests`), TASK-060 change-request.md §4. A collection of a project the caller may
// not see is an empty page. Every POST and PUT carries an Idempotency-Key; a retried create would raise a second request
// (TASK-060 F-7), so nothing here is retried.

/** The lifecycle commands that carry no body; each is one edge of the state machine (TASK-060 §3). */
export type ChangeRequestCommand =
  'submit' | 'withdraw' | 'start-review' | 'start-implementation' | 'mark-implemented' | 'close';

export const changeRequestsApi = {
  /** A project's change requests, most recently changed first (the API's fixed order). */
  list: (projectId: string, signal?: AbortSignal) =>
    readAllPages<ChangeRequestDetail>('/change-requests', { projectId }, signal),
  /** One request with the ETag its PUT, DELETE and commands send back. */
  get: (id: string, signal?: AbortSignal) =>
    apiRequest<ChangeRequestDetail>(`/change-requests/${id}`, { signal }),
  /** SCR-106: raised DRAFT. */
  create: (request: ChangeRequestCreateRequest) =>
    apiRequest<ChangeRequestDetail>('/change-requests', { method: 'POST', body: request }),
  /** A DRAFT or RETURNED request's fields as a whole; If-Match is required. */
  update: (id: string, request: ChangeRequestRequest, etag: string | null) =>
    apiRequest<ChangeRequestDetail>(`/change-requests/${id}`, {
      method: 'PUT',
      body: request,
      ifMatch: etag,
    }),
  /** HARD_DRAFT: a DRAFT never submitted. */
  remove: (id: string, etag: string | null) =>
    apiRequest<undefined>(`/change-requests/${id}`, { method: 'DELETE', ifMatch: etag }),
  /**
   * The classification the request would get now, by the rule its review applies: advisory, recorded nowhere
   * (`evaluationId` null). Before the review only (409 CHANGE_REQUEST_EVALUATED after).
   */
  previewMateriality: async (id: string) =>
    (
      await apiRequest<MaterialityAssessment>(`/change-requests/${id}/preview-materiality`, {
        method: 'POST',
      })
    ).data,
  command: (id: string, command: ChangeRequestCommand, etag: string | null) =>
    apiRequest<ChangeRequestDetail>(`/change-requests/${id}/${command}`, {
      method: 'POST',
      ifMatch: etag,
    }),
  /**
   * The WF-11 runs that reviewed the request, newest first, as many as the caller may see (TASK-035 D-10; the subject
   * TASK-060 starts them with).
   */
  approvalRuns: async (id: string, signal?: AbortSignal) =>
    readAllPages<ApprovalInstanceSummary>(
      '/approval-instances',
      { subjectModule: 'ChangeRequest', subjectType: 'ChangeRequest', subjectId: id },
      signal,
    ),
};
