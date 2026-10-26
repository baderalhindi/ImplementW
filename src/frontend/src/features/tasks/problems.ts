import {
  fieldMessage,
  problemMessage,
  type Translate,
} from '@/features/identity-access/problems.ts';
import { ApiError } from '@/shared/api/httpClient.ts';
import { type TranslationKey } from '@/shared/i18n/i18n.ts';

// What a person reads for each WF-04 refusal (TASK-048 project-task.md §5). Codes not listed here fall back to the
// platform messages.

const TASK_PROBLEMS: Record<string, TranslationKey> = {
  TASK_PROJECT_NOT_ELIGIBLE: 'tasks.problems.projectNotEligible',
  TASK_NOT_EDITABLE: 'tasks.problems.notEditable',
  TASK_HIERARCHY_INVALID: 'tasks.problems.hierarchyInvalid',
  TASK_SCHEDULE_ACTIVITY_INVALID: 'tasks.problems.scheduleActivityInvalid',
  TASK_ASSIGNEE_NOT_ELIGIBLE: 'tasks.problems.assigneeNotEligible',
  TASK_PRIORITY_INVALID: 'tasks.problems.priorityInvalid',
  TASK_DEPENDENCY_UNMET: 'tasks.problems.dependencyUnmet',
  TASK_SUBTASKS_OPEN: 'tasks.problems.subtasksOpen',
  TASK_IN_USE: 'tasks.problems.inUse',
  TASK_PROGRESS_NOT_ENTERABLE: 'tasks.problems.progressNotEnterable',
  TASK_DEPENDENCY_INVALID: 'tasks.problems.dependencyInvalid',
  TASK_DEPENDENCY_CIRCULAR: 'tasks.problems.dependencyCircular',
  TASK_DEPENDENCY_EXISTS: 'tasks.problems.dependencyExists',
  INVALID_TRANSITION: 'tasks.problems.invalidTransition',
  PERMISSION_DENIED: 'tasks.problems.permissionDenied',
};

export function taskProblemMessage(error: unknown, t: Translate): string {
  const key = error instanceof ApiError ? TASK_PROBLEMS[error.code] : undefined;
  return key === undefined ? problemMessage(error, t) : t(key);
}

/** A read refused for want of TASK_VIEW (or SCHEDULE_VIEW, MASTER_DATA_VIEW): the screen says what it cannot show. */
export function isForbidden(error: unknown): boolean {
  return error instanceof ApiError && error.status === 403;
}

/** The task moved on since it was read (412, cancelled, or gone): the screen reads it again instead of retrying. */
export function isStale(error: unknown): boolean {
  return (
    error instanceof ApiError &&
    (error.code === 'PRECONDITION_FAILED' ||
      error.code === 'TASK_NOT_EDITABLE' ||
      error.code === 'NOT_FOUND')
  );
}

/** Field messages of WF-04's own: who may own a task, and MOD-015's checks. Everything else is the platform's. */
const FIELD_MESSAGES: Partial<Record<string, Partial<Record<string, TranslationKey>>>> = {
  assigneeUserId: {
    NOT_ALLOWED: 'tasks.fieldErrors.assigneeNotEligible',
    MALFORMED: 'tasks.fieldErrors.userIdMalformed',
  },
  scheduleActivityId: { NOT_FOUND: 'tasks.fieldErrors.scheduleActivityInvalid' },
  priorityItemId: { NOT_FOUND: 'tasks.fieldErrors.priorityInvalid' },
  plannedFinishDate: { DATE_BEFORE_START: 'tasks.fieldErrors.finishBeforeStart' },
  actualPercentComplete: {
    OUT_OF_RANGE: 'tasks.fieldErrors.percentOutOfRange',
    MALFORMED: 'tasks.fieldErrors.percentMalformed',
  },
  successorTaskId: {
    SAME_TASK: 'tasks.fieldErrors.sameTask',
    DEPENDENCY_EXISTS: 'tasks.problems.dependencyExists',
    DEPENDENCY_UNMET: 'tasks.fieldErrors.dependencyBroken',
  },
};

export function taskFieldMessage(field: string, code: string, t: Translate): string {
  const key = FIELD_MESSAGES[field]?.[code];
  return key === undefined ? fieldMessage(code, t) : t(key);
}
