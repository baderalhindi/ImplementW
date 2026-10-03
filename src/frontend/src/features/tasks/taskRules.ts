import { checkText } from '@/features/identity-access/forms.ts';
import { narrativeRequest } from '@/features/progress/progressUpdate.ts';
import { type ProjectSummary } from '@/features/projects/api/types.ts';
import { TEXT_LENGTH } from '@/features/projects/registration.ts';
import { checkDate } from '@/features/schedule/activityForm.ts';
import { dayNumber } from '@/features/schedule/dependencyRules.ts';
import { type FieldCodes } from '@/shared/forms/useFieldErrors.ts';
import { cyclePath } from '@/shared/graph/dependencyGraph.ts';
import { type Language } from '@/shared/i18n/i18n.ts';

import {
  type ProjectTaskDetail,
  type ProjectTaskRequest,
  type ProjectTaskStatus,
  type TaskDependencyDetail,
  type TaskDependencyType,
} from './api/types.ts';

// The WF-04 rules the screens apply (TASK-048 D-4 to D-8, `TaskDependencyRules`), mirrored so a person learns of a
// broken rule before anything is sent, and the definitions of the four task lists (SCR-063–066). The API stays the
// authority and its refusals are shown as they come. Dates are UTC calendar dates (TASK-048 F-9).

/** The states work is still to be done in: the only ones that can be overdue or need an update. */
export const OPEN_STATUSES: readonly ProjectTaskStatus[] = [
  'NOT_STARTED',
  'IN_PROGRESS',
  'BLOCKED',
];

export function isOpen(task: Pick<ProjectTaskDetail, 'status'>): boolean {
  return OPEN_STATUSES.includes(task.status);
}

/** A task with no live subtask: the only kind whose percentage is entered and that a dependency joins (D-6, D-7). */
export function isLeaf(task: Pick<ProjectTaskDetail, 'subtaskCount'>): boolean {
  return task.subtaskCount === 0;
}

export function isLiveLeaf(task: Pick<ProjectTaskDetail, 'status' | 'subtaskCount'>): boolean {
  return task.status !== 'CANCELLED' && isLeaf(task);
}

/** Today as a UTC calendar date, the way the API dates a task. */
export function todayUtc(now: Date = new Date()): string {
  return now.toISOString().slice(0, 10);
}

/**
 * The whole days a task is overdue: open, and its planned finish before today. Null when it is not overdue, so a task
 * due today is not flagged.
 */
export function overdueDays(
  task: Pick<ProjectTaskDetail, 'status' | 'plannedFinishDate'>,
  today: string,
): number | null {
  if (!isOpen(task)) {
    return null;
  }
  const days = dayNumber(today) - dayNumber(task.plannedFinishDate);
  return days > 0 ? days : null;
}

/** A task whose figure has not changed for this many days needs a fresh one (F-6: no WF-04 specification sets it). */
export const STALE_UPDATE_DAYS = 7;

/** Why a task is on SCR-066 Updates Required. */
export type UpdateReason =
  | { kind: 'notStarted'; since: string }
  | { kind: 'noProgress' }
  | { kind: 'staleProgress'; days: number };

/**
 * What a task's owner owes the plan (SCR-066): a start that is due (NOT_STARTED past its planned start), a first
 * percentage (a started leaf without one), or a fresh one (a started leaf unchanged for STALE_UPDATE_DAYS). A parent's
 * percentage is never entered (D-7), so only its start can be due.
 */
export function updateReasons(task: ProjectTaskDetail, today: string): UpdateReason[] {
  if (task.status === 'NOT_STARTED') {
    return dayNumber(task.plannedStartDate) < dayNumber(today)
      ? [{ kind: 'notStarted', since: task.plannedStartDate }]
      : [];
  }
  if ((task.status !== 'IN_PROGRESS' && task.status !== 'BLOCKED') || !isLeaf(task)) {
    return [];
  }
  if (task.actualPercentComplete === null) {
    return [{ kind: 'noProgress' }];
  }
  const days = dayNumber(today) - dayNumber(task.updatedAt.slice(0, 10));
  return days >= STALE_UPDATE_DAYS ? [{ kind: 'staleProgress', days }] : [];
}

/** A task with the project it belongs to, as the cross-project lists show it. */
export interface TaskEntry {
  task: ProjectTaskDetail;
  project: ProjectSummary;
}

/** SCR-063 My Tasks, SCR-064 Team Tasks, SCR-065 Overdue Tasks, SCR-066 Updates Required. */
export type TaskListKind = 'mine' | 'team' | 'overdue' | 'updates';

export const TASK_LISTS: readonly TaskListKind[] = ['mine', 'team', 'overdue', 'updates'];

/** The tasks a person answers for: those they own, and every task of the projects they manage. */
function answersFor(entry: TaskEntry, userId: string): boolean {
  return entry.task.assigneeUserId === userId || entry.project.projectManagerUserId === userId;
}

/** Whether the person manages any of the projects read: SCR-064's empty state says which case it is. */
export function managesAny(projects: ProjectSummary[], userId: string): boolean {
  return projects.some((project) => project.projectManagerUserId === userId);
}

/**
 * A list's tasks: SCR-063 the person's own, SCR-064 those of the projects they manage, whoever owns them; SCR-065 and
 * SCR-066 the overdue and the due-for-update among both. Most overdue first, then by planned finish.
 */
export function listEntries(
  list: TaskListKind,
  entries: TaskEntry[],
  userId: string,
  today: string,
): TaskEntry[] {
  const chosen = entries.filter((entry) => {
    switch (list) {
      case 'mine':
        return entry.task.assigneeUserId === userId;
      case 'team':
        return entry.project.projectManagerUserId === userId;
      case 'overdue':
        return answersFor(entry, userId) && overdueDays(entry.task, today) !== null;
      case 'updates':
        return answersFor(entry, userId) && updateReasons(entry.task, today).length > 0;
    }
  });
  return sortByUrgency(chosen, today);
}

export function sortByUrgency(entries: TaskEntry[], today: string): TaskEntry[] {
  const urgency = (entry: TaskEntry) => overdueDays(entry.task, today) ?? 0;
  return [...entries].sort(
    (a, b) =>
      urgency(b) - urgency(a) ||
      dayNumber(a.task.plannedFinishDate) - dayNumber(b.task.plannedFinishDate) ||
      a.task.title.text.localeCompare(b.task.title.text),
  );
}

/** The status filter of SCR-047, SCR-063 and SCR-064: open work by default. */
export type StatusFilter = 'open' | 'all' | ProjectTaskStatus;

export function matchesStatus(task: Pick<ProjectTaskDetail, 'status'>, filter: StatusFilter) {
  return filter === 'all' || (filter === 'open' ? isOpen(task) : task.status === filter);
}

// -------------------------------------------------------------------------------------------- dependencies (D-6)

/** FS and SS gate the successor's start, FF and SF its completion. */
export function gatesStart(type: TaskDependencyType): boolean {
  return type === 'FS' || type === 'SS';
}

/** The predecessor meets an F by being COMPLETED and an S by having started (an actual start). */
export function isMet(
  type: TaskDependencyType,
  predecessor: Pick<ProjectTaskDetail, 'status' | 'actualStartDate'>,
): boolean {
  return type === 'FS' || type === 'FF'
    ? predecessor.status === 'COMPLETED'
    : predecessor.actualStartDate !== null;
}

/** The dependencies still holding back a task's start (or, if not `start`, its completion). */
export function unmetDependencies(
  taskId: string,
  start: boolean,
  tasks: ProjectTaskDetail[],
  dependencies: TaskDependencyDetail[],
): TaskDependencyDetail[] {
  const byId = new Map(tasks.map((task) => [task.id, task]));
  return dependencies.filter((dependency) => {
    const predecessor = byId.get(dependency.predecessorTaskId);
    return (
      dependency.successorTaskId === taskId &&
      gatesStart(dependency.dependencyType) === start &&
      predecessor !== undefined &&
      !isMet(dependency.dependencyType, predecessor)
    );
  });
}

export function isDependencyEnd(taskId: string, dependencies: TaskDependencyDetail[]): boolean {
  return dependencies.some(
    (dependency) =>
      dependency.predecessorTaskId === taskId || dependency.successorTaskId === taskId,
  );
}

export interface TaskDependencyValues {
  predecessorTaskId: string;
  successorTaskId: string;
  dependencyType: TaskDependencyType | '';
}

export interface TaskDependencyCheck {
  /** Field path → API code, or this screen's own: SAME_TASK, DEPENDENCY_EXISTS, CIRCULAR, DEPENDENCY_UNMET. */
  codes: Record<keyof TaskDependencyValues, string | null>;
  /** The chain the dependency would close, successor first; null when it closes none. */
  cycle: string[] | null;
}

/**
 * MOD-015's checks, in the API's order (D-6): both ends chosen, two distinct tasks, not already linked (the pair is
 * unique whatever the type), no cycle, and not already broken (the successor took the step it would gate before the
 * predecessor got there).
 */
export function checkTaskDependency(
  values: TaskDependencyValues,
  tasks: ProjectTaskDetail[],
  dependencies: TaskDependencyDetail[],
): TaskDependencyCheck {
  const { predecessorTaskId: predecessorId, successorTaskId: successorId, dependencyType } = values;
  const chosen = predecessorId !== '' && successorId !== '';
  const linked =
    chosen &&
    dependencies.some(
      (dependency) =>
        dependency.predecessorTaskId === predecessorId &&
        dependency.successorTaskId === successorId,
    );
  const cycle =
    chosen && predecessorId !== successorId
      ? cyclePath(
          dependencies.map((dependency) => ({
            from: dependency.predecessorTaskId,
            to: dependency.successorTaskId,
          })),
          predecessorId,
          successorId,
        )
      : null;
  const predecessor = tasks.find((task) => task.id === predecessorId);
  const successor = tasks.find((task) => task.id === successorId);
  const broken =
    dependencyType !== '' &&
    predecessor !== undefined &&
    successor !== undefined &&
    (gatesStart(dependencyType)
      ? successor.actualStartDate !== null
      : successor.status === 'COMPLETED') &&
    !isMet(dependencyType, predecessor);
  return {
    codes: {
      predecessorTaskId: predecessorId === '' ? 'REQUIRED' : null,
      successorTaskId:
        successorId === ''
          ? 'REQUIRED'
          : predecessorId === successorId
            ? 'SAME_TASK'
            : linked
              ? 'DEPENDENCY_EXISTS'
              : cycle !== null
                ? 'CIRCULAR'
                : broken
                  ? 'DEPENDENCY_UNMET'
                  : null,
      dependencyType: dependencyType === '' ? 'REQUIRED' : null,
    },
    cycle,
  };
}

// -------------------------------------------------------------------------------------------- the task form

/** MOD-010, MOD-011 and MOD-013: the inputs ProjectTaskRequest.Validate checks, as typed. */
export interface TaskFormValues {
  title: string;
  description: string;
  scheduleActivityId: string;
  priorityItemId: string;
  plannedStartDate: string;
  plannedFinishDate: string;
}

export function emptyTaskForm(
  start: string,
  finish: string,
  scheduleActivityId = '',
): TaskFormValues {
  return {
    title: '',
    description: '',
    scheduleActivityId,
    priorityItemId: '',
    plannedStartDate: start,
    plannedFinishDate: finish,
  };
}

export function taskFormValuesOf(task: ProjectTaskDetail): TaskFormValues {
  return {
    title: task.title.text,
    description: task.description?.text ?? '',
    scheduleActivityId: task.scheduleActivityId ?? '',
    priorityItemId: task.priorityItemId ?? '',
    plannedStartDate: task.plannedStartDate,
    plannedFinishDate: task.plannedFinishDate,
  };
}

export function checkTaskForm(values: TaskFormValues): FieldCodes {
  const start = checkDate(values.plannedStartDate);
  const finish = checkDate(values.plannedFinishDate);
  return {
    title: checkText(values.title, { required: true, maxLength: TEXT_LENGTH }),
    description: checkText(values.description, { maxLength: TEXT_LENGTH }),
    plannedStartDate: start,
    plannedFinishDate:
      finish ??
      (start === null && dayNumber(values.plannedFinishDate) < dayNumber(values.plannedStartDate)
        ? 'DATE_BEFORE_START'
        : null),
  };
}

/**
 * The request, as a whole (R-5): the form's values and the owner, who is MOD-014's to change. Unchanged text keeps
 * the language it was entered in.
 */
export function toTaskRequest(
  values: TaskFormValues,
  assigneeUserId: string | null,
  language: Language,
  before: ProjectTaskDetail | null,
): ProjectTaskRequest {
  const description = values.description.trim();
  return {
    scheduleActivityId: values.scheduleActivityId === '' ? null : values.scheduleActivityId,
    title: narrativeRequest(values.title, language, before?.title ?? null),
    description:
      description === ''
        ? null
        : narrativeRequest(description, language, before?.description ?? null),
    assigneeUserId,
    priorityItemId: values.priorityItemId === '' ? null : values.priorityItemId,
    plannedStartDate: values.plannedStartDate,
    plannedFinishDate: values.plannedFinishDate,
  };
}

/** The task's plan re-sent unchanged with another owner: the PUT MOD-014 makes. */
export function withAssignee(
  task: ProjectTaskDetail,
  assigneeUserId: string | null,
): ProjectTaskRequest {
  return toTaskRequest(taskFormValuesOf(task), assigneeUserId, 'en', task);
}

// -------------------------------------------------------------------------------------------- the percentage (D-7)

const PERCENT_PATTERN = /^\d{1,3}(\.\d{1,4})?$/;

/** 0 to 100, to four places: REQUIRED, MALFORMED or OUT_OF_RANGE, as TaskProgressCommand.Validate. */
export function checkPercent(value: string): string | null {
  const trimmed = value.trim();
  if (trimmed === '') {
    return 'REQUIRED';
  }
  if (!PERCENT_PATTERN.test(trimmed)) {
    return 'MALFORMED';
  }
  return Number(trimmed) > 100 ? 'OUT_OF_RANGE' : null;
}
