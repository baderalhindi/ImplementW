import { type NarrativeText, type NarrativeTextRequest } from '@/features/projects/api/types.ts';

// WF-13's representations as the TASK-066 API serves them (external-participation.md §4; openapi.v1.json, tag
// ExternalParticipation). Enums SNAKE_UPPER, ids as strings, instants ISO 8601, dates `yyyy-MM-dd`.
//
// One representation per resource, two audiences (TASK-066 D-6): an external caller gets the least-disclosure
// projection — `projection` EXTERNAL, every internal-only field named in `maskedFields` and absent from the JSON
// (R-20(b)). Those fields are optional here for that reason; projection.ts lists them.

/** AHDA drafts, issues; the entity answers; AHDA reviews; it closes or is cancelled (TASK-066 §3). */
export type ExternalUpdateRequestStatus =
  'DRAFT' | 'ISSUED' | 'IN_PROGRESS' | 'RESPONDED' | 'CLOSED' | 'CANCELLED';

/** One revision of the entity's answer, immutable once submitted (TASK-066 D-5). */
export type ExternalContributionStatus =
  | 'DRAFT'
  | 'SUBMITTED'
  | 'UNDER_REVIEW'
  | 'RETURNED'
  | 'REJECTED'
  | 'ACCEPTED_PENDING_APPLICATION'
  | 'APPLIED'
  | 'APPLICATION_FAILED';

/** An application attempt as made: a CONFLICT is a recorded state, not an error (TASK-066 D-10). */
export type SourceApplicationStatus = 'APPLIED' | 'CONFLICT' | 'FAILED';

/** Derived on read, never stored: whether the entity's answer is due (EXT-F-020). DUE_SOON is not built (TASK-066 F-1). */
export type ResponseDueCondition = 'NOT_APPLICABLE' | 'NOT_DUE' | 'DUE' | 'OVERDUE';

/** AHDA's full view, or the external least-disclosure projection (WF-13 §8.2). */
export type ParticipationAudience = 'INTERNAL' | 'EXTERNAL';

/** A typed source is applied after review; a reference-only answer is kept as accepted input (TASK-066 D-8). */
export type ContributionApplicationMode = 'REFERENCE_ONLY' | 'UPDATE_ALLOWED_SOURCE_FIELDS';

export type ContributionFieldType = 'NARRATIVE' | 'NUMBER' | 'DATE';

/** One field of the request's typed schema: the form to render (`responseFields`). */
export interface ContributionFieldDefinition {
  fieldCode: string;
  fieldType: ContributionFieldType;
  required: boolean;
  minimum: number | null;
  maximum: number | null;
}

/** A stored value in its canonical text form; a narrative carries its language. */
export interface ContributionFieldValue {
  fieldCode: string;
  value: string;
  language: 'AR' | 'EN' | null;
}

export interface ExternalUpdateRequestDetail {
  id: string;
  projectId: string;
  /** The project as the entity may see it; null until AHDA approves the registration. */
  formalProjectId: string | null;
  externalEntityId: string;
  origin: 'AHDA_ISSUED';
  contributionTypeItemId: string;
  /** The typed schema it is answered in, pinned when drafted: TASK_PROGRESS or PROJECT_INFORMATION. */
  contributionSchemaCode: string;
  applicationMode: ContributionApplicationMode;
  targetModule: string | null;
  targetType: string | null;
  targetId: string | null;
  /** The source record's safe label (a task's title, WF-13 §8.2). */
  targetLabel: NarrativeText | null;
  responseFields: ContributionFieldDefinition[];
  instructions: NarrativeText;
  responsibleUserId: string | null;
  /** Internal only. */
  reviewerUserId?: string | null;
  dueDate: string | null;
  dueCondition: ResponseDueCondition;
  status: ExternalUpdateRequestStatus;
  /** Internal only. */
  participationConfigurationVersionId?: string | null;
  /** Internal only. */
  issuedByUserId?: string | null;
  issuedAt: string | null;
  cancelledAt: string | null;
  cancellationReason: NarrativeText | null;
  closedAt: string | null;
  createdAt: string;
  /** Internal only. */
  createdBy?: string | null;
  updatedAt: string;
  /** Internal only. */
  updatedBy?: string | null;
  projection: ParticipationAudience;
  maskedFields: string[];
}

export interface ExternalContributionDetail {
  id: string;
  externalUpdateRequestId: string;
  projectId: string;
  externalEntityId: string;
  revisionNo: number;
  /** The RETURNED revision this one corrects. */
  previousRevisionId: string | null;
  status: ExternalContributionStatus;
  contributorUserId: string;
  fields: ContributionFieldValue[];
  submittedAt: string | null;
  /** Internal only: the source's row version when the answer was submitted. */
  targetVersion?: number | null;
  /** Internal only: the source's state when the answer was submitted. */
  targetState?: string | null;
  /** Internal only. */
  reviewedByUserId?: string | null;
  /** Internal only. */
  reviewStartedAt?: string | null;
  reviewedAt: string | null;
  /** The reason the entity reads for a return or a rejection (EXT-F-127). */
  reviewReason: NarrativeText | null;
  /** Internal only: AHDA's own note on the decision (EXT-F-128). */
  reviewInternalNote?: NarrativeText | null;
  createdAt: string;
  /** Internal only. */
  createdBy?: string | null;
  updatedAt: string;
  /** Internal only. */
  updatedBy?: string | null;
  projection: ParticipationAudience;
  maskedFields: string[];
}

/** One attempt to apply an accepted revision to its source; AHDA's only (an external caller reads none). */
export interface SourceApplicationDetail {
  id: string;
  externalContributionId: string;
  externalUpdateRequestId: string;
  attemptNo: number;
  status: SourceApplicationStatus;
  applicationMode: ContributionApplicationMode;
  targetModule: string | null;
  targetType: string | null;
  targetId: string | null;
  /** The source version the attempt expected: the one answered against, or the one a revalidation confirmed. */
  expectedTargetRevisionNo: number | null;
  /** The version found; differs from the expected one on a CONFLICT. */
  actualTargetRevisionNo: number | null;
  /** A safe code (EXT-F-156): SOURCE_RECORD_NOT_FOUND, SOURCE_RECORD_TERMINAL, SOURCE_RECORD_STATE_INVALID, … */
  failureCode: string | null;
  attemptedByUserId: string;
  attemptedAt: string;
  completedAt: string;
  revalidatedAt: string | null;
  revalidatedByUserId: string | null;
  revalidatedTargetRevisionNo: number | null;
  correlationId: string;
}

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

/** PUT /external-update-requests/{id}: a DRAFT's fields as a whole (R-5). */
export interface ExternalUpdateRequestRequest {
  contributionTypeItemId: string;
  targetId: string | null;
  instructions: NarrativeTextRequest;
  responsibleUserId: string | null;
  reviewerUserId: string | null;
  dueDate: string | null;
}

/** POST /external-update-requests: one project, one entity, one typed purpose (BR-EXT-004). */
export interface ExternalUpdateRequestCreateRequest extends ExternalUpdateRequestRequest {
  projectId: string;
  externalEntityId: string;
}

/** One answered field as sent: a narrative with its language, any other value with none. */
export interface ContributionFieldRequest {
  fieldCode: string;
  value: string;
  language: 'ar' | 'en' | null;
}

/** A review decision's words: the reason the entity reads, and AHDA's internal note. */
export interface ContributionDecision {
  reason: NarrativeTextRequest | null;
  internalNote: NarrativeTextRequest | null;
}
