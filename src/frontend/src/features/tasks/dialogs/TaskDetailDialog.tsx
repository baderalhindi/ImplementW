import { type ReactElement, type SyntheticEvent, useCallback, useRef, useState } from 'react';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { checkText, type SaveResult, useSaveAction } from '@/features/identity-access/forms.ts';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { formatPercent } from '@/features/progress/presentation.ts';
import { narrativeRequest } from '@/features/progress/progressUpdate.ts';
import { type NarrativeTextRequest, type ProjectSummary } from '@/features/projects/api/types.ts';
import { Detail } from '@/features/projects/components/Detail.tsx';
import { shortId } from '@/features/projects/presentation.ts';
import { TEXT_LENGTH } from '@/features/projects/registration.ts';
import { type ApiResponse } from '@/shared/api/httpClient.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useFieldErrors } from '@/shared/forms/useFieldErrors.ts';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { TextAreaField, TextField } from '@/shared/ui/FormFields.tsx';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
import { ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';

import {
  canAddSubtask,
  canPlanTask,
  type TaskCommand,
  taskCommands,
  taskRights,
} from '../access.ts';
import { tasksApi } from '../api/tasksApi.ts';
import { type ProjectTaskDetail, type TaskDependencyDetail } from '../api/types.ts';
import { OverdueFlag, PercentFigure, TaskStatusBadge } from '../components/TaskBadges.tsx';
import { isStale, taskFieldMessage, taskProblemMessage } from '../problems.ts';
import {
  checkPercent,
  isLeaf,
  isLiveLeaf,
  isMet,
  overdueDays,
  todayUtc,
  unmetDependencies,
} from '../taskRules.ts';
import { activityName, type ProjectBoard, type TaskLookups } from '../useTaskData.ts';

import { type TaskDialog } from './taskDialog.ts';

interface TaskDetailDialogProps {
  taskId: string;
  project: ProjectSummary;
  user: SessionUser;
  board: ProjectBoard;
  lookups: TaskLookups;
  /** Another of the task's modals: MOD-011, MOD-013, MOD-014, MOD-015, or another task's MOD-012. */
  onOpen: (dialog: TaskDialog) => void;
  onClose: () => void;
  /** A command was saved: the board behind the dialog is read again. */
  onChanged: () => void;
}

/**
 * MOD-012 Task Detail: everything about one task, and every action the person may take on it, as native buttons,
 * links and fields in reading order, so the keyboard reaches each of them with Tab and the dialog closes with Escape
 * (acceptance criterion 3). The task is read on its own for its ETag, which each command sends (R-21).
 */
export function TaskDetailDialog(props: TaskDetailDialogProps): ReactElement {
  const { t } = useI18n();
  const { taskId } = props;
  const load = useCallback((signal: AbortSignal) => tasksApi.task(taskId, signal), [taskId]);
  const task = useApiResource(load, { keepWhileReloading: true });
  return (
    <Dialog
      open
      title={task.data?.data.title.text ?? t('tasks.detail.title')}
      onClose={props.onClose}
    >
      {task.data === undefined ? (
        task.loading ? (
          <LoadingState />
        ) : (
          <ErrorState message={taskProblemMessage(task.error, t)} onRetry={task.reload} />
        )
      ) : (
        <TaskDetailBody {...props} task={task.data} reload={task.reload} />
      )}
    </Dialog>
  );
}

/** The commands that take an input or a confirmation first, shown in place under the buttons. */
type InlineCommand = 'block' | 'reportProgress' | 'cancel';

function isInline(name: TaskCommand): name is InlineCommand {
  return name === 'block' || name === 'reportProgress' || name === 'cancel';
}

const DONE: Record<Exclude<TaskCommand, InlineCommand>, TranslationKey> = {
  start: 'tasks.done.started',
  complete: 'tasks.done.completed',
  unblock: 'tasks.done.unblocked',
  reopen: 'tasks.done.reopened',
};

function TaskDetailBody({
  task: { data: task, etag },
  reload,
  project,
  user,
  board,
  lookups,
  onOpen,
  onClose,
  onChanged,
}: TaskDetailDialogProps & {
  task: ApiResponse<ProjectTaskDetail>;
  reload: () => void;
}): ReactElement {
  const { t } = useI18n();
  const save = useSaveAction(taskProblemMessage);
  const [pending, setPending] = useState<InlineCommand | null>(null);
  const [notice, setNotice] = useState<Notice | null>(null);
  const personName = usePersonNames([task.assigneeUserId]);

  const byId = new Map(board.tasks.map((candidate) => [candidate.id, candidate]));
  const nameOf = (id: string) => byId.get(id)?.title.text ?? shortId(id);
  const parentId = task.parentTaskId;
  const parent = parentId === null ? null : (byId.get(parentId) ?? null);
  const rights = taskRights(user, project, task);
  const commands = taskCommands(task, rights, parent, board.dependencies);
  const plannable = canPlanTask(task, rights);
  const subtasks = board.tasks.filter((candidate) => candidate.parentTaskId === task.id);
  const waitsFor = board.dependencies.filter((link) => link.successorTaskId === task.id);
  const holdsBack = board.dependencies.filter((link) => link.predecessorTaskId === task.id);
  const linkable = rights.manage && isLiveLeaf(task) && board.tasks.filter(isLiveLeaf).length >= 2;
  const overdue = overdueDays(task, todayUtc());

  /** Runs a command; the task and the board are read again after it, saved or overtaken by another change. */
  const run = async (
    action: () => Promise<unknown>,
    done: TranslationKey,
  ): Promise<SaveResult<unknown>> => {
    const result = await save.run(action);
    if (result.ok || isStale(result.error)) {
      setPending(null);
      setNotice(
        result.ok
          ? { tone: 'success', message: t(done) }
          : { tone: 'warning', message: t('tasks.done.stale') },
      );
      reload();
      onChanged();
    }
    return result;
  };

  const immediate = (name: Exclude<TaskCommand, InlineCommand>) => {
    switch (name) {
      case 'start':
        return () => tasksApi.start(task.id, etag);
      case 'complete':
        return () => tasksApi.complete(task.id, etag);
      case 'unblock':
        return () => tasksApi.unblock(task.id, etag);
      case 'reopen':
        return () => tasksApi.reopen(task.id, etag);
    }
  };

  const waiting = [
    ...(task.status === 'NOT_STARTED'
      ? unmetDependencies(task.id, true, board.tasks, board.dependencies)
      : []),
    ...(task.status === 'IN_PROGRESS'
      ? unmetDependencies(task.id, false, board.tasks, board.dependencies)
      : []),
  ];
  const openSubtasks = subtasks.filter(
    (subtask) => subtask.status !== 'COMPLETED' && subtask.status !== 'CANCELLED',
  );

  return (
    <div className="task-detail">
      <p className="figure-group">
        <TaskStatusBadge task={task} />
        {overdue !== null && <OverdueFlag days={overdue} />}
        <span>
          {t('tasks.detail.percent', { percent: formatPercent(task.percentComplete) ?? '' })}
        </span>
      </p>
      <PageNotice notice={notice} />
      {task.status === 'BLOCKED' && task.blockedReason !== null && (
        <p className="notice notice--warning" dir="auto">
          {t('tasks.detail.blockedBecause', { reason: task.blockedReason.text })}
        </p>
      )}

      <dl className="details">
        <Detail term={t('tasks.table.project')}>
          <span dir="auto">{project.title.text}</span>
        </Detail>
        {parentId !== null && (
          <Detail term={t('tasks.detail.parent')}>
            <button
              type="button"
              className="button button--link"
              dir="auto"
              onClick={() => {
                onOpen({ kind: 'detail', taskId: parentId });
              }}
            >
              {nameOf(parentId)}
            </button>
          </Detail>
        )}
        <Detail term={t('tasks.fields.owner')}>
          {task.assigneeUserId === null ? t('tasks.owner.none') : personName(task.assigneeUserId)}
        </Detail>
        <Detail term={t('tasks.fields.scheduleActivity')}>
          <span dir="auto">
            {activityName(task.scheduleActivityId, board.activities) ??
              t('tasks.detail.noActivity')}
          </span>
        </Detail>
        <Detail term={t('tasks.fields.priority')}>
          {lookups.priorityLabel(task.priorityItemId) ?? t('tasks.detail.noPriority')}
        </Detail>
        <Detail term={t('tasks.table.planned')}>
          <span dir="ltr">{`${task.plannedStartDate} – ${task.plannedFinishDate}`}</span>
          <span className="details__aside">
            {task.plannedDurationDays === 1
              ? t('tasks.detail.daysOne')
              : t('tasks.detail.days', { days: task.plannedDurationDays })}
          </span>
        </Detail>
        <Detail term={t('tasks.detail.actual')}>
          {task.actualStartDate === null ? (
            t('tasks.detail.notStarted')
          ) : (
            <span dir="ltr">{`${task.actualStartDate} – ${task.actualFinishDate ?? '…'}`}</span>
          )}
        </Detail>
        <Detail term={t('tasks.table.progress')}>
          <PercentFigure task={task} />
          <span className="details__aside">
            {!isLeaf(task)
              ? t('tasks.detail.rolledUp', { count: task.subtaskCount })
              : task.actualPercentComplete === null
                ? t('tasks.detail.notEntered')
                : t('tasks.detail.entered')}
          </span>
        </Detail>
        {task.reopenedCount > 0 && (
          <Detail term={t('tasks.detail.reopened')}>{task.reopenedCount}</Detail>
        )}
        {task.description !== null && (
          <Detail term={t('tasks.fields.description')}>
            <span className="pre-line" dir="auto">
              {task.description.text}
            </span>
          </Detail>
        )}
      </dl>

      {(commands.length > 0 || waiting.length > 0) && (
        <section className="task-detail__section" aria-labelledby={`work-${task.id}`}>
          <h3 id={`work-${task.id}`}>{t('tasks.detail.work')}</h3>
          {waiting.length > 0 && (
            <p className="form__note" dir="auto">
              {t('tasks.detail.waitingFor', {
                list: waiting
                  .map(
                    (link) =>
                      `${nameOf(link.predecessorTaskId)} (${t(`tasks.dependencyTypeShort.${link.dependencyType}`)})`,
                  )
                  .join(', '),
              })}
            </p>
          )}
          {task.status === 'IN_PROGRESS' && openSubtasks.length > 0 && (
            <p className="form__note">
              {t('tasks.detail.subtasksOpen', { count: openSubtasks.length })}
            </p>
          )}
          {task.status === 'BLOCKED' && rights.execute && (
            <p className="form__note">{t('tasks.detail.unblockFirst')}</p>
          )}
          <FormAlert message={pending === null ? save.formError : null} />
          <div className="form__actions">
            {commands.map((name) => (
              <button
                key={name}
                type="button"
                className={name === 'cancel' ? 'button button--danger' : 'button'}
                aria-expanded={isInline(name) ? pending === name : undefined}
                disabled={save.saving}
                onClick={() => {
                  if (isInline(name)) {
                    setPending(pending === name ? null : name);
                  } else {
                    void run(immediate(name), DONE[name]);
                  }
                }}
              >
                {t(`tasks.commands.${name}`)}
              </button>
            ))}
          </div>
          {pending === 'block' && (
            <BlockForm
              saving={save.saving}
              formError={save.formError}
              onSubmit={(reason) =>
                run(() => tasksApi.block(task.id, reason, etag), 'tasks.done.blocked')
              }
              onCancel={() => {
                setPending(null);
              }}
            />
          )}
          {pending === 'reportProgress' && (
            <ProgressForm
              current={
                task.actualPercentComplete === null ? '' : String(task.actualPercentComplete)
              }
              saving={save.saving}
              formError={save.formError}
              onSubmit={(percent) =>
                run(
                  () => tasksApi.reportProgress(task.id, percent, etag),
                  'tasks.done.progressReported',
                )
              }
              onCancel={() => {
                setPending(null);
              }}
            />
          )}
          {pending === 'cancel' && (
            <div className="task-detail__confirm">
              <p>{t('tasks.cancel.body')}</p>
              <FormAlert message={save.formError} />
              <div className="form__actions">
                <button
                  type="button"
                  className="button button--danger"
                  disabled={save.saving}
                  onClick={() =>
                    void run(() => tasksApi.cancel(task.id, etag), 'tasks.done.cancelled')
                  }
                >
                  {t('tasks.cancel.confirm')}
                </button>
                <button
                  type="button"
                  className="button"
                  disabled={save.saving}
                  onClick={() => {
                    setPending(null);
                  }}
                >
                  {t('tasks.cancel.keep')}
                </button>
              </div>
            </div>
          )}
        </section>
      )}

      {parentId === null &&
        (subtasks.length > 0 || canAddSubtask(task, rights, board.dependencies)) && (
          <section className="task-detail__section" aria-labelledby={`subtasks-${task.id}`}>
            <h3 id={`subtasks-${task.id}`}>{t('tasks.detail.subtasks')}</h3>
            {subtasks.length === 0 ? (
              <p className="form__note">{t('tasks.detail.noSubtasks')}</p>
            ) : (
              <ul className="task-detail__list">
                {subtasks.map((subtask) => (
                  <li key={subtask.id}>
                    <button
                      type="button"
                      className="button button--link"
                      dir="auto"
                      onClick={() => {
                        onOpen({ kind: 'detail', taskId: subtask.id });
                      }}
                    >
                      {subtask.title.text}
                    </button>
                    <TaskStatusBadge task={subtask} />
                    <PercentFigure task={subtask} />
                  </li>
                ))}
              </ul>
            )}
            {canAddSubtask(task, rights, board.dependencies) && (
              <button
                type="button"
                className="button"
                onClick={() => {
                  onOpen({ kind: 'subtask', parentId: task.id });
                }}
              >
                {t('tasks.actions.addSubtask')}
              </button>
            )}
          </section>
        )}

      {(waitsFor.length > 0 || holdsBack.length > 0 || linkable) && (
        <section className="task-detail__section" aria-labelledby={`links-${task.id}`}>
          <h3 id={`links-${task.id}`}>{t('tasks.detail.dependencies')}</h3>
          <DependencyList
            title={t('tasks.detail.waitsFor')}
            links={waitsFor}
            other={(link) => link.predecessorTaskId}
            byId={byId}
            removable={rights.manage}
            onRemove={(dependency) => {
              onOpen({ kind: 'removeDependency', dependency, taskId: task.id });
            }}
          />
          <DependencyList
            title={t('tasks.detail.holdsBack')}
            links={holdsBack}
            other={(link) => link.successorTaskId}
            byId={byId}
            removable={rights.manage}
            onRemove={(dependency) => {
              onOpen({ kind: 'removeDependency', dependency, taskId: task.id });
            }}
          />
          {linkable && (
            <button
              type="button"
              className="button"
              onClick={() => {
                onOpen({ kind: 'dependency', taskId: task.id });
              }}
            >
              {t('tasks.actions.addDependency')}
            </button>
          )}
        </section>
      )}

      <div className="form__actions">
        {plannable && (
          <button
            type="button"
            className="button"
            onClick={() => {
              onOpen({ kind: 'edit', taskId: task.id });
            }}
          >
            {t('tasks.actions.edit')}
          </button>
        )}
        {plannable && (
          <button
            type="button"
            className="button"
            onClick={() => {
              onOpen({ kind: 'assign', taskId: task.id });
            }}
          >
            {t('tasks.actions.assign')}
          </button>
        )}
        <button type="button" className="button button--primary" onClick={onClose}>
          {t('tasks.actions.close')}
        </button>
      </div>
    </div>
  );
}

function DependencyList({
  title,
  links,
  other,
  byId,
  removable,
  onRemove,
}: {
  title: string;
  links: TaskDependencyDetail[];
  /** The task at the other end. */
  other: (link: TaskDependencyDetail) => string;
  byId: ReadonlyMap<string, ProjectTaskDetail>;
  removable: boolean;
  onRemove: (link: TaskDependencyDetail) => void;
}): ReactElement | null {
  const { t } = useI18n();
  if (links.length === 0) {
    return null;
  }
  return (
    <>
      <h4 className="task-detail__subheading">{title}</h4>
      <ul className="task-detail__list">
        {links.map((link) => {
          const otherTask = byId.get(other(link));
          const name = otherTask?.title.text ?? shortId(other(link));
          const predecessor = byId.get(link.predecessorTaskId);
          const met = predecessor !== undefined && isMet(link.dependencyType, predecessor);
          return (
            <li key={link.id}>
              <span dir="auto">{name}</span>
              <span className="cell__aside">
                {t(`tasks.dependencyTypeShort.${link.dependencyType}`)}
                {' · '}
                {met ? t('tasks.detail.met') : t('tasks.detail.notMet')}
              </span>
              {removable && (
                <button
                  type="button"
                  className="button button--link"
                  aria-label={t('tasks.actions.removeDependencyOn', { task: name })}
                  onClick={() => {
                    onRemove(link);
                  }}
                >
                  {t('tasks.actions.removeDependency')}
                </button>
              )}
            </li>
          );
        })}
      </ul>
    </>
  );
}

interface InlineFormProps<T> {
  saving: boolean;
  formError: string | null;
  onSubmit: (value: T) => Promise<SaveResult<unknown>>;
  onCancel: () => void;
}

/** Block: why, in the language it is typed in (TASK-048 D-4). */
function BlockForm({
  saving,
  formError,
  onSubmit,
  onCancel,
}: InlineFormProps<NarrativeTextRequest>) {
  const { t, language } = useI18n();
  const formRef = useRef<HTMLFormElement>(null);
  const [reason, setReason] = useState('');
  const fields = useFieldErrors(
    { reason: checkText(reason, { required: true, maxLength: TEXT_LENGTH }) },
    formRef,
    taskFieldMessage,
  );
  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (!fields.attempt()) {
      return;
    }
    const result = await onSubmit(narrativeRequest(reason, language));
    if (!result.ok) {
      fields.showServer(result.error);
    }
  };
  return (
    <form
      ref={formRef}
      className="form task-detail__confirm"
      noValidate
      onSubmit={(event) => void submit(event)}
    >
      <FormAlert message={fields.summary ?? formError} />
      <TextAreaField
        label={t('tasks.block.reason')}
        name="reason"
        required
        rows={3}
        maxLength={TEXT_LENGTH}
        value={reason}
        onChange={(value) => {
          setReason(value);
          fields.clearServer();
        }}
        error={fields.errorOf('reason')}
      />
      <div className="form__actions">
        <button type="submit" className="button button--primary" disabled={saving}>
          {saving ? t('common.states.saving') : t('tasks.block.confirm')}
        </button>
        <button type="button" className="button" disabled={saving} onClick={onCancel}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}

/** A leaf's actual percentage, 0 to 100 to four places (ADR-009). */
function ProgressForm({
  current,
  saving,
  formError,
  onSubmit,
  onCancel,
}: InlineFormProps<number> & { current: string }) {
  const { t } = useI18n();
  const formRef = useRef<HTMLFormElement>(null);
  const [percent, setPercent] = useState(current);
  const fields = useFieldErrors(
    { actualPercentComplete: checkPercent(percent) },
    formRef,
    taskFieldMessage,
  );
  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (!fields.attempt()) {
      return;
    }
    const result = await onSubmit(Number(percent.trim()));
    if (!result.ok) {
      fields.showServer(result.error);
    }
  };
  return (
    <form
      ref={formRef}
      className="form task-detail__confirm"
      noValidate
      onSubmit={(event) => void submit(event)}
    >
      <FormAlert message={fields.summary ?? formError} />
      <TextField
        label={t('tasks.progress.percent')}
        name="actualPercentComplete"
        required
        inputMode="decimal"
        dir="ltr"
        hint={t('tasks.progress.hint')}
        value={percent}
        onChange={(value) => {
          setPercent(value);
          fields.clearServer();
        }}
        error={fields.errorOf('actualPercentComplete')}
      />
      <div className="form__actions">
        <button type="submit" className="button button--primary" disabled={saving}>
          {saving ? t('common.states.saving') : t('tasks.progress.confirm')}
        </button>
        <button type="button" className="button" disabled={saving} onClick={onCancel}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
