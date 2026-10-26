import { type NarrativeText, type NarrativeTextRequest } from '@/features/projects/api/types.ts';

// WF-07's representations as the TASK-057 API serves them (management-concern.md §4; openapi.v1.json, tag
// ManagementConcern). Dates are `YYYY-MM-DD`, instants ISO 8601, enums SNAKE_UPPER.

export type ConcernType = 'ISSUE' | 'CHALLENGE';

/** The row's lifecycle (TASK-057 D-3). Validation is WF-11's; a return sends the concern back to IN_PROGRESS. */
export type ConcernStatus =
  'OPEN' | 'ASSIGNED' | 'IN_PROGRESS' | 'PENDING_VALIDATION' | 'RESOLVED' | 'CLOSED';

export type ConcernEscalationStatus = 'OPEN' | 'RESOLVED' | 'WITHDRAWN';

export interface ConcernImpactDetail {
  impactDimensionItemId: string;
  impactLevel: number;
  rationale: NarrativeText | null;
}

export interface ConcernEscalationDetail {
  id: string;
  managementConcernId: string;
  /** Numbered within its concern, from 1. */
  escalationNo: number;
  escalatedByUserId: string;
  escalatedAt: string;
  /** The role WORKFLOW_POLICY's CONCERN_ESCALATION_ROLE named when it was raised. */
  escalatedToRoleId: string;
  reason: NarrativeText;
  status: ConcernEscalationStatus;
  resolvedAt: string | null;
  resolvedByUserId: string | null;
  resolution: NarrativeText | null;
  updatedAt: string;
}

/**
 * An issue or a challenge. `severityItemId` is the server's, computed from `impacts` under the rule of
 * `severityConfigurationVersionId` and null until the concern is assessed (TASK-057 D-4); no request carries it.
 * `priorityItemId` is chosen by people (D-5).
 */
export interface ConcernDetail {
  id: string;
  projectId: string;
  concernType: ConcernType;
  title: NarrativeText;
  description: NarrativeText;
  categoryItemId: string;
  priorityItemId: string;
  /** The highest impact level; null until assessed. */
  overallImpactLevel: number | null;
  severityItemId: string | null;
  severityConfigurationVersionId: string | null;
  impacts: ConcernImpactDetail[];
  status: ConcernStatus;
  /** Raised by one each time validation returns the resolution. */
  revisionNo: number;
  raisedByUserId: string;
  raisedAt: string;
  assigneeUserId: string | null;
  /** The risk this issue materialised from (WF-06, edge 15). */
  originatingRiskId: string | null;
  targetResolutionDate: string | null;
  nextReviewDate: string;
  lastReviewedAt: string | null;
  resolution: NarrativeText | null;
  resolvedAt: string | null;
  closedAt: string | null;
  /** Its OPEN escalation, if any: an overlay that never changes the concern's status. */
  openEscalation: ConcernEscalationDetail | null;
  createdAt: string;
  createdBy: string;
  updatedAt: string;
  updatedBy: string;
}

/** PUT /management-concerns/{id}: the concern's own fields as a whole (R-5). No severity: it is never input. */
export interface ConcernRequest {
  title: NarrativeTextRequest;
  description: NarrativeTextRequest;
  categoryItemId: string;
  priorityItemId: string;
  targetResolutionDate: string | null;
}

/** MOD-036 and MOD-038: raised OPEN and unassessed; impacts are given through an assessment. */
export interface ConcernCreateRequest extends ConcernRequest {
  projectId: string;
  concernType: ConcernType;
  impacts: ConcernImpactRequest[];
}

export interface ConcernImpactRequest {
  impactDimensionItemId: string;
  impactLevel: number;
  rationale: NarrativeTextRequest | null;
}

/** The impact levels only: the overall impact and the severity are the server's. */
export interface ConcernAssessCommand {
  impacts: ConcernImpactRequest[];
}

export interface ConcernAssignCommand {
  assigneeUserId: string;
}

/** The resolution put to validation, or an escalation's management direction. */
export interface ConcernResolutionCommand {
  resolution: NarrativeTextRequest;
}

/** MOD-039: a reason only; where it goes is the server's routing. */
export interface ConcernEscalationCreateRequest {
  managementConcernId: string;
  reason: NarrativeTextRequest;
}
