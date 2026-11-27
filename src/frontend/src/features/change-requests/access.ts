import {
  type ApprovalInboxItem,
  type ApprovalInstanceSummary,
  type ApprovalSubject,
} from '@/features/approvals/api/types.ts';
import { type Permissions } from '@/features/identity-access/permissions/permissions.ts';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { holdsOnProject, isInternal } from '@/features/projects/access.ts';
import { type ProjectDetail } from '@/features/projects/api/types.ts';

import { type ChangeRequestDetail } from './api/types.ts';
import {
  allAuthorizationsApplied,
  isEditable,
  isRaisableProjectStatus,
} from './changeRequestRules.ts';

// What the WF-08 screens offer a person. This is navigation, not protection: every command is decided again by the API
// (TASK-060 D-11), and a refusal is shown as it comes. Shipped grants: CHANGE_REQUEST_RAISE's holders raise (R02 ALL, R03
// DEPT, R04 OWN, internal or entity, ADR-013); CHANGE_REQUEST_IMPLEMENT's internal holders implement (R04 OWN: the project's
// Project Manager); the department's manager (R03 at DEPT) reviews; WF-11 decides.
// The project may be unreadable to a reviewer (no PROJECT_VIEW), so `project` is null when it could not be read.

type ProjectFacts = Pick<
  ProjectDetail,
  'id' | 'status' | 'departmentId' | 'externalEntityId' | 'projectManagerUserId'
>;

/**
 * SCR-106 is offered to a holder of CHANGE_REQUEST_RAISE on the project (R02 ALL, R03 DEPT, R04 OWN) while the project is
 * APPROVED_PLANNED, ACTIVE or SUSPENDED.
 */
export function canRaiseChangeRequests(
  user: SessionUser,
  permissions: Permissions,
  project: ProjectFacts,
): boolean {
  return (
    isRaisableProjectStatus(project.status) &&
    holdsOnProject(permissions, user, project, 'CHANGE_REQUEST_RAISE')
  );
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
 * Starting the review is AHDA's (ADR-013): offered to an internal holder of CHANGE_REQUEST_REVIEW on the project (R02
 * ALL, R03 DEPT) who did not raise it — or on holding it at all when the project cannot be read; the API decides.
 */
export function canStartReview(
  user: SessionUser,
  permissions: Permissions,
  request: ChangeRequestDetail,
  project: ProjectFacts | null,
): boolean {
  return (
    request.status === 'SUBMITTED' &&
    isInternal(user) &&
    !isRaiser(user, request, project) &&
    holdsOnProject(permissions, user, project, 'CHANGE_REQUEST_REVIEW')
  );
}

/**
 * The implementation is AHDA's and coordinated by the project's internal Project Manager (WF-08 §3): offered, as the API
 * decides it, to an internal holder of CHANGE_REQUEST_IMPLEMENT whose scope reaches the project (R04 OWN). Raising the
 * request is not enough: an R02 raiser holds no CHANGE_REQUEST_IMPLEMENT and is refused 403 (QA DEV-D1-02).
 */
function canImplement(
  user: SessionUser,
  permissions: Permissions,
  project: ProjectFacts | null,
): boolean {
  return isInternal(user) && holdsOnProject(permissions, user, project, 'CHANGE_REQUEST_IMPLEMENT');
}

export function canStartImplementation(
  user: SessionUser,
  permissions: Permissions,
  request: ChangeRequestDetail,
  project: ProjectFacts | null,
): boolean {
  return request.status === 'APPROVED' && canImplement(user, permissions, project);
}

/** Offered once every authorisation is applied by its module; before, the screen says what it waits for. */
export function canMarkImplemented(
  user: SessionUser,
  permissions: Permissions,
  request: ChangeRequestDetail,
  project: ProjectFacts | null,
): boolean {
  return (
    request.status === 'IMPLEMENTATION' &&
    allAuthorizationsApplied(request) &&
    canImplement(user, permissions, project)
  );
}

export function canCloseChangeRequest(
  user: SessionUser,
  permissions: Permissions,
  request: ChangeRequestDetail,
  project: ProjectFacts | null,
): boolean {
  return request.status === 'IMPLEMENTED' && canImplement(user, permissions, project);
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
