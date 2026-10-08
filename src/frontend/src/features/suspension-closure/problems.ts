import {
  fieldMessage,
  problemMessage,
  type Translate,
} from '@/features/identity-access/problems.ts';
import { ApiError } from '@/shared/api/httpClient.ts';
import { type TranslationKey } from '@/shared/i18n/i18n.ts';

import { type ReadinessCheckCode } from './api/types.ts';
import { READINESS_CHECK_CODES } from './closeoutRules.ts';

// What a person reads for each refusal of the WF-09 and WF-10 APIs (TASK-062 suspension.md §5, TASK-063 closure.md §5).
// Codes not listed here fall back to the platform messages.

const PROBLEMS: Record<string, TranslationKey> = {
  SUSPENSION_PROJECT_NOT_ELIGIBLE: 'suspensionClosure.problems.suspensionProjectNotEligible',
  SUSPENSION_ALREADY_EXISTS: 'suspensionClosure.problems.suspensionAlreadyExists',
  SUSPENSION_RESUMPTION_ALREADY_EXISTS: 'suspensionClosure.problems.resumptionAlreadyExists',
  SUSPENSION_INCOMPLETE: 'suspensionClosure.problems.suspensionIncomplete',
  SUSPENSION_DATE_INVALID: 'suspensionClosure.problems.suspensionDateInvalid',
  SUSPENSION_NOT_EDITABLE: 'suspensionClosure.problems.notEditable',
  SUSPENSION_UNDER_REVIEW: 'suspensionClosure.problems.underReview',
  SUSPENSION_NOT_YET_EFFECTIVE: 'suspensionClosure.problems.notYetEffective',
  CLOSURE_PROJECT_NOT_ELIGIBLE: 'suspensionClosure.problems.closureProjectNotEligible',
  CLOSURE_CASE_ALREADY_OPEN: 'suspensionClosure.problems.caseAlreadyOpen',
  CLOSURE_CASE_NOT_EDITABLE: 'suspensionClosure.problems.notEditable',
  CLOSURE_CASE_IN_USE: 'suspensionClosure.problems.caseInUse',
  CLOSURE_CASE_UNDER_REVIEW: 'suspensionClosure.problems.underReview',
  CLOSURE_INCOMPLETE: 'suspensionClosure.problems.closureIncomplete',
  CLOSURE_COMPLETION_DATE_INVALID: 'suspensionClosure.problems.completionDateInvalid',
  CLOSURE_CHECK_NOT_FAILED: 'suspensionClosure.problems.checkNotFailed',
  CLOSURE_CHECK_NOT_WAIVABLE: 'suspensionClosure.problems.checkNotWaivable',
  CLOSURE_OBLIGATION_CASE_INVALID: 'suspensionClosure.problems.obligationCaseInvalid',
  CLOSURE_OBLIGATION_OWNER_INVALID: 'suspensionClosure.problems.obligationOwnerInvalid',
  CLOSURE_OBLIGATION_SETTLED: 'suspensionClosure.problems.obligationSettled',
  PROJECT_CLOSED: 'suspensionClosure.problems.projectClosed',
  APPROVAL_ALREADY_PENDING: 'suspensionClosure.problems.approvalPending',
  CONFIGURATION_MISSING: 'suspensionClosure.problems.configurationMissing',
  INVALID_TRANSITION: 'suspensionClosure.problems.invalidTransition',
  TERMINAL_STATE: 'suspensionClosure.problems.terminalState',
  PERMISSION_DENIED: 'suspensionClosure.problems.permissionDenied',
};

/** The criteria a CLOSURE_BLOCKER_EXISTS refusal names, each as an error field (TASK-063 D-5). */
export function blockingChecksOf(error: unknown): ReadinessCheckCode[] {
  if (!(error instanceof ApiError) || error.code !== 'CLOSURE_BLOCKER_EXISTS') {
    return [];
  }
  return READINESS_CHECK_CODES.filter((code) =>
    error.fieldIssues.some((issue) => issue.field === code),
  );
}

export function suspensionClosureProblemMessage(error: unknown, t: Translate): string {
  if (error instanceof ApiError && error.code === 'CLOSURE_BLOCKER_EXISTS') {
    const criteria = blockingChecksOf(error).map((code) =>
      t(`suspensionClosure.readiness.checks.${code}`),
    );
    return criteria.length === 0
      ? t('suspensionClosure.problems.blockerExists')
      : t('suspensionClosure.problems.blockersNamed', { criteria: criteria.join('; ') });
  }
  const key = error instanceof ApiError ? PROBLEMS[error.code] : undefined;
  return key === undefined ? problemMessage(error, t) : t(key);
}

/**
 * The record moved on since it was read (412; a transition no longer allowed; final; no longer editable; gone): the
 * screen reads it again instead of retrying.
 */
export function isStale(error: unknown): boolean {
  return (
    error instanceof ApiError &&
    (error.code === 'PRECONDITION_FAILED' ||
      error.code === 'INVALID_TRANSITION' ||
      error.code === 'TERMINAL_STATE' ||
      error.code === 'SUSPENSION_NOT_EDITABLE' ||
      error.code === 'CLOSURE_CASE_NOT_EDITABLE' ||
      error.code === 'CLOSURE_OBLIGATION_SETTLED' ||
      error.code === 'NOT_FOUND')
  );
}

export function isForbidden(error: unknown): boolean {
  return error instanceof ApiError && error.status === 403;
}

const FIELD_MESSAGES: Partial<Record<string, Partial<Record<string, TranslationKey>>>> = {
  requestedEffectiveDate: {
    REQUIRED: 'suspensionClosure.fieldErrors.effectiveDateRequired',
    NOT_ALLOWED: 'suspensionClosure.fieldErrors.effectiveDatePast',
  },
  plannedResumptionDate: {
    OUT_OF_RANGE: 'suspensionClosure.fieldErrors.plannedResumptionBeforeEffective',
    NOT_ALLOWED: 'suspensionClosure.fieldErrors.plannedResumptionBeforeEffective',
  },
  actualProjectCompletionDate: {
    REQUIRED: 'suspensionClosure.fieldErrors.completionDateRequired',
    NOT_ALLOWED: 'suspensionClosure.fieldErrors.completionDateInvalid',
    OUT_OF_RANGE: 'suspensionClosure.fieldErrors.completionDateInvalid',
  },
  narrative: { REQUIRED: 'suspensionClosure.fieldErrors.narrativeRequired' },
  completionNarrative: { REQUIRED: 'suspensionClosure.fieldErrors.narrativeRequired' },
  closureNarrative: { REQUIRED: 'suspensionClosure.fieldErrors.narrativeRequired' },
};

export function suspensionClosureFieldMessage(field: string, code: string, t: Translate): string {
  const key = FIELD_MESSAGES[field]?.[code];
  return key === undefined ? fieldMessage(code, t) : t(key);
}
