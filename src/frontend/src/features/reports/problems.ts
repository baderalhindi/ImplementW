import { problemMessage, type Translate } from '@/features/identity-access/problems.ts';
import { ApiError } from '@/shared/api/httpClient.ts';
import { type TranslationKey } from '@/shared/i18n/i18n.ts';

// What a person reads for each FG-02 refusal (TASK-071 reports.md §6). Codes not listed fall back to the platform's.

const PROBLEMS: Record<string, TranslationKey> = {
  NOT_FOUND: 'reports.problems.notFound',
  PERMISSION_DENIED: 'reports.problems.permissionDenied',
  CONFIGURATION_MISSING: 'reports.problems.configurationMissing',
  REPORT_COLUMN_NOT_SUPPORTED: 'reports.problems.columnNotSupported',
  REPORT_FILTER_NOT_SUPPORTED: 'reports.problems.filterNotSupported',
  REPORT_FILTER_VALUE_INVALID: 'reports.problems.filterValueInvalid',
  REPORT_SORT_NOT_SUPPORTED: 'reports.problems.sortNotSupported',
  REPORT_PARAMETER_INVALID: 'reports.problems.parameterInvalid',
  REPORT_PARAMETER_REQUIRED: 'reports.problems.parameterRequired',
  REPORT_PARAMETER_UNAUTHORIZED: 'reports.problems.parameterUnauthorized',
  REPORT_SAVED_VIEW_NOT_ALLOWED: 'reports.problems.savedViewNotAllowed',
  REPORT_SAVED_VIEW_INCOMPATIBLE: 'reports.problems.savedViewIncompatible',
  REPORT_DUPLICATE_REQUEST: 'reports.problems.duplicateRequest',
  REPORT_JOB_NOT_CANCELLABLE: 'reports.problems.jobNotCancellable',
  REPORT_OUTPUT_NOT_AVAILABLE: 'reports.problems.outputNotAvailable',
  REPORT_OUTPUT_EXPIRED: 'reports.problems.outputExpired',
};

export function reportProblemMessage(error: unknown, t: Translate): string {
  const key = error instanceof ApiError ? PROBLEMS[error.code] : undefined;
  return key === undefined ? problemMessage(error, t) : t(key);
}

/** A download refused because the requester may no longer see what the file holds (D-7): said as such, not as a fault. */
export function downloadProblemMessage(error: unknown, t: Translate): string {
  return error instanceof ApiError && error.code === 'PERMISSION_DENIED'
    ? t('reports.problems.downloadDenied')
    : reportProblemMessage(error, t);
}

export function isProblem(error: unknown, code: string): boolean {
  return error instanceof ApiError && error.code === code;
}
