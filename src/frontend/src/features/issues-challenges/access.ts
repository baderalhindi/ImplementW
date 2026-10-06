import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { assignmentsReaching, isInternal } from '@/features/projects/access.ts';
import { type ProjectSummary } from '@/features/projects/api/types.ts';

import { type ConcernDetail, type ConcernEscalationDetail } from './api/types.ts';
import { EDITABLE_STATUSES, ESCALATABLE_STATUSES } from './concernRules.ts';

// What the issue, challenge and escalation screens offer a person. This is navigation, not protection: every command
// is decided again by the API, and a refusal is shown as it comes. As shipped (TASK-057 D-8): the Project Manager (R04,
// OWN) raises, manages and escalates; the delivering entity (R08, ENTITY) raises and sees; the Department Manager (R03,
// DEPT) sees and resolves escalations addressed to their role. Management and escalation are refused to an external
// user whatever they hold (ADR-013).

type ProjectFacts = Pick<
  ProjectSummary,
  'id' | 'status' | 'projectManagerUserId' | 'departmentId' | 'externalEntityId'
>;

/** A project a concern can be raised on (else 422 CONCERN_PROJECT_NOT_ELIGIBLE). */
function raisesConcerns(project: ProjectFacts): boolean {
  return (
    project.status === 'APPROVED_PLANNED' ||
    project.status === 'ACTIVE' ||
    project.status === 'SUSPENDED'
  );
}

/** A project whose concerns can still change or be escalated: a COMPLETED one's too. */
function changesConcerns(project: ProjectFacts): boolean {
  return raisesConcerns(project) || project.status === 'COMPLETED';
}

function manages(user: SessionUser, project: ProjectFacts): boolean {
  return project.projectManagerUserId === user.id;
}

/** MOD-036 Create Issue and MOD-038 Create Challenge: the Project Manager, or the entity the project reaches. */
export function canRaiseConcerns(user: SessionUser, project: ProjectFacts): boolean {
  return (
    raisesConcerns(project) &&
    (manages(user, project) || (!isInternal(user) && assignmentsReaching(user, project).length > 0))
  );
}

/** CONCERN_MANAGE: the internal Project Manager, on a concern that is not closed. */
function managesConcern(
  user: SessionUser,
  project: ProjectFacts,
  concern: Pick<ConcernDetail, 'status'>,
): boolean {
  return (
    changesConcerns(project) &&
    isInternal(user) &&
    manages(user, project) &&
    concern.status !== 'CLOSED'
  );
}

/**
 * MOD-037 Edit Issue (priority among its fields), the impact assessment and the assignment: until the resolution goes to
 * validation (else 409 CONCERN_NOT_EDITABLE). Severity is never edited: it follows the assessment.
 */
export function canEditConcern(
  user: SessionUser,
  project: ProjectFacts,
  concern: Pick<ConcernDetail, 'status'>,
): boolean {
  return managesConcern(user, project, concern) && EDITABLE_STATUSES.includes(concern.status);
}

/** ASSIGNED → IN_PROGRESS. */
export function canStartConcern(
  user: SessionUser,
  project: ProjectFacts,
  concern: Pick<ConcernDetail, 'status'>,
): boolean {
  return managesConcern(user, project, concern) && concern.status === 'ASSIGNED';
}

/** IN_PROGRESS → PENDING_VALIDATION, through WF-11. */
export function canSubmitResolution(
  user: SessionUser,
  project: ProjectFacts,
  concern: Pick<ConcernDetail, 'status'>,
): boolean {
  return managesConcern(user, project, concern) && concern.status === 'IN_PROGRESS';
}

/** A review at the governance profile's cadence (ADR-015): until resolved. */
export function canReviewConcern(
  user: SessionUser,
  project: ProjectFacts,
  concern: Pick<ConcernDetail, 'status'>,
): boolean {
  return managesConcern(user, project, concern) && ESCALATABLE_STATUSES.includes(concern.status);
}

/** RESOLVED → CLOSED, once no escalation is OPEN (else 409 CONCERN_ESCALATION_OPEN). */
export function canCloseConcern(
  user: SessionUser,
  project: ProjectFacts,
  concern: Pick<ConcernDetail, 'status' | 'openEscalation'>,
): boolean {
  return (
    managesConcern(user, project, concern) &&
    concern.status === 'RESOLVED' &&
    concern.openEscalation === null
  );
}

/** MOD-039 Escalate Item: one OPEN escalation at a time, until the concern is resolved. */
export function canEscalateConcern(
  user: SessionUser,
  project: ProjectFacts,
  concern: Pick<ConcernDetail, 'status' | 'openEscalation'>,
): boolean {
  return (
    managesConcern(user, project, concern) &&
    ESCALATABLE_STATUSES.includes(concern.status) &&
    concern.openEscalation === null
  );
}

/**
 * Resolving an escalation is for whoever holds CONCERN_ESCALATION_RESOLVE through the role it is addressed to (D-8).
 * The session names its roles by code and the escalation its role by id; `roleCode` is that id's code when the roles
 * can be read (ROLE_VIEW), else null, and then any internal person but the escalator is offered it and the API decides.
 */
export function canResolveEscalation(
  user: SessionUser,
  escalation: Pick<ConcernEscalationDetail, 'status' | 'escalatedByUserId'>,
  roleCode: string | null,
): boolean {
  return (
    escalation.status === 'OPEN' &&
    isInternal(user) &&
    (roleCode === null
      ? escalation.escalatedByUserId !== user.id
      : user.roleAssignments.some((assignment) => assignment.roleCode === roleCode))
  );
}

/** Its escalator alone withdraws it. */
export function canWithdrawEscalation(
  user: SessionUser,
  escalation: Pick<ConcernEscalationDetail, 'status' | 'escalatedByUserId'>,
): boolean {
  return escalation.status === 'OPEN' && escalation.escalatedByUserId === user.id;
}
