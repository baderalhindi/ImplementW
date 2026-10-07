import {
  type SessionRoleAssignment,
  type SessionUser,
} from '@/features/identity-access/session/sessionApi.ts';

import { type ProjectDetail, type ProjectStatus } from './api/types.ts';

// What the workspace offers a person, from the session's role assignments (TASK-030 scope anchors) and user type.
// This is navigation, not protection: every read and command is decided again by the API (TASK-041 D-10), and a
// refusal is shown as it comes. The session carries each assignment's anchors, not its permissions, so a tab or
// command is offered on whether an assignment reaches the project and on ADR-013's internal/external line.

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

/** Who sees a workspace tab, beyond seeing the project at all (which the API decided by answering). */
export type TabAudience = 'anyone' | 'reached' | 'internalReached';

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
  | 'reviews'
  | 'documents';

export interface WorkspaceTab {
  key: WorkspaceTabKey;
  /** Relative to /projects/:projectId; '' is the overview. */
  path: string;
  audience: TabAudience;
}

/**
 * SCR-040 Overview, SCR-041 Registration, SCR-035 Location, SCR-048 Progress, SCR-060 Schedule, SCR-047 Tasks,
 * SCR-046 Milestones, SCR-049 Financials, SCR-050 KPIs, SCR-080/082 Risks, SCR-083–086 Issues & challenges, SCR-105 Change requests, SCR-042 Review History, SCR-043 Documents (D-4). Progress is for those an assignment reaches: PROGRESS_VIEW ships to
 * the project's Project Manager and its delivering entity (TASK-044 D-10), and the API answers an empty page to anyone
 * it does not reach. Schedule likewise: SCHEDULE_VIEW ships to the project's Project Manager (TASK-046 D-12); and Tasks:
 * TASK_VIEW ships to the Project Manager, and a task's owner holds a role over the project (TASK-048 D-11, D-12); and
 * Milestones: SCHEDULE_VIEW and MILESTONE_VIEW ship to the Project Manager (TASK-050 D-10); and Financials and KPIs:
 * FINANCIAL_VIEW and KPI_VIEW ship to the delivering entity (TASK-052 D-10, ADR-013); and Risks: RISK_VIEW ships to the
 * Project Manager (TASK-055); and Issues & challenges: CONCERN_VIEW ships to the Project Manager, the delivering entity
 * and the department's manager (TASK-057 D-8); and Change requests: CHANGE_REQUEST_VIEW ships to the Project Manager and
 * the department's manager (TASK-060 D-11).
 * Review history is AHDA's: a review's requester is the AHDA reviewer (TASK-041 F-11) and a run carries no entity
 * anchor (TASK-035 F-8), so an external user can never read one.
 */
export const WORKSPACE_TABS: WorkspaceTab[] = [
  { key: 'overview', path: '', audience: 'anyone' },
  { key: 'registration', path: 'registration', audience: 'anyone' },
  { key: 'location', path: 'location', audience: 'anyone' },
  { key: 'progress', path: 'progress', audience: 'reached' },
  { key: 'schedule', path: 'schedule', audience: 'reached' },
  { key: 'tasks', path: 'tasks', audience: 'reached' },
  { key: 'milestones', path: 'milestones', audience: 'reached' },
  { key: 'financials', path: 'financials', audience: 'reached' },
  { key: 'kpis', path: 'kpis', audience: 'reached' },
  { key: 'risks', path: 'risks', audience: 'reached' },
  { key: 'issuesChallenges', path: 'issues-challenges', audience: 'reached' },
  { key: 'changeRequests', path: 'change-requests', audience: 'reached' },
  { key: 'reviews', path: 'reviews', audience: 'internalReached' },
  { key: 'documents', path: 'documents', audience: 'reached' },
];

export interface ProjectAccess {
  internal: boolean;
  /** At least one assignment reaches the project. */
  reached: boolean;
  /** The person who created the project: only they may delete its draft (TASK-041 D-11). */
  creator: boolean;
}

export function projectAccess(user: SessionUser, project: ProjectDetail): ProjectAccess {
  return {
    internal: isInternal(user),
    reached: assignmentsReaching(user, project).length > 0,
    creator: project.createdBy === user.id,
  };
}

export function canSeeTab(tab: WorkspaceTab, access: ProjectAccess): boolean {
  switch (tab.audience) {
    case 'anyone':
      return true;
    case 'reached':
      return access.reached;
    case 'internalReached':
      return access.internal && access.reached;
  }
}

export function visibleTabs(access: ProjectAccess): WorkspaceTab[] {
  return WORKSPACE_TABS.filter((tab) => canSeeTab(tab, access));
}

/** DRAFT and RETURNED are the registrant's to change (TASK-041 D-12). */
export function isEditable(status: ProjectStatus): boolean {
  return status === 'DRAFT' || status === 'RETURNED';
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
