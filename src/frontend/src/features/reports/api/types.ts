import { type BilingualLabel } from '@/features/identity-access/api/types.ts';

// FG-02's wire shapes (TASK-071 reports.md §5, TASK-072's sharing and requester). Codes are the API's; a value is the
// source's own string — a code, an exact decimal, an ISO date or an id — never a number the SPA computed.

export type ReportCode =
  | 'PORTFOLIO_SUMMARY'
  | 'PROJECT_REGISTER'
  | 'PROJECT_REPORT'
  | 'PROGRESS_REPORTING'
  | 'PROGRESS_HISTORY'
  | 'SCHEDULE_DELIVERY'
  | 'RISK_ISSUE'
  | 'FINANCIAL_PERFORMANCE'
  | 'KPI_PERFORMANCE'
  | 'GOVERNANCE_CHANGE';

/** Which of SCR-131 to SCR-137 presents a report. */
export type ReportAudienceFamily =
  'EXECUTIVE' | 'PORTFOLIO' | 'DEPARTMENT' | 'PROJECT' | 'FINANCIAL' | 'PROGRESS' | 'RISK_ISSUE';

export type ReportValueType =
  | 'CODE'
  | 'TEXT'
  | 'REFERENCE'
  | 'COUNT'
  | 'PERCENT'
  | 'DAYS'
  | 'SAR'
  | 'BOOLEAN'
  | 'DATE'
  | 'DATE_TIME';

export type ProjectionSemanticState = 'CURRENT_LIVE' | 'PUBLISHED_OFFICIAL' | 'HISTORICAL_SNAPSHOT';
export type ProjectionFreshness = 'FRESH' | 'STALE' | 'UNKNOWN';
export type ProjectionCoverage = 'COMPLETE' | 'PARTIAL' | 'NONE';
export type ReportUnknownReason =
  'MISSING' | 'NOT_APPLICABLE' | 'RESTRICTED' | 'SOURCE_UNAVAILABLE';
export type ReportParameterDataType = 'PROJECT' | 'DEPARTMENT' | 'OPTION';
export type ReportExportFormat = 'PDF' | 'XLSX' | 'CSV';
export type ReportSortDirection = 'ASC' | 'DESC';
export type ReportFilterOperator =
  'EQ' | 'NEQ' | 'IN' | 'GT' | 'GTE' | 'LT' | 'LTE' | 'BETWEEN' | 'CONTAINS';

export interface ReportCatalogueEntry {
  code: ReportCode;
  reportDefinitionId: string;
  versionNo: number;
  name: BilingualLabel;
  description: BilingualLabel | null;
  audienceFamily: ReportAudienceFamily;
  allowsSavedViews: boolean;
}

export interface ReportColumnView {
  sourceEntityCode: string;
  fieldCode: string;
  label: BilingualLabel;
  valueType: ReportValueType;
  projectionCode: string | null;
  semanticState: ProjectionSemanticState | null;
  isDefaultVisible: boolean;
  isSensitive: boolean;
}

export interface ReportParameterOptionView {
  value: string;
  label: BilingualLabel;
  catalogueEntryReference: string | null;
}

export interface ReportParameterView {
  code: string;
  label: BilingualLabel;
  dataType: ReportParameterDataType;
  isRequired: boolean;
  options: ReportParameterOptionView[];
}

export interface ReportView {
  code: ReportCode;
  reportDefinitionId: string;
  versionNo: number;
  name: BilingualLabel;
  description: BilingualLabel | null;
  audienceFamily: ReportAudienceFamily;
  allowsSavedViews: boolean;
  columns: ReportColumnView[];
  parameters: ReportParameterView[];
  exportFormats: ReportExportFormat[];
  mayExport: boolean;
  maySaveView: boolean;
}

export interface ProjectionMeta {
  semanticState: ProjectionSemanticState;
  freshness: ProjectionFreshness;
  asOf: string | null;
  coverage: ProjectionCoverage;
}

export interface ReportCoverage {
  eligibleCount: number;
  includedCount: number;
  excludedCount: number;
  staleCount: number;
}

export interface ReportSection {
  projectionCode: string;
  sourceDomain: string;
  projectionVersion: string;
  projection: ProjectionMeta;
  unknownReason: ReportUnknownReason | null;
  coverage: ReportCoverage;
}

export interface ReportCell {
  value: string | null;
  label: BilingualLabel | null;
  freshness: ProjectionFreshness;
  asOf: string | null;
  unknownReason: ReportUnknownReason | null;
  isMasked: boolean;
}

export interface ReportRow {
  projectId: string;
  snapshotIndex: number | null;
  cells: ReportCell[];
}

export interface ReportResultPage {
  reportCode: ReportCode | null;
  versionNo: number | null;
  columns: ReportColumnView[];
  sections: ReportSection[];
  items: ReportRow[];
  page: number;
  pageSize: number;
  totalCount: number;
  departmentOptions: string[];
  generatedAt: string;
}

/** A field as configuration names it: its source entity and its code. */
export interface ReportFieldReference {
  sourceEntityCode: string;
  fieldCode: string;
}

export interface ReportSort extends ReportFieldReference {
  direction: ReportSortDirection;
}

export interface ReportFilter extends ReportFieldReference {
  operator: ReportFilterOperator;
  /** One value; IN several and BETWEEN two, separated by commas. Never an expression. */
  value: string;
}

export interface ReportParameterValue {
  code: string;
  value: string;
}

export interface ReportRunRequest {
  parameters: ReportParameterValue[];
  /** The columns chosen among those the report publishes; its default columns when null. */
  columns: ReportFieldReference[] | null;
  sort: ReportSort[];
}

export interface ExplorerRunRequest {
  columns: ReportFieldReference[];
  filters: ReportFilter[];
  sort: ReportSort[];
}

export interface ExportOptions {
  format: ReportExportFormat;
  language: 'ar' | 'en';
}

/** An entry of the PUBLISHED REPORT_RULES allowlist: the only fields SCR-138 offers or the API accepts. */
export interface ReportAllowlistEntry {
  id: string;
  sourceEntityCode: string;
  fieldCode: string;
  label: BilingualLabel;
  valueType: ReportValueType;
  isFilterable: boolean;
  isSortable: boolean;
  operators: ReportFilterOperator[];
  /** The field's closed set of values, when it has one. */
  values: string[];
  projectionCode: string | null;
  semanticState: ProjectionSemanticState | null;
  isSensitive: boolean;
}

export interface ReportAllowlist {
  items: ReportAllowlistEntry[];
  configurationVersionNo: number;
}

export type ReportJobKind = 'REPORT' | 'EXPLORER';
export type ReportJobStatus =
  | 'REQUESTED'
  | 'VALIDATING'
  | 'QUEUED'
  | 'RUNNING'
  | 'COMPLETED'
  | 'FAILED'
  | 'CANCELLED'
  | 'EXPIRED';
export type OutputSensitivity = 'STANDARD' | 'SENSITIVE';
export type GeneratedOutputStatus = 'AVAILABLE' | 'EXPIRED' | 'PURGED';

export interface GeneratedOutputDetail {
  fileName: string;
  contentType: string;
  sizeBytes: number;
  checksumSha256: string;
  sensitivity: OutputSensitivity;
  rowCount: number;
  sourceAsOf: string | null;
  expiresAt: string;
  status: GeneratedOutputStatus;
}

export interface ReportJobDetail {
  id: string;
  requestedByUserId: string;
  kind: ReportJobKind;
  reportCode: ReportCode | null;
  reportDefinitionId: string | null;
  exportFormat: ReportExportFormat;
  reportLanguage: string;
  status: ReportJobStatus;
  requestedAt: string;
  startedAt: string | null;
  completedAt: string | null;
  failureCode: string | null;
  correlationId: string;
  output: GeneratedOutputDetail | null;
}

export interface ReportJobSummary {
  id: string;
  requestedByUserId: string;
  kind: ReportJobKind;
  reportCode: ReportCode | null;
  exportFormat: ReportExportFormat;
  status: ReportJobStatus;
  failureCode: string | null;
  requestedAt: string;
  completedAt: string | null;
  sensitivity: OutputSensitivity | null;
  expiresAt: string | null;
}

export type SavedViewType = 'REPORT_PARAMETERS' | 'EXPLORER_COMPOSITION';
export type SavedViewCompatibility = 'VALID' | 'INCOMPATIBLE';

/** Free text as entered, with the language it was entered in (ADR-012); the API writes the language `AR`/`EN`. */
export interface NarrativeText {
  text: string;
  language: string;
}

export interface SavedViewColumn extends ReportFieldReference {
  sortDirection: ReportSortDirection | null;
}

export interface SavedViewIssue {
  field: string;
  code: string;
}

export interface SavedViewDetail {
  id: string;
  ownerUserId: string;
  viewType: SavedViewType;
  name: NarrativeText;
  reportCode: ReportCode | null;
  reportDefinitionId: string | null;
  reportVersionNo: number | null;
  parameters: ReportParameterValue[];
  columns: SavedViewColumn[];
  filters: ReportFilter[];
  /** The roles it is shared with: its owner's to read; a person it is shared with reads it empty. */
  sharedWithRoleCodes: string[];
  compatibility: SavedViewCompatibility;
  issues: SavedViewIssue[];
  createdAt: string;
  updatedAt: string;
}

export interface SavedViewSummary {
  id: string;
  ownerUserId: string;
  viewType: SavedViewType;
  name: NarrativeText;
  reportCode: ReportCode | null;
  sharedWithRoleCodes: string[];
  compatibility: SavedViewCompatibility;
  updatedAt: string;
}

export interface SavedViewRequest {
  name: { text: string; language: 'ar' | 'en' };
  viewType: SavedViewType;
  reportCode: ReportCode | null;
  parameters: ReportParameterValue[];
  columns: SavedViewColumn[];
  filters: ReportFilter[];
  sharedWithRoleCodes: string[];
}

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}
