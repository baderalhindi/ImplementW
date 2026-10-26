import { apiRequest } from '@/shared/api/httpClient.ts';
import { readAllPages } from '@/shared/api/paging.ts';

import {
  type ActiveSuspensionDetail,
  type SuspensionRequestCreateRequest,
  type SuspensionRequestDetail,
  type SuspensionRequestRequest,
} from './types.ts';

// WF-09's requests (`/suspension-requests`) and periods (`/active-suspensions`), TASK-062 suspension.md §4. A collection
// of a project the caller may not see is an empty page. Every POST and PUT carries an Idempotency-Key; a retried create
// is refused by the one-open-request rule (TASK-062 F-9), so nothing here is retried.

/** The lifecycle commands that carry no body; each is one edge of the state machine (TASK-062 §3). */
export type SuspensionCommand = 'submit' | 'withdraw' | 'start-review' | 'activate';

export const suspensionApi = {
  /** A project's suspension and resumption requests, most recently changed first (the API's fixed order). */
  list: (projectId: string, signal?: AbortSignal) =>
    readAllPages<SuspensionRequestDetail>('/suspension-requests', { projectId }, signal),
  /** One request with the ETag its PUT, DELETE and commands send back. */
  get: (id: string, signal?: AbortSignal) =>
    apiRequest<SuspensionRequestDetail>(`/suspension-requests/${id}`, { signal }),
  /** SCR-109: raised DRAFT. */
  create: (request: SuspensionRequestCreateRequest) =>
    apiRequest<SuspensionRequestDetail>('/suspension-requests', { method: 'POST', body: request }),
  /** A DRAFT or RETURNED request's fields as a whole; If-Match is required. */
  update: (id: string, request: SuspensionRequestRequest, etag: string | null) =>
    apiRequest<SuspensionRequestDetail>(`/suspension-requests/${id}`, {
      method: 'PUT',
      body: request,
      ifMatch: etag,
    }),
  /** HARD_DRAFT: a DRAFT never submitted. */
  remove: (id: string, etag: string | null) =>
    apiRequest<undefined>(`/suspension-requests/${id}`, { method: 'DELETE', ifMatch: etag }),
  command: (id: string, command: SuspensionCommand, etag: string | null) =>
    apiRequest<SuspensionRequestDetail>(`/suspension-requests/${id}/${command}`, {
      method: 'POST',
      ifMatch: etag,
    }),
  /** The project's suspension periods across cycles, most recently started first (BR-SUS-039). */
  periods: (projectId: string, signal?: AbortSignal) =>
    readAllPages<ActiveSuspensionDetail>('/active-suspensions', { projectId }, signal),
};
