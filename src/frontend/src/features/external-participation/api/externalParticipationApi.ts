import { type NarrativeTextRequest } from '@/features/projects/api/types.ts';
import { apiRequest } from '@/shared/api/httpClient.ts';
import { readAllPages } from '@/shared/api/paging.ts';

import {
  type ContributionDecision,
  type ContributionFieldRequest,
  type ExternalContributionDetail,
  type ExternalUpdateRequestCreateRequest,
  type ExternalUpdateRequestDetail,
  type ExternalUpdateRequestRequest,
  type ExternalUpdateRequestStatus,
  type SourceApplicationDetail,
} from './types.ts';

// WF-13 (TASK-066 external-participation.md §4): `/external-update-requests`, `/external-contributions`,
// `/source-applications`, 22 operations. A record outside the caller's scope is 404, as a nonexistent id (R-47); a
// list holds only what the caller may see. Every POST and PUT carries an Idempotency-Key. Only `apply` replays a
// repeated key (TASK-066 F-11), so only `apply` is sent with a key the caller keeps for a retry.

export interface ExternalUpdateRequestQuery {
  projectId?: string | undefined;
  externalEntityId?: string | undefined;
  status?: readonly ExternalUpdateRequestStatus[] | undefined;
  responsibleUserId?: string | undefined;
  reviewerUserId?: string | undefined;
}

/** The request commands with no body beyond an optional If-Match. */
type BareRequestCommand = 'issue';

/** The review's steps: start-review has no body; the three decisions take only words, never a field value (D-5). */
export type ReviewDecision = 'accept' | 'return' | 'reject';

export const externalRequestsApi = {
  /** Every request the caller may see that matches, most recently changed first (the API's order). */
  list: (query: ExternalUpdateRequestQuery, signal?: AbortSignal) =>
    readAllPages<ExternalUpdateRequestDetail>(
      '/external-update-requests',
      {
        projectId: query.projectId,
        externalEntityId: query.externalEntityId,
        // A set, comma-separated (R-31).
        status: query.status?.join(','),
        responsibleUserId: query.responsibleUserId,
        reviewerUserId: query.reviewerUserId,
      },
      signal,
    ),
  /** One request with the ETag its PUT, DELETE and commands send back. */
  get: (id: string, signal?: AbortSignal) =>
    apiRequest<ExternalUpdateRequestDetail>(`/external-update-requests/${id}`, { signal }),
  /** SCR-161: a DRAFT, invisible to the entity until issued. */
  create: (request: ExternalUpdateRequestCreateRequest) =>
    apiRequest<ExternalUpdateRequestDetail>('/external-update-requests', {
      method: 'POST',
      body: request,
    }),
  /** A DRAFT's fields as a whole; If-Match is required. */
  update: (id: string, request: ExternalUpdateRequestRequest, etag: string | null) =>
    apiRequest<ExternalUpdateRequestDetail>(`/external-update-requests/${id}`, {
      method: 'PUT',
      body: request,
      ifMatch: etag,
    }),
  /** HARD_DRAFT: only a DRAFT. */
  remove: (id: string, etag: string | null) =>
    apiRequest<undefined>(`/external-update-requests/${id}`, { method: 'DELETE', ifMatch: etag }),
  command: (id: string, command: BareRequestCommand, etag: string | null) =>
    apiRequest<ExternalUpdateRequestDetail>(`/external-update-requests/${id}/${command}`, {
      method: 'POST',
      ifMatch: etag,
    }),
  /** ISSUED or IN_PROGRESS → CANCELLED, with the reason; never once the answer is with its reviewer. */
  cancel: (id: string, reason: NarrativeTextRequest, etag: string | null) =>
    apiRequest<ExternalUpdateRequestDetail>(`/external-update-requests/${id}/cancel`, {
      method: 'POST',
      body: { reason },
      ifMatch: etag,
    }),
  assignResponder: (id: string, responsibleUserId: string, etag: string | null) =>
    apiRequest<ExternalUpdateRequestDetail>(`/external-update-requests/${id}/assign-responder`, {
      method: 'POST',
      body: { responsibleUserId },
      ifMatch: etag,
    }),
  assignReviewer: (id: string, reviewerUserId: string, etag: string | null) =>
    apiRequest<ExternalUpdateRequestDetail>(`/external-update-requests/${id}/assign-reviewer`, {
      method: 'POST',
      body: { reviewerUserId },
      ifMatch: etag,
    }),
};

export const contributionsApi = {
  /** A request's revisions, newest first. */
  list: (externalUpdateRequestId: string, signal?: AbortSignal) =>
    readAllPages<ExternalContributionDetail>(
      '/external-contributions',
      { externalUpdateRequestId },
      signal,
    ),
  get: (id: string, signal?: AbortSignal) =>
    apiRequest<ExternalContributionDetail>(`/external-contributions/${id}`, { signal }),
  /** Revision 1 of the answer to an ISSUED request, as its responder: a DRAFT. */
  create: (externalUpdateRequestId: string, fields: ContributionFieldRequest[]) =>
    apiRequest<ExternalContributionDetail>('/external-contributions', {
      method: 'POST',
      body: { externalUpdateRequestId, fields },
    }),
  /** A DRAFT revision's values as a whole; If-Match is required. A submitted revision is never edited (409). */
  update: (id: string, fields: ContributionFieldRequest[], etag: string | null) =>
    apiRequest<ExternalContributionDetail>(`/external-contributions/${id}`, {
      method: 'PUT',
      body: { fields },
      ifMatch: etag,
    }),
  submit: (id: string, etag: string | null) =>
    apiRequest<ExternalContributionDetail>(`/external-contributions/${id}/submit`, {
      method: 'POST',
      ifMatch: etag,
    }),
  startReview: (id: string, etag: string | null) =>
    apiRequest<ExternalContributionDetail>(`/external-contributions/${id}/start-review`, {
      method: 'POST',
      ifMatch: etag,
    }),
  /** Accept takes the internal note only; return and reject also the reason the entity reads. */
  decide: (
    id: string,
    decision: ReviewDecision,
    words: ContributionDecision,
    etag: string | null,
  ) =>
    apiRequest<ExternalContributionDetail>(`/external-contributions/${id}/${decision}`, {
      method: 'POST',
      body: decision === 'accept' ? { internalNote: words.internalNote } : words,
      ifMatch: etag,
    }),
};

export const sourceApplicationsApi = {
  /** A revision's attempts, newest first; AHDA's only. */
  list: (externalContributionId: string, signal?: AbortSignal) =>
    readAllPages<SourceApplicationDetail>(
      '/source-applications',
      { externalContributionId },
      signal,
    ),
  /**
   * One attempt, recorded and answered 201 whatever its outcome (APPLIED, CONFLICT, FAILED). The caller keeps
   * `idempotencyKey` for the attempt it means: sent again after a lost answer, the API replays the same attempt
   * (`Idempotent-Replayed: true`) and applies nothing twice.
   */
  apply: (externalContributionId: string, idempotencyKey: string) =>
    apiRequest<SourceApplicationDetail>('/source-applications', {
      method: 'POST',
      body: { externalContributionId },
      idempotencyKey,
    }),
  /** Confirms the version a CONFLICT found, which the next attempt then expects. */
  revalidate: (id: string) =>
    apiRequest<SourceApplicationDetail>(`/source-applications/${id}/revalidate`, {
      method: 'POST',
    }),
};
