import { checkText } from '@/features/identity-access/forms.ts';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { assignmentsReaching, isClosed, isInternal } from '@/features/projects/access.ts';
import { narrativeRequest } from '@/features/progress/progressUpdate.ts';
import { type ProjectStatus } from '@/features/projects/api/types.ts';
import { TEXT_LENGTH } from '@/features/projects/registration.ts';
import { todayUtc } from '@/features/tasks/taskRules.ts';
import { type FieldCodes } from '@/shared/forms/useFieldErrors.ts';
import { type Language } from '@/shared/i18n/i18n.ts';

import { type CloseoutStage, type ObligationCommand } from './api/closeoutApi.ts';
import {
  type ClosureCaseDetail,
  type ClosureCaseRequest,
  type CompletionCaseDetail,
  type CompletionCaseRequest,
  type PostProjectObligationCreateRequest,
  type PostProjectObligationDetail,
  type PostProjectObligationRequest,
  type PostProjectObligationStatus,
  type ReadinessCheckCode,
  type ReadinessCheckDetail,
} from './api/types.ts';
import { isOpen, managesProject, type ProjectFacts } from './governedRequest.ts';

// WF-10's rules as the screens apply them (TASK-063 closure.md). Completion and Closure are two cases, raised, reviewed
// and activated one after the other (D-3, D-4): the screens never offer them as one action. The server is the
// authority, computes the readiness roll-up and decides every command; these rules only say where a project stands.

export const CLOSEOUT_STAGES: CloseoutStage[] = ['completion', 'closure'];

/**
 * Where a stage stands for the project.
 * - `NOT_YET`: the project is not ACTIVE yet; `LOCKED`: stage 2 waits for stage 1.
 * - `READY`: a case may be raised now; `IN_PROGRESS`: a case is open (draft to approved); `DONE`: effected.
 * - `ON_HOLD`: completion of a SUSPENDED project, which is resumed first or closed without completion.
 * - `SKIPPED`: completion of a project closed from SUSPENDED (TERMINATED_WITHOUT_COMPLETION).
 */
export type StageState =
  'NOT_YET' | 'LOCKED' | 'READY' | 'IN_PROGRESS' | 'DONE' | 'ON_HOLD' | 'SKIPPED';

export interface StageView<C> {
  state: StageState;
  /** The case that stands for the stage: its open case, or the effected one. */
  current: C | null;
}

export interface CloseoutStages {
  completion: StageView<CompletionCaseDetail>;
  closure: StageView<ClosureCaseDetail>;
  /** The terminal path: closed from SUSPENDED without a completion case (TASK-063 D-6). */
  terminal: boolean;
}

function currentOf<C extends { status: CompletionCaseDetail['status'] }>(
  cases: readonly C[],
): C | null {
  return (
    cases.find((candidate) => candidate.status === 'EFFECTED') ??
    cases.find((candidate) => isOpen(candidate.status)) ??
    null
  );
}

/** The two stages of the project's closeout, from its state and its cases (any order). */
export function closeoutStagesOf(
  projectStatus: ProjectStatus,
  completions: readonly CompletionCaseDetail[],
  closures: readonly ClosureCaseDetail[],
): CloseoutStages {
  const completion = currentOf(completions);
  const closure = currentOf(closures);
  const terminal =
    closure !== null
      ? closure.outcome === 'TERMINATED_WITHOUT_COMPLETION'
      : projectStatus === 'SUSPENDED';

  const completionState: StageState =
    completion?.status === 'EFFECTED'
      ? 'DONE'
      : completion !== null
        ? 'IN_PROGRESS'
        : closure !== null && terminal
          ? 'SKIPPED'
          : projectStatus === 'ACTIVE'
            ? 'READY'
            : projectStatus === 'SUSPENDED'
              ? 'ON_HOLD'
              : projectStatus === 'COMPLETED' || projectStatus === 'CLOSED'
                ? 'DONE'
                : 'NOT_YET';

  const closureState: StageState =
    closure?.status === 'EFFECTED' || projectStatus === 'CLOSED'
      ? 'DONE'
      : closure !== null
        ? 'IN_PROGRESS'
        : projectStatus === 'COMPLETED' || projectStatus === 'SUSPENDED'
          ? 'READY'
          : 'LOCKED';

  return {
    completion: { state: completionState, current: completion },
    closure: { state: closureState, current: closure },
    terminal,
  };
}

/** SCR-112 is offered to the project's Project Manager for the stage that may be raised now (TASK-063 §3). */
export function canRaiseCase(
  user: SessionUser,
  project: ProjectFacts,
  stages: CloseoutStages,
  stage: CloseoutStage,
): boolean {
  return managesProject(user, project) && stages[stage].state === 'READY';
}

/** WF-10 withdraws a case from DRAFT, SUBMITTED or RETURNED (TASK-063 D-3). */
export const CLOSEOUT_WITHDRAWABLE = ['DRAFT', 'SUBMITTED', 'RETURNED'] as const;

// ---------------------------------------------------------------------------------------------------------------------
// Readiness (TASK-063 D-5): the server's roll-up and per-criterion counts, shown as they are

/** The order criteria are listed in: the source modules', then the obligations'. */
export const READINESS_CHECK_CODES: ReadinessCheckCode[] = [
  'DECISIONS_SETTLED',
  'TASKS_DISPOSITIONED',
  'SCHEDULE_RECONCILED',
  'MILESTONES_DISPOSITIONED',
  'RISKS_DISPOSITIONED',
  'ISSUES_DISPOSITIONED',
  'CHANGES_DISPOSITIONED',
  'SUSPENSION_REQUESTS_SETTLED',
  'PROGRESS_REPORTED',
  'FINANCIALS_SETTLED',
  'OBLIGATIONS_OWNED',
  'OBLIGATIONS_SATISFIED',
];

/**
 * Where the records blocking a criterion are settled, as a workspace tab of the project: each criterion is a count, and
 * its owning module's register lists the records (TASK-063 F-7).
 */
export const CHECK_SOURCES: Record<ReadinessCheckCode, string> = {
  DECISIONS_SETTLED: 'reviews',
  TASKS_DISPOSITIONED: 'tasks',
  SCHEDULE_RECONCILED: 'schedule/baselines',
  MILESTONES_DISPOSITIONED: 'milestones',
  RISKS_DISPOSITIONED: 'risks',
  ISSUES_DISPOSITIONED: 'issues-challenges',
  CHANGES_DISPOSITIONED: 'change-requests',
  SUSPENSION_REQUESTS_SETTLED: 'suspension',
  PROGRESS_REPORTED: 'progress',
  FINANCIALS_SETTLED: 'financials',
  OBLIGATIONS_OWNED: 'closeout',
  OBLIGATIONS_SATISFIED: 'closeout',
};

export function sortedChecks(checks: readonly ReadinessCheckDetail[]): ReadinessCheckDetail[] {
  return [...checks].sort(
    (a, b) =>
      READINESS_CHECK_CODES.indexOf(a.checkCode) - READINESS_CHECK_CODES.indexOf(b.checkCode),
  );
}

/** A criterion that failed in the latest evaluation and may be waived (422 CLOSURE_CHECK_NOT_FAILED / …_NOT_WAIVABLE). */
export function isWaivableNow(check: ReadinessCheckDetail): boolean {
  return check.result === 'FAIL' && check.waivable;
}

// ---------------------------------------------------------------------------------------------------------------------
// The form (SCR-112)

export interface CloseoutFormValues {
  /** Stage 1 only. */
  actualProjectCompletionDate: string;
  narrative: string;
}

const FIELDS = ['actualProjectCompletionDate', 'narrative'] as const;

/** The API names the narrative by stage; the form has one field for it. */
export const NARRATIVE_FIELDS: Record<CloseoutStage, string> = {
  completion: 'completionNarrative',
  closure: 'closureNarrative',
};

export function emptyCloseoutForm(): CloseoutFormValues {
  return { actualProjectCompletionDate: '', narrative: '' };
}

export function closeoutFormValuesOf(
  existing: CompletionCaseDetail | ClosureCaseDetail,
): CloseoutFormValues {
  return 'actualProjectCompletionDate' in existing
    ? {
        actualProjectCompletionDate: existing.actualProjectCompletionDate ?? '',
        narrative: existing.completionNarrative?.text ?? '',
      }
    : { actualProjectCompletionDate: '', narrative: existing.closureNarrative?.text ?? '' };
}

export function sameCloseoutValues(a: CloseoutFormValues, b: CloseoutFormValues): boolean {
  return FIELDS.every((field) => a[field] === b[field]);
}

const DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/;

/**
 * The completion date is the requester's: required before submission and never after today (the UTC date); the API
 * also refuses one before the project's activation (422 CLOSURE_COMPLETION_DATE_INVALID, TASK-063 D-7). A draft may be
 * saved without it or the narrative; submission needs both (422 CLOSURE_INCOMPLETE).
 */
export function checkCloseoutForm(
  values: CloseoutFormValues,
  stage: CloseoutStage,
  today: string = todayUtc(),
): FieldCodes {
  const date = values.actualProjectCompletionDate;
  return {
    actualProjectCompletionDate:
      stage !== 'completion' || date === ''
        ? null
        : !DATE_PATTERN.test(date)
          ? 'MALFORMED'
          : date > today
            ? 'NOT_ALLOWED'
            : null,
    narrative: checkText(values.narrative, { maxLength: TEXT_LENGTH }),
  };
}

export function closeoutErrorCount(codes: FieldCodes): number {
  return FIELDS.filter((field) => codes[field]).length;
}

export function toCompletionRequest(
  values: CloseoutFormValues,
  language: Language,
  before: CompletionCaseDetail | null,
): CompletionCaseRequest {
  return {
    actualProjectCompletionDate:
      values.actualProjectCompletionDate === '' ? null : values.actualProjectCompletionDate,
    completionNarrative:
      values.narrative.trim() === ''
        ? null
        : narrativeRequest(values.narrative, language, before?.completionNarrative ?? null),
  };
}

export function toClosureRequest(
  values: CloseoutFormValues,
  language: Language,
  before: ClosureCaseDetail | null,
): ClosureCaseRequest {
  return {
    closureNarrative:
      values.narrative.trim() === ''
        ? null
        : narrativeRequest(values.narrative, language, before?.closureNarrative ?? null),
  };
}

// ---------------------------------------------------------------------------------------------------------------------
// Post-project obligations (TASK-063 D-9)

export const OBLIGATION_STATUSES: PostProjectObligationStatus[] = [
  'OPEN',
  'IN_PROGRESS',
  'SATISFIED',
  'WAIVED',
  'CANCELLED',
];

/** SATISFIED, CANCELLED and WAIVED are final (409 CLOSURE_OBLIGATION_SETTLED). */
export function isSettled(status: PostProjectObligationStatus): boolean {
  return status === 'SATISFIED' || status === 'CANCELLED' || status === 'WAIVED';
}

/**
 * The case a new obligation is recorded against: the project's open or effected completion case, or its open terminal
 * closure case (422 CLOSURE_OBLIGATION_CASE_INVALID otherwise); null when there is none.
 */
export function obligationCaseOf(
  stages: CloseoutStages,
): Pick<PostProjectObligationCreateRequest, 'completionCaseId' | 'closureCaseId'> | null {
  const completion = stages.completion.current;
  if (completion !== null) {
    return { completionCaseId: completion.id, closureCaseId: null };
  }
  const closure = stages.closure.current;
  return closure !== null && stages.terminal && closure.status !== 'EFFECTED'
    ? { completionCaseId: null, closureCaseId: closure.id }
    : null;
}

export interface ObligationFormValues {
  title: string;
  description: string;
  ownerUserId: string | null;
  dueDate: string;
}

export function emptyObligationForm(): ObligationFormValues {
  return { title: '', description: '', ownerUserId: null, dueDate: '' };
}

export function obligationFormValuesOf(
  obligation: PostProjectObligationDetail,
): ObligationFormValues {
  return {
    title: obligation.title.text,
    description: obligation.description?.text ?? '',
    ownerUserId: obligation.ownerUserId,
    dueDate: obligation.dueDate ?? '',
  };
}

/** Owner and due date are optional to record; completion needs both on every open obligation (OBLIGATIONS_OWNED). */
export function checkObligation(values: ObligationFormValues): FieldCodes {
  return {
    title: checkText(values.title, { required: true, maxLength: TEXT_LENGTH }),
    description: checkText(values.description, { maxLength: TEXT_LENGTH }),
    dueDate: values.dueDate === '' || DATE_PATTERN.test(values.dueDate) ? null : 'MALFORMED',
  };
}

export function toObligationRequest(
  values: ObligationFormValues,
  language: Language,
  before: PostProjectObligationDetail | null,
): PostProjectObligationRequest {
  return {
    title: narrativeRequest(values.title, language, before?.title ?? null),
    description:
      values.description.trim() === ''
        ? null
        : narrativeRequest(values.description, language, before?.description ?? null),
    ownerUserId: values.ownerUserId,
    dueDate: values.dueDate === '' ? null : values.dueDate,
  };
}

/** What the person is offered on an obligation: the Project Manager moves it along; AHDA waives it (CLOSEOUT_WAIVE). */
export function obligationCommands(
  user: SessionUser,
  project: ProjectFacts,
  obligation: Pick<PostProjectObligationDetail, 'status'>,
): ObligationCommand[] {
  if (isSettled(obligation.status) || isClosed(project.status)) {
    return [];
  }
  const commands: ObligationCommand[] = [];
  if (managesProject(user, project)) {
    commands.push(...(obligation.status === 'OPEN' ? ['start' as const] : []), 'satisfy', 'cancel');
  }
  if (
    isInternal(user) &&
    project.projectManagerUserId !== user.id &&
    assignmentsReaching(user, project).length > 0
  ) {
    commands.push('waive');
  }
  return commands;
}
