import { type BilingualLabel } from '@/features/identity-access/api/types.ts';

// The FG-01 representations (TASK-069 dashboards.md §5, `docs/api/openapi.v1.json`) as the API serialises them: enums
// in SNAKE_CASE_UPPER, ids as strings, instants as ISO 8601. A figure is an exact decimal string (R-16), never a
// binary float.

/** The three dashboards of ADR-006 ("FIXED at 3 dashboards"); DSH-001 to DSH-012 are renderings of them (D-2). */
export type DashboardCode = 'PORTFOLIO' | 'PROJECT' | 'GOVERNANCE';

/** PROJECT is read for one project (DSH-009 in SCR-040, DSH-008); PORTFOLIO over every project the caller may see. */
export type DashboardContextKind = 'PROJECT' | 'PORTFOLIO';

export type DashboardWidgetType =
  | 'METRIC_CARD'
  | 'STATUS_DISTRIBUTION'
  | 'BAR_COLUMN'
  | 'LINE_TREND'
  | 'DONUT_PIE'
  | 'PROGRESS_INDICATOR';

/** R-20(c) `semanticState`: never merged with another. */
export type ProjectionSemanticState = 'CURRENT_LIVE' | 'PUBLISHED_OFFICIAL' | 'HISTORICAL_SNAPSHOT';

/** R-20(c) `freshness`: UNKNOWN is a value of its own, set exactly when the widget has no data. */
export type ProjectionFreshness = 'FRESH' | 'STALE' | 'UNKNOWN';

export type ProjectionCoverage = 'COMPLETE' | 'PARTIAL' | 'NONE';

/** Why a widget's freshness is UNKNOWN (FG-01 §7.3): its data is then null, never 0. */
export type WidgetUnknownReason =
  'MISSING' | 'NOT_APPLICABLE' | 'RESTRICTED' | 'SOURCE_UNAVAILABLE';

export type CoverageExclusionReason = 'MISSING' | 'MASKED' | 'INCOMPATIBLE';

/** A PUBLISHED dashboard the caller's roles may open; `isDefaultLanding` marks where they land (Blueprint §20.2). */
export interface DashboardCatalogueEntry {
  code: DashboardCode;
  dashboardDefinitionId: string;
  versionNo: number;
  name: BilingualLabel;
  description: BilingualLabel | null;
  contextKind: DashboardContextKind;
  allowsPersonalization: boolean;
  isDefaultLanding: boolean;
}

export interface ProjectionMeta {
  semanticState: ProjectionSemanticState;
  freshness: ProjectionFreshness;
  /** When the source data was current; null when there is no value to date. */
  asOf: string | null;
  coverage: ProjectionCoverage;
}

/** One measure: a masked figure has no value and `isMasked` (ADR-010, R-20(b)); an absent one is null. */
export interface WidgetFigure {
  measure: string;
  value: string | null;
  /** COUNT, PERCENT, DAYS, SAR or BOOLEAN. */
  unit: string;
  isMasked: boolean;
}

/** A count by a source-owned value; `label` is the source's own when it has one (a risk rating). */
export interface WidgetBucket {
  key: string;
  label: BilingualLabel | null;
  count: number;
}

/** A point of a source-provided history (BR-DSH-018), oldest first. */
export interface WidgetSeriesPoint {
  asOf: string;
  periodStart: string | null;
  periodEnd: string | null;
  state: string | null;
  figures: WidgetFigure[];
}

export interface WidgetData {
  state: string | null;
  figures: WidgetFigure[];
  distribution: WidgetBucket[];
  series: WidgetSeriesPoint[];
}

export interface CoverageExclusion {
  reason: CoverageExclusionReason;
  count: number;
}

/** FG-01 §7.4: the projects an aggregate expected, counted and left out, and why. */
export interface WidgetCoverage {
  eligibleCount: number;
  includedCount: number;
  excludedCount: number;
  staleCount: number;
  exclusions: CoverageExclusion[];
}

export interface DashboardWidgetResult {
  code: string;
  title: BilingualLabel;
  widgetType: DashboardWidgetType;
  projectionCode: string;
  projectionVersion: string;
  sourceDomain: string;
  layoutRow: number;
  layoutColumn: number;
  /** Columns of a twelve-column grid. */
  layoutSpan: number;
  isOptionalVisibility: boolean;
  isHidden: boolean;
  personalSortOrder: number | null;
  projection: ProjectionMeta;
  unknownReason: WidgetUnknownReason | null;
  data: WidgetData | null;
  coverageDetail: WidgetCoverage | null;
  maskedFields: string[];
  /** The canonical screen a widget drills to; null when restricted. */
  drillTargetScreenId: string | null;
}

export interface DashboardView {
  code: DashboardCode;
  dashboardDefinitionId: string;
  versionNo: number;
  name: BilingualLabel;
  description: BilingualLabel | null;
  contextKind: DashboardContextKind;
  allowsPersonalization: boolean;
  projectId: string | null;
  departmentId: string | null;
  /** The only values the department filter takes (DSH-CC-17). */
  departmentOptions: string[];
  /** When FG-01 assembled the view; never a source's as-of (BR-DSH-042). */
  refreshedAt: string;
  widgets: DashboardWidgetResult[];
}

/** ADR-019: one optional widget's presentation choice. */
export interface WidgetPersonalization {
  widgetCode: string;
  isHidden: boolean;
  sortOrder: number | null;
}

export interface DashboardPersonalizationDetail {
  code: DashboardCode;
  dashboardDefinitionId: string;
  widgets: WidgetPersonalization[];
}
