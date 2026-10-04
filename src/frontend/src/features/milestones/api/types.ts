import { type BusinessLinkDetail } from '@/features/documents/api/types.ts';
import { type NarrativeText, type NarrativeTextRequest } from '@/features/projects/api/types.ts';

// The milestone representations (TASK-050, milestone-achievement.md §4) as the API serialises them: enums in
// SNAKE_CASE_UPPER, ids as strings, calendar dates as ISO 8601 `YYYY-MM-DD` (UTC calendar dates, TASK-050 F-13).

/** WF-03's status of the shared milestone (ICD-04). ACHIEVED is set when WF-05 accepts a claim; ACHIEVED and CANCELLED are final. */
export type ProjectMilestoneStatus = 'PLANNED' | 'ACHIEVED' | 'CANCELLED';

/**
 * A revision of a claim (TASK-050 §3): born DRAFT, SUBMITTED to WF-11, then ACCEPTED or RETURNED. An ACCEPTED revision
 * becomes SUPERSEDED when a later one is accepted. RETURNED and SUPERSEDED are final.
 */
export type MilestoneAchievementStatus =
  'DRAFT' | 'SUBMITTED' | 'RETURNED' | 'ACCEPTED' | 'SUPERSEDED';

/**
 * The shared milestone as WF-03 holds it, with the ACTIVE baseline's date for it and the forecast's variance from it in
 * days, positive = late; both null when the ACTIVE baseline does not include it.
 */
export interface ProjectMilestoneDetail {
  id: string;
  projectId: string;
  projectScheduleId: string;
  scheduleActivityId: string | null;
  title: NarrativeText;
  milestoneCategoryItemId: string;
  forecastDate: string;
  status: ProjectMilestoneStatus;
  sortOrder: number;
  baselineId: string | null;
  baselinePlannedDate: string | null;
  forecastVarianceDays: number | null;
  createdAt: string;
  createdBy: string;
  updatedAt: string;
  updatedBy: string;
}

/** MOD-016's inputs, as a whole (R-5). The status is set by commands and by WF-05's acceptance, never sent. */
export interface ProjectMilestoneRequest {
  scheduleActivityId: string | null;
  title: NarrativeTextRequest;
  milestoneCategoryItemId: string;
  forecastDate: string;
  sortOrder: number;
}

export interface ProjectMilestoneCreateRequest extends ProjectMilestoneRequest {
  projectId: string;
}

/**
 * One revision of a milestone's achievement claim. `isCurrent` marks the ACCEPTED revision, whose accepted date is the
 * milestone's Actual Achievement Date. `returnReason` is the reviewer's, set on a RETURNED revision.
 */
export interface MilestoneAchievementDetail {
  id: string;
  projectMilestoneId: string;
  projectId: string;
  revisionNo: number;
  status: MilestoneAchievementStatus;
  isCurrent: boolean;
  claimedAchievementDate: string;
  acceptedActualAchievementDate: string | null;
  narrative: NarrativeText | null;
  submittedByUserId: string | null;
  submittedAt: string | null;
  reviewedByUserId: string | null;
  reviewedAt: string | null;
  returnReason: NarrativeText | null;
  supersededByAchievementId: string | null;
  projectIntakeId: string | null;
  createdAt: string;
  createdBy: string;
  updatedAt: string;
  updatedBy: string;
}

/** A DRAFT's claim, as a whole (R-5). */
export interface MilestoneAchievementRequest {
  claimedAchievementDate: string;
  narrative: NarrativeTextRequest | null;
}

/** The next revision: a first claim, a claim after a return, or a correction of an accepted achievement. */
export interface MilestoneAchievementCreateRequest extends MilestoneAchievementRequest {
  projectMilestoneId: string;
}

/**
 * A revision's evidence (PTBC-006): the EVIDENCE_POLICY version in force, the evidence types it makes mandatory for
 * the milestone's category, the types the revision holds satisfying evidence for, and its links.
 * `evidencePolicyVersionId` is null while no EVIDENCE_POLICY version is published: nothing is mandatory yet.
 */
export interface MilestoneEvidenceDetail {
  milestoneAchievementId: string;
  evidencePolicyVersionId: string | null;
  mandatoryEvidenceTypeItemIds: string[];
  satisfiedEvidenceTypeItemIds: string[];
  links: BusinessLinkDetail[];
}

export interface MilestoneEvidenceRequest {
  documentId: string;
  documentVersionId: string;
  evidenceTypeItemId: string;
}
