import { type NarrativeText, type NarrativeTextRequest } from '@/features/projects/api/types.ts';

// The WF-14 representations (TASK-052, financial-kpi.md §4) as the API serialises them: enums in SNAKE_CASE_UPPER, ids
// as strings, dates as ISO 8601. An amount is SAR, an R-16 decimal string with two places, and SAR only (ADR-008): no
// representation names a currency. A KPI figure is a decimal the API may write as a number or as a string.
//
// ADR-010: a field the caller's audience may not see is omitted from the JSON and named in `maskedFields`, so it is
// optional here; a figure that is not known is present and null, with `valueStatus` saying why (D-2, D-12).

/** An R-16 SAR amount, e.g. "1250000.00". */
export type Money = string;

export type Decimal = number | string;

/** Whether there is a figure, and if not, why. A figure is present exactly when MEASURED. */
export type ValueStatus = 'MEASURED' | 'MISSING' | 'STALE' | 'NOT_APPLICABLE';

/** The two views of a financial figure, never merged (M-12). */
export type SemanticState = 'CURRENT_LIVE' | 'PUBLISHED_OFFICIAL';

/** UNKNOWN unless the actuals are MEASURED and a positive budget and a forecast are present (D-2). */
export type FinancialStatus = 'GREEN' | 'AMBER' | 'RED' | 'UNKNOWN';

export type FinancialUpdateStatus =
  'DRAFT' | 'SUBMITTED' | 'UNDER_REVIEW' | 'RETURNED' | 'PUBLISHED';

/** A commitment or target version approved through WF-11 (D-5). */
export type ApprovedVersionStatus =
  | 'DRAFT'
  | 'SUBMITTED'
  | 'UNDER_REVIEW'
  | 'RETURNED'
  | 'ACTIVE'
  | 'SUPERSEDED'
  | 'REJECTED'
  | 'WITHDRAWN';

/** OPEN_COMMITMENT is refused (D-3) and never shown: the gate hides the commitments field. */
export type CommitmentType = 'APPROVED_BUDGET' | 'DECLARED_BUDGET' | 'OPEN_COMMITMENT';

export type FinancialSourceType = 'MANUAL' | 'ETIMAD' | 'OTHER';

export type FinancialField =
  'APPROVED_BUDGET' | 'ACTUAL_EXPENDITURE' | 'FORECAST_AT_COMPLETION' | 'OPEN_COMMITMENT';

export type SourceMode = 'MANUAL' | 'INTEGRATED' | 'HYBRID';

export type AggregateCoverage = 'COMPLETE' | 'PARTIAL' | 'NONE';

export type AggregateExclusionReason =
  'NOT_AVAILABLE' | 'CURRENCY_UNVERIFIED' | 'VALUE_NOT_MEASURED' | 'MASKED' | 'NO_PUBLISHED_FIGURE';

/** ADR-008 extended: where a financial figure came from, the day it is true as of, and who entered it. */
export interface Provenance {
  sourceType?: FinancialSourceType;
  sourceReference?: string | null;
  asOfDate?: string;
  enteredByUserId?: string;
}

interface Masked {
  /** The JSON names of the fields withheld from the caller's audience (ADR-010). */
  maskedFields: string[];
}

/** One version of the Approved Budget, approved through WF-11 with a referenced document. */
export interface FinancialCommitmentDetail extends Provenance, Masked {
  id: string;
  projectId: string;
  commitmentType: CommitmentType;
  versionNo: number;
  revisionNo: number;
  status: ApprovedVersionStatus;
  isCurrent: boolean;
  amountSar?: Money | null;
  effectiveFrom: string | null;
  activatedAt: string | null;
  supersededByCommitmentId: string | null;
  createdAt: string;
  createdBy: string;
}

/** One revision of a period's actual expenditure and forecast, reviewed and published by AHDA (D-7). */
export interface FinancialProgressUpdateDetail extends Provenance, Masked {
  id: string;
  projectId: string;
  reportingCycleId: string;
  revisionNo: number;
  status: FinancialUpdateStatus;
  actualExpenditureToDateSar?: Money | null;
  forecastAtCompletionSar?: Money | null;
  valueStatus: ValueStatus;
  narrative?: NarrativeText | null;
  projectIntakeId: string | null;
  submittedByUserId: string | null;
  submittedAt: string | null;
  reviewedByUserId: string | null;
  reviewedAt: string | null;
  returnReason: NarrativeText | null;
  createdAt: string;
  updatedAt: string;
}

/** PUBLISHED/OFFICIAL: a period's figures as published, with the update's provenance; never changed (D-9). */
export interface PublishedFinancialSnapshotDetail extends Provenance, Masked {
  id: string;
  projectId: string;
  reportingCycleId: string;
  financialProgressUpdateId: string;
  financialCommitmentId: string | null;
  semanticState: SemanticState;
  publishedAt: string;
  publishedByUserId: string;
  approvedBudgetSar?: Money | null;
  actualExpenditureToDateSar?: Money | null;
  forecastAtCompletionSar?: Money | null;
  valueStatus: ValueStatus;
  financialStatus: FinancialStatus;
  thresholdConfigurationVersionId: string;
}

/**
 * CURRENT/LIVE: the ACTIVE Approved Budget beside the latest revision submitted or published, computed on read and
 * stored nowhere (D-9). Its figures' provenance is on the commitment and the update it names.
 */
export interface FinancialPositionDetail extends Masked {
  projectId: string;
  semanticState: SemanticState;
  financialCommitmentId: string | null;
  approvedBudgetSar?: Money | null;
  financialProgressUpdateId: string | null;
  updateStatus: FinancialUpdateStatus | null;
  actualExpenditureToDateSar?: Money | null;
  forecastAtCompletionSar?: Money | null;
  valueStatus: ValueStatus;
  financialStatus: FinancialStatus;
  thresholdConfigurationVersionId: string | null;
  asOfDate?: string | null;
  computedAt: string;
}

/** A field with no row is MANUAL (D-8); `id` is null then. */
export interface FinancialSourceModeDetail {
  id: string | null;
  projectId: string;
  fieldCode: FinancialField;
  sourceMode: SourceMode;
  configuredAt: string | null;
}

/** MOD-023: a DRAFT's figures, as a whole. A figure that is not known is null with its status, never "0.00". */
export interface FinancialProgressUpdateRequest {
  actualExpenditureToDateSar: Money | null;
  forecastAtCompletionSar: Money | null;
  valueStatus: ValueStatus;
  narrative: NarrativeTextRequest | null;
  sourceReference: string | null;
  asOfDate: string;
}

export type KpiAssignmentStatus = 'ACTIVE' | 'SUSPENDED' | 'RETIRED';

export type KpiDirection = 'HIGHER_IS_BETTER' | 'LOWER_IS_BETTER' | 'TARGET_BAND';

/** A rating without its value or thresholds is UNKNOWN; an N/A period is NOT_APPLICABLE (D-2). */
export type KpiRagStatus = 'GREEN' | 'AMBER' | 'RED' | 'UNKNOWN' | 'NOT_APPLICABLE';

export type KpiMeasurementStatus = 'DRAFT' | 'SUBMITTED' | 'PUBLISHED';

export interface KpiAssignmentDetail {
  id: string;
  projectId: string;
  kpiDefinitionId: string;
  unitItemId: string;
  ownerUserId: string | null;
  measurementFrequencyItemId: string;
  status: KpiAssignmentStatus;
  assignedAt: string;
}

/** A target and its RAG thresholds, immutable once ACTIVE: a new target is a new version (D-13, D-14). */
export interface KpiTargetVersionDetail {
  id: string;
  kpiAssignmentId: string;
  versionNo: number;
  revisionNo: number;
  status: ApprovedVersionStatus;
  isCurrent: boolean;
  targetValue: Decimal;
  greenThreshold: Decimal | null;
  amberThreshold: Decimal | null;
  effectiveFrom: string | null;
  activatedAt: string | null;
  supersededByTargetVersionId: string | null;
}

/**
 * One period's value, pinned for good to the target version in force when it was recorded, with the RAG rated against
 * that version (criterion: a target change never back-edits it).
 */
export interface KpiMeasurementDetail extends Masked {
  id: string;
  kpiAssignmentId: string;
  projectId: string;
  kpiTargetVersionId: string;
  targetVersionNo: number;
  targetValue: Decimal;
  periodStart: string;
  periodEnd: string;
  measuredValue?: Decimal | null;
  valueStatus: ValueStatus;
  ragStatus: KpiRagStatus;
  asOfDate: string;
  recordedByUserId: string;
  narrative: NarrativeText | null;
  status: KpiMeasurementStatus;
  submittedAt: string | null;
  publishedByUserId: string | null;
  publishedAt: string | null;
  createdAt: string;
  updatedAt: string;
}

/** MOD-024 edit: a DRAFT's value, as a whole. Its period and its pinned target never change. */
export interface KpiMeasurementRequest {
  measuredValue: number | null;
  valueStatus: ValueStatus;
  asOfDate: string;
  narrative: NarrativeTextRequest | null;
}

/** MOD-024 create: the period too; the target version in force now is pinned by the server. */
export interface KpiMeasurementCreateRequest extends KpiMeasurementRequest {
  kpiAssignmentId: string;
  periodStart: string;
  periodEnd: string;
}

export interface AggregateExclusion {
  projectId: string;
  kpiDefinitionId: string | null;
  reason: AggregateExclusionReason;
}

export interface KpiRagCounts {
  green: number;
  amber: number;
  red: number;
  unknown: number;
  notApplicable: number;
}

/** The latest published value of one KPI across projects: a mean only within one unit, else partial (D-11). */
export interface KpiPortfolioAggregate {
  isUnitCompatible: boolean;
  unitItemId: string | null;
  isPartial: boolean;
  coverage: AggregateCoverage;
  measuredCount: number;
  meanValue: Decimal | null;
  ragCounts: KpiRagCounts;
  exclusions: AggregateExclusion[];
}

/** A catalogue KPI as `GET /kpi-definitions` lists it (MASTER_DATA_VIEW). */
export interface KpiDefinitionSummary {
  id: string;
  code: string;
  name: { ar: string; en: string };
  unitItemId: string;
  direction: KpiDirection;
  lifecycleState: string;
}
