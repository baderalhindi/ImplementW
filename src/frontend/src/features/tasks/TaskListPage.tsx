import { type ReactElement, useState } from 'react';

import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { useSession } from '@/features/identity-access/session/useSession.ts';
import { type ProjectSummary } from '@/features/projects/api/types.ts';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { StatusFilterField } from './components/StatusFilterField.tsx';
import { TaskTable } from './components/TaskTable.tsx';
import { type TaskDialog } from './dialogs/taskDialog.ts';
import { TaskDialogs } from './dialogs/TaskDialogs.tsx';
import { updateReasonLabel } from './presentation.ts';
import { isForbidden, taskProblemMessage } from './problems.ts';
import {
  listEntries,
  managesAny,
  matchesStatus,
  type StatusFilter,
  type TaskListKind,
  todayUtc,
  updateReasons,
} from './taskRules.ts';
import { useProjectBoard, useTaskLookups, useTaskPortfolio } from './useTaskData.ts';

interface ListCopy {
  title: TranslationKey;
  description: TranslationKey;
  caption: TranslationKey;
  emptyTitle: TranslationKey;
  emptyBody: TranslationKey;
}

/** Each list's words, its empty state among them (task-boards-ui.md §3.1 documents every empty state). */
const COPY: Record<TaskListKind, ListCopy> = {
  mine: {
    title: 'tasks.lists.mine.title',
    description: 'tasks.lists.mine.description',
    caption: 'tasks.lists.mine.caption',
    emptyTitle: 'tasks.empty.mine.title',
    emptyBody: 'tasks.empty.mine.body',
  },
  team: {
    title: 'tasks.lists.team.title',
    description: 'tasks.lists.team.description',
    caption: 'tasks.lists.team.caption',
    emptyTitle: 'tasks.empty.team.title',
    emptyBody: 'tasks.empty.team.body',
  },
  overdue: {
    title: 'tasks.lists.overdue.title',
    description: 'tasks.lists.overdue.description',
    caption: 'tasks.lists.overdue.caption',
    emptyTitle: 'tasks.empty.overdue.title',
    emptyBody: 'tasks.empty.overdue.body',
  },
  updates: {
    title: 'tasks.lists.updates.title',
    description: 'tasks.lists.updates.description',
    caption: 'tasks.lists.updates.caption',
    emptyTitle: 'tasks.empty.updates.title',
    emptyBody: 'tasks.empty.updates.body',
  },
};

/** SCR-063 and SCR-064 list every state behind a filter; SCR-065 and SCR-066 are open work by definition. */
const FILTERED: Record<TaskListKind, boolean> = {
  mine: true,
  team: true,
  overdue: false,
  updates: false,
};

/**
 * SCR-063 My Tasks, SCR-064 Team Tasks, SCR-065 Overdue Tasks and SCR-066 Updates Required: the tasks of every
 * project the person may see (TASK-049 D-2), chosen as taskRules.ts `listEntries` defines each list. A title opens
 * MOD-012 over the list, with the rest of the task's modals.
 */
export function TaskListPage({ list }: { list: TaskListKind }): ReactElement | null {
  const { t } = useI18n();
  const { session } = useSession();
  const portfolio = useTaskPortfolio();
  const [filter, setFilter] = useState<StatusFilter>('open');
  const [notice, setNotice] = useState<Notice | null>(null);
  const [open, setOpen] = useState<{ project: ProjectSummary; dialog: TaskDialog } | null>(null);
  const copy = COPY[list];

  if (session === null) {
    return null;
  }
  const user = session.user;
  const header = <PageHeader title={t(copy.title)} description={t(copy.description)} />;

  if (portfolio.data === undefined) {
    return (
      <>
        {header}
        {portfolio.loading ? (
          <LoadingState />
        ) : isForbidden(portfolio.error) ? (
          <p className="state">{t('tasks.forbidden')}</p>
        ) : (
          <ErrorState message={taskProblemMessage(portfolio.error, t)} onRetry={portfolio.reload} />
        )}
      </>
    );
  }

  const today = todayUtc();
  const { projects, entries } = portfolio.data;
  const listed = listEntries(list, entries, user.id, today);
  const shown = FILTERED[list] ? listed.filter(({ task }) => matchesStatus(task, filter)) : listed;
  const parents = new Map(entries.map(({ task }) => [task.id, task]));
  const noTeam = list === 'team' && !managesAny(projects, user.id);

  return (
    <>
      {header}
      <PageNotice notice={notice} />
      {listed.length === 0 ? (
        <EmptyState title={t(noTeam ? 'tasks.empty.team.noProjectTitle' : copy.emptyTitle)}>
          <p>{t(noTeam ? 'tasks.empty.team.noProjectBody' : copy.emptyBody)}</p>
        </EmptyState>
      ) : (
        <>
          {FILTERED[list] && <StatusFilterField value={filter} onChange={setFilter} />}
          {shown.length === 0 ? (
            <EmptyState title={t('tasks.empty.filtered.title')}>
              <p>{t('tasks.empty.filtered.body')}</p>
            </EmptyState>
          ) : (
            <TaskTable
              caption={t(copy.caption)}
              entries={shown}
              today={today}
              showProject
              nested={false}
              parents={parents}
              aside={
                list === 'updates'
                  ? ({ task }) => (
                      <ul className="cell__aside reason-list">
                        {updateReasons(task, today).map((reason) => (
                          <li key={reason.kind}>{updateReasonLabel(reason, t)}</li>
                        ))}
                      </ul>
                    )
                  : undefined
              }
              onOpen={({ task, project }) => {
                setOpen({ project, dialog: { kind: 'detail', taskId: task.id } });
              }}
            />
          )}
        </>
      )}
      {open !== null && (
        <ProjectTaskDialogs
          key={open.project.id}
          project={open.project}
          user={user}
          dialog={open.dialog}
          onDialogChange={(dialog) => {
            setOpen(dialog === null ? null : { project: open.project, dialog });
          }}
          onChanged={(message, tone = 'success') => {
            if (message !== null) {
              setNotice({ tone, message: t(message) });
            }
            portfolio.reload();
          }}
        />
      )}
    </>
  );
}

/** The task's modals over a list: the project's board is read for them, as SCR-047 reads it. */
function ProjectTaskDialogs({
  project,
  user,
  dialog,
  onDialogChange,
  onChanged,
}: {
  project: ProjectSummary;
  user: SessionUser;
  dialog: TaskDialog;
  onDialogChange: (dialog: TaskDialog | null) => void;
  onChanged: (message: TranslationKey | null, tone?: Notice['tone']) => void;
}): ReactElement | null {
  const board = useProjectBoard(project.id);
  const lookups = useTaskLookups();
  if (board.data === undefined) {
    return null;
  }
  return (
    <TaskDialogs
      dialog={dialog}
      onDialogChange={onDialogChange}
      project={project}
      user={user}
      board={board.data}
      lookups={lookups}
      onChanged={(message, tone) => {
        board.reload();
        onChanged(message, tone);
      }}
    />
  );
}
