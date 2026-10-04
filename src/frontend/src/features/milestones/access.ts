import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { type ProjectSummary } from '@/features/projects/api/types.ts';

import { type ProjectMilestoneDetail } from './api/types.ts';

// What the milestone screens offer a person. This is navigation, not protection: every command is decided again by the
// API, and a refusal is shown as it comes. SCHEDULE_EDIT and MILESTONE_SUBMIT ship to R04 at OWN, and a project's owner
// anchor is its Project Manager (TASK-046 D-12, TASK-050 D-10), internal or entity (ADR-013). Acceptance is WF-11's,
// internal only, and has no milestone command (TASK-050 D-7).

type ProjectFacts = Pick<ProjectSummary, 'status' | 'projectManagerUserId'>;

function managedBy(user: SessionUser, project: ProjectFacts) {
  return project.projectManagerUserId === user.id;
}

/** MOD-016 and cancelling: WF-03's side, the project's own Project Manager while it is APPROVED_PLANNED or ACTIVE. */
export function canPlanMilestones(user: SessionUser, project: ProjectFacts): boolean {
  return (
    (project.status === 'APPROVED_PLANNED' || project.status === 'ACTIVE') &&
    managedBy(user, project)
  );
}

/** A PLANNED milestone is edited and cancelled; ACHIEVED and CANCELLED are final (TASK-050 D-11). */
export function canChangeMilestone(
  user: SessionUser,
  project: ProjectFacts,
  milestone: Pick<ProjectMilestoneDetail, 'status'>,
): boolean {
  return canPlanMilestones(user, project) && milestone.status === 'PLANNED';
}

/** Claims (open, edit, evidence, submit, delete a draft): the project's own Project Manager on an ACTIVE project (D-12). */
export function canClaimAchievement(user: SessionUser, project: ProjectFacts): boolean {
  return project.status === 'ACTIVE' && managedBy(user, project);
}
