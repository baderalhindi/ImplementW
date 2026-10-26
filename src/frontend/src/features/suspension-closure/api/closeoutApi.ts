import { apiRequest } from '@/shared/api/httpClient.ts';
import { readAllPages } from '@/shared/api/paging.ts';

import {
  type ClosureCaseCreateRequest,
  type ClosureCaseDetail,
  type ClosureCaseRequest,
  type CompletionCaseCreateRequest,
  type CompletionCaseDetail,
  type CompletionCaseRequest,
  type PostProjectObligationCreateRequest,
  type PostProjectObligationDetail,
  type PostProjectObligationRequest,
  type WaiveCheckCommand,
} from './types.ts';

// WF-10's two kinds of case (`/completion-cases`, `/closure-cases`, the same eleven operations each) and the project's
// post-project obligations, TASK-063 closure.md §4. Nothing here is retried: a retried create is refused by the
// one-open-case rule and a retried activation by the final state (TASK-063 F-9).

/** Stage 1 (completion) or stage 2 (closure): the two resources share every operation. */
export type CloseoutStage = 'completion' | 'closure';

export interface CloseoutCaseOf {
  completion: CompletionCaseDetail;
  closure: ClosureCaseDetail;
}

export interface CloseoutRequestOf {
  completion: CompletionCaseRequest;
  closure: ClosureCaseRequest;
}

export interface CloseoutCreateRequestOf {
  completion: CompletionCaseCreateRequest;
  closure: ClosureCaseCreateRequest;
}

/** The lifecycle commands that carry no body (TASK-063 §3). */
export type CloseoutCommand =
  'evaluate-readiness' | 'submit' | 'withdraw' | 'start-review' | 'activate';

const COLLECTIONS: Record<CloseoutStage, string> = {
  completion: '/completion-cases',
  closure: '/closure-cases',
};

/** The WF-11 subject type each kind of case starts its review with (TASK-063 §4). */
export const SUBJECT_TYPES: Record<CloseoutStage, string> = {
  completion: 'CompletionCase',
  closure: 'ClosureCase',
};

export type ObligationCommand = 'start' | 'satisfy' | 'cancel' | 'waive';

export const closeoutApi = {
  /** A project's cases of one stage, most recently changed first. */
  list: <S extends CloseoutStage>(stage: S, projectId: string, signal?: AbortSignal) =>
    readAllPages<CloseoutCaseOf[S]>(COLLECTIONS[stage], { projectId }, signal),
  /** One case with its latest readiness and roll-up, and the ETag its commands send back. */
  get: <S extends CloseoutStage>(stage: S, id: string, signal?: AbortSignal) =>
    apiRequest<CloseoutCaseOf[S]>(`${COLLECTIONS[stage]}/${id}`, { signal }),
  /** SCR-112: raised DRAFT. */
  create: <S extends CloseoutStage>(stage: S, request: CloseoutCreateRequestOf[S]) =>
    apiRequest<CloseoutCaseOf[S]>(COLLECTIONS[stage], { method: 'POST', body: request }),
  /** A DRAFT or RETURNED case's fields as a whole; If-Match is required. */
  update: <S extends CloseoutStage>(
    stage: S,
    id: string,
    request: CloseoutRequestOf[S],
    etag: string | null,
  ) =>
    apiRequest<CloseoutCaseOf[S]>(`${COLLECTIONS[stage]}/${id}`, {
      method: 'PUT',
      body: request,
      ifMatch: etag,
    }),
  /** HARD_DRAFT: a DRAFT with no readiness records or obligations (409 CLOSURE_CASE_IN_USE otherwise). */
  remove: (stage: CloseoutStage, id: string, etag: string | null) =>
    apiRequest<undefined>(`${COLLECTIONS[stage]}/${id}`, { method: 'DELETE', ifMatch: etag }),
  command: <S extends CloseoutStage>(
    stage: S,
    id: string,
    command: CloseoutCommand,
    etag: string | null,
  ) =>
    apiRequest<CloseoutCaseOf[S]>(`${COLLECTIONS[stage]}/${id}/${command}`, {
      method: 'POST',
      ifMatch: etag,
    }),
  /** A criterion that failed in the latest evaluation, waived with a reason (CLOSEOUT_WAIVE, internal). */
  waiveCheck: <S extends CloseoutStage>(
    stage: S,
    id: string,
    command: WaiveCheckCommand,
    etag: string | null,
  ) =>
    apiRequest<CloseoutCaseOf[S]>(`${COLLECTIONS[stage]}/${id}/waive-check`, {
      method: 'POST',
      body: command,
      ifMatch: etag,
    }),
};

export const obligationsApi = {
  /** The project's obligations, whichever case they were recorded against. */
  list: (projectId: string, signal?: AbortSignal) =>
    readAllPages<PostProjectObligationDetail>('/post-project-obligations', { projectId }, signal),
  get: (id: string, signal?: AbortSignal) =>
    apiRequest<PostProjectObligationDetail>(`/post-project-obligations/${id}`, { signal }),
  create: (request: PostProjectObligationCreateRequest) =>
    apiRequest<PostProjectObligationDetail>('/post-project-obligations', {
      method: 'POST',
      body: request,
    }),
  update: (id: string, request: PostProjectObligationRequest, etag: string | null) =>
    apiRequest<PostProjectObligationDetail>(`/post-project-obligations/${id}`, {
      method: 'PUT',
      body: request,
      ifMatch: etag,
    }),
  command: (id: string, command: ObligationCommand, etag: string | null) =>
    apiRequest<PostProjectObligationDetail>(`/post-project-obligations/${id}/${command}`, {
      method: 'POST',
      ifMatch: etag,
    }),
};
