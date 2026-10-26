import { type ReactElement, type ReactNode } from 'react';
import { Link } from 'react-router';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { DateRange } from '@/features/schedule/components/DateRange.tsx';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { TableContainer } from '@/shared/ui/Layout.tsx';

import { type ProjectTaskDetail } from '../api/types.ts';
import { overdueDays, type TaskEntry } from '../taskRules.ts';

import { OverdueFlag, PercentFigure, TaskStatusBadge } from './TaskBadges.tsx';

interface TaskTableProps {
  caption: string;
  entries: TaskEntry[];
  today: string;
  /** SCR-063–066 name each task's project; SCR-047 is one project's. */
  showProject: boolean;
  /** SCR-047 indents subtasks under their parent; the lists name the parent instead. */
  nested: boolean;
  /** Every task of the project, to name a subtask's parent. */
  parents: ReadonlyMap<string, ProjectTaskDetail>;
  /** A line under the task's title (SCR-066's reasons). */
  aside?: ((entry: TaskEntry) => ReactNode) | undefined;
  onOpen: (entry: TaskEntry) => void;
}

/**
 * The task rows of SCR-047 and SCR-063–066: title (which opens MOD-012), status, owner, planned dates and progress. An
 * overdue row carries the icon and the words, and a thick edge, as well as the colour (acceptance criterion 1).
 */
export function TaskTable({
  caption,
  entries,
  today,
  showProject,
  nested,
  parents,
  aside,
  onOpen,
}: TaskTableProps): ReactElement {
  const { t } = useI18n();
  const personName = usePersonNames(entries.map((entry) => entry.task.assigneeUserId));

  return (
    <TableContainer caption={caption}>
      <thead>
        <tr>
          <th scope="col">{t('tasks.table.task')}</th>
          {showProject && <th scope="col">{t('tasks.table.project')}</th>}
          <th scope="col">{t('tasks.table.status')}</th>
          <th scope="col">{t('tasks.table.owner')}</th>
          <th scope="col">{t('tasks.table.planned')}</th>
          <th scope="col">{t('tasks.table.progress')}</th>
        </tr>
      </thead>
      <tbody>
        {entries.map((entry) => {
          const { task, project } = entry;
          const overdue = overdueDays(task, today);
          const parent = task.parentTaskId === null ? undefined : parents.get(task.parentTaskId);
          const rowClass =
            overdue !== null
              ? 'row--overdue'
              : task.status === 'CANCELLED'
                ? 'row--muted'
                : undefined;
          return (
            <tr key={task.id} className={rowClass} data-overdue={overdue !== null || undefined}>
              <td
                style={
                  nested && task.parentTaskId !== null ? { paddingInlineStart: '2rem' } : undefined
                }
              >
                <button
                  type="button"
                  className="button button--link task__title"
                  dir="auto"
                  onClick={() => {
                    onOpen(entry);
                  }}
                >
                  {task.title.text}
                </button>
                {!nested && parent !== undefined && (
                  <span className="cell__aside" dir="auto">
                    {t('tasks.table.subtaskOf', { parent: parent.title.text })}
                  </span>
                )}
                {task.subtaskCount > 0 && (
                  <span className="cell__aside">
                    {task.subtaskCount === 1
                      ? t('tasks.table.subtasksOne')
                      : t('tasks.table.subtasks', { count: task.subtaskCount })}
                  </span>
                )}
                {overdue !== null && <OverdueFlag days={overdue} />}
                {aside?.(entry)}
              </td>
              {showProject && (
                <td>
                  <Link to={`/projects/${project.id}/tasks`} dir="auto">
                    {project.title.text}
                  </Link>
                </td>
              )}
              <td>
                <TaskStatusBadge task={task} />
              </td>
              <td>
                {task.assigneeUserId === null ? (
                  <span className="figure figure--none">{t('tasks.owner.none')}</span>
                ) : (
                  personName(task.assigneeUserId)
                )}
              </td>
              <td>
                <DateRange start={task.plannedStartDate} finish={task.plannedFinishDate} />
              </td>
              <td>
                <PercentFigure task={task} />
              </td>
            </tr>
          );
        })}
      </tbody>
    </TableContainer>
  );
}
