import {
  fieldMessage,
  problemMessage,
  type Translate,
} from '@/features/identity-access/problems.ts';
import { ApiError } from '@/shared/api/httpClient.ts';
import { type TranslationKey } from '@/shared/i18n/i18n.ts';

// What a person reads for each refusal of the WF-08 API (TASK-060 change-request.md §5). Codes not listed here fall back
// to the platform messages.

const CHANGE_REQUEST_PROBLEMS: Record<string, TranslationKey> = {
  CHANGE_REQUEST_PROJECT_NOT_ELIGIBLE: 'changeRequests.problems.projectNotEligible',
  CHANGE_REQUEST_INCOMPLETE: 'changeRequests.problems.incomplete',
  CHANGE_REQUEST_PROFILE_INVALID: 'changeRequests.problems.profileInvalid',
  CHANGE_REQUEST_TARGET_UNAVAILABLE: 'changeRequests.problems.targetUnavailable',
  CHANGE_REQUEST_NOT_EDITABLE: 'changeRequests.problems.notEditable',
  CHANGE_REQUEST_EVALUATED: 'changeRequests.problems.evaluated',
  CHANGE_REQUEST_UNDER_REVIEW: 'changeRequests.problems.underReview',
  CHANGE_REQUEST_AUTHORIZATION_PENDING: 'changeRequests.problems.authorizationPending',
  APPROVAL_ALREADY_PENDING: 'changeRequests.problems.approvalPending',
  CONFIGURATION_MISSING: 'changeRequests.problems.configurationMissing',
  INVALID_TRANSITION: 'changeRequests.problems.invalidTransition',
  TERMINAL_STATE: 'changeRequests.problems.terminalState',
  PERMISSION_DENIED: 'changeRequests.problems.permissionDenied',
};

export function changeRequestProblemMessage(error: unknown, t: Translate): string {
  const key = error instanceof ApiError ? CHANGE_REQUEST_PROBLEMS[error.code] : undefined;
  return key === undefined ? problemMessage(error, t) : t(key);
}

/**
 * The request moved on since it was read (412; a transition no longer allowed; final; no longer editable; its review
 * already started; gone): the screen reads it again instead of retrying.
 */
export function isStale(error: unknown): boolean {
  return (
    error instanceof ApiError &&
    (error.code === 'PRECONDITION_FAILED' ||
      error.code === 'INVALID_TRANSITION' ||
      error.code === 'TERMINAL_STATE' ||
      error.code === 'CHANGE_REQUEST_NOT_EDITABLE' ||
      error.code === 'CHANGE_REQUEST_EVALUATED' ||
      error.code === 'NOT_FOUND')
  );
}

export function isForbidden(error: unknown): boolean {
  return error instanceof ApiError && error.status === 403;
}

const FIELD_MESSAGES: Partial<Record<string, Partial<Record<string, TranslationKey>>>> = {
  costImpactSar: {
    OUT_OF_RANGE: 'changeRequests.fieldErrors.impactZero',
    MALFORMED: 'changeRequests.fieldErrors.costMalformed',
    REQUIRED: 'changeRequests.fieldErrors.costRequired',
  },
  scheduleImpactDays: {
    OUT_OF_RANGE: 'changeRequests.fieldErrors.daysOutOfRange',
    MALFORMED: 'changeRequests.fieldErrors.daysMalformed',
    REQUIRED: 'changeRequests.fieldErrors.daysRequired',
  },
  scopeImpact: { REQUIRED: 'changeRequests.fieldErrors.scopeRequired' },
  isContractualObligation: { REQUIRED: 'changeRequests.fieldErrors.contractualRequired' },
  requestedGovernanceProfileItemId: {
    REQUIRED: 'changeRequests.fieldErrors.profileRequired',
    NOT_ALLOWED: 'changeRequests.fieldErrors.profileSameAsProject',
  },
};

export function changeRequestFieldMessage(field: string, code: string, t: Translate): string {
  const key = FIELD_MESSAGES[field]?.[code];
  return key === undefined ? fieldMessage(code, t) : t(key);
}
