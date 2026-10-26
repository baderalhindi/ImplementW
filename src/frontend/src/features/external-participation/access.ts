import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { assignmentsReaching, isInternal } from '@/features/projects/access.ts';
import { type ProjectDetail } from '@/features/projects/api/types.ts';

import { type ExternalContributionDetail, type ExternalUpdateRequestDetail } from './api/types.ts';
import { isCancellable, isReassignable, isRequestableProjectStatus } from './rules.ts';

// What the WF-13 screens offer a person (TASK-066 D-9). This is navigation, not protection: the API decides every read
// and command again, and a refusal is shown as it comes. AHDA issues, reviews and applies; an entity answers. Gate
// decision (19 Sep 2026): the external UI has no origination and no escalation control — an entity user is never
// offered to create a request, issue, cancel, assign, review or apply, whatever role they hold (R04 is
// employer-neutral, ADR-013). Shipped grants: R04 manages, reviews and applies at OWN; R03 manages and reviews at
// DEPT; R08 reads at ENTITY and answers at ASSIGNED.

/** SCR-161 from a project's workspace: an internal person reached by the project, while it takes requests. */
export function canCreateRequest(
  user: SessionUser,
  project: Pick<ProjectDetail, 'id' | 'status' | 'departmentId' | 'externalEntityId'>,
): boolean {
  return (
    isInternal(user) &&
    assignmentsReaching(user, project).length > 0 &&
    isRequestableProjectStatus(project.status)
  );
}

/** Edit, delete and issue a DRAFT: AHDA's (EXTERNAL_REQUEST_MANAGE, refused to an external user). */
export function canManageDraft(user: SessionUser, request: ExternalUpdateRequestDetail): boolean {
  return isInternal(user) && request.status === 'DRAFT';
}

export function canCancelRequest(user: SessionUser, request: ExternalUpdateRequestDetail): boolean {
  return isInternal(user) && isCancellable(request.status);
}

/** Replace the responder or the reviewer of an issued request that is not final (WF-13 §9.4). */
export function canReassign(user: SessionUser, request: ExternalUpdateRequestDetail): boolean {
  return isInternal(user) && isReassignable(request.status);
}

/** The named responder, an entity user, starts the answer to an ISSUED request or continues its draft. */
export function isResponder(user: SessionUser, request: ExternalUpdateRequestDetail): boolean {
  return !isInternal(user) && request.responsibleUserId === user.id;
}

/** Start the review, then accept, return or reject: the request's assigned AHDA reviewer only (TASK-066 D-11). */
export function isReviewer(user: SessionUser, request: ExternalUpdateRequestDetail): boolean {
  return isInternal(user) && request.reviewerUserId === user.id;
}

export function canStartReview(
  user: SessionUser,
  request: ExternalUpdateRequestDetail,
  revision: ExternalContributionDetail,
): boolean {
  return isReviewer(user, request) && revision.status === 'SUBMITTED';
}

export function canDecide(
  user: SessionUser,
  request: ExternalUpdateRequestDetail,
  revision: ExternalContributionDetail,
): boolean {
  return isReviewer(user, request) && revision.status === 'UNDER_REVIEW';
}

/**
 * Apply, revalidate and apply again: AHDA's (EXTERNAL_CONTRIBUTION_APPLY, R04 at OWN, internal). The project's
 * Project Manager is not readable on the shipped grants (TASK-061 F-1), so it is offered to internal users; the API
 * refuses the rest.
 */
export function canApply(user: SessionUser): boolean {
  return isInternal(user);
}
