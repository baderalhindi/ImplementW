import { formatPoints, PROJECT_STATUSES } from '@/features/projects/presentation.ts';
import { type ProjectLookups } from '@/features/projects/useProjectLookups.ts';
import { type OnPlanStatus } from '@/features/projects/api/types.ts';
import { sarCompact } from '@/shared/format/money.ts';
import { type Language, translate, type TranslationKey } from '@/shared/i18n/i18n.ts';
import { type SelectOption } from '@/shared/ui/FormFields.tsx';

import { labelOfValue } from '../presentation.ts';
import {
  type FieldAggregation,
  type FieldDescriptor,
  type FieldFilter,
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

/** Whether a value reads as a percentage (and a gauge or column chart scales it to 100). */
export function isPercentValue(aggregation: FieldAggregation, type: FieldType): boolean {
  return aggregation !== 'COUNT' && type === 'PERCENT';
}

/** A field value as a card, a chart and a table write it: a count as a count, else by the field's type; "not available" for null. */
export function formatFieldValue(
  aggregation: FieldAggregation,
  type: FieldType,
  figure: number | null,
  t: Translate,
): string {
  if (figure === null) {
    return t('dashboards.insights.notAvailable');
  }
  if (aggregation === 'COUNT') {
    return String(figure);
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
  SECTION: 0,
};

const GAUGE_AGGREGATIONS: readonly FieldAggregation[] = ['AVERAGE', 'MIN', 'MAX'];
const SHARE_AGGREGATIONS: readonly FieldAggregation[] = ['COUNT', 'SUM'];

/**
 * The calculations a field takes on a visual of `type`: its type's, narrowed for a gauge (a percentage's average, minimum or maximum) and for a
 * pie or donut (a count or a sum, which add up to a whole).
 */
export function aggregationsFor(field: FieldDescriptor, type: VisualType): FieldAggregation[] {
  switch (type) {
    case 'SECTION':
      return [];
    case 'GAUGE':
      return field.type === 'PERCENT'
        ? field.aggregations.filter((aggregation) => GAUGE_AGGREGATIONS.includes(aggregation))
        : [];
    case 'PIE':
    case 'DONUT':
      return field.aggregations.filter((aggregation) => SHARE_AGGREGATIONS.includes(aggregation));
    default:
      return field.aggregations;
  }
}

/** Whether a visual draws field values rather than the Portfolio figures (`measures`). */
export function drawsFields(visual: Pick<Visual, 'values'>): boolean {
  return (visual.values?.length ?? 0) > 0;
}

/** The types that group their projects, by a project attribute or a status field. */
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
export const NO_FIELD_DATA = { values: null, groupField: null, filters: null } as const;

/** A visual as it is compared and sent: its field data only where it has some, so a design read back equals the one sent. */
export function normalized<T extends Visual>(visual: T): T {
  const { values, groupField, filters, ...rest } = visual;
  return {
    ...rest,
    ...(values === undefined || values === null || values.length === 0 ? {} : { values }),
    ...(groupField === undefined || groupField === null ? {} : { groupField }),
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

/** A CODE field's value in words, as the dashboard's widgets label it. */
export function codeText(key: string, value: string, language: Language, t: Translate): string {
  return labelOfValue(fieldParts(key).projectionCode, value, null, language, t).text;
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
    case null:
      return (fields.find((field) => field.key === key)?.values ?? []).map((value) => ({
        value,
        label: codeText(key, value, language, t),
      }));
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
