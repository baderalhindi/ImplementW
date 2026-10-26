import {
  fieldMessage,
  problemMessage,
  type Translate,
} from '@/features/identity-access/problems.ts';
import { ApiError } from '@/shared/api/httpClient.ts';
import { type TranslationKey } from '@/shared/i18n/i18n.ts';

// What a person reads for each WF-14 refusal (TASK-052 financial-kpi.md §5). Codes not listed fall back to the
// platform messages.

const PROBLEMS: Record<string, TranslationKey> = {
  FINANCIAL_KPI_PROJECT_NOT_ELIGIBLE: 'financialKpi.problems.projectNotEligible',
  FINANCIAL_FIELD_INTEGRATED: 'financialKpi.problems.fieldIntegrated',
  FINANCIAL_NOTHING_TO_REPORT: 'financialKpi.problems.nothingToReport',
  FINANCIAL_UPDATE_EXISTS: 'financialKpi.problems.updateExists',
  FINANCIAL_KPI_NOT_EDITABLE: 'financialKpi.problems.notEditable',
  FINANCIAL_KPI_VALUE_STATUS_INVALID: 'financialKpi.problems.valueStatusInvalid',
  FINANCIAL_KPI_AS_OF_DATE_INVALID: 'financialKpi.problems.asOfDateInvalid',
  KPI_ASSIGNMENT_NOT_ACTIVE: 'financialKpi.problems.assignmentNotActive',
  KPI_TARGET_NOT_APPROVED: 'financialKpi.problems.targetNotApproved',
  KPI_MEASUREMENT_EXISTS: 'financialKpi.problems.measurementExists',
  KPI_PERIOD_INVALID: 'financialKpi.problems.periodInvalid',
  CONFIGURATION_MISSING: 'financialKpi.problems.configurationMissing',
  INVALID_TRANSITION: 'financialKpi.problems.invalidTransition',
  PERMISSION_DENIED: 'financialKpi.problems.permissionDenied',
};

export function financialKpiProblemMessage(error: unknown, t: Translate): string {
  const key = error instanceof ApiError ? PROBLEMS[error.code] : undefined;
  return key === undefined ? problemMessage(error, t) : t(key);
}

/** A read refused for want of FINANCIAL_VIEW, KPI_VIEW or MASTER_DATA_VIEW: the screen says what it cannot show. */
export function isForbidden(error: unknown): boolean {
  return error instanceof ApiError && error.status === 403;
}

export function isRefusal(error: unknown, code: string): boolean {
  return error instanceof ApiError && error.code === code;
}

/** The record moved on since it was read (412; no longer a DRAFT; gone): the screen reads it again. */
export function isStale(error: unknown): boolean {
  return (
    error instanceof ApiError &&
    (error.code === 'PRECONDITION_FAILED' ||
      error.code === 'FINANCIAL_KPI_NOT_EDITABLE' ||
      error.code === 'INVALID_TRANSITION' ||
      error.code === 'NOT_FOUND')
  );
}

/** A field path as the API names it: a narrative's `.text` or `.language` belongs to the narrative's input. */
function inputOf(field: string): string {
  return field.replace(/\.(text|language)$/, '');
}

/** The API's field codes of a refusal, by input, so each lands on the input it names. */
export function serverCodesOf(error: unknown): Partial<Record<string, string | null>> {
  const codes: Partial<Record<string, string | null>> = {};
  if (error instanceof ApiError) {
    for (const issue of error.fieldIssues) {
      codes[inputOf(issue.field)] ??= issue.code;
    }
  }
  return codes;
}

const FIELD_MESSAGES: Partial<Record<string, TranslationKey>> = {
  DATE_IN_FUTURE: 'financialKpi.fieldErrors.asOfInFuture',
  BEFORE_START: 'financialKpi.fieldErrors.periodEndBeforeStart',
};

const AMOUNT_MESSAGES: Partial<Record<string, TranslationKey>> = {
  MALFORMED: 'financialKpi.fieldErrors.amountMalformed',
  OUT_OF_RANGE: 'financialKpi.fieldErrors.amountMalformed',
};

const KPI_VALUE_MESSAGES: Partial<Record<string, TranslationKey>> = {
  MALFORMED: 'financialKpi.fieldErrors.kpiValueMalformed',
  OUT_OF_RANGE: 'financialKpi.fieldErrors.kpiValueOutOfRange',
};

const AMOUNT_FIELDS = new Set(['actualExpenditureToDateSar', 'forecastAtCompletionSar']);

/**
 * The message for a field's code. The API reads an as-of date after today as `asOfDate NOT_ALLOWED` and a figure
 * sent beside a status that carries none as NOT_ALLOWED too; both are said in the field's own words.
 */
export function financialKpiFieldMessage(field: string, code: string, t: Translate): string {
  const key =
    field === 'asOfDate' && code === 'NOT_ALLOWED'
      ? 'financialKpi.fieldErrors.asOfInFuture'
      : AMOUNT_FIELDS.has(field)
        ? AMOUNT_MESSAGES[code]
        : field === 'measuredValue'
          ? KPI_VALUE_MESSAGES[code]
          : FIELD_MESSAGES[code];
  return key === undefined ? fieldMessage(code, t) : t(key);
}
