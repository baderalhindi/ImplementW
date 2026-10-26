import { apiRequest, type ApiResponse } from '@/shared/api/httpClient.ts';

import {
  type ApprovalDecision,
  type ApprovalDelegationCreateRequest,
  type ApprovalDelegationDetail,
  type ApprovalDelegationList,
  type ApprovalInboxItem,
  type ApprovalInstanceDetail,
  type ApprovalInstanceQuery,
  type ApprovalInstanceSummary,
  type NarrativeTextRequest,
  type Page,
} from './types.ts';

// WF-11 (TASK-035 approval-framework.md §3). Every POST is a sensitive write: the client sends an Idempotency-Key.

async function data<T>(request: Promise<ApiResponse<T>>): Promise<T> {
  return (await request).data;
}

export const approvalTasksApi = {
  /** SCR-100: only the tasks the caller may decide now, by their own authority or a delegator's; earliest due first. */
  listInbox: (page: number, pageSize: number, signal?: AbortSignal) =>
    data(
      apiRequest<Page<ApprovalInboxItem>>('/approval-tasks', { query: { page, pageSize }, signal }),
    ),
  /** MOD-040–042. `reason` is required to reject or return, and optional to approve. */
  decide: (taskId: string, decision: ApprovalDecision, reason: NarrativeTextRequest | null) =>
    data(
      apiRequest<ApprovalInstanceDetail>(`/approval-tasks/${taskId}/${decision}`, {
        method: 'POST',
        body: reason === null ? {} : { reason },
      }),
    ),
  /** MOD-045, by the requester, once the task is overdue. */
  escalate: (taskId: string, reason: NarrativeTextRequest | null) =>
    data(
      apiRequest<ApprovalInstanceDetail>(`/approval-tasks/${taskId}/escalate`, {
        method: 'POST',
        body: reason === null ? {} : { reason },
      }),
    ),
};

export const approvalInstancesApi = {
  /** SCR-101 My Requests, newest first. */
  listMine: (query: ApprovalInstanceQuery, signal?: AbortSignal) =>
    data(
      apiRequest<Page<ApprovalInstanceSummary>>('/approval-instances', {
        query: { requestedBy: 'me', ...query },
        signal,
      }),
    ),
  /** SCR-115. */
  get: (id: string, signal?: AbortSignal) =>
    data(apiRequest<ApprovalInstanceDetail>(`/approval-instances/${id}`, { signal })),
  /** MOD-044, by the requester, while PENDING. */
  withdraw: (id: string) =>
    data(
      apiRequest<ApprovalInstanceDetail>(`/approval-instances/${id}/withdraw`, { method: 'POST' }),
    ),
};

export const approvalDelegationsApi = {
  /** SCR-114: unpaged (TASK-035 F-11). */
  list: (signal?: AbortSignal) =>
    data(apiRequest<ApprovalDelegationList>('/approval-delegations', { signal })),
  /** MOD-043. */
  create: (request: ApprovalDelegationCreateRequest) =>
    data(
      apiRequest<ApprovalDelegationDetail>('/approval-delegations', {
        method: 'POST',
        body: request,
      }),
    ),
  revoke: (id: string) =>
    data(
      apiRequest<ApprovalDelegationDetail>(`/approval-delegations/${id}/revoke`, {
        method: 'POST',
      }),
    ),
};
