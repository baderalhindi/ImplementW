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
  type GroupBy,
  type Measure,
  type Section,
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

export const MEASURES: readonly Measure[] = [
  'PROJECTS',
  'IN_PROGRESS',
  'VALUE_SAR',
  'AVERAGE_PLANNED',
  'AVERAGE_ACTUAL',
  'GAP_POINTS',
  'NEEDING_FOLLOW_UP',
  'OVERDUE_ACTIONS',
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
};

/** The size a new visual of a type takes, in columns of twelve and rows. */
const DEFAULT_SIZE: Record<VisualType, { width: number; height: number }> = {
  CARD: { width: 4, height: 2 },
  BAR: { width: 6, height: 5 },
  COLUMN: { width: 6, height: 5 },
  PIE: { width: 4, height: 5 },
  DONUT: { width: 4, height: 5 },
  TABLE: { width: 6, height: 5 },
  GAUGE: { width: 3, height: 4 },
  LINE: { width: 6, height: 5 },
  SECTION: { width: 12, height: 6 },
};

export const COLUMNS = 12;
export const MAX_HEIGHT = 24;
export const MAX_VISUALS = 60;

/** The groupings by a project attribute a type takes; a line groups by a field only. */
export function groupBysFor(type: VisualType): readonly GroupBy[] {
  if (type === 'CARD' || type === 'GAUGE' || type === 'LINE' || type === 'SECTION') {
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
];

/** The measures a type may draw: a gauge a percentage, a pie or donut a total that adds up; a line draws fields only. */
export function measuresFor(type: VisualType): readonly Measure[] {
  if (type === 'SECTION' || type === 'LINE') {
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
  visual: Visual,
  type: VisualType,
  fields: readonly FieldDescriptor[] = [],
): Visual {
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
  visual: Pick<Visual, 'measures' | 'groupBy' | 'values' | 'groupField'>,
  language: Language,
): string {
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

interface Box {
  layoutColumn: number;
  layoutRow: number;
  layoutWidth: number;
  layoutHeight: number;
}

function overlaps(a: Box, b: Box): boolean {
  return (
    a.layoutColumn < b.layoutColumn + b.layoutWidth &&
    b.layoutColumn < a.layoutColumn + a.layoutWidth &&
    a.layoutRow < b.layoutRow + b.layoutHeight &&
    b.layoutRow < a.layoutRow + a.layoutHeight
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
    box.layoutColumn + box.layoutWidth <= COLUMNS &&
    visuals.every((other, index) => index === self || !overlaps(box, other))
  );
}

/** The first place, row by row from the top, a box of this size fits. */
export function freePlace(
  visuals: Box[],
  width: number,
  height: number,
): { column: number; row: number } | null {
  if (visuals.length >= MAX_VISUALS) {
    return null;
  }
  const bottom = visuals.reduce((most, box) => Math.max(most, box.layoutRow + box.layoutHeight), 0);
  for (let row = 0; row <= bottom; row++) {
    for (let column = 0; column + width <= COLUMNS; column++) {
      if (
        fits(
          { layoutColumn: column, layoutRow: row, layoutWidth: width, layoutHeight: height },
          visuals,
          null,
        )
      ) {
        return { column, row };
      }
    }
  }
  return null;
}

/** The rows the design takes: the bottom of its lowest visual. */
export function rowsOf(visuals: Box[]): number {
  return visuals.reduce((most, box) => Math.max(most, box.layoutRow + box.layoutHeight), 0);
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

/** A visual's place on the twelve-column grid; on a narrow screen the visuals stack in reading order instead (styles.css). */
export function gridPlace(visual: Visual): CSSProperties {
  return {
    '--visual-column': String(visual.layoutColumn + 1),
    '--visual-row': String(visual.layoutRow + 1),
    '--visual-width': String(visual.layoutWidth),
    '--visual-height': String(visual.layoutHeight),
  } as CSSProperties;
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

/** The dashboard's own sections, in the order the gallery offers them (18 Nov 2026). */
export const SECTIONS: readonly Section[] = [
  'PORTFOLIO_TABLE',
  'INITIATIVE_TABLE',
  'SECTOR_TABLE',
  'STATUS_TABLE',
  'CURRENT_STAGE_TABLE',
  'ON_PLAN_COUNTS',
  'FOLLOW_UP_REASONS',
  'TOP_FOLLOW_UP',
  'ENTITY_COMPLETION',
  'PROJECT_LOCATIONS',
  'DASHBOARD_WIDGETS',
  'ENTITY_COMPLETION_TABLE',
];

/** A section at the first free place, titled as the dashboard names it; null when the grid has no room. */
export function newSection(section: Section, visuals: Visual[]): Visual | null {
  const { width, height } = DEFAULT_SIZE.SECTION;
  const place = freePlace(visuals, width, height);
  if (place === null) {
    return null;
  }
  const title = (language: Language) =>
    translate(language, `dashboards.designer.section.${section}`).replace(/[\u2068\u2069]/g, '');
  return {
    title: { ar: title('ar'), en: title('en') },
    visualType: 'SECTION',
    measures: [],
    groupBy: 'NONE',
    section,
    layoutColumn: place.column,
    layoutRow: place.row,
    layoutWidth: width,
    layoutHeight: height,
  };
}

/** A box kept inside the grid: one to twelve columns starting within it, one to twelve rows from the top. */
function clamped(box: Box): Box {
  const width = Math.min(COLUMNS, Math.max(1, Math.round(box.layoutWidth)));
  return {
    layoutWidth: width,
    layoutHeight: Math.min(MAX_HEIGHT, Math.max(1, Math.round(box.layoutHeight))),
    layoutColumn: Math.min(COLUMNS - width, Math.max(0, Math.round(box.layoutColumn))),
    layoutRow: Math.max(0, Math.round(box.layoutRow)),
  };
}

/**
 * The visuals laid out in their order with no room left between them (the user, 18 Nov 2026: "I don't want white spaces"): each, in turn,
 * takes the first place from the top, then from the reading start, where it fits beside those before it, so a gap is filled by whatever fits.
 * Room no visual fits is then taken by its neighbour: a visual widens toward the reading end, then grows down, into empty cells beside or under
 * it, never past the grid's bottom. `keep` (the visual just sized) keeps the size it was given. Every change of order, size or membership is
 * laid out again, so the others move at once; the dashboard lays out what the caller sees the same way.
 */
export function packed<T extends Box>(visuals: T[], keep: number | null = null): T[] {
  const placed: T[] = [];
  for (const visual of visuals) {
    const size = clamped(visual);
    let spot: Box | undefined;
    for (let row = 0; spot === undefined; row++) {
      for (let column = 0; column + size.layoutWidth <= COLUMNS && spot === undefined; column++) {
        const box = { ...size, layoutColumn: column, layoutRow: row };
        if (placed.every((other) => !overlaps(box, other))) {
          spot = box;
        }
      }
    }
    placed.push({ ...visual, ...spot });
  }
  return filled(placed, keep);
}

/** The cells no visual covers, closed by the visual beside (toward the reading end) or above them; see `packed`. */
function filled<T extends Box>(visuals: T[], keep: number | null): T[] {
  const next = [...visuals];
  const bottom = rowsOf(next);
  const free = (box: Box, self: number) =>
    box.layoutColumn + box.layoutWidth <= COLUMNS &&
    box.layoutRow + box.layoutHeight <= bottom &&
    next.every((other, at) => at === self || !overlaps(box, other));
  next.forEach((visual, at) => {
    if (at === keep) {
      return;
    }
    let grown: T = visual;
    while (free({ ...grown, layoutWidth: grown.layoutWidth + 1 }, at)) {
      grown = { ...grown, layoutWidth: grown.layoutWidth + 1 };
      next[at] = grown;
    }
    while (
      grown.layoutHeight < MAX_HEIGHT &&
      free({ ...grown, layoutHeight: grown.layoutHeight + 1 }, at)
    ) {
      grown = { ...grown, layoutHeight: grown.layoutHeight + 1 };
      next[at] = grown;
    }
  });
  return next;
}

/** The order with the visual at `from` moved to `to`, the others keeping theirs. */
export function moved<T>(items: T[], from: number, to: number): T[] {
  const next = [...items];
  const [item] = next.splice(from, 1);
  if (item !== undefined) {
    next.splice(Math.max(0, Math.min(to, next.length)), 0, item);
  }
  return next;
}

/** The visual drawn over a grid cell, or -1. */
export function visualAt(visuals: Box[], column: number, row: number): number {
  return visuals.findIndex(
    (visual) =>
      column >= visual.layoutColumn &&
      column < visual.layoutColumn + visual.layoutWidth &&
      row >= visual.layoutRow &&
      row < visual.layoutRow + visual.layoutHeight,
  );
}
