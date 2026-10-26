import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { type ProjectSummary } from '@/features/projects/api/types.ts';

import { type ProjectTaskDetail, type TaskDependencyDetail } from './api/types.ts';
import { isDependencyEnd, isLeaf, isOpen } from './taskRules.ts';

// What the task screens offer a person. This is navigation, not protection: every command is decided again by the API
// (TASK-048 D-11), and a refusal is shown as it comes. The four task permissions ship to R04 at OWN, and a project's
// owner anchor is its Project Manager; an owner works their tasks through an ASSIGNED grant Appendix A has yet to give
// a role (TASK-048 F-2), so the owner is offered execution and the API decides.

function managedBy(user: SessionUser, project: Pick<ProjectSummary, 'projectManagerUserId'>) {
  return project.projectManagerUserId === user.id;
}

/**
 * TASK_MANAGE (plan, assign, dependencies, cancel): the project's own Project Manager, while the project is
 * APPROVED_PLANNED or ACTIVE (TASK-048 D-9).
 */
export function canManageTasks(
  user: SessionUser,
  project: Pick<ProjectSummary, 'status' | 'projectManagerUserId'>,
): boolean {
  return (
    (project.status === 'APPROVED_PLANNED' || project.status === 'ACTIVE') &&
    managedBy(user, project)
  );
}

/** TASK_UPDATE (execution and the percentage): the task's owner or the Project Manager, on an ACTIVE project. */
export function canExecuteTask(
  user: SessionUser,
  project: Pick<ProjectSummary, 'status' | 'projectManagerUserId'>,
  task: Pick<ProjectTaskDetail, 'assigneeUserId'>,
): boolean {
  return (
    project.status === 'ACTIVE' && (managedBy(user, project) || task.assigneeUserId === user.id)
  );
}

/** TASK_REOPEN alone reopens (TASK-048 D-11): only the Project Manager holds it, on an ACTIVE project. */
export function canReopenTasks(
  user: SessionUser,
  project: Pick<ProjectSummary, 'status' | 'projectManagerUserId'>,
): boolean {
  return project.status === 'ACTIVE' && managedBy(user, project);
}

/** The commands of MOD-012, one per edge family of the state machine (TASK-048 §3), and the percentage. */
export type TaskCommand =
  'start' | 'block' | 'unblock' | 'complete' | 'reportProgress' | 'reopen' | 'cancel';

export interface TaskRights {
  manage: boolean;
  execute: boolean;
  reopen: boolean;
}

export function taskRights(
  user: SessionUser,
  project: Pick<ProjectSummary, 'status' | 'projectManagerUserId'>,
  task: Pick<ProjectTaskDetail, 'assigneeUserId'>,
): TaskRights {
  return {
    manage: canManageTasks(user, project),
    execute: canExecuteTask(user, project, task),
    reopen: canReopenTasks(user, project),
  };
}

/**
 * The commands a task's state and the person allow. There is no BLOCKED → COMPLETED: a blocked task is unblocked
 * first. A subtask reopens only under a parent that is not COMPLETED, and a task is cancelled only with no live subtask
 * and no dependency (TASK-048 D-5, D-6). Unmet dependencies are shown, not pre-empted: the API decides them.
 */
export function taskCommands(
  task: ProjectTaskDetail,
  rights: TaskRights,
  parent: ProjectTaskDetail | null,
  dependencies: TaskDependencyDetail[],
): TaskCommand[] {
  const commands: TaskCommand[] = [];
  if (rights.execute) {
    if (task.status === 'NOT_STARTED') {
      commands.push('start');
    }
    if (task.status === 'IN_PROGRESS') {
      commands.push('complete');
    }
    if (task.status === 'BLOCKED') {
      commands.push('unblock');
    }
    if ((task.status === 'IN_PROGRESS' || task.status === 'BLOCKED') && isLeaf(task)) {
      commands.push('reportProgress');
    }
    if (task.status === 'NOT_STARTED' || task.status === 'IN_PROGRESS') {
      commands.push('block');
    }
  }
  if (rights.reopen && task.status === 'COMPLETED' && parent?.status !== 'COMPLETED') {
    commands.push('reopen');
  }
  if (
    rights.manage &&
    isOpen(task) &&
    task.subtaskCount === 0 &&
    !isDependencyEnd(task.id, dependencies)
  ) {
    commands.push('cancel');
  }
  return commands;
}

/** MOD-013: a subtask goes under a live, not COMPLETED, top-level task that is no dependency end (TASK-048 D-5). */
export function canAddSubtask(
  task: ProjectTaskDetail,
  rights: TaskRights,
  dependencies: TaskDependencyDetail[],
): boolean {
  return (
    rights.manage &&
    task.parentTaskId === null &&
    isOpen(task) &&
    !isDependencyEnd(task.id, dependencies)
  );
}

/** MOD-011 and MOD-014: a task's plan and owner change until it is CANCELLED (409 TASK_NOT_EDITABLE after). */
export function canPlanTask(task: ProjectTaskDetail, rights: TaskRights): boolean {
  return rights.manage && task.status !== 'CANCELLED';
}
