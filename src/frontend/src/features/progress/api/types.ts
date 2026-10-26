import { type NarrativeText, type NarrativeTextRequest } from '@/features/projects/api/types.ts';

// The WF-02 representations (TASK-044, progress-update.md §4) as the API serialises them: enums in
// SNAKE_CASE_UPPER, ids as strings, dates as ISO 8601. A percentage is a decimal the API may write as a number or as
// a string (R-16), 0–100 to four places.

export type Decimal = number | string;

/** The review and publication workflow of one revision of a period's progress. RETURNED and PUBLISHED are final. */
export type ProgressSubmissionStatus =
  'DRAFT' | 'SUBMITTED' | 'UNDER_REVIEW' | 'RETURNED' | 'PUBLISHED';

/** A rating whose inputs are missing is UNKNOWN, never coerced to a colour. */
export type HealthStatus = 'GREEN' | 'AMBER' | 'RED' | 'UNKNOWN';

export type ReportingCycleStatus = 'OPEN' | 'CLOSED';

export interface ReportingCycleSummary {
  id: string;
  projectId: string;
  periodStart: string;
  periodEnd: string;
  dueDate: string;
  status: ReportingCycleStatus;
}

/**
 * One revision of a period's progress, actual and planned side by side (ADR-009). `actualPercent` is the reported
 * figure: the override when `isOverridden`, else the calculated one, which is always kept. `projectIntakeId` marks
 * ADR-014's opening position.
 */
export interface ProgressSubmissionDetail {
  id: string;
  projectId: string;
  reportingCycleId: string;
  revisionNo: number;
  status: ProgressSubmissionStatus;
  actualPercent: Decimal;
  actualPercentCalculated: Decimal;
  actualPercentOverride: Decimal | null;
  overrideReason: NarrativeText | null;
  isOverridden: boolean;
  plannedPercent: Decimal | null;
  baselineId: string | null;
  narrative: NarrativeText | null;
  projectIntakeId: string | null;
  submittedByUserId: string | null;
  submittedAt: string | null;
  reviewedByUserId: string | null;
  reviewedAt: string | null;
  returnReason: NarrativeText | null;
  createdAt: string;
  createdBy: string;
  updatedAt: string;
  updatedBy: string;
}

/** PUBLISHED/OFFICIAL: a period's record as it was published; nothing changes it afterwards. */
export interface PublishedProgressSnapshotDetail {
  id: string;
  projectId: string;
  reportingCycleId: string;
  progressSubmissionId: string;
  publishedAt: string;
  publishedByUserId: string;
  actualPercent: Decimal;
  isOverridden: boolean;
  plannedPercent: Decimal | null;
  overallHealth: HealthStatus;
  scheduleHealth: HealthStatus | null;
  financialStatus: HealthStatus | null;
  healthRuleConfigurationVersionId: string;
}

/** CURRENT/LIVE: the Overall Project Health WF-02 last computed, from the derived figures, never an override. */
export interface ProjectHealthStatusDetail {
  id: string;
  projectId: string;
  overallHealth: HealthStatus;
  actualPercent: Decimal | null;
  plannedPercent: Decimal | null;
  computedAt: string;
  healthRuleConfigurationVersionId: string;
}

/** ADR-009: the only project-level figure a person enters, with its reason. */
export interface ProgressOverrideRequest {
  actualPercent: number;
  reason: NarrativeTextRequest;
}

/** What a person writes on a DRAFT, as a whole (R-5). There is no actual or planned figure: both are derived. */
export interface ProgressSubmissionRequest {
  narrative: NarrativeTextRequest | null;
  override: ProgressOverrideRequest | null;
}
