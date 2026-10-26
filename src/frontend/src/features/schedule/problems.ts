import {
  fieldMessage,
  problemMessage,
  type Translate,
} from '@/features/identity-access/problems.ts';
import { ApiError } from '@/shared/api/httpClient.ts';
import { type TranslationKey } from '@/shared/i18n/i18n.ts';

// What a person reads for each WF-03 refusal (TASK-046 schedule-baseline.md §5). Codes not listed here fall back to
// the platform messages.

const SCHEDULE_PROBLEMS: Record<string, TranslationKey> = {
  SCHEDULE_PROJECT_NOT_ELIGIBLE: 'schedule.problems.projectNotEligible',
  SCHEDULE_NOT_INITIALIZED: 'schedule.problems.notInitialized',
  SCHEDULE_EXISTS: 'schedule.problems.exists',
  SCHEDULE_NOT_EDITABLE: 'schedule.problems.notEditable',
  SCHEDULE_WBS_CODE_EXISTS: 'schedule.problems.wbsCodeExists',
  SCHEDULE_HIERARCHY_INVALID: 'schedule.problems.hierarchyInvalid',
  SCHEDULE_HIERARCHY_CIRCULAR: 'schedule.problems.hierarchyCircular',
  SCHEDULE_ACTIVITY_IN_USE: 'schedule.problems.activityInUse',
  SCHEDULE_FORECAST_INVALID: 'schedule.problems.forecastInvalid',
  SCHEDULE_DEPENDENCY_INVALID: 'schedule.problems.dependencyInvalid',
  SCHEDULE_DEPENDENCY_CIRCULAR: 'schedule.problems.dependencyCircular',
  SCHEDULE_DEPENDENCY_EXISTS: 'schedule.problems.dependencyExists',
  SCHEDULE_ACTIVE_BASELINE_REQUIRED: 'schedule.problems.activeBaselineRequired',
  SCHEDULE_BASELINE_CANDIDATE_EXISTS: 'schedule.problems.candidateExists',
  SCHEDULE_BASELINE_EMPTY: 'schedule.problems.baselineEmpty',
  SCHEDULE_BASELINE_NOT_EDITABLE: 'schedule.problems.baselineNotEditable',
  SCHEDULE_CHANGE_AUTHORIZATION_REQUIRED: 'schedule.problems.changeAuthorizationRequired',
  SCHEDULE_SINGLE_ACTIVE_BASELINE: 'schedule.problems.singleActiveBaseline',
  CONFIGURATION_MISSING: 'schedule.problems.configurationMissing',
  INVALID_TRANSITION: 'schedule.problems.invalidTransition',
  PERMISSION_DENIED: 'schedule.problems.permissionDenied',
};

export function scheduleProblemMessage(error: unknown, t: Translate): string {
  const key = error instanceof ApiError ? SCHEDULE_PROBLEMS[error.code] : undefined;
  return key === undefined ? problemMessage(error, t) : t(key);
}

/** A read refused for want of SCHEDULE_VIEW: the screen says what it cannot show instead of failing. */
export function isForbidden(error: unknown): boolean {
  return error instanceof ApiError && error.status === 403;
}

export function isRefusal(error: unknown, code: string): boolean {
  return error instanceof ApiError && error.code === code;
}

/**
 * The record moved on since it was read (412, frozen by a submitted candidate, or no longer in the state the command
 * needs): the screen reads it again instead of retrying.
 */
export function isStale(error: unknown): boolean {
  return (
    error instanceof ApiError &&
    (error.code === 'PRECONDITION_FAILED' ||
      error.code === 'SCHEDULE_NOT_EDITABLE' ||
      error.code === 'SCHEDULE_BASELINE_NOT_EDITABLE' ||
      error.code === 'INVALID_TRANSITION' ||
      error.code === 'NOT_FOUND')
  );
}

const DAY_FIELDS: Partial<Record<string, TranslationKey>> = {
  plannedDurationDays: 'schedule.fieldErrors.durationOutOfRange',
  lagDays: 'schedule.fieldErrors.lagOutOfRange',
  sortOrder: 'schedule.fieldErrors.sortOrderOutOfRange',
};

/**
 * The message for a field error, client- or server-side. The platform reads OUT_OF_RANGE as "choose a listed value";
 * on a number of days it means outside the accepted range. SAME_ACTIVITY and DEPENDENCY_EXISTS are MOD-015's own.
 */
export function scheduleFieldMessage(field: string, code: string, t: Translate): string {
  const days = DAY_FIELDS[field];
  if (days !== undefined && code === 'OUT_OF_RANGE') {
    return t(days);
  }
  if (days !== undefined && code === 'MALFORMED') {
    return t('schedule.fieldErrors.daysMalformed');
  }
  switch (code) {
    case 'SAME_ACTIVITY':
      return t('schedule.fieldErrors.sameActivity');
    case 'DEPENDENCY_EXISTS':
      return t('schedule.problems.dependencyExists');
    default:
      return fieldMessage(code, t);
  }
}
