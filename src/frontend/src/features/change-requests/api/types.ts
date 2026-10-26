import { type NarrativeText, type NarrativeTextRequest } from '@/features/projects/api/types.ts';

// WF-08's representations as the TASK-060 API serves them (change-request.md §4; openapi.v1.json, tag ChangeRequest).
// Enums SNAKE_UPPER, ids as strings, instants ISO 8601, money as an R-16 string with two fraction digits.

/** What the request changes; fixed when it is raised. */
export type ChangeType =
  'SCOPE' | 'COST' | 'SCHEDULE' | 'CONTRACTUAL_OBLIGATION' | 'GOVERNANCE_PROFILE';

/**
 * The row's lifecycle (TASK-060 D-3): Draft → Submitted → Under Review → Returned / Approved / Rejected →
 * Implementation → Implemented → Closed, and Withdrawn. REJECTED, WITHDRAWN and CLOSED are final. APPROVED and
 * IMPLEMENTED are different states: an APPROVED request's authorisations have not been applied by their modules.
 */
export type ChangeRequestStatus =
  | 'DRAFT'
  | 'SUBMITTED'
  | 'UNDER_REVIEW'
  | 'RETURNED'
  | 'APPROVED'
  | 'REJECTED'
  | 'IMPLEMENTATION'
  | 'IMPLEMENTED'
  | 'CLOSED'
  | 'WITHDRAWN';

/** The one kind of change an authorisation permits. PROFILE_CHANGE and SCOPE_CHANGE are never issued yet (TASK-060 F-4). */
export type ChangeAuthorizationScope =
  'REBASELINE' | 'COMMITMENT_CHANGE' | 'PROFILE_CHANGE' | 'SCOPE_CHANGE';

/** ISSUED until its target module applies it, once. EXPIRED and REVOKED are never set yet (TASK-060 F-9). */
export type ChangeAuthorizationStatus = 'ISSUED' | 'APPLIED' | 'EXPIRED' | 'REVOKED';

/**
 * A materiality classification (ADR-016): the band each dimension triggers — null where the request states no impact on
 * it — and the highest of them, against the cumulative position since the active baseline. `evaluationId` is null for a
 * preview: computed by the same rule, recorded nowhere, advisory until the review records one (TASK-060 D-4).
 */
export interface MaterialityAssessment {
  evaluationId: string | null;
  revisionNo: number;
  evaluatedAt: string;
  materialityConfigurationVersionId: string;
  projectBaselineId: string | null;
  financialCommitmentId: string | null;
  cumulativeCostImpactSar: string;
  cumulativeScheduleImpactDays: number;
  costBandNo: number | null;
  scheduleBandNo: number | null;
  scopeBandNo: number | null;
  resultingBandNo: number;
}

/** Permission to change one governed commitment, pinned to the version approved; applied once by its target module. */
export interface ChangeAuthorizationDetail {
  id: string;
  changeRequestId: string;
  approvalInstanceId: string;
  authorizationScope: ChangeAuthorizationScope;
  targetModule: string;
  targetType: string;
  targetId: string;
  targetRevisionNo: number;
  status: ChangeAuthorizationStatus;
  issuedAt: string;
  expiresAt: string | null;
  appliedAt: string | null;
  appliedByUserId: string | null;
  /** The target module's record that applied it, e.g. `Schedule.ProjectBaseline:{id}`. */
  appliedReference: string | null;
}

/** A change request with its latest recorded evaluation and the authorisations its approval issued. */
export interface ChangeRequestDetail {
  id: string;
  projectId: string;
  title: NarrativeText;
  justification: NarrativeText;
  changeType: ChangeType;
  status: ChangeRequestStatus;
  /** Raised by one each time a RETURNED request is resubmitted. */
  revisionNo: number;
  requestedByUserId: string;
  submittedAt: string | null;
  /** Negative for a reduction; never zero. */
  costImpactSar: string | null;
  /** Calendar days, negative to bring the finish forward; never zero. */
  scheduleImpactDays: number | null;
  scopeImpact: NarrativeText | null;
  isContractualObligation: boolean;
  requestedGovernanceProfileItemId: string | null;
  /** Recorded when AHDA starts the review; null before. */
  materiality: MaterialityAssessment | null;
  authorizations: ChangeAuthorizationDetail[];
  implementedAt: string | null;
  closedAt: string | null;
  createdAt: string;
  createdBy: string;
  updatedAt: string;
  updatedBy: string;
}

/** PUT /change-requests/{id}: the request's own fields as a whole (R-5). No materiality: the server computes it. */
export interface ChangeRequestRequest {
  title: NarrativeTextRequest;
  justification: NarrativeTextRequest;
  costImpactSar: string | null;
  scheduleImpactDays: number | null;
  scopeImpact: NarrativeTextRequest | null;
  isContractualObligation: boolean;
  requestedGovernanceProfileItemId: string | null;
}

/** POST /change-requests: raised DRAFT, of a change type that never changes. */
export interface ChangeRequestCreateRequest extends ChangeRequestRequest {
  projectId: string;
  changeType: ChangeType;
}
