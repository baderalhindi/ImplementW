import { type CSSProperties } from 'react';

import { formatPoints } from '@/features/projects/presentation.ts';
import { NO_STAGE, NOT_MEASURED } from '@/features/projects/register/projectFilterValues.ts';
import { sarCompact } from '@/shared/format/money.ts';
import { type Language, translate, type TranslationKey } from '@/shared/i18n/i18n.ts';

import { type PortfolioFilters } from '../api/dashboardsApi.ts';
import { followUpPath, registerPath } from '../drillLinks.ts';
import {
  type GroupBy,
  type Measure,
  type Visual,
  type VisualGroup,
  type VisualType,
} from './designApi.ts';

// What a visual may be and how its figures read: the same rules the API checks (PortfolioDesignService), so the designer offers only what
// saves. A card and a gauge show one figure, the charts a figure per group, a table either.

type Translate = (key: TranslationKey, params?: Record<string, string | number>) => string;

export const VISUAL_TYPES: readonly VisualType[] = [
  'CARD',
  'BAR',
  'COLUMN',
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
};

export const COLUMNS = 12;
export const MAX_HEIGHT = 12;
export const MAX_VISUALS = 40;

/** The groupings a type takes. */
export function groupBysFor(type: VisualType): readonly GroupBy[] {
  if (type === 'CARD' || type === 'GAUGE') {
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

/** The measures a type may draw: a gauge a percentage, a pie or donut a total that adds up. */
export function measuresFor(type: VisualType): readonly Measure[] {
  if (type === 'GAUGE') {
    return PERCENT_MEASURES;
  }
  return type === 'PIE' || type === 'DONUT' ? ADDITIVE_MEASURES : MEASURES;
}

export function isPercent(measure: Measure): boolean {
  return PERCENT_MEASURES.includes(measure);
}

/** A visual changed to `type`, keeping what still fits: its measures up to the type's count, a grouping the type takes. */
export function withType(visual: Visual, type: VisualType): Visual {
  const allowed = measuresFor(type);
  const measures = visual.measures
    .filter((measure) => allowed.includes(measure))
    .slice(0, MAX_MEASURES[type]);
  const groupings = groupBysFor(type);
  return {
    ...visual,
    visualType: type,
    measures: measures.length > 0 ? measures : [allowed[0] ?? 'PROJECTS'],
    groupBy: groupings.includes(visual.groupBy) ? visual.groupBy : firstGrouping(type),
  };
}

/** "Projects by portfolio": the title a visual takes from its first measure and its grouping until someone writes another. */
export function autoTitle(
  visual: Pick<Visual, 'measures' | 'groupBy'>,
  language: Language,
): string {
  const measure = translate(
    language,
    `dashboards.designer.measure.${visual.measures[0] ?? 'PROJECTS'}`,
  );
  const title =
    visual.groupBy === 'NONE'
      ? measure
      : translate(language, 'dashboards.designer.titleByGroup', {
          measure,
          group: translate(language, `dashboards.designer.groupBy.${visual.groupBy}`),
        });
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
function firstGrouping(type: VisualType): GroupBy {
  return groupBysFor(type).includes('PORTFOLIO') ? 'PORTFOLIO' : 'NONE';
}

/** A new visual of `type` with sensible first values, at the first free place; null when the grid has no room left for it. */
export function newVisual(type: VisualType, visuals: Visual[]): Visual | null {
  const { width, height } = DEFAULT_SIZE[type];
  const place = freePlace(visuals, width, height);
  if (place === null) {
    return null;
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
    return groupBy === 'NONE' ? t('dashboards.designer.total') : t('dashboards.designer.notSet');
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
