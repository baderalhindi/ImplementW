import { type NarrativeText, type NarrativeTextRequest } from '@/features/projects/api/types.ts';

// The WF-09 and WF-10 representations as the API serialises them (TASK-062 suspension.md §4, TASK-063 closure.md §4):
// enums in SNAKE_CASE_UPPER, ids as strings, dates as ISO 8601.

// ---------------------------------------------------------------------------------------------------------------------
// WF-09 suspension and resumption

export type SuspensionRequestType = 'SUSPEND' | 'RESUME';

/** The ERD's request lifecycle (TASK-062 D-3); REJECTED, WITHDRAWN and EFFECTED are final. */
export type SuspensionRequestStatus =
  | 'DRAFT'
  | 'SUBMITTED'
  | 'UNDER_REVIEW'
  | 'RETURNED'
  | 'APPROVED'
  | 'REJECTED'
  | 'WITHDRAWN'
  | 'EFFECTED';

export type SuspensionEndReason = 'RESUMED' | 'PROJECT_CLOSED';

/** A suspension period: opened by an effected suspension, ended by an effected resumption or the project's closure. */
export interface ActiveSuspensionDetail {
  id: string;
  projectId: string;
  suspensionRequestId: string;
  startedAt: string;
  endedAt: string | null;
  endReason: SuspensionEndReason | null;
  resumptionRequestId: string | null;
}

export interface SuspensionRequestDetail {
  id: string;
  projectId: string;
  requestType: SuspensionRequestType;
  status: SuspensionRequestStatus;
  revisionNo: number;
  reason: NarrativeText;
  requestedByUserId: string;
  submittedAt: string | null;
  requestedEffectiveDate: string | null;
  /** A suspension's planning information only: nothing resumes on it (BR-SUS-031). */
  plannedResumptionDate: string | null;
  effectedAt: string | null;
  /** The period its effect opened or ended; null until EFFECTED. */
  suspension: ActiveSuspensionDetail | null;
  createdAt: string;
  createdBy: string;
  updatedAt: string;
  updatedBy: string;
}

/** A DRAFT or RETURNED request's fields as a whole (PUT, If-Match). */
export interface SuspensionRequestRequest {
  reason: NarrativeTextRequest;
  requestedEffectiveDate: string | null;
  plannedResumptionDate: string | null;
}

export interface SuspensionRequestCreateRequest extends SuspensionRequestRequest {
  projectId: string;
  requestType: SuspensionRequestType;
}

// ---------------------------------------------------------------------------------------------------------------------
// WF-10 completion and closure

/** One machine for both kinds of case (TASK-063 D-3); REJECTED, WITHDRAWN and EFFECTED are final. */
export type CloseoutCaseStatus =
  | 'DRAFT'
  | 'SUBMITTED'
  | 'UNDER_REVIEW'
  | 'RETURNED'
  | 'APPROVED'
  | 'REJECTED'
  | 'WITHDRAWN'
  | 'EFFECTED';

/** How a closure ends the project: after its effected completion, or from SUSPENDED without one. */
export type ProjectOutcome = 'COMPLETED' | 'TERMINATED_WITHOUT_COMPLETION';

export type ReadinessCheckCode =
  | 'DECISIONS_SETTLED'
  | 'TASKS_DISPOSITIONED'
  | 'SCHEDULE_RECONCILED'
  | 'MILESTONES_DISPOSITIONED'
  | 'RISKS_DISPOSITIONED'
  | 'ISSUES_DISPOSITIONED'
  | 'CHANGES_DISPOSITIONED'
  | 'SUSPENSION_REQUESTS_SETTLED'
  | 'PROGRESS_REPORTED'
  | 'FINANCIALS_SETTLED'
  | 'OBLIGATIONS_OWNED'
  | 'OBLIGATIONS_SATISFIED';

export type ReadinessResult = 'PASS' | 'FAIL' | 'WAIVED';

/** The server's roll-up of the latest evaluation and the waivers since (TASK-063 D-5). */
export type ReadinessStatus = 'READY' | 'READY_WITH_CONDITIONS' | 'NOT_READY' | 'INCOMPLETE';

export interface ReadinessCheckDetail {
  checkCode: ReadinessCheckCode;
  result: ReadinessResult;
  /** How many of the project's records block the criterion; finding them is the owning module's register (F-7). */
  blockingCount: number;
  waivable: boolean;
  waivedByUserId: string | null;
  waiverReason: NarrativeText | null;
}

export interface ReadinessDetail {
  status: ReadinessStatus;
  evaluatedAt: string | null;
  checks: ReadinessCheckDetail[];
}

interface CloseoutCaseBase {
  id: string;
  projectId: string;
  status: CloseoutCaseStatus;
  revisionNo: number;
  requestedByUserId: string;
  submittedAt: string | null;
  effectedAt: string | null;
  readiness: ReadinessDetail;
  createdAt: string;
  createdBy: string;
  updatedAt: string;
  updatedBy: string;
}

/** Stage 1: ACTIVE → COMPLETED. */
export interface CompletionCaseDetail extends CloseoutCaseBase {
  actualProjectCompletionDate: string | null;
  completionNarrative: NarrativeText | null;
}

/** Stage 2: COMPLETED → CLOSED, or SUSPENDED → CLOSED on the terminal path (no completion case). */
export interface ClosureCaseDetail extends CloseoutCaseBase {
  completionCaseId: string | null;
  outcome: ProjectOutcome;
  closureNarrative: NarrativeText | null;
}

export interface CompletionCaseRequest {
  actualProjectCompletionDate: string | null;
  completionNarrative: NarrativeTextRequest | null;
}

export interface CompletionCaseCreateRequest extends CompletionCaseRequest {
  projectId: string;
}

export interface ClosureCaseRequest {
  closureNarrative: NarrativeTextRequest | null;
}

export interface ClosureCaseCreateRequest extends ClosureCaseRequest {
  projectId: string;
}

export interface WaiveCheckCommand {
  checkCode: ReadinessCheckCode;
  reason: NarrativeTextRequest;
}

export type PostProjectObligationStatus =
  'OPEN' | 'IN_PROGRESS' | 'SATISFIED' | 'WAIVED' | 'CANCELLED';

export interface PostProjectObligationDetail {
  id: string;
  projectId: string;
  completionCaseId: string | null;
  closureCaseId: string | null;
  title: NarrativeText;
  description: NarrativeText | null;
  ownerUserId: string | null;
  dueDate: string | null;
  status: PostProjectObligationStatus;
  satisfiedAt: string | null;
  createdAt: string;
  createdBy: string;
  updatedAt: string;
  updatedBy: string;
}

export interface PostProjectObligationRequest {
  title: NarrativeTextRequest;
  description: NarrativeTextRequest | null;
  ownerUserId: string | null;
  dueDate: string | null;
}

/** Recorded against the project's open or effected completion case, or its open terminal closure case: exactly one. */
export interface PostProjectObligationCreateRequest extends PostProjectObligationRequest {
  completionCaseId: string | null;
  closureCaseId: string | null;
}
