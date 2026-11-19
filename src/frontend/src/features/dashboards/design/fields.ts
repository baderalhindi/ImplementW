import { formatPoints, PROJECT_STATUSES } from '@/features/projects/presentation.ts';
import { type ProjectLookups } from '@/features/projects/useProjectLookups.ts';
import { type OnPlanStatus } from '@/features/projects/api/types.ts';
import { sarCompact } from '@/shared/format/money.ts';
import { DATE_LOCALES, type Language, translate, type TranslationKey } from '@/shared/i18n/i18n.ts';
import { type SelectOption } from '@/shared/ui/FormFields.tsx';

import { labelOfValue } from '../presentation.ts';
import { sentAxes } from './axes.ts';
import {
  type DateUnit,
  type FieldAggregation,
  type FieldDescriptor,
  type FieldFilter,
  type FieldGrain,
  type FieldType,
  type FieldValue,
  type GroupBy,
  type Visual,
  type VisualResult,
  type VisualType,
} from './designApi.ts';

// The governed fields a designed visual draws (TASK-112, data fields contract): their names, the calculations each visual type takes, how a
// calculated value reads and what a visual's filters may test. The API checks the same rules (PortfolioDesignService); nothing here
// calculates a figure.

type Translate = (key: TranslationKey, params?: Record<string, string | number>) => string;

/** The projection a field key belongs to and the field's own code: `PROGRESS.PROJECT_HEALTH_STATUS.OVERALL_HEALTH`. */
export function fieldParts(key: string): { projectionCode: string; fieldCode: string } {
  const at = key.lastIndexOf('.');
  return { projectionCode: key.slice(0, at), fieldCode: key.slice(at + 1) };
}

/** A translation, or `fallback` where the resources have none (a field added to the register before its words). */
function wordOr(language: Language, key: string, fallback: string): string {
  const word = translate(language, key);
  return word === key ? fallback : word;
}

/** A projection's name as the Data panel heads its fields. */
export function sourceName(projectionCode: string, language: Language): string {
  return wordOr(
    language,
    `dashboards.designer.fieldSource.${projectionCode.replace('.', '_')}`,
    projectionCode,
  );
}

/** A field's name; its code where no name is registered yet. */
export function fieldName(key: string, language: Language): string {
  const { projectionCode, fieldCode } = fieldParts(key);
  return wordOr(
    language,
    `dashboards.designer.field.${projectionCode.replace('.', '_')}.${fieldCode}`,
    fieldCode,
  );
}

/** "Sum of Actual expenditure to date": a value's words, as a title, a series and a column heading read it. */
export function fieldValueText(value: FieldValue, language: Language): string {
  // translate() isolates each parameter for display; a stored title is plain text.
  return translate(language, `dashboards.designer.aggregationOf.${value.aggregation}`, {
    field: fieldName(value.field, language),
  }).replace(/[⁨⁩]/g, '');
}

/**
 * The type of a visual's value at `index`: the type the API returned with the figures (`valueTypes`, which the dashboard's readers get without
 * the designer's catalogue), else the catalogue's, else a count.
 */
export function valueTypeOf(
  visual: VisualResult,
  index: number,
  fields: readonly FieldDescriptor[],
): FieldType {
  const key = visual.values?.[index]?.field;
  return visual.valueTypes?.[index] ?? fields.find((field) => field.key === key)?.type ?? 'COUNT';
}

/** Whether a value reads as a percentage (and a gauge, column or line chart scales it to 100): a percentage's or a yes/no share. */
export function isPercentValue(aggregation: FieldAggregation, type: FieldType): boolean {
  return aggregation === 'SHARE_TRUE' || (aggregation !== 'COUNT' && type === 'PERCENT');
}

export function isDateType(type: FieldType): boolean {
  return type === 'DATE' || type === 'DATE_TIME';
}

const DAY_MS = 86_400_000;

/** A date's earliest or latest, which the API gives as the day number since 1970-01-01, as the app writes a date (Gregorian). */
export function formatDayNumber(day: number, language: Language): string {
  return new Intl.DateTimeFormat(DATE_LOCALES[language], {
    dateStyle: 'medium',
    timeZone: 'UTC',
  }).format(new Date(day * DAY_MS));
}

/**
 * A field value as a card, a chart and a table write it: a count as a count, a yes/no share as a percentage, a date's earliest or latest as a
 * date, else by the field's type; "not available" for null.
 */
export function formatFieldValue(
  aggregation: FieldAggregation,
  type: FieldType,
  figure: number | null,
  t: Translate,
  language: Language,
): string {
  if (figure === null) {
    return t('dashboards.insights.notAvailable');
  }
  if (aggregation === 'COUNT' || aggregation === 'COUNT_TRUE') {
    return String(figure);
  }
  if (aggregation === 'SHARE_TRUE') {
    return `${formatPoints(figure)}%`;
  }
  if (isDateType(type)) {
    return formatDayNumber(figure, language);
  }
  switch (type) {
    case 'SAR':
      return sarCompact(String(figure), t);
    case 'PERCENT':
      return `${formatPoints(figure)}%`;
    case 'DAYS':
      return t('dashboards.units.days', { value: formatPoints(figure) });
    default:
      return formatPoints(figure);
  }
}

/** How many field values a type draws: as many as its measures, but a table six. */
export const MAX_VALUES: Record<VisualType, number> = {
  CARD: 2,
  BAR: 1,
  COLUMN: 3,
  PIE: 1,
  DONUT: 1,
  TABLE: 6,
  GAUGE: 1,
  LINE: 3,
  SECTION: 0,
};

const GAUGE_AGGREGATIONS: readonly FieldAggregation[] = ['AVERAGE', 'MIN', 'MAX'];
const SHARE_AGGREGATIONS: readonly FieldAggregation[] = ['COUNT', 'SUM', 'COUNT_TRUE'];

/** The types that draw figures over time (snapshot-grain fields): each groups by a period or the snapshot's status. */
export const SNAPSHOT_TYPES: readonly VisualType[] = ['LINE', 'COLUMN', 'BAR', 'TABLE'];

/**
 * The calculations a field takes on a visual of `type`: its type's, narrowed for a gauge (a percentage's average, minimum or maximum, or a
 * yes/no share) and for a pie or donut (a count, a sum or a count of yes, which add up to a whole); none where the type does not draw the
 * field's grain.
 */
export function aggregationsFor(field: FieldDescriptor, type: VisualType): FieldAggregation[] {
  if (field.grain === 'SNAPSHOT' && !SNAPSHOT_TYPES.includes(type)) {
    return [];
  }
  switch (type) {
    case 'SECTION':
      return [];
    case 'GAUGE':
      return field.aggregations.filter((aggregation) =>
        field.type === 'PERCENT'
          ? GAUGE_AGGREGATIONS.includes(aggregation)
          : field.type === 'BOOLEAN' && aggregation === 'SHARE_TRUE',
      );
    case 'PIE':
    case 'DONUT':
      return field.aggregations.filter((aggregation) => SHARE_AGGREGATIONS.includes(aggregation));
    default:
      return field.aggregations;
  }
}

/** A visual's grain: its first value's field's (all its values and its group field are of one grain); a project's when unknown. */
export function grainOf(
  visual: Pick<Visual, 'values' | 'groupField'>,
  fields: readonly FieldDescriptor[],
): FieldGrain {
  const key = visual.values?.[0]?.field ?? visual.groupField ?? null;
  return fields.find((field) => field.key === key)?.grain ?? 'PROJECT';
}

/** Whether a field may group a visual: a status, a yes/no or a date field. */
function groups(field: FieldDescriptor): boolean {
  return field.type === 'CODE' || field.type === 'BOOLEAN' || isDateType(field.type);
}

/**
 * The fields a visual of this grain and type may group by: its grain's status, yes/no and date fields. A snapshot visual takes its own
 * projection's date and status fields only (there are no others of its grain).
 */
export function groupFieldsFor(
  grain: FieldGrain,
  type: VisualType,
  fields: readonly FieldDescriptor[],
): FieldDescriptor[] {
  return groupsByField(type)
    ? fields.filter((field) => field.grain === grain && groups(field))
    : [];
}

/** Whether a visual groups by its fields only, never by a project attribute: a line, and any visual over time. */
export function groupsByFieldOnly(type: VisualType, grain: FieldGrain): boolean {
  return type === 'LINE' || grain === 'SNAPSHOT';
}

/** The grouping a visual of this grain and type starts with when it must group by a field: a date (by month), else a status. */
export function firstGroupField(
  grain: FieldGrain,
  type: VisualType,
  fields: readonly FieldDescriptor[],
): string | null {
  if (!groupsByFieldOnly(type, grain)) {
    return null;
  }
  const own = groupFieldsFor(grain, type, fields);
  return (
    (
      own.find((field) => field.type === 'DATE') ??
      own.find((field) => field.type === 'DATE_TIME') ??
      own.find((field) => field.type === 'CODE')
    )?.key ?? null
  );
}

/** A visual grouped by `groupField` (null: by none), with a date unit where the field is a date: the one it had, else by month. */
export function groupedBy(
  visual: Visual,
  groupField: string | null,
  fields: readonly FieldDescriptor[],
): Pick<Visual, 'groupField' | 'groupDateUnit'> {
  const field = fields.find((candidate) => candidate.key === groupField);
  const isDate = field !== undefined && isDateType(field.type);
  return {
    groupField,
    groupDateUnit: isDate ? (visual.groupDateUnit ?? 'MONTH') : null,
  };
}

/** Whether a filter may narrow a visual of this grain: a project attribute or project field always, a snapshot status only over time. */
export function filterFits(
  filter: Pick<FieldFilter, 'field'>,
  grain: FieldGrain,
  fields: readonly FieldDescriptor[],
): boolean {
  const field = fields.find((candidate) => candidate.key === filter.field);
  return field === undefined || field.grain === 'PROJECT' || grain === 'SNAPSHOT';
}

/** The fields a visual's filter may test: the project fields of status, yes/no and date type, and over time the snapshot's statuses. */
export function filterFieldsFor(
  grain: FieldGrain,
  fields: readonly FieldDescriptor[],
): FieldDescriptor[] {
  return fields.filter(
    (field) =>
      (field.grain === 'PROJECT' && groups(field)) ||
      (field.grain === 'SNAPSHOT' && grain === 'SNAPSHOT' && field.type === 'CODE'),
  );
}

/** The years a date filter offers: the last ten, this one and the next five. */
export function filterYears(now: Date = new Date()): string[] {
  const year = now.getUTCFullYear();
  return Array.from({ length: 16 }, (_, index) => String(year - 10 + index));
}

/** A visual's first field value when it becomes a line: a published percentage's average over time where there is one. */
export function lineStart(fields: readonly FieldDescriptor[]): FieldValue | null {
  const field =
    fields.find((candidate) => candidate.grain === 'SNAPSHOT' && candidate.type === 'PERCENT') ??
    fields.find(
      (candidate) =>
        aggregationsFor(candidate, 'LINE').length > 0 &&
        firstGroupField(candidate.grain, 'LINE', fields) !== null,
    );
  if (field === undefined || firstGroupField(field.grain, 'LINE', fields) === null) {
    return null;
  }
  const allowed = aggregationsFor(field, 'LINE');
  const aggregation = allowed.includes('AVERAGE') ? 'AVERAGE' : allowed[0];
  return aggregation === undefined ? null : { field: field.key, aggregation };
}

/** Whether a visual draws field values rather than the Portfolio figures (`measures`). */
export function drawsFields(visual: Pick<Visual, 'values'>): boolean {
  return (visual.values?.length ?? 0) > 0;
}

/** The types that group their projects, by a project attribute or a status, yes/no or date field. */
export function groupsByField(type: VisualType): boolean {
  return type !== 'CARD' && type !== 'GAUGE' && type !== 'SECTION';
}

/** The field values that still fit a visual of `type`: each with a calculation the type takes, up to the type's count. */
export function valuesFor(
  values: readonly FieldValue[],
  type: VisualType,
  fields: readonly FieldDescriptor[],
): FieldValue[] {
  return values
    .flatMap((value) => {
      const field = fields.find((candidate) => candidate.key === value.field);
      if (field === undefined) {
        // Not in the catalogue (not loaded, or no longer offered): kept as it is, and the API says if it no longer saves.
        return [value];
      }
      const allowed = aggregationsFor(field, type);
      const [first] = allowed;
      if (first === undefined) {
        return [];
      }
      return [allowed.includes(value.aggregation) ? value : { ...value, aggregation: first }];
    })
    .slice(0, MAX_VALUES[type]);
}

/** A visual with no field data: what a visual drawing the Portfolio figures, or a section, carries. */
export const NO_FIELD_DATA = {
  values: null,
  groupField: null,
  groupDateUnit: null,
  filters: null,
} as const;

/**
 * A visual as it is compared and sent: its field data and widget code only where it has some, so a design read back equals the one sent; its axes as the API
 * takes them, null for the defaults (axes.ts).
 */
export function normalized<T extends Visual>(visual: T): T {
  const { values, groupField, groupDateUnit, filters, axes: _axes, widgetCode, ...rest } = visual;
  return {
    ...rest,
    axes: sentAxes(visual),
    ...(widgetCode === undefined || widgetCode === null ? {} : { widgetCode }),
    ...(values === undefined || values === null || values.length === 0 ? {} : { values }),
    ...(groupField === undefined || groupField === null ? {} : { groupField }),
    ...(groupDateUnit === undefined || groupDateUnit === null ? {} : { groupDateUnit }),
    ...(filters === undefined || filters === null || filters.length === 0 ? {} : { filters }),
  } as T;
}

/** The most filters a visual takes, and the most values one filter tests (contract rule 5). */
export const MAX_FILTERS = 5;
export const MAX_FILTER_VALUES = 20;

/** The project attributes a visual may filter on, as `PROJECT.<GROUP_BY>`; a stage is not offered, as the designer has no list of stages. */
export const FILTER_DIMENSIONS: readonly Exclude<GroupBy, 'NONE' | 'STAGE'>[] = [
  'STATUS',
  'ON_PLAN',
  'PORTFOLIO',
  'INITIATIVE',
  'SECTOR',
  'REGION',
  'CITY',
  'PRIORITY',
  'DEPARTMENT',
  'ENTITY',
];

const DIMENSION_PREFIX = 'PROJECT.';

export function dimensionKey(groupBy: GroupBy): string {
  return `${DIMENSION_PREFIX}${groupBy}`;
}

/** The project attribute a filter tests, or null when it tests a field. */
export function dimensionOf(key: string): GroupBy | null {
  const groupBy = key.slice(DIMENSION_PREFIX.length);
  return key.startsWith(DIMENSION_PREFIX) &&
    (FILTER_DIMENSIONS as readonly string[]).includes(groupBy)
    ? (groupBy as GroupBy)
    : null;
}

/** A filter's subject in words: the project attribute's name or the field's. */
export function filterFieldName(key: string, language: Language): string {
  const dimension = dimensionOf(key);
  return dimension === null
    ? fieldName(key, language)
    : translate(language, `dashboards.designer.groupBy.${dimension}`);
}

const ON_PLAN_VALUES: readonly OnPlanStatus[] = ['ON_TRACK', 'LATE', 'STALLED'];

/**
 * A CODE field's value in words: the designer's own word for a field whose projection has no vocabulary (the project profile's funding
 * source, participation and progress method), else as the dashboard's widgets label it.
 */
export function codeText(key: string, value: string, language: Language, t: Translate): string {
  const { projectionCode, fieldCode } = fieldParts(key);
  const own = `dashboards.designer.fieldValue.${projectionCode.replace('.', '_')}.${fieldCode}.${value}`;
  const word = translate(language, own);
  return word === own ? labelOfValue(projectionCode, value, null, language, t).text : word;
}

/** A yes/no value in words. */
export function booleanText(value: string, t: Translate): string {
  return t(value === 'TRUE' ? 'dashboards.designer.yes' : 'dashboards.designer.no');
}

/** A date group's key in words: a year as it is, a month (`2026-03`) as "Mar 2026" in the interface's language (Gregorian). */
export function dateKeyText(key: string, unit: DateUnit, language: Language): string {
  const [year, month] = key.split('-').map(Number);
  if (unit === 'YEAR' || year === undefined || month === undefined || Number.isNaN(month)) {
    return key;
  }
  return new Intl.DateTimeFormat(DATE_LOCALES[language], {
    month: 'short',
    year: 'numeric',
    timeZone: 'UTC',
  }).format(new Date(Date.UTC(year, month - 1, 1)));
}

/** The values a filter may test, with their words: a CODE field's codes, a status or on-plan word, an item, department or entity name. */
export function filterOptions(
  key: string,
  fields: readonly FieldDescriptor[],
  lookups: ProjectLookups,
  language: Language,
  t: Translate,
): SelectOption[] {
  switch (dimensionOf(key)) {
    case null: {
      const field = fields.find((candidate) => candidate.key === key);
      if (field?.type === 'BOOLEAN') {
        return ['TRUE', 'FALSE'].map((value) => ({ value, label: booleanText(value, t) }));
      }
      if (field !== undefined && isDateType(field.type)) {
        return filterYears().map((year) => ({ value: year, label: year }));
      }
      return (field?.values ?? []).map((value) => ({
        value,
        label: codeText(key, value, language, t),
      }));
    }
    case 'STATUS':
      return PROJECT_STATUSES.map((status) => ({
        value: status,
        label: t(`projects.status.${status}`),
      }));
    case 'ON_PLAN':
      return ON_PLAN_VALUES.map((status) => ({
        value: status,
        label: t(`projects.onPlan.${status}`),
      }));
    case 'PORTFOLIO':
      return lookups.options('STRATEGIC_PORTFOLIO');
    case 'INITIATIVE':
      return lookups.options('STRATEGIC_INITIATIVE');
    case 'SECTOR':
      return lookups.options('PROJECT_CLASSIFICATION');
    case 'REGION':
      return lookups.options('REGION');
    case 'CITY':
      return lookups.options('CITY');
    case 'PRIORITY':
      return lookups.options('PRIORITY');
    case 'DEPARTMENT':
      return lookups.departmentOptions;
    case 'ENTITY':
      return lookups.entityOptions;
    default:
      return [];
  }
}

/** A visual's filters with one replaced (`next`), or removed (`next` null). */
export function withFilter(
  filters: readonly FieldFilter[],
  at: number,
  next: FieldFilter | null,
): FieldFilter[] {
  return filters.flatMap((filter, index) =>
    index !== at ? [filter] : next === null ? [] : [next],
  );
}
