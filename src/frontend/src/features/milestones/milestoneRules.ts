import { checkText } from '@/features/identity-access/forms.ts';
import { narrativeRequest } from '@/features/progress/progressUpdate.ts';
import { type ProjectSummary } from '@/features/projects/api/types.ts';
import { TEXT_LENGTH } from '@/features/projects/registration.ts';
import { dayNumber } from '@/features/schedule/dependencyRules.ts';
import { type Language } from '@/shared/i18n/i18n.ts';

import {
  type MilestoneAchievementDetail,
  type MilestoneAchievementRequest,
  type MilestoneEvidenceDetail,
  type ProjectMilestoneDetail,
  type ProjectMilestoneRequest,
} from './api/types.ts';

// The rules the milestone screens apply before anything is sent. Each mirrors the API (TASK-050 §2–§5), which stays the
// authority: whatever it refuses is shown as it comes.

/** A milestone and the project it belongs to, as SCR-062 lists them. */
export interface MilestoneEntry {
  milestone: ProjectMilestoneDetail;
  project: ProjectSummary;
  /** The milestone's revisions, newest first; null when the caller may not read claims (no MILESTONE_VIEW). */
  revisions: MilestoneAchievementDetail[] | null;
}

/** A milestone's revisions, newest first. */
export function revisionsOf(
  milestoneId: string,
  achievements: MilestoneAchievementDetail[],
): MilestoneAchievementDetail[] {
  return achievements
    .filter((revision) => revision.projectMilestoneId === milestoneId)
    .sort((a, b) => b.revisionNo - a.revisionNo);
}

/** The revision being prepared or reviewed, DRAFT or SUBMITTED: a milestone has at most one (TASK-050 D-4). */
export function openRevision(
  revisions: MilestoneAchievementDetail[],
): MilestoneAchievementDetail | null {
  return (
    revisions.find((revision) => revision.status === 'DRAFT' || revision.status === 'SUBMITTED') ??
    null
  );
}

/** The ACCEPTED revision, whose accepted date is the milestone's Actual Achievement Date. */
export function currentRevision(
  revisions: MilestoneAchievementDetail[],
): MilestoneAchievementDetail | null {
  return revisions.find((revision) => revision.isCurrent) ?? null;
}

/**
 * Where a milestone's claim stands, from its newest revision. `accepted` is the current accepted revision, kept while
 * a correction is prepared, reviewed or returned (TASK-050 D-4).
 */
export type AchievementState =
  | { kind: 'unclaimed' }
  | {
      kind: 'draft';
      revision: MilestoneAchievementDetail;
      accepted: MilestoneAchievementDetail | null;
    }
  | {
      kind: 'submitted';
      revision: MilestoneAchievementDetail;
      accepted: MilestoneAchievementDetail | null;
    }
  | {
      kind: 'returned';
      revision: MilestoneAchievementDetail;
      accepted: MilestoneAchievementDetail | null;
    }
  | { kind: 'accepted'; revision: MilestoneAchievementDetail };

export function achievementState(revisions: MilestoneAchievementDetail[]): AchievementState {
  const latest = revisions[0];
  if (latest === undefined) {
    return { kind: 'unclaimed' };
  }
  const accepted = currentRevision(revisions);
  switch (latest.status) {
    case 'DRAFT':
      return { kind: 'draft', revision: latest, accepted };
    case 'SUBMITTED':
      return { kind: 'submitted', revision: latest, accepted };
    case 'RETURNED':
      return { kind: 'returned', revision: latest, accepted };
    case 'ACCEPTED':
    case 'SUPERSEDED':
      // The newest revision is SUPERSEDED only if the data is inconsistent; the accepted one still answers.
      return accepted === null ? { kind: 'unclaimed' } : { kind: 'accepted', revision: accepted };
  }
}

/** What the next claim is, or null when none can be opened now (one is open, or the milestone is cancelled). */
export type ClaimKind = 'first' | 'afterReturn' | 'correction';

export function nextClaimKind(
  milestone: Pick<ProjectMilestoneDetail, 'status'>,
  revisions: MilestoneAchievementDetail[],
): ClaimKind | null {
  if (milestone.status === 'CANCELLED' || openRevision(revisions) !== null) {
    return null;
  }
  if (currentRevision(revisions) !== null) {
    return 'correction';
  }
  return revisions[0]?.status === 'RETURNED' ? 'afterReturn' : 'first';
}

/** The whole days a PLANNED milestone's forecast has passed; null when it is not overdue, so one due today is not. */
export function overdueDays(
  milestone: Pick<ProjectMilestoneDetail, 'status' | 'forecastDate'>,
  today: string,
): number | null {
  if (milestone.status !== 'PLANNED') {
    return null;
  }
  const days = dayNumber(today) - dayNumber(milestone.forecastDate);
  return days > 0 ? days : null;
}

/**
 * The evidence rule in force for a revision, as the API states it (PTBC-006; TASK-050 D-8):
 * - `pending`: no EVIDENCE_POLICY version is published, so nothing is mandatory yet and the API accepts a claim without
 *   evidence. The screen says so in words, and asks before submitting a claim with none.
 * - `notRequired`: a policy is in force and makes no evidence mandatory for the milestone's category.
 * - `required`: a policy makes these types mandatory; `missing` are those the revision holds no satisfying evidence for
 *   (VALID, on an active link, pinned to a CLEAN version). The API refuses submission until none is missing.
 * - `undetermined`: the policy could not be read unambiguously (422 CONFIGURATION_MISSING); the API fails closed.
 */
export type EvidenceRule =
  | { kind: 'pending' }
  | { kind: 'notRequired' }
  | { kind: 'required'; mandatory: string[]; missing: string[] }
  | { kind: 'undetermined' };

export function evidenceRule(evidence: MilestoneEvidenceDetail): EvidenceRule {
  if (evidence.evidencePolicyVersionId === null) {
    return { kind: 'pending' };
  }
  const mandatory = evidence.mandatoryEvidenceTypeItemIds;
  if (mandatory.length === 0) {
    return { kind: 'notRequired' };
  }
  const satisfied = new Set(evidence.satisfiedEvidenceTypeItemIds);
  return { kind: 'required', mandatory, missing: mandatory.filter((id) => !satisfied.has(id)) };
}

/** Whether the rule lets the claim be submitted: the API refuses a missing mandatory type, and an undetermined policy. */
export function evidenceAllowsSubmission(rule: EvidenceRule): boolean {
  switch (rule.kind) {
    case 'pending':
    case 'notRequired':
      return true;
    case 'required':
      return rule.missing.length === 0;
    case 'undetermined':
      return false;
  }
}

/** The pieces of evidence still in force: VALID, on a link not ended. Withdrawn ones stay as history. */
export function liveEvidence(evidence: MilestoneEvidenceDetail) {
  return evidence.links
    .filter((link) => link.unlinkedAt === null)
    .flatMap((link) => link.evidence.filter((piece) => piece.status === 'VALID'));
}

/** SCR-062's filters: one each for what a person looks for in a register of milestones. */
export type RegisterFilter =
  'all' | 'planned' | 'overdue' | 'draft' | 'inReview' | 'returned' | 'achieved' | 'cancelled';

export const REGISTER_FILTERS: readonly RegisterFilter[] = [
  'all',
  'planned',
  'overdue',
  'draft',
  'inReview',
  'returned',
  'achieved',
  'cancelled',
];

export function matchesFilter(entry: MilestoneEntry, filter: RegisterFilter, today: string) {
  const { milestone, revisions } = entry;
  const state = revisions === null ? null : achievementState(revisions).kind;
  switch (filter) {
    case 'all':
      return true;
    case 'planned':
      return milestone.status === 'PLANNED';
    case 'overdue':
      return overdueDays(milestone, today) !== null;
    case 'draft':
      return state === 'draft';
    case 'inReview':
      return state === 'submitted';
    case 'returned':
      return state === 'returned';
    case 'achieved':
      return milestone.status === 'ACHIEVED';
    case 'cancelled':
      return milestone.status === 'CANCELLED';
  }
}

/** Earliest forecast first, then by project and title, so a register reads as a timeline. */
export function byForecast(a: MilestoneEntry, b: MilestoneEntry): number {
  return (
    a.milestone.forecastDate.localeCompare(b.milestone.forecastDate) ||
    a.project.title.text.localeCompare(b.project.title.text) ||
    a.milestone.title.text.localeCompare(b.milestone.title.text)
  );
}

/** MOD-016's inputs as typed. */
export interface MilestoneFormValues {
  title: string;
  milestoneCategoryItemId: string;
  forecastDate: string;
  scheduleActivityId: string;
  sortOrder: string;
}

export const EMPTY_MILESTONE_FORM: MilestoneFormValues = {
  title: '',
  milestoneCategoryItemId: '',
  forecastDate: '',
  scheduleActivityId: '',
  sortOrder: '',
};

export function milestoneFormValuesOf(milestone: ProjectMilestoneDetail): MilestoneFormValues {
  return {
    title: milestone.title.text,
    milestoneCategoryItemId: milestone.milestoneCategoryItemId,
    forecastDate: milestone.forecastDate,
    scheduleActivityId: milestone.scheduleActivityId ?? '',
    sortOrder: String(milestone.sortOrder),
  };
}

const SORT_ORDER_PATTERN = /^\d{1,9}$/;

/** The API's checks of a milestone's inputs (ProjectMilestoneRequest): a title, a category, a date, a sort order ≥ 0. */
export function checkMilestoneForm(values: MilestoneFormValues): Record<string, string | null> {
  const sortOrder = values.sortOrder.trim();
  return {
    title: checkText(values.title, { required: true, maxLength: TEXT_LENGTH }),
    milestoneCategoryItemId: values.milestoneCategoryItemId === '' ? 'REQUIRED' : null,
    forecastDate: values.forecastDate === '' ? 'REQUIRED' : null,
    sortOrder: sortOrder === '' || SORT_ORDER_PATTERN.test(sortOrder) ? null : 'OUT_OF_RANGE',
  };
}

export function toMilestoneRequest(
  values: MilestoneFormValues,
  language: Language,
  before: ProjectMilestoneDetail | null,
): ProjectMilestoneRequest {
  const sortOrder = values.sortOrder.trim();
  return {
    title: narrativeRequest(values.title, language, before?.title ?? null),
    milestoneCategoryItemId: values.milestoneCategoryItemId,
    forecastDate: values.forecastDate,
    scheduleActivityId: values.scheduleActivityId === '' ? null : values.scheduleActivityId,
    sortOrder: sortOrder === '' ? 0 : Number(sortOrder),
  };
}

/** A claim as typed: the date the milestone was achieved on, and what the claimant says of it. */
export interface ClaimValues {
  claimedAchievementDate: string;
  narrative: string;
}

/** A claim is made once the milestone has been achieved: its date is not after today, UTC (TASK-050 D-12). */
export function checkClaim(values: ClaimValues, today: string): Record<string, string | null> {
  const date = values.claimedAchievementDate;
  return {
    claimedAchievementDate: date === '' ? 'REQUIRED' : date > today ? 'DATE_IN_FUTURE' : null,
    narrative: checkText(values.narrative, { maxLength: TEXT_LENGTH }),
  };
}

export function toClaimRequest(
  values: ClaimValues,
  language: Language,
  before: MilestoneAchievementDetail | null,
): MilestoneAchievementRequest {
  return {
    claimedAchievementDate: values.claimedAchievementDate,
    narrative:
      values.narrative.trim() === ''
        ? null
        : narrativeRequest(values.narrative, language, before?.narrative ?? null),
  };
}
