import { isInternal } from '@/features/projects/access.ts';
import { type ProjectDetail } from '@/features/projects/api/types.ts';
import { type ApprovalInboxItem, type ApprovalSubject } from '@/features/approvals/api/types.ts';
import { decidesSubject } from '@/features/approvals/sourceReview.ts';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';

import { type ProjectBaselineDetail } from './api/types.ts';

// What the schedule screens offer a person. This is navigation, not protection: every command is decided again by
// the API (TASK-046 D-12, WF-11), and a refusal is shown as it comes.

/**
 * SCHEDULE_EDIT ships to R04 at OWN, and a project's owner anchor is its Project Manager (TASK-046 D-12): the
 * project's own Project Manager plans it, while the project is APPROVED_PLANNED or ACTIVE (SCH-CC-03).
 */
export function canEditSchedule(user: SessionUser, project: ProjectDetail): boolean {
  return (
    (project.status === 'APPROVED_PLANNED' || project.status === 'ACTIVE') &&
    project.projectManagerUserId === user.id
  );
}

/** The candidate on its way, if any: a project has one at a time (TASK-046 D-6). */
export function candidateOf(baselines: ProjectBaselineDetail[]): ProjectBaselineDetail | null {
  return (
    baselines.find(
      (baseline) =>
        baseline.status === 'DRAFT' ||
        baseline.status === 'SUBMITTED' ||
        baseline.status === 'UNDER_REVIEW' ||
        baseline.status === 'RETURNED',
    ) ?? null
  );
}

/**
 * While a candidate is with WF-11 the plan it will activate from is frozen: activities and dependencies cannot change,
 * the forecast still can (TASK-046 D-8, D-5).
 */
export function isPlanFrozen(baselines: ProjectBaselineDetail[]): boolean {
  return baselines.some(
    (baseline) => baseline.status === 'SUBMITTED' || baseline.status === 'UNDER_REVIEW',
  );
}

/** Whether an inbox task decides this revision of this baseline (the subject WF-11 was started with, TASK-046 D-7). */
export function decidesBaseline(
  item: ApprovalInboxItem,
  baseline: Pick<ProjectBaselineDetail, 'id' | 'revisionNo'>,
): boolean {
  return decidesSubject(item, baselineSubject(baseline));
}

/** The subject TASK-046 starts a baseline's WF-11 run with. */
export function baselineSubject(
  baseline: Pick<ProjectBaselineDetail, 'id' | 'revisionNo'>,
): ApprovalSubject {
  return {
    module: 'Schedule',
    type: 'ProjectBaseline',
    id: baseline.id,
    revisionNo: baseline.revisionNo,
  };
}

/**
 * MOD-018 is AHDA's decision through WF-11 (ADR-013): offered on a submitted baseline when the person's inbox holds a
 * task deciding it, to an internal person who did not submit it. The inbox lists only what WF-11 lets them decide now;
 * the other two checks repeat ADR-013 on this screen, and the API decides again (403 otherwise).
 */
export function canDecideBaseline(
  user: SessionUser,
  baseline: ProjectBaselineDetail,
  task: ApprovalInboxItem | null,
): boolean {
  return (
    baseline.status === 'SUBMITTED' &&
    task !== null &&
    decidesBaseline(task, baseline) &&
    isInternal(user) &&
    task.instance.requestedByUserId !== user.id
  );
}

/**
 * Every rebaseline after the first APPROVED baseline names the WF-08 change authorisation it implements (BR-SCH-034).
 * ADR-014's Declared Baseline is superseded by the first approved plan without one (TASK-046 D-13).
 */
export function needsChangeAuthorization(active: ProjectBaselineDetail | null): boolean {
  return active?.baselineType === 'APPROVED';
}
