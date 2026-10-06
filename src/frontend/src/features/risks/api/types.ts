import { type NarrativeText, type NarrativeTextRequest } from '@/features/projects/api/types.ts';

// WF-06's representations as the TASK-055 API serves them (risk-management.md §4; openapi.v1.json, tag Risk). Dates
// are `YYYY-MM-DD`, instants ISO 8601, enums SNAKE_UPPER.

export type RiskStatus = 'IDENTIFIED' | 'ASSESSED' | 'TREATMENT' | 'MONITORING' | 'CLOSED';
export type RiskTreatmentActionStatus = 'PLANNED' | 'IN_PROGRESS' | 'COMPLETED' | 'CANCELLED';
export type RiskTreatmentActionType = 'MITIGATE' | 'AVOID' | 'TRANSFER' | 'CONTINGENCY';
export type RiskAcceptanceStatus = 'ACTIVE' | 'EXPIRED' | 'REVOKED';

export interface BilingualLabel {
  ar: string;
  en: string;
}

/** The rating the matrix version pinned by an assessment gave it; its label is that version's. */
export interface RiskRatingDetail {
  id: string;
  code: string;
  label: BilingualLabel;
}

export interface RiskAssessmentSummary {
  id: string;
  versionNo: number;
  assessedAt: string;
  matrixConfigurationVersionId: string;
  probabilityLevel: number;
  overallImpactLevel: number;
  rating: RiskRatingDetail;
}

export interface RiskDetail {
  id: string;
  projectId: string;
  title: NarrativeText;
  description: NarrativeText;
  riskCategoryItemId: string;
  ownerUserId: string | null;
  status: RiskStatus;
  identifiedDate: string;
  nextReviewDate: string | null;
  /** The latest assessment; null while IDENTIFIED. */
  currentAssessment: RiskAssessmentSummary | null;
  /** The ACTIVE acceptance's expiry: the first day it no longer holds. */
  acceptedUntil: string | null;
  materialisedAt: string | null;
  materialisedIssueIds: string[];
  closureRationale: NarrativeText | null;
  closedAt: string | null;
  closedByUserId: string | null;
  reopenedCount: number;
  createdAt: string;
  createdBy: string;
  updatedAt: string;
  updatedBy: string;
}

export interface RiskAssessmentImpactDetail {
  impactDimensionItemId: string;
  impactLevel: number;
  rationale: NarrativeText | null;
}

export interface RiskAssessmentDetail {
  id: string;
  riskId: string;
  versionNo: number;
  assessedAt: string;
  assessedByUserId: string;
  matrixConfigurationVersionId: string;
  probabilityLevel: number;
  overallImpactLevel: number;
  rating: RiskRatingDetail;
  impacts: RiskAssessmentImpactDetail[];
  rationale: NarrativeText | null;
}

export interface RiskTreatmentActionDetail {
  id: string;
  riskId: string;
  title: NarrativeText;
  description: NarrativeText | null;
  actionType: RiskTreatmentActionType;
  ownerUserId: string | null;
  dueDate: string | null;
  status: RiskTreatmentActionStatus;
  completedAt: string | null;
  createdAt: string;
  createdBy: string;
  updatedAt: string;
  updatedBy: string;
}

export interface RiskAcceptanceDetail {
  id: string;
  riskId: string;
  acceptedByUserId: string;
  acceptedAt: string;
  expiresOn: string;
  rationale: NarrativeText;
  status: RiskAcceptanceStatus;
  revokedAt: string | null;
  updatedAt: string;
}

/** PUT /risks/{id}: every register field at once (R-5), the owner among them (MOD-031, MOD-033). */
export interface RiskRequest {
  title: NarrativeTextRequest;
  description: NarrativeTextRequest;
  riskCategoryItemId: string;
  ownerUserId: string | null;
  identifiedDate: string;
  nextReviewDate: string | null;
}

export interface RiskCreateRequest extends RiskRequest {
  projectId: string;
}

export interface RiskImpactRequest {
  impactDimensionItemId: string;
  impactLevel: number;
  rationale: NarrativeTextRequest | null;
}

/** The levels only: the server works out the overall impact and the rating (D-5). */
export interface RiskAssessCommand {
  probabilityLevel: number;
  impacts: RiskImpactRequest[];
  rationale: NarrativeTextRequest | null;
}

export interface RiskAcceptCommand {
  expiresOn: string;
  rationale: NarrativeTextRequest;
}

export interface RiskCloseCommand {
  rationale: NarrativeTextRequest;
}

export interface RiskTreatmentActionRequest {
  title: NarrativeTextRequest;
  description: NarrativeTextRequest | null;
  actionType: RiskTreatmentActionType;
  ownerUserId: string | null;
  dueDate: string | null;
}

export interface RiskTreatmentActionCreateRequest extends RiskTreatmentActionRequest {
  riskId: string;
}

// FG-04's RISK_MATRIX version in force (`GET /configuration-resolutions/RISK_MATRIX`, ResolvedConfiguration). Only the
// matrix sections are read; the levels, ratings and cells are whatever the version publishes (ADR-011, OQ-006).

export interface ProbabilityLevelEntry {
  level: number;
  label: BilingualLabel;
  lowerPct: number | null;
  upperPct: number | null;
}

export interface ImpactLevelEntry {
  impactDimensionItemId: string;
  level: number;
  label: BilingualLabel;
  description: BilingualLabel | null;
  lowerBound: number | null;
  upperBound: number | null;
}

export interface RiskRatingEntry {
  code: string;
  label: BilingualLabel;
  sortOrder: number;
}

export interface RiskMatrixCellEntry {
  probabilityLevel: number;
  impactLevel: number;
  ratingCode: string;
}

export interface RiskMatrixResolution {
  versionId: string;
  familyCode: string;
  versionNo: number;
  effectiveFrom: string;
  effectiveTo: string | null;
  content: {
    probabilityLevels: ProbabilityLevelEntry[];
    impactLevels: ImpactLevelEntry[];
    riskRatings: RiskRatingEntry[];
    riskMatrixCells: RiskMatrixCellEntry[];
  };
}
