import { canReadActivity, canReadProjectAudit } from '@/features/audit-activity/access.ts';
import { type AuditAccessView } from '@/features/audit-activity/api/types.ts';
import { type PermissionCode } from '@/features/identity-access/permissions/permissionCodes.ts';
import { type Permissions } from '@/features/identity-access/permissions/permissions.ts';
import {
  type SessionRoleAssignment,
  type SessionUser,
} from '@/features/identity-access/session/sessionApi.ts';

import { type ProjectDetail, type ProjectStatus } from './api/types.ts';

// What the workspace offers a person, from the session's role assignments (TASK-030 scope anchors), user type and the
// permissions the API reports they hold (GET /sessions/current/permissions). This is navigation, not protection: every
// read and command is decided again by the API (TASK-041 D-10), and a refusal is shown as it comes. A tab is offered on
// whether an assignment reaches the project, ADR-013's internal/external line, and the permission it reads under.

/**
 * The assignments whose anchors reach the project: one with no anchor reaches every project; a department, entity or
 * project anchor reaches the projects that carry the same value.
 */
export function assignmentsReaching(
  user: SessionUser,
  project: Pick<ProjectDetail, 'id' | 'departmentId' | 'externalEntityId'>,
): SessionRoleAssignment[] {
  return user.roleAssignments.filter(
    (assignment) =>
      (assignment.projectId === null || assignment.projectId === project.id) &&
      (assignment.departmentId === null || assignment.departmentId === project.departmentId) &&
      (assignment.externalEntityId === null ||
        assignment.externalEntityId === project.externalEntityId),
  );
}

/** ADR-013: AHDA keeps every approval and lifecycle gate. An external user is never offered one. */
export function isInternal(user: SessionUser): boolean {
  return user.userType !== 'EXTERNAL';
}

/** The entity an external user registers for (ADR-013), when their assignments name exactly one. */
export function ownEntityId(user: SessionUser): string | null {
  const entities = new Set(
    user.roleAssignments
      .map((assignment) => assignment.externalEntityId)
      .filter((id): id is string => id !== null),
  );
  return !isInternal(user) && entities.size === 1 ? ([...entities][0] ?? null) : null;
}

/**
 * Who sees a workspace tab, beyond seeing the project at all (which the API decided by answering). FG-06's two
 * histories follow audit permissions, which GET /audit-access reports: Activity for ACTIVITY_VIEW, the formal audit for
 * a project audit category (TASK-074).
 */
export type TabAudience = 'anyone' | 'reached' | 'internalReached' | 'activity' | 'projectAudit';

export type WorkspaceTabKey =
  | 'overview'
  | 'registration'
  | 'location'
  | 'progress'
  | 'schedule'
  | 'tasks'
  | 'milestones'
  | 'financials'
  | 'kpis'
  | 'risks'
  | 'issuesChallenges'
  | 'changeRequests'
  | 'suspension'
  | 'closeout'
  | 'externalRequests'
  | 'reviews'
  | 'documents'
  | 'activity'
  | 'audit';

export interface WorkspaceTab {
  key: WorkspaceTabKey;
  /** Relative to /projects/:projectId; '' is the overview. */
  path: string;
  audience: TabAudience;
  /** The permission the tab reads under: offered only to a holder (the 24 Oct 2026 review, M-12). */
  permission?: PermissionCode;
}

/**
 * SCR-040 Overview, SCR-041 Registration, SCR-035 Location, SCR-048 Progress, SCR-060 Schedule, SCR-047 Tasks,
 * SCR-046 Milestones, SCR-049 Financials, SCR-050 KPIs, SCR-080/082 Risks, SCR-083–086 Issues & challenges, SCR-105 Change requests, SCR-108 Suspension, SCR-111 Closeout, SCR-042 Review History, SCR-043 Documents (D-4). Progress is for those an assignment reaches: PROGRESS_VIEW ships to
 * the project's Project Manager and its delivering entity (TASK-044 D-10), and the API answers an empty page to anyone
 * it does not reach. Schedule likewise: SCHEDULE_VIEW ships to the project's Project Manager (TASK-046 D-12); and Tasks:
 * TASK_VIEW ships to the Project Manager, and a task's owner holds a role over the project (TASK-048 D-11, D-12); and
 * Milestones: SCHEDULE_VIEW and MILESTONE_VIEW ship to the Project Manager (TASK-050 D-10); and Financials and KPIs:
 * FINANCIAL_VIEW and KPI_VIEW ship to the delivering entity (TASK-052 D-10, ADR-013); and Risks: RISK_VIEW ships to the
 * Project Manager (TASK-055); and Issues & challenges: CONCERN_VIEW ships to the Project Manager, the delivering entity
 * and the department's manager (TASK-057 D-8); and Change requests: CHANGE_REQUEST_VIEW ships to the Project Manager and
 * the department's manager (TASK-060 D-11); and Suspension and Closeout: SUSPENSION_VIEW and CLOSEOUT_VIEW ship to the
 * Project Manager and the department's manager (TASK-062 D-9, TASK-063 D-10); and External requests: EXTERNAL_REQUEST_VIEW
 * ships to the Project Manager, the department's manager and the entity's users (TASK-066 D-9), each answered only what
 * their scope reaches.
 * Review history is AHDA's: a review's requester is the AHDA reviewer (TASK-041 F-11) and a run carries no entity
 * anchor (TASK-035 F-8), so an external user can never read one.
 * SCR-057 Activity History and SCR-058 Audit History are two tabs, never one (TASK-074 acceptance criterion 1).
 */
export const WORKSPACE_TABS: WorkspaceTab[] = [
  { key: 'overview', path: '', audience: 'anyone' },
  { key: 'registration', path: 'registration', audience: 'anyone' },
  { key: 'location', path: 'location', audience: 'anyone' },
  { key: 'progress', path: 'progress', audience: 'reached', permission: 'PROGRESS_VIEW' },
  { key: 'schedule', path: 'schedule', audience: 'reached', permission: 'SCHEDULE_VIEW' },
  { key: 'tasks', path: 'tasks', audience: 'reached', permission: 'TASK_VIEW' },
  { key: 'milestones', path: 'milestones', audience: 'reached', permission: 'MILESTONE_VIEW' },
  { key: 'financials', path: 'financials', audience: 'reached', permission: 'FINANCIAL_VIEW' },
  { key: 'kpis', path: 'kpis', audience: 'reached', permission: 'KPI_VIEW' },
  { key: 'risks', path: 'risks', audience: 'reached', permission: 'RISK_VIEW' },
  {
    key: 'issuesChallenges',
    path: 'issues-challenges',
    audience: 'reached',
    permission: 'CONCERN_VIEW',
  },
  {
    key: 'changeRequests',
    path: 'change-requests',
    audience: 'reached',
    permission: 'CHANGE_REQUEST_VIEW',
  },
  { key: 'suspension', path: 'suspension', audience: 'reached', permission: 'SUSPENSION_VIEW' },
  { key: 'closeout', path: 'closeout', audience: 'reached', permission: 'CLOSEOUT_VIEW' },
  {
    key: 'externalRequests',
    path: 'external-requests',
    audience: 'reached',
    permission: 'EXTERNAL_REQUEST_VIEW',
  },
  { key: 'reviews', path: 'reviews', audience: 'internalReached', permission: 'APPROVAL_VIEW' },
  { key: 'documents', path: 'documents', audience: 'reached', permission: 'DOCUMENT_VIEW' },
  { key: 'activity', path: 'activity-history', audience: 'activity' },
  { key: 'audit', path: 'audit-history', audience: 'projectAudit' },
];

export interface ProjectAccess {
  internal: boolean;
  /** At least one assignment reaches the project. */
  reached: boolean;
  /** The person who created the project: only they may delete its draft (TASK-041 D-11). */
  creator: boolean;
  /** ACTIVITY_VIEW: SCR-057. */
  activity: boolean;
  /** An explicit project audit permission: SCR-058. R01 alone does not give it. */
  projectAudit: boolean;
  /** What the person holds now: a tab is offered only to a holder of the permission it reads under. */
  permissions: Permissions;
}

/**
 * `audit` is the API's GET /audit-access, or null while it is unknown: no audit tab is offered until it says so;
 * `permissions` what GET /sessions/current/permissions reports.
 */
export function projectAccess(
  user: SessionUser,
  project: ProjectDetail,
  audit: AuditAccessView | null,
  permissions: Permissions,
): ProjectAccess {
  return {
    internal: isInternal(user),
    reached: assignmentsReaching(user, project).length > 0,
    creator: project.createdBy === user.id,
    activity: canReadActivity(audit),
    projectAudit: canReadProjectAudit(audit),
    permissions,
  };
}

export function canSeeTab(tab: WorkspaceTab, access: ProjectAccess): boolean {
  return (
    inAudience(tab.audience, access) &&
    (tab.permission === undefined || access.permissions.can(tab.permission))
  );
}

function inAudience(audience: TabAudience, access: ProjectAccess): boolean {
  switch (audience) {
    case 'anyone':
      return true;
    case 'reached':
      return access.reached;
    case 'internalReached':
      return access.internal && access.reached;
    case 'activity':
      return access.activity;
    case 'projectAudit':
      return access.projectAudit;
  }
}

export function visibleTabs(access: ProjectAccess): WorkspaceTab[] {
  return WORKSPACE_TABS.filter((tab) => canSeeTab(tab, access));
}

/** DRAFT and RETURNED are the registrant's to change (TASK-041 D-12). */
export function isEditable(status: ProjectStatus): boolean {
  return status === 'DRAFT' || status === 'RETURNED';
}

/**
 * CLOSED is terminal and read-only (TASK-063 D-8): every module refuses a write to a closed project's records with
 * 409 PROJECT_CLOSED, so no screen offers one, whatever the state of the record it shows.
 */
export function isClosed(status: ProjectStatus): boolean {
  return status === 'CLOSED';
}

/** MOD-003's commands: one per lifecycle edge a person starts (TASK-041 §3). Submission is MOD-002. */
export type StatusCommand = 'withdraw' | 'startReview' | 'activate';

/**
 * The commands MOD-003 offers from a state. Withdrawing is the registrant's; starting the review and activating are
 * AHDA's gates and are never offered to an external user (ADR-013), though the API refuses them too.
 */
export function statusCommands(status: ProjectStatus, access: ProjectAccess): StatusCommand[] {
  if (!access.reached) {
    return [];
  }
  switch (status) {
    case 'SUBMITTED':
      return access.internal ? ['withdraw', 'startReview'] : ['withdraw'];
    case 'APPROVED_PLANNED':
      return access.internal ? ['activate'] : [];
    default:
      return [];
  }
}
