import {
  type ApprovalInboxItem,
  type ApprovalInstanceSummary,
} from '@/features/approvals/api/types.ts';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { assignmentsReaching, isClosed, isInternal } from '@/features/projects/access.ts';
import { type ProjectDetail } from '@/features/projects/api/types.ts';

import { type CloseoutCaseStatus, type SuspensionRequestStatus } from './api/types.ts';

// What WF-09's requests and WF-10's cases share: one state machine shape (TASK-062 D-3, TASK-063 D-3), review through
// WF-11, and an activation that is a separate event from the approval (TASK-062 D-5, TASK-063 D-4). This is navigation,
// not protection: every command is decided again by the API, and a refusal is shown as it comes. The project may be
// unreadable to the person (no PROJECT_VIEW), so `project` is null when it could not be read.

/** The two state machines have the same eight states. */
export type GovernedStatus = SuspensionRequestStatus & CloseoutCaseStatus;

/** In lifecycle order, the final ones last. */
export const GOVERNED_STATUSES: GovernedStatus[] = [
  'DRAFT',
  'SUBMITTED',
  'UNDER_REVIEW',
  'RETURNED',
  'APPROVED',
  'EFFECTED',
  'REJECTED',
  'WITHDRAWN',
];

export interface GovernedRecord {
  status: GovernedStatus;
  revisionNo: number;
  requestedByUserId: string;
}

export type ProjectFacts = Pick<
  ProjectDetail,
  'id' | 'status' | 'departmentId' | 'externalEntityId' | 'projectManagerUserId'
>;

/** Fields change while DRAFT or RETURNED only (409 …_NOT_EDITABLE after). */
export function isEditable(status: GovernedStatus): boolean {
  return status === 'DRAFT' || status === 'RETURNED';
}

/** Open: neither rejected, withdrawn nor effected. One open record of each kind per project. */
export function isOpen(status: GovernedStatus): boolean {
  return status !== 'REJECTED' && status !== 'WITHDRAWN' && status !== 'EFFECTED';
}

/** A run exists once a review has started: from UNDER_REVIEW on, or for any revision after the first. */
export function hasReviewRuns(record: Pick<GovernedRecord, 'status' | 'revisionNo'>): boolean {
  return (record.status !== 'DRAFT' && record.status !== 'SUBMITTED') || record.revisionNo > 1;
}

// ---------------------------------------------------------------------------------------------------------------------
// Approval and activation, shown apart

/** WF-11's side: where the review stands. An effected record stays Approved. */
export type ApprovalState =
  | 'NOT_SUBMITTED'
  | 'AWAITING_REVIEW'
  | 'IN_REVIEW'
  | 'RETURNED'
  | 'APPROVED'
  | 'REJECTED'
  | 'WITHDRAWN';

/** The source module's side: whether the approved change has been applied to the project's lifecycle. */
export type ActivationState = 'NOT_APPLICABLE' | 'PENDING' | 'EFFECTED';

const APPROVAL_STATES: Record<GovernedStatus, ApprovalState> = {
  DRAFT: 'NOT_SUBMITTED',
  SUBMITTED: 'AWAITING_REVIEW',
  UNDER_REVIEW: 'IN_REVIEW',
  RETURNED: 'RETURNED',
  APPROVED: 'APPROVED',
  EFFECTED: 'APPROVED',
  REJECTED: 'REJECTED',
  WITHDRAWN: 'WITHDRAWN',
};

export function approvalStateOf(status: GovernedStatus): ApprovalState {
  return APPROVAL_STATES[status];
}

export function activationStateOf(status: GovernedStatus): ActivationState {
  return status === 'APPROVED' ? 'PENDING' : status === 'EFFECTED' ? 'EFFECTED' : 'NOT_APPLICABLE';
}

// ---------------------------------------------------------------------------------------------------------------------
// Who is offered what. Shipped grants: the project's Project Manager (R04 at OWN, internal or entity, ADR-013) raises;
// the department's manager (R03 at DEPT) reviews and waives; WF-11 decides; the worker activates. A CLOSED project
// takes no write at all (409 PROJECT_CLOSED, TASK-063 D-8), so nothing is offered on one.

function writable(project: ProjectFacts | null): boolean {
  return project === null || !isClosed(project.status);
}

/** Whoever raised it, or the project's Project Manager: …_RAISE is held at OWN. */
export function isRaiser(
  user: SessionUser,
  record: GovernedRecord,
  project: ProjectFacts | null,
): boolean {
  return record.requestedByUserId === user.id || project?.projectManagerUserId === user.id;
}

/** The project's Project Manager raises new records of it. */
export function managesProject(user: SessionUser, project: ProjectFacts): boolean {
  return !isClosed(project.status) && project.projectManagerUserId === user.id;
}

/**
 * AHDA's gates (ADR-013): an internal person who did not raise it and whose assignments reach the project — or anyone
 * internal but the raiser when the project cannot be read; the API decides.
 */
export function isAhdaGatekeeper(
  user: SessionUser,
  record: GovernedRecord,
  project: ProjectFacts | null,
): boolean {
  return (
    writable(project) &&
    isInternal(user) &&
    !isRaiser(user, record, project) &&
    (project === null || assignmentsReaching(user, project).length > 0)
  );
}

export function canEditRecord(
  user: SessionUser,
  record: GovernedRecord,
  project: ProjectFacts | null,
): boolean {
  return writable(project) && isEditable(record.status) && isRaiser(user, record, project);
}

/** HARD_DRAFT: only a DRAFT never submitted. */
export function canDeleteRecord(
  user: SessionUser,
  record: GovernedRecord,
  project: ProjectFacts | null,
): boolean {
  return writable(project) && record.status === 'DRAFT' && isRaiser(user, record, project);
}

/**
 * Before the review starts the requester withdraws the record itself (WF-09 from SUBMITTED or RETURNED, WF-10 from a
 * DRAFT too); under review, the WF-11 run (MOD-044).
 */
export function canWithdrawRecord(
  user: SessionUser,
  record: GovernedRecord,
  project: ProjectFacts | null,
  withdrawable: readonly GovernedStatus[],
): boolean {
  return (
    writable(project) && withdrawable.includes(record.status) && isRaiser(user, record, project)
  );
}

export function canStartReview(
  user: SessionUser,
  record: GovernedRecord,
  project: ProjectFacts | null,
): boolean {
  return record.status === 'SUBMITTED' && isAhdaGatekeeper(user, record, project);
}

/**
 * Activation is the worker's (WF-09 §14, WF-10 §13 "System/service"); AHDA may activate an approved record at once or
 * retry one the pass left APPROVED (…_ACTIVATE, internal; shipped to no role, so the API usually refuses it).
 */
export function canActivate(
  user: SessionUser,
  record: GovernedRecord,
  project: ProjectFacts | null,
): boolean {
  return record.status === 'APPROVED' && isAhdaGatekeeper(user, record, project);
}

/**
 * MOD-040–042 on the person's inbox task deciding this revision: the inbox lists only what WF-11 lets them decide now;
 * an external user and the run's requester (the originator) are never offered it.
 */
export function canDecide(
  user: SessionUser,
  record: GovernedRecord,
  task: ApprovalInboxItem | null,
): boolean {
  return (
    record.status === 'UNDER_REVIEW' &&
    task !== null &&
    isInternal(user) &&
    task.instance.requestedByUserId !== user.id
  );
}

/** MOD-044 by the run's requester while it is PENDING: withdrawing the run withdraws the record. */
export function canWithdrawReview(
  user: SessionUser,
  record: GovernedRecord,
  run: ApprovalInstanceSummary | null,
): boolean {
  return (
    record.status === 'UNDER_REVIEW' &&
    run !== null &&
    run.status === 'PENDING' &&
    run.requestedByUserId === user.id
  );
}
