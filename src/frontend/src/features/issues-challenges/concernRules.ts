import { checkText } from '@/features/identity-access/forms.ts';
import { narrativeRequest } from '@/features/progress/progressUpdate.ts';
import { type ProjectSummary } from '@/features/projects/api/types.ts';
import { TEXT_LENGTH } from '@/features/projects/registration.ts';
import { type RiskMatrix, SEVERITY_STEPS } from '@/features/risks/riskRules.ts';
import { type Language } from '@/shared/i18n/i18n.ts';

import {
  type ConcernAssessCommand,
  type ConcernDetail,
  type ConcernEscalationDetail,
  type ConcernEscalationStatus,
  type ConcernRequest,
  type ConcernStatus,
} from './api/types.ts';

// The rules the issue, challenge and escalation screens apply before anything is sent, and how they order and count
// what they show. Each mirrors the API (TASK-057 §2–§5), which stays the authority. Nothing here computes a severity:
// it is the server's, from the impacts and the RISK_MATRIX version it pinned (D-4); the screens show it, never edit it.

/** A concern and the project it belongs to, as the registers list them. */
export interface ConcernEntry {
  concern: ConcernDetail;
  project: ProjectSummary;
}

export function isOpen(concern: Pick<ConcernDetail, 'status'>): boolean {
  return concern.status !== 'CLOSED';
}

/** Still being worked on: neither resolved nor closed. */
export function isUnresolved(concern: Pick<ConcernDetail, 'status'>): boolean {
  return concern.status !== 'RESOLVED' && concern.status !== 'CLOSED';
}

/** The states an edit, assessment or assignment is taken in; from PENDING_VALIDATION the fields are fixed (D-3). */
export const EDITABLE_STATUSES: readonly ConcernStatus[] = ['OPEN', 'ASSIGNED', 'IN_PROGRESS'];

/** The states an escalation is raised, and a review recorded, in (D-11). */
export const ESCALATABLE_STATUSES: readonly ConcernStatus[] = [
  ...EDITABLE_STATUSES,
  'PENDING_VALIDATION',
];

/** Unresolved, and its target resolution date has passed (UTC, TASK-057 F-16). */
export function isOverdue(
  concern: Pick<ConcernDetail, 'status' | 'targetResolutionDate'>,
  today: string,
): boolean {
  return (
    isUnresolved(concern) &&
    concern.targetResolutionDate !== null &&
    concern.targetResolutionDate < today
  );
}

/** Unresolved, and its next review date has come (ADR-015's cadence, TASK-057 D-9). */
export function isReviewDue(
  concern: Pick<ConcernDetail, 'status' | 'nextReviewDate'>,
  today: string,
): boolean {
  return isUnresolved(concern) && concern.nextReviewDate <= today;
}

/** A resolution validation sent back: the concern is worked on again, its resolution kept for correction. */
export function isReturned(
  concern: Pick<ConcernDetail, 'status' | 'revisionNo' | 'resolution'>,
): boolean {
  return concern.status === 'IN_PROGRESS' && concern.revisionNo > 1 && concern.resolution !== null;
}

// ---------------------------------------------------------------------------------------------------------------------
// The registers (SCR-083, SCR-085)

export const REGISTER_VIEWS = [
  'open',
  'mine',
  'escalated',
  'overdue',
  'pendingValidation',
  'closed',
  'all',
] as const;
export type RegisterView = (typeof REGISTER_VIEWS)[number];

export function matchesView(
  concern: ConcernDetail,
  view: RegisterView,
  today: string,
  userId: string,
): boolean {
  switch (view) {
    case 'open':
      return isOpen(concern);
    case 'mine':
      return isOpen(concern) && concern.assigneeUserId === userId;
    case 'escalated':
      return concern.openEscalation !== null;
    case 'overdue':
      return isOverdue(concern, today);
    case 'pendingValidation':
      return concern.status === 'PENDING_VALIDATION';
    case 'closed':
      return concern.status === 'CLOSED';
    case 'all':
      return true;
  }
}

/** The filter value that matches any severity or priority. */
export const ANY = '';

/** The filter value for a concern not yet assessed, so it has no severity. */
export const UNASSESSED = 'unassessed';

export function matchesSeverity(concern: ConcernDetail, severity: string): boolean {
  if (severity === ANY) {
    return true;
  }
  return severity === UNASSESSED
    ? concern.severityItemId === null
    : concern.severityItemId === severity;
}

export function matchesPriority(concern: ConcernDetail, priority: string): boolean {
  return priority === ANY || concern.priorityItemId === priority;
}

/**
 * Most severe first: the overall impact the server derived the severity from (higher first), then unassessed, closed
 * last; within each, the earliest target resolution date, then the title. The severity items carry no rank the SPA
 * could rely on, and the overall impact is the input of the server's rule.
 */
export function bySeverity(a: ConcernEntry, b: ConcernEntry): number {
  const rank = (concern: ConcernDetail) =>
    concern.status === 'CLOSED' ? -2 : (concern.overallImpactLevel ?? -1);
  return (
    rank(b.concern) - rank(a.concern) ||
    (a.concern.targetResolutionDate ?? '9999-12-31').localeCompare(
      b.concern.targetResolutionDate ?? '9999-12-31',
    ) ||
    a.concern.title.text.localeCompare(b.concern.title.text)
  );
}

/** A severity's colour step: its overall impact level on the five-level scale (ADR-011), never its label. */
export function severityStep(overallImpactLevel: number | null): number | null {
  return overallImpactLevel === null
    ? null
    : Math.min(Math.max(overallImpactLevel, 1), SEVERITY_STEPS);
}

export interface ConcernSummary {
  open: number;
  /** OPEN: raised, nobody assigned yet. */
  unassigned: number;
  escalated: number;
  overdue: number;
  reviewDue: number;
  pendingValidation: number;
}

export function concernSummary(concerns: ConcernDetail[], today: string): ConcernSummary {
  const count = (predicate: (concern: ConcernDetail) => boolean) =>
    concerns.filter(predicate).length;
  return {
    open: count(isOpen),
    unassigned: count((concern) => concern.status === 'OPEN'),
    escalated: count((concern) => concern.openEscalation !== null),
    overdue: count((concern) => isOverdue(concern, today)),
    reviewDue: count((concern) => isReviewDue(concern, today)),
    pendingValidation: count((concern) => concern.status === 'PENDING_VALIDATION'),
  };
}

// ---------------------------------------------------------------------------------------------------------------------
// MOD-036, MOD-037, MOD-038

export interface ConcernFormValues {
  title: string;
  description: string;
  categoryItemId: string;
  priorityItemId: string;
  targetResolutionDate: string;
}

export function emptyConcernForm(): ConcernFormValues {
  return {
    title: '',
    description: '',
    categoryItemId: '',
    priorityItemId: '',
    targetResolutionDate: '',
  };
}

export function concernFormValuesOf(concern: ConcernDetail): ConcernFormValues {
  return {
    title: concern.title.text,
    description: concern.description.text,
    categoryItemId: concern.categoryItemId,
    priorityItemId: concern.priorityItemId,
    targetResolutionDate: concern.targetResolutionDate ?? '',
  };
}

/**
 * The API's checks (ConcernRequest, D-11): title, description, category and priority required; a target resolution
 * date today or later when it is set or changed, so an edit keeps a date that has since passed.
 */
export function checkConcernForm(
  values: ConcernFormValues,
  today: string,
  before: ConcernDetail | null,
): Record<string, string | null> {
  const target = values.targetResolutionDate;
  const changed = target !== (before?.targetResolutionDate ?? '');
  return {
    title: checkText(values.title, { required: true, maxLength: TEXT_LENGTH }),
    description: checkText(values.description, { required: true, maxLength: TEXT_LENGTH }),
    categoryItemId: values.categoryItemId === '' ? 'REQUIRED' : null,
    priorityItemId: values.priorityItemId === '' ? 'REQUIRED' : null,
    targetResolutionDate: target !== '' && changed && target < today ? 'DATE_IN_PAST' : null,
  };
}

/** The concern's own fields; a severity is never part of it (acceptance criterion: severity is not editable). */
export function toConcernRequest(
  values: ConcernFormValues,
  language: Language,
  before: ConcernDetail | null,
): ConcernRequest {
  return {
    title: narrativeRequest(values.title, language, before?.title ?? null),
    description: narrativeRequest(values.description, language, before?.description ?? null),
    categoryItemId: values.categoryItemId,
    priorityItemId: values.priorityItemId,
    targetResolutionDate: values.targetResolutionDate === '' ? null : values.targetResolutionDate,
  };
}

// ---------------------------------------------------------------------------------------------------------------------
// Impact assessment

/** Dimension id → chosen level, '' when the dimension does not apply (VAL-ISS-006). */
export type ImpactValues = Record<string, string>;

/** The concern's current levels that still fit the version in force; any other starts as not applicable. */
export function impactValuesOf(matrix: RiskMatrix, concern: ConcernDetail): ImpactValues {
  return Object.fromEntries(
    matrix.dimensions.map((dimension) => {
      const level = concern.impacts.find(
        (impact) => impact.impactDimensionItemId === dimension.id,
      )?.impactLevel;
      const fits = dimension.levels.some((entry) => entry.level === level);
      return [dimension.id, fits && level !== undefined ? String(level) : ''];
    }),
  );
}

function chosen(values: ImpactValues): { id: string; level: number }[] {
  return Object.entries(values)
    .filter(([, level]) => level !== '')
    .map(([id, level]) => ({ id, level: Number(level) }));
}

/** At least one dimension, as the API requires (`impacts` REQUIRED). */
export function checkImpacts(values: ImpactValues): Record<string, string | null> {
  return { impacts: chosen(values).length === 0 ? 'REQUIRED' : null };
}

/** The highest chosen level: what the server will take as the overall impact (BR-ISS-008), shown as a preview. */
export function overallImpactOf(values: ImpactValues): number | null {
  const levels = chosen(values).map((entry) => entry.level);
  return levels.length === 0 ? null : Math.max(...levels);
}

export function toAssessCommand(values: ImpactValues): ConcernAssessCommand {
  return {
    impacts: chosen(values).map((entry) => ({
      impactDimensionItemId: entry.id,
      impactLevel: entry.level,
      rationale: null,
    })),
  };
}

// ---------------------------------------------------------------------------------------------------------------------
// Resolutions, escalations

/** A resolution, an escalation's reason or its direction: required, never blank (400 REQUIRED). */
export function checkNarrative(field: string, text: string): Record<string, string | null> {
  return { [field]: checkText(text, { required: true, maxLength: TEXT_LENGTH }) };
}

/** An escalation with the concern and project it belongs to, as SCR-087 lists them. */
export interface EscalationEntry {
  escalation: ConcernEscalationDetail;
  concern: ConcernDetail;
  project: ProjectSummary;
}

export const ESCALATION_VIEWS = ['open', 'resolved', 'withdrawn', 'all'] as const;
export type EscalationView = (typeof ESCALATION_VIEWS)[number];

/** Acceptance criterion: the Escalations list opens on the open escalations only. */
export const DEFAULT_ESCALATION_VIEW: EscalationView = 'open';

const VIEW_STATUS: Record<Exclude<EscalationView, 'all'>, ConcernEscalationStatus> = {
  open: 'OPEN',
  resolved: 'RESOLVED',
  withdrawn: 'WITHDRAWN',
};

export function matchesEscalationView(
  escalation: Pick<ConcernEscalationDetail, 'status'>,
  view: EscalationView,
): boolean {
  return view === 'all' || escalation.status === VIEW_STATUS[view];
}

/** Every concern carries its OPEN escalation, so the open view needs no read of each concern's history. */
export function needsHistory(view: EscalationView): boolean {
  return view !== 'open';
}

/** The open escalations, from the concerns that carry one. */
export function openEscalationsOf(entries: ConcernEntry[]): EscalationEntry[] {
  return entries.flatMap(({ concern, project }) =>
    concern.openEscalation === null
      ? []
      : [{ escalation: concern.openEscalation, concern, project }],
  );
}

/** Open first, then the most recently raised. */
export function byEscalation(a: EscalationEntry, b: EscalationEntry): number {
  const open = (entry: EscalationEntry) => (entry.escalation.status === 'OPEN' ? 0 : 1);
  return open(a) - open(b) || b.escalation.escalatedAt.localeCompare(a.escalation.escalatedAt);
}

const DAY_MS = 86_400_000;

/** Whole days an escalation has been (or was) open: to its end, or to now. */
export function escalationAgeDays(
  escalation: Pick<ConcernEscalationDetail, 'escalatedAt' | 'resolvedAt'>,
  now: Date = new Date(),
): number {
  const end = escalation.resolvedAt === null ? now.getTime() : Date.parse(escalation.resolvedAt);
  return Math.max(0, Math.floor((end - Date.parse(escalation.escalatedAt)) / DAY_MS));
}
