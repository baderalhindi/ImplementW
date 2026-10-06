import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { assignmentsReaching, isInternal } from '@/features/projects/access.ts';
import { type ProjectSummary } from '@/features/projects/api/types.ts';

import { type RiskDetail } from './api/types.ts';
import { isOpen } from './riskRules.ts';

// What the risk screens offer a person. This is navigation, not protection: every command is decided again by the API,
// and a refusal is shown as it comes. RISK_VIEW and RISK_MANAGE ship to R04 at OWN, and a project's owner anchor is its
// Project Manager, internal or entity (ADR-013). RISK_ASSESS, RISK_ACCEPT and RISK_REOPEN ship to no role yet (TASK-055
// F-2); the API refuses the first two to an external user, so they are offered to an internal person the project's
// scope reaches, and the API decides.

type ProjectFacts = Pick<
  ProjectSummary,
  'id' | 'status' | 'projectManagerUserId' | 'departmentId' | 'externalEntityId'
>;

/** A project a risk can be registered on (else 422 RISK_PROJECT_NOT_ELIGIBLE). */
function registersRisks(project: ProjectFacts): boolean {
  return (
    project.status === 'APPROVED_PLANNED' ||
    project.status === 'ACTIVE' ||
    project.status === 'SUSPENDED'
  );
}

/** A project whose risks can still change: a COMPLETED one's too. */
function changesRisks(project: ProjectFacts): boolean {
  return registersRisks(project) || project.status === 'COMPLETED';
}

function manages(user: SessionUser, project: ProjectFacts): boolean {
  return project.projectManagerUserId === user.id;
}

/** AHDA's authority (ADR-013): internal, and reaching the project. */
function decides(user: SessionUser, project: ProjectFacts): boolean {
  return isInternal(user) && assignmentsReaching(user, project).length > 0;
}

/** MOD-030 Create Risk. */
export function canRegisterRisks(user: SessionUser, project: ProjectFacts): boolean {
  return registersRisks(project) && manages(user, project);
}

/** MOD-031, MOD-033, MOD-034, MOD-035, start-treatment and monitor: an open risk of the Project Manager's project. */
export function canManageRisk(
  user: SessionUser,
  project: ProjectFacts,
  risk: Pick<RiskDetail, 'status'>,
): boolean {
  return changesRisks(project) && manages(user, project) && isOpen(risk);
}

/** MOD-032 Risk Assessment. */
export function canAssessRisk(
  user: SessionUser,
  project: ProjectFacts,
  risk: Pick<RiskDetail, 'status'>,
): boolean {
  return changesRisks(project) && decides(user, project) && isOpen(risk);
}

/** An assessed risk with no ACTIVE acceptance (else 409 INVALID_TRANSITION or RISK_ACCEPTANCE_ACTIVE). */
export function canAcceptRisk(
  user: SessionUser,
  project: ProjectFacts,
  risk: Pick<RiskDetail, 'status' | 'acceptedUntil'>,
): boolean {
  return (
    canAssessRisk(user, project, risk) &&
    risk.status !== 'IDENTIFIED' &&
    risk.acceptedUntil === null
  );
}

export function canRevokeAcceptance(
  user: SessionUser,
  project: ProjectFacts,
  risk: Pick<RiskDetail, 'status' | 'acceptedUntil'>,
): boolean {
  return canAssessRisk(user, project, risk) && risk.acceptedUntil !== null;
}

/** Controlled reopen: a permission of its own, never RISK_MANAGE's (TASK-055 D-8). */
export function canReopenRisk(
  user: SessionUser,
  project: ProjectFacts,
  risk: Pick<RiskDetail, 'status'>,
): boolean {
  return changesRisks(project) && decides(user, project) && risk.status === 'CLOSED';
}

/** ASSESSED or MONITORING → TREATMENT, with a live action and no ACTIVE acceptance. */
export function canStartTreatment(
  user: SessionUser,
  project: ProjectFacts,
  risk: Pick<RiskDetail, 'status' | 'acceptedUntil'>,
  hasLiveAction: boolean,
): boolean {
  return (
    canManageRisk(user, project, risk) &&
    (risk.status === 'ASSESSED' || risk.status === 'MONITORING') &&
    risk.acceptedUntil === null &&
    hasLiveAction
  );
}

/** ASSESSED or TREATMENT → MONITORING. */
export function canMonitorRisk(
  user: SessionUser,
  project: ProjectFacts,
  risk: Pick<RiskDetail, 'status'>,
): boolean {
  return (
    canManageRisk(user, project, risk) &&
    (risk.status === 'ASSESSED' || risk.status === 'TREATMENT')
  );
}
