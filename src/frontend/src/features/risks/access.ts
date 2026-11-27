import { type Permissions } from '@/features/identity-access/permissions/permissions.ts';
import { type PermissionCode } from '@/features/identity-access/permissions/permissionCodes.ts';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { holdsOnProject, isInternal } from '@/features/projects/access.ts';
import { type ProjectSummary } from '@/features/projects/api/types.ts';

import { type RiskDetail } from './api/types.ts';
import { isOpen } from './riskRules.ts';

// What the risk screens offer a person. This is navigation, not protection: every command is decided again by the API,
// and a refusal is shown as it comes. Each action is offered to whoever holds the permission its endpoint requires
// (RisksController) at a scope reaching the project: RISK_MANAGE registers and manages (R02 ALL, R03 DEPT, R04 OWN),
// RISK_ASSESS assesses, RISK_ACCEPT accepts and revokes an acceptance, RISK_REOPEN reopens (R02 ALL, R03 DEPT;
// shipped-permission-matrix.md §3). The API refuses assessment and acceptance to an external user (ADR-013).

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

/** AHDA's decisions (ADR-013): internal, and holding `code` on the project. */
function decides(
  user: SessionUser,
  permissions: Permissions,
  project: ProjectFacts,
  code: PermissionCode,
): boolean {
  return isInternal(user) && holdsOnProject(permissions, user, project, code);
}

/** MOD-030 Create Risk. */
export function canRegisterRisks(
  user: SessionUser,
  permissions: Permissions,
  project: ProjectFacts,
): boolean {
  return registersRisks(project) && holdsOnProject(permissions, user, project, 'RISK_MANAGE');
}

/** MOD-031, MOD-033, MOD-034, MOD-035, start-treatment and monitor: an open risk of a project the person manages risks on. */
export function canManageRisk(
  user: SessionUser,
  permissions: Permissions,
  project: ProjectFacts,
  risk: Pick<RiskDetail, 'status'>,
): boolean {
  return (
    changesRisks(project) &&
    holdsOnProject(permissions, user, project, 'RISK_MANAGE') &&
    isOpen(risk)
  );
}

/** MOD-032 Risk Assessment. */
export function canAssessRisk(
  user: SessionUser,
  permissions: Permissions,
  project: ProjectFacts,
  risk: Pick<RiskDetail, 'status'>,
): boolean {
  return (
    changesRisks(project) && decides(user, permissions, project, 'RISK_ASSESS') && isOpen(risk)
  );
}

/** An assessed risk with no ACTIVE acceptance (else 409 INVALID_TRANSITION or RISK_ACCEPTANCE_ACTIVE). */
export function canAcceptRisk(
  user: SessionUser,
  permissions: Permissions,
  project: ProjectFacts,
  risk: Pick<RiskDetail, 'status' | 'acceptedUntil'>,
): boolean {
  return (
    changesRisks(project) &&
    decides(user, permissions, project, 'RISK_ACCEPT') &&
    isOpen(risk) &&
    risk.status !== 'IDENTIFIED' &&
    risk.acceptedUntil === null
  );
}

export function canRevokeAcceptance(
  user: SessionUser,
  permissions: Permissions,
  project: ProjectFacts,
  risk: Pick<RiskDetail, 'status' | 'acceptedUntil'>,
): boolean {
  return (
    changesRisks(project) &&
    decides(user, permissions, project, 'RISK_ACCEPT') &&
    isOpen(risk) &&
    risk.acceptedUntil !== null
  );
}

/** Controlled reopen: a permission of its own, never RISK_MANAGE's (TASK-055 D-8). */
export function canReopenRisk(
  user: SessionUser,
  permissions: Permissions,
  project: ProjectFacts,
  risk: Pick<RiskDetail, 'status'>,
): boolean {
  return (
    changesRisks(project) &&
    decides(user, permissions, project, 'RISK_REOPEN') &&
    risk.status === 'CLOSED'
  );
}

/** ASSESSED or MONITORING → TREATMENT, with a live action and no ACTIVE acceptance. */
export function canStartTreatment(
  user: SessionUser,
  permissions: Permissions,
  project: ProjectFacts,
  risk: Pick<RiskDetail, 'status' | 'acceptedUntil'>,
  hasLiveAction: boolean,
): boolean {
  return (
    canManageRisk(user, permissions, project, risk) &&
    (risk.status === 'ASSESSED' || risk.status === 'MONITORING') &&
    risk.acceptedUntil === null &&
    hasLiveAction
  );
}

/** ASSESSED or TREATMENT → MONITORING. */
export function canMonitorRisk(
  user: SessionUser,
  permissions: Permissions,
  project: ProjectFacts,
  risk: Pick<RiskDetail, 'status'>,
): boolean {
  return (
    canManageRisk(user, permissions, project, risk) &&
    (risk.status === 'ASSESSED' || risk.status === 'TREATMENT')
  );
}
