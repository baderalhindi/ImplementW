import { apiRequest } from '@/shared/api/httpClient.ts';
import { readAllPages } from '@/shared/api/paging.ts';

import {
  type ConcernAssessCommand,
  type ConcernAssignCommand,
  type ConcernCreateRequest,
  type ConcernDetail,
  type ConcernEscalationCreateRequest,
  type ConcernEscalationDetail,
  type ConcernRequest,
  type ConcernResolutionCommand,
  type ConcernType,
} from './types.ts';

// WF-07's register (`/management-concerns`) and escalations (`/concern-escalations`), TASK-057 management-concern.md
// §4. A collection of a project the caller may not see is an empty page. Every POST and PUT carries an
// Idempotency-Key; only an escalation replays a repeated key (R-37, TASK-057 D-6), so only MOD-039 passes its own
// and resends it on a retry. A retried raise would raise a second concern (TASK-057 F-7): it is never retried here.

/** The concern commands that carry no body. */
export type ConcernCommand = 'start' | 'review' | 'close';

export const concernsApi = {
  /** A project's concerns of one type, most recently changed first (the API's fixed order). */
  concerns: (projectId: string, concernType: ConcernType | null, signal?: AbortSignal) =>
    readAllPages<ConcernDetail>(
      '/management-concerns',
      concernType === null ? { projectId } : { projectId, concernType },
      signal,
    ),
  /** One concern with the ETag its PUT and commands send back. */
  concern: (id: string, signal?: AbortSignal) =>
    apiRequest<ConcernDetail>(`/management-concerns/${id}`, { signal }),
  /** MOD-036 and MOD-038: raised OPEN. */
  raise: (request: ConcernCreateRequest) =>
    apiRequest<ConcernDetail>('/management-concerns', { method: 'POST', body: request }),
  /** MOD-037: the concern's own fields as a whole, priority among them; never its severity. */
  update: (id: string, request: ConcernRequest, etag: string | null) =>
    apiRequest<ConcernDetail>(`/management-concerns/${id}`, {
      method: 'PUT',
      body: request,
      ifMatch: etag,
    }),
  /** The impacts replaced; the server recomputes the severity and pins the version in force. */
  assess: (id: string, command: ConcernAssessCommand, etag: string | null) =>
    apiRequest<ConcernDetail>(`/management-concerns/${id}/assess`, {
      method: 'POST',
      body: command,
      ifMatch: etag,
    }),
  assign: (id: string, command: ConcernAssignCommand, etag: string | null) =>
    apiRequest<ConcernDetail>(`/management-concerns/${id}/assign`, {
      method: 'POST',
      body: command,
      ifMatch: etag,
    }),
  /** IN_PROGRESS → PENDING_VALIDATION: a WF-11 run validates it. */
  submitResolution: (id: string, command: ConcernResolutionCommand, etag: string | null) =>
    apiRequest<ConcernDetail>(`/management-concerns/${id}/submit-resolution`, {
      method: 'POST',
      body: command,
      ifMatch: etag,
    }),
  command: (id: string, command: ConcernCommand, etag: string | null) =>
    apiRequest<ConcernDetail>(`/management-concerns/${id}/${command}`, {
      method: 'POST',
      body: {},
      ifMatch: etag,
    }),
  /** A concern's escalations, newest first. */
  escalations: (concernId: string, signal?: AbortSignal) =>
    readAllPages<ConcernEscalationDetail>(
      '/concern-escalations',
      { managementConcernId: concernId },
      signal,
    ),
  escalation: async (id: string, signal?: AbortSignal) =>
    (await apiRequest<ConcernEscalationDetail>(`/concern-escalations/${id}`, { signal })).data,
  /**
   * MOD-039. `idempotencyKey` is the dialog's, the same on every attempt with the same reason: a retry after a lost
   * answer finds the escalation already raised (201, Idempotent-Replayed) and nothing is notified twice.
   */
  escalate: async (request: ConcernEscalationCreateRequest, idempotencyKey: string) =>
    (
      await apiRequest<ConcernEscalationDetail>('/concern-escalations', {
        method: 'POST',
        body: request,
        idempotencyKey,
      })
    ).data,
  /** OPEN → RESOLVED with the management direction, by the role it is addressed to. */
  resolveEscalation: async (id: string, command: ConcernResolutionCommand) =>
    (
      await apiRequest<ConcernEscalationDetail>(`/concern-escalations/${id}/resolve`, {
        method: 'POST',
        body: command,
      })
    ).data,
  /** OPEN → WITHDRAWN, by its escalator. */
  withdrawEscalation: async (id: string) =>
    (
      await apiRequest<ConcernEscalationDetail>(`/concern-escalations/${id}/withdraw`, {
        method: 'POST',
        body: {},
      })
    ).data,
};
