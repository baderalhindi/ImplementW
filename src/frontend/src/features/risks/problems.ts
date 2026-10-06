import {
  fieldMessage,
  problemMessage,
  type Translate,
} from '@/features/identity-access/problems.ts';
import { ApiError } from '@/shared/api/httpClient.ts';
import { type TranslationKey } from '@/shared/i18n/i18n.ts';

// What a person reads for each refusal of the risk API (TASK-055 risk-management.md §5). Codes not listed here fall
// back to the platform messages.

const RISK_PROBLEMS: Record<string, TranslationKey> = {
  RISK_PROJECT_NOT_ELIGIBLE: 'risks.problems.projectNotEligible',
  RISK_NOT_IN_PROFILE: 'risks.problems.notInProfile',
  RISK_CATEGORY_INVALID: 'risks.problems.categoryInvalid',
  RISK_OWNER_NOT_ELIGIBLE: 'risks.problems.ownerNotEligible',
  RISK_IDENTIFIED_DATE_INVALID: 'risks.problems.identifiedDateInvalid',
  RISK_CLOSED: 'risks.problems.closed',
  RISK_IMPACT_INVALID: 'risks.problems.impactInvalid',
  RISK_TREATMENT_ACTION_REQUIRED: 'risks.problems.treatmentActionRequired',
  RISK_ACCEPTANCE_ACTIVE: 'risks.problems.acceptanceActive',
  RISK_ACCEPTANCE_NOT_ACTIVE: 'risks.problems.acceptanceNotActive',
  RISK_ACCEPTANCE_EXPIRY_INVALID: 'risks.problems.acceptanceExpiryInvalid',
  RISK_ACTION_NOT_EDITABLE: 'risks.problems.actionNotEditable',
  CONFIGURATION_MISSING: 'risks.problems.configurationMissing',
  INVALID_TRANSITION: 'risks.problems.invalidTransition',
  PERMISSION_DENIED: 'risks.problems.permissionDenied',
};

export function riskProblemMessage(error: unknown, t: Translate): string {
  const key = error instanceof ApiError ? RISK_PROBLEMS[error.code] : undefined;
  return key === undefined ? problemMessage(error, t) : t(key);
}

/** A read refused for want of a permission (RISK_VIEW, CONFIGURATION_VIEW, MASTER_DATA_VIEW): shown as such. */
export function isForbidden(error: unknown): boolean {
  return error instanceof ApiError && error.status === 403;
}

/** No RISK_MATRIX version is in force (422): the matrix is "not configured", never a default grid. */
export function isMatrixMissing(error: unknown): boolean {
  return error instanceof ApiError && error.code === 'CONFIGURATION_MISSING';
}

/**
 * The record moved on since it was read (412; a transition no longer allowed; an action no longer editable; gone): the
 * screen reads it again instead of retrying.
 */
export function isStale(error: unknown): boolean {
  return (
    error instanceof ApiError &&
    (error.code === 'PRECONDITION_FAILED' ||
      error.code === 'INVALID_TRANSITION' ||
      error.code === 'RISK_CLOSED' ||
      error.code === 'RISK_ACTION_NOT_EDITABLE' ||
      error.code === 'NOT_FOUND')
  );
}

const FIELD_MESSAGES: Partial<Record<string, Partial<Record<string, TranslationKey>>>> = {
  identifiedDate: { DATE_IN_FUTURE: 'risks.fieldErrors.identifiedInFuture' },
  nextReviewDate: { DATE_BEFORE_START: 'risks.fieldErrors.reviewBeforeIdentified' },
  expiresOn: { EXPIRY_NOT_AFTER_TODAY: 'risks.fieldErrors.expiryNotAfterToday' },
  rationale: { REQUIRED: 'risks.fieldErrors.rationaleRequired' },
};

export function riskFieldMessage(field: string, code: string, t: Translate): string {
  const key = FIELD_MESSAGES[field]?.[code];
  return key === undefined ? fieldMessage(code, t) : t(key);
}
