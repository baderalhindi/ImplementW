import {
  type ApprovalInboxItem,
  type ApprovalInstanceSummary,
  type ApprovalSubject,
} from '@/features/approvals/api/types.ts';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { assignmentsReaching, isInternal } from '@/features/projects/access.ts';
import { type ProjectDetail } from '@/features/projects/api/types.ts';

import { type ChangeRequestDetail } from './api/types.ts';
import {
  allAuthorizationsApplied,
  isEditable,
  isRaisableProjectStatus,
} from './changeRequestRules.ts';

// What the WF-08 screens offer a person. This is navigation, not protection: every command is decided again by the API
// (TASK-060 D-11), and a refusal is shown as it comes. Shipped grants: the project's Project Manager (R04 at OWN, internal
// or entity, ADR-013) raises, and, if internal, implements; the department's manager (R03 at DEPT) reviews; WF-11 decides.
// The project may be unreadable to a reviewer (no PROJECT_VIEW), so `project` is null when it could not be read.

type ProjectFacts = Pick<
  ProjectDetail,
  'id' | 'status' | 'departmentId' | 'externalEntityId' | 'projectManagerUserId'
>;

/** SCR-106 is offered to the project's Project Manager while the project is APPROVED_PLANNED, ACTIVE or SUSPENDED. */
export function canRaiseChangeRequests(user: SessionUser, project: ProjectFacts): boolean {
  return project.projectManagerUserId === user.id && isRaisableProjectStatus(project.status);
}

/** Whoever raised it, or the project's Project Manager: CHANGE_REQUEST_RAISE is held at OWN. */
function isRaiser(
  user: SessionUser,
  request: ChangeRequestDetail,
  project: ProjectFacts | null,
): boolean {
  return request.requestedByUserId === user.id || project?.projectManagerUserId === user.id;
}

/** Correct and (re)submit through SCR-106, where the classification is shown before submission. */
export function canEditChangeRequest(
  user: SessionUser,
  request: ChangeRequestDetail,
  project: ProjectFacts | null,
): boolean {
  return isEditable(request.status) && isRaiser(user, request, project);
}

/** HARD_DRAFT: only a DRAFT never submitted. */
export function canDeleteChangeRequest(
  user: SessionUser,
  request: ChangeRequestDetail,
  project: ProjectFacts | null,
): boolean {
  return request.status === 'DRAFT' && isRaiser(user, request, project);
}

/** Before the review starts, the requester withdraws the request itself; under review, the WF-11 run (MOD-044). */
export function canWithdrawChangeRequest(
  user: SessionUser,
  request: ChangeRequestDetail,
  project: ProjectFacts | null,
): boolean {
  return (
    (request.status === 'SUBMITTED' || request.status === 'RETURNED') &&
    isRaiser(user, request, project)
  );
}

/**
 * Starting the review is AHDA's (ADR-013): offered to an internal person who did not raise it and whose assignments
 * reach the project — or anyone internal but the raiser when the project cannot be read; the API decides.
 */
export function canStartReview(
  user: SessionUser,
  request: ChangeRequestDetail,
  project: ProjectFacts | null,
): boolean {
  return (
    request.status === 'SUBMITTED' &&
    isInternal(user) &&
    !isRaiser(user, request, project) &&
    (project === null || assignmentsReaching(user, project).length > 0)
  );
}

/** The implementation is AHDA's and coordinated by the project's internal Project Manager (WF-08 §3). */
function canImplement(
  user: SessionUser,
  request: ChangeRequestDetail,
  project: ProjectFacts | null,
): boolean {
  return isInternal(user) && isRaiser(user, request, project);
}

export function canStartImplementation(
  user: SessionUser,
  request: ChangeRequestDetail,
  project: ProjectFacts | null,
): boolean {
  return request.status === 'APPROVED' && canImplement(user, request, project);
}

/** Offered once every authorisation is applied by its module; before, the screen says what it waits for. */
export function canMarkImplemented(
  user: SessionUser,
  request: ChangeRequestDetail,
  project: ProjectFacts | null,
): boolean {
  return (
    request.status === 'IMPLEMENTATION' &&
    allAuthorizationsApplied(request) &&
    canImplement(user, request, project)
  );
}

export function canCloseChangeRequest(
  user: SessionUser,
  request: ChangeRequestDetail,
  project: ProjectFacts | null,
): boolean {
  return request.status === 'IMPLEMENTED' && canImplement(user, request, project);
}

/** The subject TASK-060 starts a request revision's WF-11 run with. */
export function changeRequestSubject(
  request: Pick<ChangeRequestDetail, 'id' | 'revisionNo'>,
): ApprovalSubject {
  return {
    module: 'ChangeRequest',
    type: 'ChangeRequest',
    id: request.id,
    revisionNo: request.revisionNo,
  };
}

/**
 * MOD-040–042 on the person's inbox task deciding this revision: the inbox lists only what WF-11 lets them decide now;
 * an external user and the run's requester (the originator, TASK-060 D-5) are never offered it.
 */
export function canDecideChangeRequest(
  user: SessionUser,
  request: ChangeRequestDetail,
  task: ApprovalInboxItem | null,
): boolean {
  return (
    request.status === 'UNDER_REVIEW' &&
    task !== null &&
    isInternal(user) &&
    task.instance.requestedByUserId !== user.id
  );
}

/** MOD-044 by the run's requester while it is PENDING: withdrawing the run withdraws the request (TASK-060 D-5). */
export function canWithdrawReview(
  user: SessionUser,
  request: ChangeRequestDetail,
  run: ApprovalInstanceSummary | null,
): boolean {
  return (
    request.status === 'UNDER_REVIEW' &&
    run !== null &&
    run.status === 'PENDING' &&
    run.requestedByUserId === user.id
  );
}
