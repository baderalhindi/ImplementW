import { problemMessage, type Translate } from '@/features/identity-access/problems.ts';
import { ApiError } from '@/shared/api/httpClient.ts';
import { type TranslationKey } from '@/shared/i18n/i18n.ts';

// What a person reads for each WF-02 refusal (TASK-044 progress-update.md §4, §5). Codes not listed here fall back to
// the platform messages.

const PROGRESS_PROBLEMS: Record<string, TranslationKey> = {
  PROGRESS_PROJECT_NOT_ACTIVE: 'progress.problems.projectNotActive',
  PROGRESS_ROLLUP_UNAVAILABLE: 'progress.problems.rollupUnavailable',
  PROGRESS_NOTHING_TO_REPORT: 'progress.problems.nothingToReport',
  PROGRESS_SUBMISSION_EXISTS: 'progress.problems.submissionExists',
  PROGRESS_NOT_EDITABLE: 'progress.problems.notEditable',
  CONFIGURATION_MISSING: 'progress.problems.configurationMissing',
  INVALID_TRANSITION: 'progress.problems.invalidTransition',
  PERMISSION_DENIED: 'progress.problems.permissionDenied',
};

export function progressProblemMessage(error: unknown, t: Translate): string {
  const key = error instanceof ApiError ? PROGRESS_PROBLEMS[error.code] : undefined;
  return key === undefined ? problemMessage(error, t) : t(key);
}

/** A read refused for want of PROGRESS_VIEW: the screen says what it cannot show instead of failing. */
export function isForbidden(error: unknown): boolean {
  return error instanceof ApiError && error.status === 403;
}

/**
 * The update already exists: a retried start whose first attempt succeeded (TASK-044 F-17), or someone else started
 * it. Either way the screen reads the project again and shows that update.
 */
export function isExistingSubmission(error: unknown): boolean {
  return error instanceof ApiError && error.code === 'PROGRESS_SUBMISSION_EXISTS';
}

/** The revision moved on since it was read (412, or no longer a DRAFT): the screen reloads instead of retrying. */
export function isStaleSubmission(error: unknown): boolean {
  return (
    error instanceof ApiError &&
    (error.code === 'PRECONDITION_FAILED' ||
      error.code === 'PROGRESS_NOT_EDITABLE' ||
      error.code === 'INVALID_TRANSITION')
  );
}

/**
 * The message for a field error code. The platform reads OUT_OF_RANGE as "choose a listed value"; on a percentage it
 * means outside 0–100 or past four decimal places.
 */
export const PERCENT_FIELD_MESSAGES: Partial<Record<string, TranslationKey>> = {
  REQUIRED: 'progress.fieldErrors.percentRequired',
  MALFORMED: 'progress.fieldErrors.percentMalformed',
  OUT_OF_RANGE: 'progress.fieldErrors.percentOutOfRange',
};
