import { type CSSProperties } from 'react';

import { formatPoints } from '@/features/projects/presentation.ts';
import { NO_STAGE, NOT_MEASURED } from '@/features/projects/register/projectFilterValues.ts';
import { sarCompact } from '@/shared/format/money.ts';
import { type Language, translate, type TranslationKey } from '@/shared/i18n/i18n.ts';

import { type PortfolioFilters } from '../api/dashboardsApi.ts';
import { followUpPath, registerPath } from '../drillLinks.ts';
import {
  type DateUnit,
  type FieldDescriptor,
  type FieldValue,
  FOLLOW_UP_MEASURES,
  type GroupBy,
  type Measure,
  ON_PLAN_MEASURES,
  type Visual,
  type VisualGroup,
  type VisualType,
} from './designApi.ts';
import {
  booleanText,
  codeText,
  dateKeyText,
  drawsFields,
  fieldName,
  fieldValueText,
  filterFits,
  firstGroupField,
  grainOf,
  groupedBy,
  groupFieldsFor,
  groupsByField,
  groupsByFieldOnly,
  lineStart,
  NO_FIELD_DATA,
  valuesFor,
} from './fields.ts';
import { textBoxVisual } from './textBox.ts';
import {
  asColumnTable,
  columnHeading,
  columnName,
  columnsOf,
  groupsRows,
  NEW_TABLE_COLUMNS,
  withoutColumns,
} from './tableColumns.ts';

// What a visual may be and how its figures read: the same rules the API checks (PortfolioDesignService), so the designer offers only what
// saves. A card and a gauge show one figure, the charts a figure per group, a table either. The governed fields' own rules are in fields.ts.

type Translate = (key: TranslationKey, params?: Record<string, string | number>) => string;

export const VISUAL_TYPES: readonly VisualType[] = [
  'CARD',
  'BAR',
  'COLUMN',
  'LINE',
  'PIE',
  'DONUT',
  'TABLE',
  'GAUGE',
];

/** The Follow-up list's main reason each follow-up figure counts. */
const MAIN_REASON: Record<(typeof FOLLOW_UP_MEASURES)[number], string> = {
  FOLLOW_UP_STALLED: 'STALLED',
  FOLLOW_UP_SUSPENDED: 'SUSPENDED',
  FOLLOW_UP_LATE: 'LATE',
  FOLLOW_UP_ACTION_OVERDUE: 'ACTION_OVERDUE',
  FOLLOW_UP_OPEN_OBSTACLE: 'OPEN_OBSTACLE',
};

/** Whether a figure counts projects needing follow-up, so it opens the Follow-up list. */
export function countsFollowUp(measure: Measure): boolean {
  return measure === 'NEEDING_FOLLOW_UP' || measure in MAIN_REASON;
}

/** Where a figure comes from, for the words beside it where two read alike ("Stalled" on plan, "Stalled" needing follow-up); null for the others. */
export function measureSource(measure: string): 'ON_PLAN' | 'FOLLOW_UP' | null {
  if ((ON_PLAN_MEASURES as readonly string[]).includes(measure)) {
    return 'ON_PLAN';
  }
  return measure in MAIN_REASON ? 'FOLLOW_UP' : null;
}

/** The words beside a figure in the field lists: the section its boxes are in ("On-plan indicator"), null for the others. */
export function measureHint(measure: string, t: Translate): string | null {
  const source = measureSource(measure);
  return source === null ? null : t(`dashboards.designer.measureSource.${source}`);
}

export const MEASURES: readonly Measure[] = [
  'PROJECTS',
  'IN_PROGRESS',
  'VALUE_SAR',
  'AVERAGE_PLANNED',
  'AVERAGE_ACTUAL',
  'GAP_POINTS',
  'NEEDING_FOLLOW_UP',
  'OVERDUE_ACTIONS',
  ...ON_PLAN_MEASURES,
  ...FOLLOW_UP_MEASURES,
];

export const GROUP_BYS: readonly GroupBy[] = [
  'NONE',
  'PORTFOLIO',
  'INITIATIVE',
  'SECTOR',
  'DEPARTMENT',
  'ENTITY',
  'STATUS',
  'STAGE',
  'ON_PLAN',
  'REGION',
  'CITY',
  'PRIORITY',
];

const PERCENT_MEASURES: readonly Measure[] = ['AVERAGE_PLANNED', 'AVERAGE_ACTUAL'];

/** How many measures a type draws: bars, pies, donuts and gauges one, a card two (the second under the first), columns and tables three. */
export const MAX_MEASURES: Record<VisualType, number> = {
  CARD: 2,
  BAR: 1,
  COLUMN: 3,
  PIE: 1,
  DONUT: 1,
  TABLE: 3,
  GAUGE: 1,
  LINE: 0,
  SECTION: 0,
  CONTAINER: 0,
  TEXT: 0,
};

/** The size a new visual of a type takes, in columns of twelve and rows. */
export const DEFAULT_SIZE: Record<VisualType, { width: number; height: number }> = {
  CARD: { width: 4, height: 2 },
  BAR: { width: 6, height: 5 },
  COLUMN: { width: 6, height: 5 },
  PIE: { width: 4, height: 5 },
  DONUT: { width: 4, height: 5 },
  TABLE: { width: 6, height: 5 },
  GAUGE: { width: 3, height: 4 },
  LINE: { width: 6, height: 5 },
  SECTION: { width: 12, height: 6 },
  CONTAINER: { width: 6, height: 6 },
  TEXT: { width: 6, height: 2 },
};

export const COLUMNS = 12;
export const MAX_HEIGHT = 24;
export const MAX_VISUALS = 60;

/** The groupings by a project attribute a type takes; a line groups by a field only. */
export function groupBysFor(type: VisualType): readonly GroupBy[] {
  if (
    type === 'CARD' ||
    type === 'GAUGE' ||
    type === 'LINE' ||
    type === 'SECTION' ||
    type === 'CONTAINER' ||
    type === 'TEXT'
  ) {
    return ['NONE'];
  }
  return type === 'TABLE' ? GROUP_BYS : GROUP_BYS.filter((groupBy) => groupBy !== 'NONE');
}

/** Measures that add up across groups: a share of their whole is meaningful, so a pie or donut may draw them. */
const ADDITIVE_MEASURES: readonly Measure[] = [
  'PROJECTS',
  'IN_PROGRESS',
  'VALUE_SAR',
  'NEEDING_FOLLOW_UP',
  'OVERDUE_ACTIONS',
  ...ON_PLAN_MEASURES,
  ...FOLLOW_UP_MEASURES,
];

/** The measures a type may draw: a gauge a percentage, a pie or donut a total that adds up; a line draws fields only. */
export function measuresFor(type: VisualType): readonly Measure[] {
  if (type === 'SECTION' || type === 'LINE' || type === 'CONTAINER' || type === 'TEXT') {
    return [];
  }
  if (type === 'GAUGE') {
    return PERCENT_MEASURES;
  }
  return type === 'PIE' || type === 'DONUT' ? ADDITIVE_MEASURES : MEASURES;
}

export function isPercent(measure: Measure): boolean {
  return PERCENT_MEASURES.includes(measure);
}

/**
 * A visual drawing `values` (all of one grain) on a visual of `type`: its grouping kept where it still fits, else the grain's first group field
 * where the type groups by fields only (a line, a chart over time), else the type's first project grouping; and the filters that still fit.
 * Null when the visual must group by a field and the catalogue has none for it.
 */
export function withValues(
  visual: Visual,
  type: VisualType,
  values: FieldValue[],
  fields: readonly FieldDescriptor[],
): Visual | null {
  const grain = grainOf({ values }, fields);
  const current = visual.groupField ?? null;
  const fieldsOnly = groupsByFieldOnly(type, grain);
  const keeps =
    current !== null && groupFieldsFor(grain, type, fields).some((field) => field.key === current);
  const groupField = keeps ? current : firstGroupField(grain, type, fields);
  if (fieldsOnly && groupField === null) {
    return null;
  }
  const groupings = groupBysFor(type);
  const filters = (visual.filters ?? []).filter((filter) => filterFits(filter, grain, fields));
  return {
    ...visual,
    visualType: type,
    measures: [],
    values,
    ...groupedBy(visual, groupField, fields),
    groupBy:
      groupField !== null
        ? 'NONE'
        : groupings.includes(visual.groupBy) && current === null
          ? visual.groupBy
          : firstGrouping(type),
    filters: filters.length === 0 ? null : filters,
  };
}

/** A visual as a line: its field values that fit (or the catalogue's first line), grouped by a field; null when the catalogue offers none. */
export function asLine(visual: Visual, fields: readonly FieldDescriptor[]): Visual | null {
  const kept = drawsFields(visual) ? valuesFor(visual.values ?? [], 'LINE', fields) : [];
  const start = lineStart(fields);
  const values = kept.length > 0 ? kept : start === null ? [] : [start];
  return values.length === 0 ? null : withValues(visual, 'LINE', values, fields);
}

/**
 * A visual changed to `type`, keeping what still fits: its measures (or field values) up to the type's count, with a calculation the type takes,
 * and a grouping the type takes. Field values none of which fit fall back to the type's first measure; a line, which draws fields only, is the
 * visual unchanged when the catalogue offers it none.
 */
export function withType(
  before: Visual,
  type: VisualType,
  fields: readonly FieldDescriptor[] = [],
): Visual {
  // A table draws columns (tableColumns.ts): its grouping and figures become columns, and back again for any other type.
  if (type === 'TABLE') {
    return asColumnTable(before, columnsOf(before, fields), fields);
  }
  const visual = withoutColumns(before);
  if (type === 'LINE') {
    return asLine(visual, fields) ?? visual;
  }
  const groupings = groupBysFor(type);
  if (drawsFields(visual)) {
    const values = valuesFor(visual.values ?? [], type, fields);
    const next =
      values.length > 0
        ? withValues(
            groupsByField(type) ? visual : { ...visual, groupField: null },
            type,
            values,
            fields,
          )
        : null;
    if (next !== null) {
      return next;
    }
  }
  const allowed = measuresFor(type);
  const measures = visual.measures
    .filter((measure) => allowed.includes(measure))
    .slice(0, MAX_MEASURES[type]);
  return {
    ...visual,
    ...NO_FIELD_DATA,
    visualType: type,
    measures: measures.length > 0 ? measures : [allowed[0] ?? 'PROJECTS'],
    groupBy: groupings.includes(visual.groupBy) ? visual.groupBy : firstGrouping(type),
  };
}

/**
 * "Projects by portfolio", "Sum of Actual expenditure to date by Overall health": the title a visual takes from its first measure or field value
 * and its grouping until someone writes another.
 */
export function autoTitle(
  visual: Pick<Visual, 'measures' | 'groupBy' | 'values' | 'groupField' | 'columns'>,
  language: Language,
): string {
  if ((visual.columns?.length ?? 0) > 0) {
    // A column table: its first calculated column by its first grouping one, "Projects by Sector".
    const columns = visual.columns ?? [];
    const figure = columns.find((column) => !groupsRows(column));
    const grouping = columns.find(groupsRows);
    const measure =
      figure === undefined
        ? translate(language, 'dashboards.designer.measure.PROJECTS')
        : columnHeading(figure, language);
    return grouping === undefined
      ? measure
      : translate(language, 'dashboards.designer.titleByGroup', {
          measure,
          group: columnName(grouping, language),
        }).replace(/[\u2068\u2069]/g, '');
  }
  const [value] = visual.values ?? [];
  const measure =
    value === undefined
      ? translate(language, `dashboards.designer.measure.${visual.measures[0] ?? 'PROJECTS'}`)
      : fieldValueText(value, language);
  const group =
    visual.groupField !== undefined && visual.groupField !== null
      ? fieldName(visual.groupField, language)
      : visual.groupBy === 'NONE'
        ? null
        : translate(language, `dashboards.designer.groupBy.${visual.groupBy}`);
  const title =
    group === null
      ? measure
      : translate(language, 'dashboards.designer.titleByGroup', { measure, group });
  // translate() isolates each parameter for display; a stored title is plain text.
  return title.replace(/[\u2068\u2069]/g, '');
}

/** Both titles of a visual follow its measures and grouping while they are still the automatic ones. */
export function retitled(before: Visual, after: Visual): Visual {
  const follows = (language: Language) => before.title[language] === autoTitle(before, language);
  return {
    ...after,
    title: {
      ar: follows('ar') ? autoTitle(after, 'ar') : after.title.ar,
      en: follows('en') ? autoTitle(after, 'en') : after.title.en,
    },
  };
}

/** The grouping a visual of `type` starts with: by portfolio where the type is grouped, as the dashboard's charts are. */
export function firstGrouping(type: VisualType): GroupBy {
  return groupBysFor(type).includes('PORTFOLIO') ? 'PORTFOLIO' : 'NONE';
}

/**
 * A new visual of `type` with sensible first values, at the first free place; null when the grid has no room left for it. A line starts from
 * the catalogue (`fields`): the gallery offers it only when `asLine` finds one.
 */
export function newVisual(
  type: VisualType,
  visuals: Visual[],
  fields: readonly FieldDescriptor[] = [],
): Visual | null {
  const { width, height } = DEFAULT_SIZE[type];
  const place = freePlace(visuals, width, height);
  if (place === null) {
    return null;
  }
  if (type === 'TEXT') {
    return textBoxVisual(place, width, height);
  }
  if (type === 'LINE') {
    const line = asLine(
      {
        title: { ar: '', en: '' },
        visualType: 'LINE',
        measures: [],
        groupBy: 'NONE',
        layoutColumn: place.column,
        layoutRow: place.row,
        layoutWidth: width,
        layoutHeight: height,
      },
      fields,
    );
    return line === null
      ? null
      : { ...line, title: { ar: autoTitle(line, 'ar'), en: autoTitle(line, 'en') } };
  }
  if (type === 'TABLE') {
    // A table starts as Power BI's: the sector, Don't summarize, and its projects.
    const table: Visual = {
      title: { ar: '', en: '' },
      visualType: 'TABLE',
      measures: [],
      groupBy: 'NONE',
      layoutColumn: place.column,
      layoutRow: place.row,
      layoutWidth: width,
      layoutHeight: height,
      columns: [...NEW_TABLE_COLUMNS],
      showTotals: true,
    };
    return { ...table, title: { ar: autoTitle(table, 'ar'), en: autoTitle(table, 'en') } };
  }
  const measures: Measure[] =
    type === 'COLUMN'
      ? ['AVERAGE_PLANNED', 'AVERAGE_ACTUAL']
      : type === 'GAUGE'
        ? ['AVERAGE_ACTUAL']
        : ['PROJECTS'];
  const groupBy = firstGrouping(type);
  return {
    title: {
      ar: autoTitle({ measures, groupBy }, 'ar'),
      en: autoTitle({ measures, groupBy }, 'en'),
    },
    visualType: type,
    measures,
    groupBy,
    layoutColumn: place.column,
    layoutRow: place.row,
    layoutWidth: width,
    layoutHeight: height,
  };
}

export interface Box {
  layoutColumn: number;
  layoutRow: number;
  layoutWidth: number;
  layoutHeight: number;
}

/**
 * A layout value as the API takes it (20 Nov 2026, docs/architecture/dashboard-designer-decimal-layout.md): places and sizes are decimals with
 * one decimal place, so every computed one is rounded to a tenth and no 4.699999 is sent.
 */
export function tenth(value: number): number {
  return Math.round(value * 10) / 10;
}

/** A value in whole tenths, so decimals compare exactly (0.7 + 0.1 is not 0.8 in floating point). */
function tenths(value: number): number {
  return Math.round(value * 10);
}

/** Whether two boxes overlap by more than nothing: boxes that only touch do not. */
export function overlaps(a: Box, b: Box): boolean {
  return (
    tenths(a.layoutColumn) < tenths(b.layoutColumn) + tenths(b.layoutWidth) &&
    tenths(b.layoutColumn) < tenths(a.layoutColumn) + tenths(a.layoutWidth) &&
    tenths(a.layoutRow) < tenths(b.layoutRow) + tenths(b.layoutHeight) &&
    tenths(b.layoutRow) < tenths(a.layoutRow) + tenths(a.layoutHeight)
  );
}

/** Whether `box` lies inside the grid and on no visual but the one at `self`. */
export function fits(box: Box, visuals: Box[], self: number | null): boolean {
  return (
    box.layoutColumn >= 0 &&
    box.layoutRow >= 0 &&
    box.layoutWidth >= 1 &&
    box.layoutHeight >= 1 &&
    box.layoutHeight <= MAX_HEIGHT &&
    tenths(box.layoutColumn) + tenths(box.layoutWidth) <= tenths(COLUMNS) &&
    visuals.every((other, index) => index === self || !overlaps(box, other))
  );
}

/** Sorted, distinct values. */
function ascending(values: number[]): number[] {
  return [...new Set(values.map(tenth))].sort((a, b) => a - b);
}

/**
 * The first place, from the top and then from the reading start, a box of `width` × `height` fits beside `boxes`: whole rows and columns are
 * tried, and the edges of the boxes there are, so a box goes right beside (or under) one of a decimal size; below the lowest box it always fits.
 */
function firstFit(boxes: Box[], width: number, height: number): { column: number; row: number } {
  const bottom = rowsOf(boxes);
  const rows = ascending([
    ...Array.from({ length: Math.ceil(bottom) + 1 }, (_, row) => row),
    ...boxes.map((box) => box.layoutRow + box.layoutHeight),
  ]);
  const columns = ascending([
    ...Array.from({ length: COLUMNS }, (_, column) => column),
    ...boxes.map((box) => box.layoutColumn + box.layoutWidth),
  ]).filter((column) => tenths(column) + tenths(width) <= tenths(COLUMNS));
  for (const row of rows) {
    for (const column of columns) {
      const box = {
        layoutColumn: column,
        layoutRow: row,
        layoutWidth: width,
        layoutHeight: height,
      };
      if (boxes.every((other) => !overlaps(box, other))) {
        return { column, row };
      }
    }
  }
  return { column: 0, row: bottom };
}

/** The first place, row by row from the top, a box of this size fits; null when the design is full or the box is larger than a visual may be. */
export function freePlace(
  visuals: Box[],
  width: number,
  height: number,
): { column: number; row: number } | null {
  if (visuals.length >= MAX_VISUALS || width > COLUMNS || height > MAX_HEIGHT) {
    return null;
  }
  return firstFit(visuals, width, height);
}

/** The rows the design takes: the bottom of its lowest visual. */
export function rowsOf(visuals: Box[]): number {
  return tenth(visuals.reduce((most, box) => Math.max(most, box.layoutRow + box.layoutHeight), 0));
}

/** A measure's figure as a card, a chart and a table write it; "not available" when it cannot be calculated or may not be seen. */
export function formatValue(measure: Measure, value: number | null, t: Translate): string {
  if (value === null) {
    return t('dashboards.insights.notAvailable');
  }
  switch (measure) {
    case 'VALUE_SAR':
      return sarCompact(String(value), t);
    case 'AVERAGE_PLANNED':
    case 'AVERAGE_ACTUAL':
      return `${formatPoints(value)}%`;
    case 'GAP_POINTS':
      return t('projects.followUp.gapValue', { points: formatPoints(value, true) });
    default:
      return String(value);
  }
}

/** Names of master data items, departments and entities, as `useProjectLookups` gives them; null for an id it has not loaded. */
export interface GroupNames {
  itemLabel: (id: string) => string | null;
  departmentName: (id: string) => string | null;
  entityName: (id: string) => string | null;
}

/**
 * A group's words when the visual groups by a field: a period ("Mar 2026", "2026") for a date field (the visual's `groupDateUnit` says so),
 * Yes or No for a yes/no field, a status's word, or "not set" for the projects whose value is not revealed.
 */
export function fieldGroupText(
  groupField: string,
  dateUnit: DateUnit | null,
  group: VisualGroup,
  t: Translate,
  language: Language,
): string {
  if (group.key === null) {
    return t('dashboards.designer.notSet');
  }
  if (dateUnit !== null) {
    return dateKeyText(group.key, dateUnit, language);
  }
  return group.key === 'TRUE' || group.key === 'FALSE'
    ? booleanText(group.key, t)
    : codeText(groupField, group.key, language, t);
}

/** A group's words: its item, department or entity name, its status, stage or on-plan label, or "not set" for the projects with none. */
export function groupText(
  groupBy: GroupBy,
  group: VisualGroup,
  names: GroupNames,
  t: Translate,
  language: Language,
): string {
  const key = group.key;
  if (key === null) {
    // The projects with no value, named as the dashboard always named them.
    switch (groupBy) {
      case 'NONE':
        return t('dashboards.designer.total');
      case 'STAGE':
        return t('dashboards.insights.noCurrentStage');
      case 'ON_PLAN':
        return t('dashboards.insights.notMeasured');
      default:
        return t('dashboards.designer.notSet');
    }
  }
  switch (groupBy) {
    case 'STATUS':
      return t(`projects.status.${key}` as TranslationKey);
    case 'ON_PLAN':
      return t(`projects.onPlan.${key}` as TranslationKey);
    case 'STAGE':
      return group.label?.[language] ?? key;
    case 'DEPARTMENT':
      return names.departmentName(key) ?? key;
    case 'ENTITY':
      return names.entityName(key) ?? key;
    default:
      return names.itemLabel(key) ?? key;
  }
}

/**
 * A visual's place on the twelve-column grid, its first column and row counted from 1: styles.css positions it from these decimals; on a
 * narrow screen the visuals stack in reading order instead.
 */
export function gridPlace(visual: Box): CSSProperties {
  return {
    '--visual-column': String(tenth(visual.layoutColumn + 1)),
    '--visual-row': String(tenth(visual.layoutRow + 1)),
    '--visual-width': String(tenth(visual.layoutWidth)),
    '--visual-height': String(tenth(visual.layoutHeight)),
  } as CSSProperties;
}

/** The rows a grid draws, so it is as tall as its lowest visual (styles.css). */
export function gridRows(rows: number): CSSProperties {
  return { '--grid-rows': String(tenth(rows)) } as CSSProperties;
}

/** A key for a visual: its place on the grid, which no other visual shares. */
export function placeKey(visual: Visual): string {
  return `${String(visual.layoutRow)}:${String(visual.layoutColumn)}`;
}

/** The register's filter for each grouping, under the list's own name (review round 8, point 01). */
const GROUP_FILTER: Record<Exclude<GroupBy, 'NONE'>, string> = {
  PORTFOLIO: 'strategicPortfolioItemId',
  INITIATIVE: 'strategicInitiativeItemId',
  SECTOR: 'classificationItemId',
  DEPARTMENT: 'departmentId',
  ENTITY: 'externalEntityId',
  STATUS: 'status',
  STAGE: 'stage',
  ON_PLAN: 'onPlan',
  REGION: 'regionItemId',
  CITY: 'cityItemId',
  PRIORITY: 'priorityItemId',
};

/**
 * The records a figure counts, as round 8 point 01 has every count open them: the register (or the Follow-up list) with the dashboard's filters,
 * the group's own filter and what the measure counts. None for a group the lists cannot filter by (an item no project names) or for overdue
 * actions, which no list filters by.
 */
export function drillPath(
  measure: Measure,
  groupBy: GroupBy,
  key: string | null,
  filters: PortfolioFilters,
): string | undefined {
  let group: Record<string, string> = {};
  if (groupBy !== 'NONE') {
    const value =
      key ?? (groupBy === 'STAGE' ? NO_STAGE : groupBy === 'ON_PLAN' ? NOT_MEASURED : null);
    if (value === null) {
      return undefined;
    }
    group = { [GROUP_FILTER[groupBy]]: value };
  }
  switch (measure) {
    case 'OVERDUE_ACTIONS':
      return undefined;
    case 'NEEDING_FOLLOW_UP':
      return followUpPath(filters, group);
    case 'FOLLOW_UP_STALLED':
    case 'FOLLOW_UP_SUSPENDED':
    case 'FOLLOW_UP_LATE':
    case 'FOLLOW_UP_ACTION_OVERDUE':
    case 'FOLLOW_UP_OPEN_OBSTACLE':
      return followUpPath(filters, { ...group, mainReason: MAIN_REASON[measure] });
    case 'ON_TRACK':
    case 'LATE':
    case 'STALLED':
    case 'NOT_MEASURED':
      // The register's on-plan filter takes the figure's code (NOT_MEASURED included); a group of another on-plan status counts none.
      return groupBy === 'ON_PLAN' && group.onPlan !== measure
        ? undefined
        : registerPath(filters, { ...group, onPlan: measure });
    case 'IN_PROGRESS':
      return registerPath(filters, { status: 'ACTIVE', ...group });
    case 'VALUE_SAR':
      return registerPath(filters, { sort: 'value:desc', ...group });
    case 'AVERAGE_PLANNED':
    case 'AVERAGE_ACTUAL':
    case 'GAP_POINTS':
      return registerPath(filters, { measured: 'true', ...group });
    case 'PROJECTS':
      return registerPath(filters, group);
  }
}

/** A box kept inside the grid: one to twelve columns starting within it, one to twenty-four rows from the top, each to a tenth. */
export function clamped(box: Box): Box {
  const width = Math.min(COLUMNS, Math.max(1, tenth(box.layoutWidth)));
  return {
    layoutWidth: width,
    layoutHeight: Math.min(MAX_HEIGHT, Math.max(1, tenth(box.layoutHeight))),
    layoutColumn: Math.min(tenth(COLUMNS - width), Math.max(0, tenth(box.layoutColumn))),
    layoutRow: Math.max(0, tenth(box.layoutRow)),
  };
}

/**
 * The visuals laid out in their order with no room left between them (the user, 18 Nov 2026: "I don't want white spaces"): each, in turn,
 * takes the first place from the top, then from the reading start, where it fits beside those before it, so a gap is filled by whatever fits.
 * Every visual keeps the size it was given (the user, 19 Nov 2026: a visual made smaller must stay so when another is sized): room no visual
 * fits stays empty. Every change of order, size or membership is laid out again, so the others move at once; the dashboard lays out what the
 * caller sees the same way.
 */
export function packed<T extends Box>(visuals: T[]): T[] {
  const placed: T[] = [];
  for (const visual of visuals) {
    const size = clamped(visual);
    const spot = firstFit(placed, size.layoutWidth, size.layoutHeight);
    placed.push({ ...visual, ...size, layoutColumn: spot.column, layoutRow: spot.row });
  }
  return placed;
}

/**
 * Free placement (the user, 19 Nov 2026: "no worries if there is white space, I want free dragging"): the visual at `index` stays where it
 * was put, and every visual it covers moves straight down to just below it, then whatever those cover in turn; nothing else moves and empty
 * room stays. The result is in reading order (top to bottom, then from the reading start), the order a screen reader and the API take.
 */
export function settled<T extends Box>(visuals: T[], index: number): T[] {
  const anchor = visuals[index];
  if (anchor === undefined) {
    return visuals;
  }
  const fixed: T[] = [anchor];
  const others = visuals
    .filter((_, at) => at !== index)
    .sort((a, b) => a.layoutRow - b.layoutRow || a.layoutColumn - b.layoutColumn);
  for (const visual of others) {
    let box = visual;
    for (let covered = fixed.filter((other) => overlaps(box, other)); covered.length > 0;) {
      box = {
        ...box,
        layoutRow: tenth(Math.max(...covered.map((other) => other.layoutRow + other.layoutHeight))),
      };
      covered = fixed.filter((other) => overlaps(box, other));
    }
    fixed.push(box);
  }
  return inReadingOrder(fixed);
}

/** The visuals top to bottom, then from the reading start. */
export function inReadingOrder<T extends Box>(visuals: T[]): T[] {
  return [...visuals].sort((a, b) => a.layoutRow - b.layoutRow || a.layoutColumn - b.layoutColumn);
}

/**
 * The visuals where they were placed, with the bands no visual covers taken out (of any height: places are decimals), so a visual the caller
 * may not see (left out by the dashboard) leaves no empty band; room beside a visual stays as the designer left it.
 */
export function withoutEmptyRows<T extends Box>(visuals: T[]): T[] {
  // The rows covered, as merged spans in tenths of a row.
  const spans: { start: number; end: number }[] = [];
  for (const visual of [...visuals].sort((a, b) => a.layoutRow - b.layoutRow)) {
    const start = tenths(visual.layoutRow);
    const end = start + tenths(visual.layoutHeight);
    const last = spans.at(-1);
    if (last !== undefined && start <= last.end) {
      last.end = Math.max(last.end, end);
    } else {
      spans.push({ start, end });
    }
  }
  // Each visual rises by the empty room above it: the gaps before the span it starts in.
  const emptyAbove = (row: number) => {
    let empty = 0;
    let reached = 0;
    for (const span of spans) {
      if (span.start > row) {
        break;
      }
      empty += span.start - reached;
      reached = span.end;
    }
    return empty;
  };
  return inReadingOrder(
    visuals.map((visual) => ({
      ...visual,
      layoutRow: (tenths(visual.layoutRow) - emptyAbove(tenths(visual.layoutRow))) / 10,
    })),
  );
}
