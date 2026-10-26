import { documentProblemMessage } from '@/features/documents/problems.ts';
import { fieldMessage, type Translate } from '@/features/identity-access/problems.ts';
import { ApiError } from '@/shared/api/httpClient.ts';
import { type TranslationKey } from '@/shared/i18n/i18n.ts';

// What a person reads for each refusal of the milestone API (TASK-050 milestone-achievement.md §5), WF-03's milestone
// codes among them. Codes not listed here fall back to WF-12's (evidence is attached through DocumentManagement) and
// then to the platform messages.

const MILESTONE_PROBLEMS: Record<string, TranslationKey> = {
  MILESTONE_PROJECT_NOT_ACTIVE: 'milestones.problems.projectNotActive',
  MILESTONE_NOT_CLAIMABLE: 'milestones.problems.notClaimable',
  MILESTONE_CLAIMED_DATE_INVALID: 'milestones.problems.claimedDateInvalid',
  MILESTONE_ACHIEVEMENT_OPEN: 'milestones.problems.achievementOpen',
  MILESTONE_ACHIEVEMENT_NOT_EDITABLE: 'milestones.problems.achievementNotEditable',
  MILESTONE_EVIDENCE_REQUIRED: 'milestones.problems.evidenceRequired',
  SCHEDULE_PROJECT_NOT_ELIGIBLE: 'milestones.problems.scheduleProjectNotEligible',
  SCHEDULE_NOT_INITIALIZED: 'milestones.problems.scheduleNotInitialized',
  SCHEDULE_NOT_EDITABLE: 'milestones.problems.scheduleNotEditable',
  SCHEDULE_MILESTONE_CATEGORY_INVALID: 'milestones.problems.categoryInvalid',
  SCHEDULE_MILESTONE_ACTIVITY_INVALID: 'milestones.problems.activityInvalid',
  DOCUMENT_LINK_ENDED: 'milestones.problems.linkEnded',
  DOCUMENT_EVIDENCE_WITHDRAWN: 'milestones.problems.evidenceWithdrawn',
  CONFIGURATION_MISSING: 'milestones.problems.configurationMissing',
  INVALID_TRANSITION: 'milestones.problems.invalidTransition',
  PERMISSION_DENIED: 'milestones.problems.permissionDenied',
};

export function milestoneProblemMessage(error: unknown, t: Translate): string {
  const key = error instanceof ApiError ? MILESTONE_PROBLEMS[error.code] : undefined;
  return key === undefined ? documentProblemMessage(error, t) : t(key);
}

/** A read refused for want of a permission (MILESTONE_VIEW, SCHEDULE_VIEW, MASTER_DATA_VIEW): shown as such. */
export function isForbidden(error: unknown): boolean {
  return error instanceof ApiError && error.status === 403;
}

export function isRefusal(error: unknown, code: string): boolean {
  return error instanceof ApiError && error.code === code;
}

/**
 * The record moved on since it was read (412; a revision no longer DRAFT; a milestone no longer PLANNED or frozen; gone):
 * the screen reads it again instead of retrying.
 */
export function isStale(error: unknown): boolean {
  return (
    error instanceof ApiError &&
    (error.code === 'PRECONDITION_FAILED' ||
      error.code === 'MILESTONE_ACHIEVEMENT_NOT_EDITABLE' ||
      error.code === 'INVALID_TRANSITION' ||
      error.code === 'NOT_FOUND')
  );
}

const FIELD_MESSAGES: Partial<Record<string, Partial<Record<string, TranslationKey>>>> = {
  claimedAchievementDate: {
    DATE_IN_FUTURE: 'milestones.fieldErrors.claimedDateInFuture',
    NOT_ALLOWED: 'milestones.fieldErrors.claimedDateInFuture',
  },
  milestoneCategoryItemId: { NOT_FOUND: 'milestones.fieldErrors.categoryInvalid' },
  scheduleActivityId: { NOT_FOUND: 'milestones.fieldErrors.activityInvalid' },
  sortOrder: { OUT_OF_RANGE: 'milestones.fieldErrors.sortOrderOutOfRange' },
  evidenceTypeItemId: { NOT_FOUND: 'milestones.fieldErrors.evidenceTypeInvalid' },
};

export function milestoneFieldMessage(field: string, code: string, t: Translate): string {
  const key = FIELD_MESSAGES[field]?.[code];
  return key === undefined ? fieldMessage(code, t) : t(key);
}
