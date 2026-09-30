// The WF-11 representations (TASK-035, approval-framework.md §3) as the API serialises them: enums in
// SNAKE_CASE_UPPER, ids as strings, instants as ISO 8601.

export type ApprovalInstanceStatus = 'PENDING' | 'APPROVED' | 'REJECTED' | 'RETURNED' | 'WITHDRAWN';

/** DELEGATED and EXPIRED are in the ERD's value set but never produced (TASK-035 F-5); they are still labelled. */
export type ApprovalTaskStatus =
  | 'PENDING'
  | 'APPROVED'
  | 'REJECTED'
  | 'RETURNED'
  | 'DELEGATED'
  | 'ESCALATED'
  | 'CANCELLED'
  | 'EXPIRED';

export type ApprovalDelegationStatus = 'ACTIVE' | 'REVOKED' | 'EXPIRED';

/** What is being approved, as its source module names it (M-8). Approval never reads the source's data. */
export interface ApprovalSubject {
  module: string;
  type: string;
  id: string;
  revisionNo: number;
}

/**
 * Free text with the language it was entered in. The API writes the language as `EN`/`AR` (TASK-035 F-12) and reads
 * it as `en`/`ar`.
 */
export interface NarrativeText {
  text: string;
  language: string;
}

export interface NarrativeTextRequest {
  text: string;
  language: 'ar' | 'en';
}

export interface Page<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface ApprovalInstanceSummary {
  id: string;
  subject: ApprovalSubject;
  routingKey: string;
  requestedByUserId: string;
  requestedAt: string;
  status: ApprovalInstanceStatus;
  completedAt: string | null;
}

/** SCR-100: a task the caller may decide now. `onBehalfOfUserId` names the delegator when the authority is delegated. */
export interface ApprovalInboxItem {
  taskId: string;
  sequenceNo: number;
  assignedRoleId: string;
  dueAt: string | null;
  onBehalfOfUserId: string | null;
  instance: ApprovalInstanceSummary;
}

/**
 * `assignedUserId` is whose authority decided and `actingUserId` who acted; they differ only under
 * `approvalDelegationId`. A later stage's task has no `dueAt` until its stage is reached.
 */
export interface ApprovalTaskDetail {
  id: string;
  sequenceNo: number;
  assignedRoleId: string;
  assignedUserId: string | null;
  actingUserId: string | null;
  approvalDelegationId: string | null;
  status: ApprovalTaskStatus;
  dueAt: string | null;
  decidedAt: string | null;
  decisionReason: NarrativeText | null;
  escalatedToTaskId: string | null;
  createdAt: string;
}

/** SCR-115: a run with every task, in stage order, including escalated and cancelled ones. */
export interface ApprovalInstanceDetail {
  id: string;
  subject: ApprovalSubject;
  routingKey: string;
  authorityConfigurationVersionId: string;
  scopeProjectId: string | null;
  scopeDepartmentId: string | null;
  requestedByUserId: string;
  requestedAt: string;
  status: ApprovalInstanceStatus;
  completedAt: string | null;
  outcomeDeliveredAt: string | null;
  previousInstanceId: string | null;
  tasks: ApprovalTaskDetail[];
}

/** A null `routingKey` covers every routing key. */
export interface ApprovalDelegationDetail {
  id: string;
  delegatorUserId: string;
  delegateUserId: string;
  routingKey: string | null;
  validFrom: string;
  validTo: string;
  status: ApprovalDelegationStatus;
  revokedAt: string | null;
}

export interface ApprovalDelegationList {
  given: ApprovalDelegationDetail[];
  received: ApprovalDelegationDetail[];
}

export interface ApprovalDelegationCreateRequest {
  delegateUserId: string;
  routingKey: string | null;
  /** Omitted: from now. */
  validFrom: string | null;
  validTo: string;
}

export interface ApprovalInstanceQuery {
  status?: ApprovalInstanceStatus | undefined;
  page?: number;
  pageSize?: number;
}

/** MOD-040 approve, MOD-041 reject, MOD-042 return. */
export type ApprovalDecision = 'approve' | 'reject' | 'return';
