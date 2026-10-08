import { type ReactElement, useState } from 'react';

import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { type ProjectDetail } from '@/features/projects/api/types.ts';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { canManageTasks } from './access.ts';
import { StatusFilterField } from './components/StatusFilterField.tsx';
import { TaskTable } from './components/TaskTable.tsx';
import { type TaskDialog } from './dialogs/taskDialog.ts';
import { TaskDialogs } from './dialogs/TaskDialogs.tsx';
import { isForbidden, taskProblemMessage } from './problems.ts';
import { isLiveLeaf, matchesStatus, type StatusFilter, todayUtc } from './taskRules.ts';
import { useProjectBoard, useTaskLookups } from './useTaskData.ts';

/**
 * SCR-047 Project Tasks: the project's tasks, each parent followed by its subtasks, with status, owner, planned dates,
 * progress and the overdue flag. A title opens MOD-012. The project's own Project Manager adds tasks (MOD-010) and
 * dependencies (MOD-015) while the project is APPROVED_PLANNED or ACTIVE.
 */
export function ProjectTasks({
  project,
  user,
}: {
  project: ProjectDetail;
  user: SessionUser;
}): ReactElement {
  const { t } = useI18n();
  const board = useProjectBoard(project.id);
  const lookups = useTaskLookups();
  const [filter, setFilter] = useState<StatusFilter>('open');
  const [notice, setNotice] = useState<Notice | null>(null);
  const [dialog, setDialog] = useState<TaskDialog | null>(null);

  if (board.data === undefined) {
    if (board.loading) {
      return <LoadingState />;
    }
    return isForbidden(board.error) ? (
      <p className="state">{t('tasks.forbidden')}</p>
    ) : (
      <ErrorState message={taskProblemMessage(board.error, t)} onRetry={board.reload} />
    );
  }

  const data = board.data;
  const manager = canManageTasks(user, project);
  const today = todayUtc();
  const parents = new Map(data.tasks.map((task) => [task.id, task]));
  const shown = data.tasks.filter((task) => matchesStatus(task, filter));
  const changed = (message: TranslationKey | null, tone: Notice['tone'] = 'success') => {
    if (message !== null) {
      setNotice({ tone, message: t(message) });
    }
    board.reload();
  };

  return (
    <div className="tasks">
      <PageNotice notice={notice} />
      <section className="section" aria-labelledby="project-tasks">
        <div className="section__header">
          <h2 id="project-tasks">{t('tasks.project.title')}</h2>
          {manager && (
            <span className="tasks__actions">
              <button
                type="button"
                className="button button--primary"
                onClick={() => {
                  setDialog({ kind: 'create' });
                }}
              >
                {t('tasks.actions.create')}
              </button>
              {data.tasks.filter(isLiveLeaf).length >= 2 && (
                <button
                  type="button"
                  className="button"
                  onClick={() => {
                    setDialog({ kind: 'dependency', taskId: null });
                  }}
                >
                  {t('tasks.actions.addDependency')}
                </button>
              )}
            </span>
          )}
        </div>
        {data.tasks.length === 0 ? (
          <EmptyState title={t('tasks.empty.project.title')}>
            <p>
              {manager
                ? t('tasks.empty.project.manager')
                : project.status === 'ACTIVE' || project.status === 'APPROVED_PLANNED'
                  ? t('tasks.empty.project.waiting')
                  : project.status === 'SUSPENDED' ||
                      project.status === 'COMPLETED' ||
                      project.status === 'CLOSED'
                    ? t('tasks.empty.project.noLonger')
                    : t('tasks.empty.project.notPlanned')}
            </p>
          </EmptyState>
        ) : (
          <>
            <StatusFilterField value={filter} onChange={setFilter} />
            {shown.length === 0 ? (
              <EmptyState title={t('tasks.empty.filtered.title')}>
                <p>{t('tasks.empty.filtered.body')}</p>
              </EmptyState>
            ) : (
              <TaskTable
                caption={t('tasks.project.caption')}
                entries={shown.map((task) => ({ task, project }))}
                today={today}
                showProject={false}
                nested={filter === 'all' || filter === 'open'}
                parents={parents}
                onOpen={({ task }) => {
                  setDialog({ kind: 'detail', taskId: task.id });
                }}
              />
            )}
          </>
        )}
      </section>

      <TaskDialogs
        dialog={dialog}
        onDialogChange={setDialog}
        project={project}
        user={user}
        board={data}
        lookups={lookups}
        onChanged={changed}
      />
    </div>
  );
}
