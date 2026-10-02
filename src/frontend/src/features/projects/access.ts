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

export type WorkspaceTabKey = 'overview' | 'registration' | 'location' | 'reviews' | 'documents';

export interface WorkspaceTab {
  key: WorkspaceTabKey;
  /** Relative to /projects/:projectId; '' is the overview. */
  path: string;
  audience: TabAudience;
}

/**
 * SCR-040 Overview, SCR-041 Registration, SCR-035 Location, SCR-042 Review History, SCR-043 Documents (D-4).
 * Review history is AHDA's: a review's requester is the AHDA reviewer (TASK-041 F-11) and a run carries no entity
 * anchor (TASK-035 F-8), so an external user can never read one.
 */
export const WORKSPACE_TABS: WorkspaceTab[] = [
  { key: 'overview', path: '', audience: 'anyone' },
  { key: 'registration', path: 'registration', audience: 'anyone' },
  { key: 'location', path: 'location', audience: 'anyone' },
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
