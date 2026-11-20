import { type BilingualLabel } from '@/features/identity-access/api/types.ts';
import { type OnPlanStatus } from '@/features/projects/api/types.ts';
import { apiRequest } from '@/shared/api/httpClient.ts';

import { type PortfolioFilters } from '../api/dashboardsApi.ts';
import { type InsightSectionCode } from '../insights/insightSections.ts';

// The Portfolio dashboard's designer (17 Nov 2026): the Portfolio Manager places visuals for everyone, with no approval. A visual is a fixed type,
// measures and a grouping — never a formula (BR-DSH-030) — on a twelve-column grid. The API calculates every figure over the same projects as the
// insights; a visual whose grouping the caller may not see comes back with `groups` null and is left out.

/**
 * CONTAINER (19 Nov 2026, docs/architecture/dashboard-designer-containers.md): a framed, titled box holding visuals, one level deep. TEXT (20 Nov
 * 2026, docs/architecture/dashboard-designer-map-text.md): the designer's own words, a heading or a note.
 */
export type VisualType =
  | 'CARD'
  | 'BAR'
  | 'COLUMN'
  | 'PIE'
  | 'DONUT'
  | 'TABLE'
  | 'GAUGE'
  | 'LINE'
  | 'SECTION'
  | 'CONTAINER'
  | 'TEXT';

/** A text box's size: about 0.875, 1, 1.25 and 1.5 rem, the heading bold. */
export type TextSize = 'SMALL' | 'NORMAL' | 'LARGE' | 'HEADING';

/** A text box's alignment, logical: the start and end mirror in Arabic. */
export type TextAlign = 'START' | 'CENTER' | 'END';

/** A text box's words in both languages (each 1–2000 characters, line breaks kept) and how they are drawn. */
export interface TextBox {
  ar: string;
  en: string;
  size: TextSize;
  align: TextAlign;
  bold: boolean;
}

/**
 * One of the dashboard's own sections a design places (18 Nov 2026): drawn as it always was, with its own figures and permission.
 * DASHBOARD_WIDGET (19 Nov 2026) draws one governed widget, named by the visual's `widgetCode`; DASHBOARD_WIDGETS, all of them in one visual,
 * stays valid for designs saved before.
 */
export type Section =
  | InsightSectionCode
  | 'PROJECT_LOCATIONS'
  | 'DASHBOARD_WIDGETS'
  | 'DASHBOARD_WIDGET'
  | 'ENTITY_COMPLETION_TABLE';

/**
 * A portfolio figure. The on-plan statuses and the follow-up reasons (20 Nov 2026, docs/architecture/dashboard-designer-box-sections.md) count
 * the projects the On-plan indicator's and the follow-up reasons' boxes count.
 */
export type Measure =
  | 'PROJECTS'
  | 'IN_PROGRESS'
  | 'VALUE_SAR'
  | 'AVERAGE_PLANNED'
  | 'AVERAGE_ACTUAL'
  | 'GAP_POINTS'
  | 'NEEDING_FOLLOW_UP'
  | 'OVERDUE_ACTIONS'
  | 'ON_TRACK'
  | 'LATE'
  | 'STALLED'
  | 'NOT_MEASURED'
  | 'FOLLOW_UP_STALLED'
  | 'FOLLOW_UP_SUSPENDED'
  | 'FOLLOW_UP_LATE'
  | 'FOLLOW_UP_ACTION_OVERDUE'
  | 'FOLLOW_UP_OPEN_OBSTACLE';

/** The figures of the On-plan indicator's boxes, in their order: projects with each on-plan status, and those not measured. */
export const ON_PLAN_MEASURES = [
  'ON_TRACK',
  'LATE',
  'STALLED',
  'NOT_MEASURED',
] as const satisfies readonly Measure[];

/** The figures of the follow-up reasons' boxes, in their order: the projects needing follow-up counted under each main reason. */
export const FOLLOW_UP_MEASURES = [
  'FOLLOW_UP_STALLED',
  'FOLLOW_UP_SUSPENDED',
  'FOLLOW_UP_LATE',
  'FOLLOW_UP_ACTION_OVERDUE',
  'FOLLOW_UP_OPEN_OBSTACLE',
] as const satisfies readonly Measure[];

export type GroupBy =
  | 'NONE'
  | 'PORTFOLIO'
  | 'INITIATIVE'
  | 'SECTOR'
  | 'DEPARTMENT'
  | 'ENTITY'
  | 'STATUS'
  | 'STAGE'
  | 'ON_PLAN'
  | 'REGION'
  | 'CITY'
  | 'PRIORITY';

/** A governed field's type (the register's ProjectionValueType). */
export type FieldType =
  'CODE' | 'COUNT' | 'PERCENT' | 'DAYS' | 'SAR' | 'BOOLEAN' | 'DATE' | 'DATE_TIME';

/** A field value's calculation; COUNT_TRUE and SHARE_TRUE are a yes/no field's (the projects whose value is yes, and their share in %). */
export type FieldAggregation =
  'COUNT' | 'SUM' | 'AVERAGE' | 'MIN' | 'MAX' | 'COUNT_TRUE' | 'SHARE_TRUE';

/** A field's rows: one per project, or one per published progress snapshot (figures over time). */
export type FieldGrain = 'PROJECT' | 'SNAPSHOT';

/** How a visual grouped by a date field groups it: by year (key `2026`) or by month (key `2026-03`). */
export type DateUnit = 'YEAR' | 'MONTH';

/** One field the designer offers (GET /portfolio-insights/design/fields): a project-grain field of the governed catalogue. */
export interface FieldDescriptor {
  /** `<PROJECTION_CODE>.<FIELD_CODE>`, e.g. FINANCIAL_KPI.FINANCIAL_POSITION.ACTUAL_EXPENDITURE_TO_DATE. */
  key: string;
  projectionCode: string;
  sourceDomain: string;
  fieldCode: string;
  type: FieldType;
  isSensitive: boolean;
  /** The codes a CODE field takes; empty for any other type. */
  values: string[];
  /** The calculations its type allows, in the register's order. */
  aggregations: FieldAggregation[];
  grain: FieldGrain;
}

/** What a table column is (20 Nov 2026, docs/architecture/dashboard-designer-power-table.md): a project attribute, a data field or a portfolio figure. */
export type TableColumnKind = 'DIMENSION' | 'FIELD' | 'MEASURE';

/** A table column's calculation, as Power BI's: NONE ("Don't summarize") groups the rows by the column's value; a figure takes none (null). */
export type TableAggregation =
  | 'NONE'
  | 'COUNT'
  | 'COUNT_DISTINCT'
  | 'SUM'
  | 'AVERAGE'
  | 'MIN'
  | 'MAX'
  | 'COUNT_TRUE'
  | 'SHARE_TRUE';

/** One column of a table visual: a dimension's, field's or figure's key and its calculation (null for a figure). */
export interface TableColumn {
  kind: TableColumnKind;
  key: string;
  aggregation: TableAggregation | null;
}

/** How a table column's cells read: an item's name, a code, text, or a figure of a kind. */
export type TableColumnType =
  | 'ITEM'
  | 'CODE'
  | 'TEXT'
  | 'COUNT'
  | 'PERCENT'
  | 'DAYS'
  | 'SAR'
  | 'DATE'
  | 'BOOLEAN'
  | 'ON_PLAN'
  | 'POINTS';

/** A table cell: a grouping column's key and words, or a calculated value; null when missing or (with `masked`) withheld. */
export interface TableCell {
  key: string | null;
  label: BilingualLabel | null;
  value: number | null;
  masked?: boolean;
}

export interface TableRow {
  projects: number;
  /** The project, on a table with one row per project (grouped by its title or ID). */
  projectId: string | null;
  cells: TableCell[];
}

/** A column table's figures: each column's type, the rows (at most 500), the totals over all their projects (null when not shown). */
export interface TableResult {
  columnTypes: string[];
  rows: TableRow[];
  totals: TableCell[] | null;
  truncated: boolean;
}

/** A value a visual draws from a field: the field's key and the calculation over each group's projects. */
export interface FieldValue {
  field: string;
  aggregation: FieldAggregation;
}

/** A visual's own filter: a CODE field's key or a project attribute (`PROJECT.STATUS`), and the values a project must have. */
export interface FieldFilter {
  field: string;
  values: string[];
}

/** A chart's category axis (what it is grouped by): shown or not, and its title, none when null. */
export interface CategoryAxis {
  show: boolean;
  title: BilingualLabel | null;
}

/** A chart's value axis: shown or not, its title, its range (null = automatic, from the figures) and its gridlines. */
export interface ValueAxis {
  show: boolean;
  title: BilingualLabel | null;
  min: number | null;
  max: number | null;
  gridlines: boolean;
}

/** A bar, column or line chart's axes (docs/architecture/dashboard-designer-axes.md); null or absent = the defaults. */
export interface VisualAxes {
  category: CategoryAxis;
  value: ValueAxis;
}

/** A visual as the designer keeps and sends it. Columns and rows count from 0, from the reading start. */
export interface Visual {
  title: BilingualLabel;
  visualType: VisualType;
  measures: Measure[];
  groupBy: GroupBy;
  layoutColumn: number;
  layoutRow: number;
  layoutWidth: number;
  layoutHeight: number;
  /** The section a SECTION places, with no measures and no grouping; absent for any other type. */
  section?: Section | null | undefined;
  /** The governed widget (its `code` in the dashboard's view) a DASHBOARD_WIDGET section draws; absent or null for any other visual. */
  widgetCode?: string | null | undefined;
  /** Field values the visual draws instead of `measures` (which is then empty); absent or null when it draws measures. */
  values?: FieldValue[] | null | undefined;
  /**
   * A CODE, yes/no or date field the visual groups by, `groupBy` being NONE; absent or null when it groups by a project attribute or not at
   * all.
   */
  groupField?: string | null | undefined;
  /** How a date `groupField` groups: required with a date field, null otherwise. */
  groupDateUnit?: DateUnit | null | undefined;
  /** The visual's own filters, applied after the dashboard's; absent or null when it has none. */
  filters?: FieldFilter[] | null | undefined;
  /** A bar, column or line chart's axes; null or absent when they are the defaults, and for any other type. */
  axes?: VisualAxes | null | undefined;
  /**
   * A container's visuals, in reading order, each placed on the container's inner grid: `layoutWidth` columns and `layoutHeight − 1` rows (its
   * first row is its title bar). Absent or null on every other visual; never a container.
   */
  children?: Visual[] | null | undefined;
  /** A table section's unticked columns (sectionColumns.ts), null when every column is shown; absent on any other visual. */
  hiddenColumns?: string[] | null | undefined;
  /** A table's columns (1–12): with them its `measures` are empty, `values` null and it is grouped by none; absent on any other visual. */
  columns?: TableColumn[] | null | undefined;
  /** Whether a column table draws its totals row; absent or null means it does. */
  showTotals?: boolean | null | undefined;
  /** A TEXT visual's words; absent or null on any other visual. */
  text?: TextBox | null | undefined;
}

/** One group's figures, a value per measure in the visual's order; null when it cannot be calculated or may not be seen. */
export interface VisualGroup {
  key: string | null;
  label: BilingualLabel | null;
  projects: number;
  measured: number;
  valueEntered: number;
  values: (number | null)[];
  onPlan: OnPlanStatus | null;
}

export interface VisualResult extends Visual {
  groups: VisualGroup[] | null;
  /** Each field value's type, in the order of `values`: how a value reads, for readers who may not read the designer's catalogue. */
  valueTypes?: FieldType[] | null | undefined;
  /** A container's visuals, each calculated as a top-level visual is. */
  children?: VisualResult[] | null | undefined;
  /** A column table's figures; its `groups` are then []. */
  table?: TableResult | null | undefined;
}

export interface DesignView {
  isStandard: boolean;
  updatedAt: string | null;
  visuals: VisualResult[];
}

export const designApi = {
  /** The saved design, or the standard one, with the caller's figures under the dashboard's filters. */
  get: async (filters: PortfolioFilters, signal?: AbortSignal) =>
    (await apiRequest<DesignView>('/portfolio-insights/design', { query: { ...filters }, signal }))
      .data,
  /** A design being edited, calculated but not saved. */
  preview: async (visuals: Visual[], signal?: AbortSignal) =>
    (
      await apiRequest<DesignView>('/portfolio-insights/design/preview', {
        method: 'POST',
        body: { visuals },
        signal,
      })
    ).data,
  /** Saves the design for everyone. */
  save: async (visuals: Visual[]) =>
    (
      await apiRequest<DesignView>('/portfolio-insights/design', {
        method: 'PUT',
        body: { visuals },
      })
    ).data,
  /** The governed fields a visual may draw, group by and filter on. */
  fields: async (signal?: AbortSignal) =>
    (
      await apiRequest<{ fields: FieldDescriptor[] }>('/portfolio-insights/design/fields', {
        signal,
      })
    ).data.fields,
  /** Back to the standard design. */
  reset: async () => {
    await apiRequest<undefined>('/portfolio-insights/design/reset', { method: 'POST', body: {} });
  },
};
