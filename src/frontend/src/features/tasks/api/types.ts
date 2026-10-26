import { type Decimal } from '@/features/progress/api/types.ts';
import { type NarrativeText, type NarrativeTextRequest } from '@/features/projects/api/types.ts';

// The WF-04 representations (TASK-048, project-task.md §4) as the API serialises them: enums in SNAKE_CASE_UPPER, ids
// as strings, calendar dates as ISO 8601 `YYYY-MM-DD` (UTC calendar dates, TASK-048 F-9), percentages as decimals.

/** CANCELLED is final; COMPLETED is left only by a reopen (TASK-048 D-4). */
export type ProjectTaskStatus =
  'NOT_STARTED' | 'IN_PROGRESS' | 'BLOCKED' | 'COMPLETED' | 'CANCELLED';

/**
 * The first letter is the predecessor's point (Start = started, Finish = completed), the second the successor's step it
 * gates (TASK-048 D-6).
 */
export type TaskDependencyType = 'FS' | 'SS' | 'FF' | 'SF';

/**
 * A task, or a subtask when `parentTaskId` is set. `actualPercentComplete` is the figure its owner entered (a leaf's
 * only); `percentComplete` is ADR-009's roll-up: a leaf's own, a parent's the duration-weighted mean of its live
 * subtasks, 100 once COMPLETED. `subtaskCount` counts the live subtasks.
 */
export interface ProjectTaskDetail {
  id: string;
  projectId: string;
  scheduleActivityId: string | null;
  parentTaskId: string | null;
  title: NarrativeText;
  description: NarrativeText | null;
  assigneeUserId: string | null;
  priorityItemId: string | null;
  status: ProjectTaskStatus;
  plannedStartDate: string;
  plannedFinishDate: string;
  plannedDurationDays: number;
  actualStartDate: string | null;
  actualFinishDate: string | null;
  actualPercentComplete: Decimal | null;
  percentComplete: Decimal;
  subtaskCount: number;
  blockedReason: NarrativeText | null;
  completedAt: string | null;
  reopenedCount: number;
  createdAt: string;
  createdBy: string;
  updatedAt: string;
  updatedBy: string;
}

export interface TaskDependencyDetail {
  id: string;
  projectId: string;
  predecessorTaskId: string;
  successorTaskId: string;
  dependencyType: TaskDependencyType;
  createdAt: string;
  createdBy: string;
}

/** A task's plan and assignment, as a whole (R-5). The planned duration is derived from the dates, never sent. */
export interface ProjectTaskRequest {
  scheduleActivityId: string | null;
  title: NarrativeTextRequest;
  description: NarrativeTextRequest | null;
  assigneeUserId: string | null;
  priorityItemId: string | null;
  plannedStartDate: string;
  plannedFinishDate: string;
}

/** A new task, or a subtask of `parentTaskId`; its project and parent never change. */
export interface ProjectTaskCreateRequest extends ProjectTaskRequest {
  projectId: string;
  parentTaskId: string | null;
}

export interface TaskDependencyRequest {
  predecessorTaskId: string;
  successorTaskId: string;
  dependencyType: TaskDependencyType;
}
