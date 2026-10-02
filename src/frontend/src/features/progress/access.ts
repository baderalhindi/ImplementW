import { assignmentsReaching, isInternal } from '@/features/projects/access.ts';
import { type ProjectDetail } from '@/features/projects/api/types.ts';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';

import { type ProgressSubmissionDetail } from './api/types.ts';

// What SCR-048 offers a person. This is navigation, not protection: every command is decided again by the API
// (TASK-044 D-9, D-10), and a refusal is shown as it comes.

/**
 * PROGRESS_SUBMIT ships to R04 at OWN, and a project's owner anchor is its Project Manager (TASK-044 D-10, ADR-013):
 * the project's own Project Manager reports its progress, on an ACTIVE project only (D-5).
 */
export function canReport(user: SessionUser, project: ProjectDetail): boolean {
  return project.status === 'ACTIVE' && project.projectManagerUserId === user.id;
}

/**
 * Review is AHDA's gate (ADR-013): never offered to an external user, nor to the person who submitted the revision,
 * since no one publishes their own progress (TASK-044 D-9).
 */
export function canReview(
  user: SessionUser,
  project: ProjectDetail,
  submission: ProgressSubmissionDetail,
): boolean {
  return (
    (submission.status === 'SUBMITTED' || submission.status === 'UNDER_REVIEW') &&
    isInternal(user) &&
    assignmentsReaching(user, project).length > 0 &&
    submission.submittedByUserId !== user.id
  );
}
