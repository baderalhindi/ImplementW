import { type ReactElement } from 'react';

import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { type ProjectSummary } from '@/features/projects/api/types.ts';
import { ConfirmDialog } from '@/features/schedule/dialogs/ConfirmDialog.tsx';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';

import { tasksApi } from '../api/tasksApi.ts';
import { isStale, taskProblemMessage } from '../problems.ts';
import { type ProjectBoard, type TaskLookups } from '../useTaskData.ts';

import { AssignTaskDialog } from './AssignTaskDialog.tsx';
import { TaskDependencyDialog } from './TaskDependencyDialog.tsx';
import { TaskDetailDialog } from './TaskDetailDialog.tsx';
import { returnTo, type TaskDialog } from './taskDialog.ts';
import { TaskFormDialog } from './TaskFormDialog.tsx';

interface TaskDialogsProps {
  dialog: TaskDialog | null;
  onDialogChange: (dialog: TaskDialog | null) => void;
  project: ProjectSummary;
  user: SessionUser;
  board: ProjectBoard;
  lookups: TaskLookups;
  /**
   * Something was saved (or overtaken): the screen reads its tasks again and, given a message, says so. MOD-012 gives
   * none: it says so itself, in the dialog.
   */
  onChanged: (message: TranslationKey | null, tone?: 'success' | 'warning') => void;
}

/** MOD-010 to MOD-015 for one project's tasks, as SCR-047 and SCR-063–066 open them. */
export function TaskDialogs({
  dialog,
  onDialogChange,
  project,
  user,
  board,
  lookups,
  onChanged,
}: TaskDialogsProps): ReactElement | null {
  const { t } = useI18n();
  if (dialog === null) {
    return null;
  }
  const back = returnTo(dialog);
  const close = () => {
    onDialogChange(back === null ? null : { kind: 'detail', taskId: back });
  };
  const done = (message: TranslationKey, taskId: string | null = back) => {
    onChanged(message);
    onDialogChange(taskId === null ? null : { kind: 'detail', taskId });
  };
  const stale = () => {
    onChanged('tasks.done.stale', 'warning');
    close();
  };
  const owners = board.tasks.map((task) => task.assigneeUserId);

  switch (dialog.kind) {
    case 'detail':
      return (
        <TaskDetailDialog
          key={dialog.taskId}
          taskId={dialog.taskId}
          project={project}
          user={user}
          board={board}
          lookups={lookups}
          onOpen={onDialogChange}
          onClose={close}
          onChanged={() => {
            onChanged(null);
          }}
        />
      );
    case 'create':
    case 'subtask':
    case 'edit': {
      const parent =
        dialog.kind === 'subtask'
          ? board.tasks.find((task) => task.id === dialog.parentId)
          : undefined;
      if (dialog.kind === 'subtask' && parent === undefined) {
        return null;
      }
      return (
        <TaskFormDialog
          mode={
            dialog.kind === 'edit'
              ? { kind: 'edit', taskId: dialog.taskId }
              : parent === undefined
                ? { kind: 'create' }
                : { kind: 'subtask', parent }
          }
          project={project}
          user={user}
          board={board}
          lookups={lookups}
          onClose={close}
          onStale={stale}
          onDone={(task) => {
            done(
              dialog.kind === 'edit'
                ? 'tasks.done.updated'
                : dialog.kind === 'subtask'
                  ? 'tasks.done.subtaskCreated'
                  : 'tasks.done.created',
              dialog.kind === 'create' ? task.id : back,
            );
          }}
        />
      );
    }
    case 'assign':
      return (
        <AssignTaskDialog
          taskId={dialog.taskId}
          project={project}
          user={user}
          knownUserIds={owners}
          onClose={close}
          onStale={stale}
          onDone={() => {
            done('tasks.done.assigned');
          }}
        />
      );
    case 'dependency':
      return (
        <TaskDependencyDialog
          tasks={board.tasks}
          dependencies={board.dependencies}
          successorId={dialog.taskId}
          onClose={close}
          onStale={stale}
          onDone={() => {
            done('tasks.done.dependencyAdded');
          }}
        />
      );
    case 'removeDependency': {
      const { dependency } = dialog;
      return (
        <ConfirmDialog
          open
          title={t('tasks.removeDependency.title')}
          body={t('tasks.removeDependency.body')}
          confirmLabel={t('tasks.actions.removeDependency')}
          danger
          describe={taskProblemMessage}
          isStale={isStale}
          action={() => tasksApi.deleteDependency(dependency.id)}
          onClose={close}
          onStale={stale}
          onDone={() => {
            done('tasks.done.dependencyRemoved');
          }}
        />
      );
    }
  }
}
