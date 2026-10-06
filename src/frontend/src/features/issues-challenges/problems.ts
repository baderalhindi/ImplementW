import {
  fieldMessage,
  problemMessage,
  type Translate,
} from '@/features/identity-access/problems.ts';
import { ApiError } from '@/shared/api/httpClient.ts';
import { type TranslationKey } from '@/shared/i18n/i18n.ts';

// What a person reads for each refusal of the WF-07 API (TASK-057 management-concern.md §5). Codes not listed here fall
// back to the platform messages.

const CONCERN_PROBLEMS: Record<string, TranslationKey> = {
  CONCERN_PROJECT_NOT_ELIGIBLE: 'issuesChallenges.problems.projectNotEligible',
  CONCERN_CATEGORY_INVALID: 'issuesChallenges.problems.categoryInvalid',
  CONCERN_PRIORITY_INVALID: 'issuesChallenges.problems.priorityInvalid',
  CONCERN_IMPACT_INVALID: 'issuesChallenges.problems.impactInvalid',
  CONCERN_ASSIGNEE_NOT_ELIGIBLE: 'issuesChallenges.problems.assigneeNotEligible',
  CONCERN_TARGET_DATE_INVALID: 'issuesChallenges.problems.targetDateInvalid',
  CONCERN_CLOSED: 'issuesChallenges.problems.closed',
  CONCERN_NOT_EDITABLE: 'issuesChallenges.problems.notEditable',
  CONCERN_ESCALATION_OPEN: 'issuesChallenges.problems.escalationOpen',
  CONCERN_NOT_ESCALATABLE: 'issuesChallenges.problems.notEscalatable',
  CONCERN_ESCALATION_NOT_OPEN: 'issuesChallenges.problems.escalationNotOpen',
  APPROVAL_ALREADY_PENDING: 'issuesChallenges.problems.approvalPending',
  IDEMPOTENCY_KEY_REUSED: 'issuesChallenges.problems.keyReused',
  CONFIGURATION_MISSING: 'issuesChallenges.problems.configurationMissing',
  INVALID_TRANSITION: 'issuesChallenges.problems.invalidTransition',
  PERMISSION_DENIED: 'issuesChallenges.problems.permissionDenied',
};

export function concernProblemMessage(error: unknown, t: Translate): string {
  const key = error instanceof ApiError ? CONCERN_PROBLEMS[error.code] : undefined;
  return key === undefined ? problemMessage(error, t) : t(key);
}

/**
 * The record moved on since it was read (412; a transition no longer allowed; closed; fixed for validation; an
 * escalation already open or already ended; gone): the screen reads it again instead of retrying.
 */
export function isStale(error: unknown): boolean {
  return (
    error instanceof ApiError &&
    (error.code === 'PRECONDITION_FAILED' ||
      error.code === 'INVALID_TRANSITION' ||
      error.code === 'CONCERN_CLOSED' ||
      error.code === 'CONCERN_NOT_EDITABLE' ||
      error.code === 'CONCERN_ESCALATION_OPEN' ||
      error.code === 'CONCERN_NOT_ESCALATABLE' ||
      error.code === 'CONCERN_ESCALATION_NOT_OPEN' ||
      error.code === 'NOT_FOUND')
  );
}

const FIELD_MESSAGES: Partial<Record<string, Partial<Record<string, TranslationKey>>>> = {
  targetResolutionDate: { DATE_IN_PAST: 'issuesChallenges.fieldErrors.targetInPast' },
  impacts: { REQUIRED: 'issuesChallenges.fieldErrors.impactsRequired' },
  resolution: { REQUIRED: 'issuesChallenges.fieldErrors.resolutionRequired' },
  reason: { REQUIRED: 'issuesChallenges.fieldErrors.reasonRequired' },
  assigneeUserId: { REQUIRED: 'issuesChallenges.fieldErrors.assigneeRequired' },
};

export function concernFieldMessage(field: string, code: string, t: Translate): string {
  const key = FIELD_MESSAGES[field]?.[code];
  return key === undefined ? fieldMessage(code, t) : t(key);
}
