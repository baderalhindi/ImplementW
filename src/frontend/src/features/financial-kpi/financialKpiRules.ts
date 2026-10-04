import { checkText, optionalText } from '@/features/identity-access/forms.ts';
import { narrativeRequest } from '@/features/progress/progressUpdate.ts';
import { type NarrativeText } from '@/features/projects/api/types.ts';
import { TEXT_LENGTH } from '@/features/projects/registration.ts';
import { type Language } from '@/shared/i18n/i18n.ts';

import {
  type Decimal,
  type FinancialProgressUpdateDetail,
  type FinancialProgressUpdateRequest,
  type KpiMeasurementCreateRequest,
  type KpiMeasurementDetail,
  type KpiMeasurementRequest,
  type KpiRagStatus,
  type KpiTargetVersionDetail,
  type Money,
  type ValueStatus,
} from './api/types.ts';

// The WF-14 rules the screens apply. The first is the task's: a figure is shown only when the API says it is MEASURED;
// a missing, stale or not-applicable one is shown as that state, and a figure withheld from the caller's audience as
// restricted (ADR-010) — never as 0, never blank, never in a success colour.

/** The value states that carry no figure (TASK-052 D-2). */
export type UnknownStatus = Exclude<ValueStatus, 'MEASURED'>;

/** What one figure shows. */
export type FigureState =
  | { kind: 'measured'; value: Decimal }
  /** MISSING, STALE or NOT_APPLICABLE: the API sends null with the reason. */
  | { kind: 'unknown'; status: UnknownStatus }
  /** Withheld from the caller's audience: omitted from the JSON and named in `maskedFields`. */
  | { kind: 'restricted' }
  /** No figure exists at all: no approved budget, no update, a forecast not given beside a measured actual. */
  | { kind: 'absent' };

interface MaskedRecord {
  maskedFields: string[];
}

/**
 * A figure of a representation. `status` is the record's `valueStatus`, or null for a figure that has none (the
 * Approved Budget). A figure is shown only when it is present and MEASURED, so a value the API ever sent beside another
 * status is not presented as known.
 */
export function figureOf<R extends MaskedRecord>(
  record: R,
  field: keyof R & string,
  status: ValueStatus | null,
): FigureState {
  if (record.maskedFields.includes(field)) {
    return { kind: 'restricted' };
  }
  if (status !== null && status !== 'MEASURED') {
    return { kind: 'unknown', status };
  }
  const value = record[field] as Decimal | null | undefined;
  return value === null || value === undefined ? { kind: 'absent' } : { kind: 'measured', value };
}

export function toNumber(value: Decimal): number {
  return typeof value === 'number' ? value : Number(value);
}

/** A rating that is a colour: only these ever use a status colour, and only the API gives them (D-2). */
export function isRated(rag: KpiRagStatus): boolean {
  return rag === 'GREEN' || rag === 'AMBER' || rag === 'RED';
}

// ---------------------------------------------------------------------------------------------------------- KPIs

/** Newest period first, whatever order they arrive in. */
export function byPeriodDescending(measurements: KpiMeasurementDetail[]): KpiMeasurementDetail[] {
  return [...measurements].sort((a, b) =>
    a.periodStart === b.periodStart
      ? b.periodEnd.localeCompare(a.periodEnd)
      : b.periodStart.localeCompare(a.periodStart),
  );
}

/**
 * The measurement for the period today falls in (UTC dates, TASK-052 D-7), or null: then the current period has no
 * data, whatever was recorded before (the workbook's validation check).
 */
export function currentMeasurement(
  measurements: KpiMeasurementDetail[],
  today: string,
): KpiMeasurementDetail | null {
  return (
    byPeriodDescending(measurements).find(
      (measurement) => measurement.periodStart <= today && today <= measurement.periodEnd,
    ) ?? null
  );
}

/** The latest period whose value AHDA published: the official one. */
export function latestPublished(measurements: KpiMeasurementDetail[]): KpiMeasurementDetail | null {
  return latestInStatus(measurements, 'PUBLISHED');
}

/** The latest-period measurement in a status: the DRAFT to edit, the SUBMITTED one to publish. */
export function latestInStatus(
  measurements: KpiMeasurementDetail[],
  status: KpiMeasurementDetail['status'],
): KpiMeasurementDetail | null {
  return byPeriodDescending(measurements).find((m) => m.status === status) ?? null;
}

/** The target version in force: the one a measurement recorded now is pinned to. */
export function activeTarget(targets: KpiTargetVersionDetail[]): KpiTargetVersionDetail | null {
  return targets.find((target) => target.status === 'ACTIVE') ?? null;
}

/** One point of SCR-073's trend: a period's value and the target version it was rated against when recorded. */
export interface TrendPoint {
  id: string;
  periodStart: string;
  periodEnd: string;
  /** Null unless MEASURED; plotted apart from the values, never at 0. */
  value: number | null;
  valueStatus: ValueStatus;
  restricted: boolean;
  ragStatus: KpiRagStatus;
  /** The measurement's own pinned target, never the version in force now (acceptance criterion 2). */
  targetVersionNo: number;
  targetValue: number;
}

/** The measurements, earliest period first, each with its own pinned target. */
export function trendPoints(measurements: KpiMeasurementDetail[]): TrendPoint[] {
  return byPeriodDescending(measurements)
    .reverse()
    .map((measurement) => {
      const figure = figureOf(measurement, 'measuredValue', measurement.valueStatus);
      return {
        id: measurement.id,
        periodStart: measurement.periodStart,
        periodEnd: measurement.periodEnd,
        value: figure.kind === 'measured' ? toNumber(figure.value) : null,
        valueStatus: measurement.valueStatus,
        restricted: figure.kind === 'restricted',
        ragStatus: measurement.ragStatus,
        targetVersionNo: measurement.targetVersionNo,
        targetValue: toNumber(measurement.targetValue),
      };
    });
}

/** A run of consecutive points rated against the same target version: one step of the target line. */
export interface TargetSegment {
  targetVersionNo: number;
  targetValue: number;
  /** Indexes into the points, inclusive. */
  first: number;
  last: number;
}

export function targetSegments(points: TrendPoint[]): TargetSegment[] {
  const segments: TargetSegment[] = [];
  points.forEach((point, index) => {
    const previous = segments.at(-1);
    if (previous?.targetVersionNo === point.targetVersionNo) {
      previous.last = index;
    } else {
      segments.push({
        targetVersionNo: point.targetVersionNo,
        targetValue: point.targetValue,
        first: index,
        last: index,
      });
    }
  });
  return segments;
}

/** The calendar month today falls in, the period MOD-024 offers first (the KPI's frequency cannot be read, F-3). */
export function monthOf(today: string): { start: string; end: string } {
  const [year = 1970, month = 1] = today.split('-').map(Number);
  // Day 0 of the next month is this month's last day.
  const end = new Date(Date.UTC(year, month, 0)).toISOString().slice(0, 10);
  return { start: `${today.slice(0, 7)}-01`, end };
}

// ---------------------------------------------------------------------------------------------------------- forms

/** What MOD-023 and MOD-024 share: the value's status, the day it is true as of, and a narrative. */
interface ValueFormBase {
  valueStatus: ValueStatus | '';
  asOfDate: string;
  narrative: string;
}

export interface FinancialUpdateValues extends ValueFormBase {
  actual: string;
  forecast: string;
  sourceReference: string;
}

export interface KpiValueValues extends ValueFormBase {
  periodStart: string;
  periodEnd: string;
  value: string;
}

/** The API's field codes by request field path, or null. */
export type FormCodes = Partial<Record<string, string | null>>;

/** The ERD's limit on a source or document reference (FinancialKpiFields.SourceReference). */
export const SOURCE_REFERENCE_LENGTH = 200;
/** numeric(18,4): four places, and below 10^14 (KpiValue.Places). */
export const KPI_PLACES = 4;
const KPI_LIMIT = 100_000_000_000_000;
const SAR_PATTERN = /^[0-9]{1,16}(\.[0-9]{1,2})?$/;
const DECIMAL_PATTERN = /^-?[0-9]+(\.[0-9]+)?$/;

/**
 * A SAR amount as typed: REQUIRED, MALFORMED unless digits with up to two places (no sign, no grouping), the API's
 * rule for an R-16 amount that is never negative.
 */
export function checkSar(value: string, required: boolean): string | null {
  const trimmed = value.trim();
  if (trimmed === '') {
    return required ? 'REQUIRED' : null;
  }
  return SAR_PATTERN.test(trimmed) ? null : 'MALFORMED';
}

/** "1250000" → "1250000.00": R-16 writes two places. */
export function toMoney(value: string): Money {
  const [whole = '0', fraction = ''] = value.trim().split('.');
  return `${whole}.${fraction.padEnd(2, '0')}`;
}

/** A KPI value as typed: REQUIRED, MALFORMED, OUT_OF_RANGE past four places or 10^14. */
export function checkKpiValue(value: string): string | null {
  const trimmed = value.trim();
  if (trimmed === '') {
    return 'REQUIRED';
  }
  if (!DECIMAL_PATTERN.test(trimmed)) {
    return 'MALFORMED';
  }
  const places = trimmed.split('.')[1]?.length ?? 0;
  return places > KPI_PLACES || Math.abs(Number(trimmed)) >= KPI_LIMIT ? 'OUT_OF_RANGE' : null;
}

/** The as-of date: required, and not after today (FINANCIAL_KPI_AS_OF_DATE_INVALID). */
export function checkAsOfDate(value: string, today: string): string | null {
  if (value === '') {
    return 'REQUIRED';
  }
  return value > today ? 'DATE_IN_FUTURE' : null;
}

function checkBase(values: ValueFormBase, today: string): FormCodes {
  return {
    valueStatus: values.valueStatus === '' ? 'REQUIRED' : null,
    asOfDate: checkAsOfDate(values.asOfDate, today),
    narrative: checkText(values.narrative, { maxLength: TEXT_LENGTH }),
  };
}

/**
 * MOD-023: a MEASURED update needs its actual expenditure and may give a forecast; any other status sends neither
 * (ValueRules.FinancialRefused), so the inputs are not even read.
 */
export function checkFinancialUpdate(values: FinancialUpdateValues, today: string): FormCodes {
  const measured = values.valueStatus === 'MEASURED';
  return {
    ...checkBase(values, today),
    actualExpenditureToDateSar: measured ? checkSar(values.actual, true) : null,
    forecastAtCompletionSar: measured ? checkSar(values.forecast, false) : null,
    sourceReference: checkText(values.sourceReference, { maxLength: SOURCE_REFERENCE_LENGTH }),
  };
}

/** MOD-024: a MEASURED value is required and sent; any other status sends null. A new one names its period. */
export function checkKpiValueForm(
  values: KpiValueValues,
  today: string,
  isNew: boolean,
): FormCodes {
  const periodEnd = !isNew
    ? null
    : values.periodEnd === ''
      ? 'REQUIRED'
      : values.periodStart !== '' && values.periodEnd < values.periodStart
        ? 'BEFORE_START'
        : null;
  return {
    ...checkBase(values, today),
    periodStart: isNew && values.periodStart === '' ? 'REQUIRED' : null,
    periodEnd,
    measuredValue: values.valueStatus === 'MEASURED' ? checkKpiValue(values.value) : null,
  };
}

export function hasErrors(codes: FormCodes): boolean {
  return Object.values(codes).some((code) => typeof code === 'string');
}

/** A form starts from the DRAFT as stored: an Unknown figure is an empty input, never "0". */
export function financialValuesOf(
  update: FinancialProgressUpdateDetail,
  today: string,
): FinancialUpdateValues {
  return {
    valueStatus: update.valueStatus,
    actual: update.actualExpenditureToDateSar ?? '',
    forecast: update.forecastAtCompletionSar ?? '',
    sourceReference: update.sourceReference ?? '',
    asOfDate: update.asOfDate ?? today,
    narrative: update.narrative?.text ?? '',
  };
}

function narrativeOf(text: string, language: Language, before: NarrativeText | null | undefined) {
  return text.trim() === '' ? null : narrativeRequest(text, language, before ?? null);
}

export function toFinancialRequest(
  values: FinancialUpdateValues,
  language: Language,
  existing: FinancialProgressUpdateDetail,
): FinancialProgressUpdateRequest {
  const measured = values.valueStatus === 'MEASURED';
  return {
    actualExpenditureToDateSar: measured ? toMoney(values.actual) : null,
    forecastAtCompletionSar:
      measured && values.forecast.trim() !== '' ? toMoney(values.forecast) : null,
    valueStatus: values.valueStatus === '' ? 'MISSING' : values.valueStatus,
    narrative: narrativeOf(values.narrative, language, existing.narrative),
    sourceReference: optionalText(values.sourceReference),
    asOfDate: values.asOfDate,
  };
}

/** A new value starts with no status chosen and the current month; a DRAFT from what it holds. */
export function kpiValuesOf(
  measurement: KpiMeasurementDetail | null,
  today: string,
): KpiValueValues {
  if (measurement === null) {
    const month = monthOf(today);
    return {
      periodStart: month.start,
      periodEnd: month.end,
      valueStatus: '',
      value: '',
      asOfDate: today,
      narrative: '',
    };
  }
  return {
    periodStart: measurement.periodStart,
    periodEnd: measurement.periodEnd,
    valueStatus: measurement.valueStatus,
    value:
      measurement.measuredValue === null || measurement.measuredValue === undefined
        ? ''
        : String(measurement.measuredValue),
    asOfDate: measurement.asOfDate,
    narrative: measurement.narrative?.text ?? '',
  };
}

export function toKpiRequest(
  values: KpiValueValues,
  language: Language,
  existing: KpiMeasurementDetail | null,
): KpiMeasurementRequest {
  return {
    measuredValue: values.valueStatus === 'MEASURED' ? Number(values.value.trim()) : null,
    valueStatus: values.valueStatus === '' ? 'MISSING' : values.valueStatus,
    asOfDate: values.asOfDate,
    narrative: narrativeOf(values.narrative, language, existing?.narrative),
  };
}

export function toKpiCreateRequest(
  values: KpiValueValues,
  language: Language,
  kpiAssignmentId: string,
): KpiMeasurementCreateRequest {
  return {
    ...toKpiRequest(values, language, null),
    kpiAssignmentId,
    periodStart: values.periodStart,
    periodEnd: values.periodEnd,
  };
}
