import { type NarrativeTextRequest } from '@/features/projects/api/types.ts';
import { apiRequest } from '@/shared/api/httpClient.ts';
import { readAllPages } from '@/shared/api/paging.ts';

import {
  type ProjectTaskCreateRequest,
  type ProjectTaskDetail,
  type ProjectTaskRequest,
  type TaskDependencyDetail,
  type TaskDependencyRequest,
} from './types.ts';

// WF-04 (TASK-048 project-task.md §4). Every collection is one project's, `projectId` required; a project the caller
// may not see answers an empty page, and a caller without TASK_VIEW is refused (403). Every POST and PUT is a sensitive
// write with an Idempotency-Key, never retried here: a retried create creates a second task (TASK-048 F-13). The PUT
// requires If-Match (428 without); the commands honour it when sent, so a change made meanwhile is 412.

function command(id: string, name: string, etag: string | null, body: unknown = {}) {
  return apiRequest<ProjectTaskDetail>(`/project-tasks/${id}/${name}`, {
    method: 'POST',
    body,
    ifMatch: etag,
  });
}

export const tasksApi = {
  /** Each parent followed by its subtasks, in creation order, cancelled ones included. */
  tasks: (projectId: string, signal?: AbortSignal) =>
    readAllPages<ProjectTaskDetail>('/project-tasks', { projectId }, signal),
  /** One task with the ETag its PUT and commands send back. */
  task: (id: string, signal?: AbortSignal) =>
    apiRequest<ProjectTaskDetail>(`/project-tasks/${id}`, { signal }),
  /** MOD-010 and MOD-013: NOT_STARTED. */
  create: (request: ProjectTaskCreateRequest) =>
    apiRequest<ProjectTaskDetail>('/project-tasks', { method: 'POST', body: request }),
  /** MOD-011 and MOD-014: the plan and the owner, as a whole. */
  update: (id: string, request: ProjectTaskRequest, etag: string | null) =>
    apiRequest<ProjectTaskDetail>(`/project-tasks/${id}`, {
      method: 'PUT',
      body: request,
      ifMatch: etag,
    }),
  /** NOT_STARTED → IN_PROGRESS, once its FS and SS predecessors allow it. */
  start: (id: string, etag: string | null) => command(id, 'start', etag),
  /** NOT_STARTED or IN_PROGRESS → BLOCKED, with the reason. */
  block: (id: string, reason: NarrativeTextRequest, etag: string | null) =>
    command(id, 'block', etag, { reason }),
  /** BLOCKED → IN_PROGRESS, or NOT_STARTED for a task blocked before it started. */
  unblock: (id: string, etag: string | null) => command(id, 'unblock', etag),
  /** IN_PROGRESS → COMPLETED. There is no BLOCKED → COMPLETED: unblock first (TASK-048 D-4). */
  complete: (id: string, etag: string | null) => command(id, 'complete', etag),
  /** COMPLETED → IN_PROGRESS, on TASK_REOPEN alone (TASK-048 D-11). */
  reopen: (id: string, etag: string | null) => command(id, 'reopen', etag),
  /** A live task → CANCELLED, final, with no live subtask and no dependency. */
  cancel: (id: string, etag: string | null) => command(id, 'cancel', etag),
  /** A leaf's actual percentage while IN_PROGRESS or BLOCKED (ADR-009). */
  reportProgress: (id: string, actualPercentComplete: number, etag: string | null) =>
    command(id, 'report-progress', etag, { actualPercentComplete }),
  /** Oldest first. */
  dependencies: (projectId: string, signal?: AbortSignal) =>
    readAllPages<TaskDependencyDetail>('/task-dependencies', { projectId }, signal),
  /** MOD-015. */
  createDependency: (request: TaskDependencyRequest) =>
    apiRequest<TaskDependencyDetail>('/task-dependencies', { method: 'POST', body: request }),
  /** 204, also when it is already gone (R-40). A dependency is never edited, only removed and added again. */
  deleteDependency: (id: string) =>
    apiRequest<undefined>(`/task-dependencies/${id}`, { method: 'DELETE' }),
};
