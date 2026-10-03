import { type ReactElement, type SyntheticEvent, useCallback, useRef, useState } from 'react';

import { useSaveAction } from '@/features/identity-access/forms.ts';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { type ProjectSummary } from '@/features/projects/api/types.ts';
import { isLiveLeaf as isLiveLeafActivity } from '@/features/schedule/dependencyRules.ts';
import { activityLabel } from '@/features/schedule/presentation.ts';
import { type ApiResponse } from '@/shared/api/httpClient.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useFieldErrors } from '@/shared/forms/useFieldErrors.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { SelectField, TextAreaField, TextField } from '@/shared/ui/FormFields.tsx';
import { ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';

import { tasksApi } from '../api/tasksApi.ts';
import { type ProjectTaskDetail } from '../api/types.ts';
import { assigneeOf, assigneeValue, useUserSearch } from '../assignee.ts';
import { AssigneeField } from '../components/AssigneeField.tsx';
import { isStale, taskFieldMessage, taskProblemMessage } from '../problems.ts';
import {
  checkTaskForm,
  emptyTaskForm,
  taskFormValuesOf,
  type TaskFormValues,
  toTaskRequest,
} from '../taskRules.ts';
import { activityName, type ProjectBoard, type TaskLookups } from '../useTaskData.ts';

/** MOD-010 Create Task, MOD-013 Add Subtask (under `parent`), MOD-011 Edit Task (the task by id). */
export type TaskFormMode =
  | { kind: 'create' }
  | { kind: 'subtask'; parent: ProjectTaskDetail }
  | { kind: 'edit'; taskId: string };

interface TaskFormDialogProps {
  mode: TaskFormMode;
  project: ProjectSummary;
  user: SessionUser;
  board: ProjectBoard;
  lookups: TaskLookups;
  onClose: () => void;
  onDone: (task: ProjectTaskDetail) => void;
  onStale: () => void;
}

const TITLES = {
  create: 'tasks.form.createTitle',
  subtask: 'tasks.form.subtaskTitle',
  edit: 'tasks.form.editTitle',
} as const;

/**
 * A task's plan: title, description, schedule activity, priority and planned dates, and on creation its owner. The
 * planned duration is the API's, from the dates (TASK-048 D-3). A subtask executes against its parent's activity
 * (D-5), so it is shown, not chosen. An edit keeps the owner; MOD-014 changes it.
 */
export function TaskFormDialog(props: TaskFormDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog open title={t(TITLES[props.mode.kind])} onClose={props.onClose}>
      {props.mode.kind === 'edit' ? (
        <EditLoader {...props} taskId={props.mode.taskId} />
      ) : (
        <TaskForm {...props} editing={null} />
      )}
    </Dialog>
  );
}

function EditLoader(props: TaskFormDialogProps & { taskId: string }): ReactElement {
  const { t } = useI18n();
  const { taskId } = props;
  const load = useCallback((signal: AbortSignal) => tasksApi.task(taskId, signal), [taskId]);
  const task = useApiResource(load);
  if (task.data === undefined) {
    return task.loading ? (
      <LoadingState />
    ) : (
      <ErrorState message={taskProblemMessage(task.error, t)} onRetry={task.reload} />
    );
  }
  return <TaskForm {...props} editing={task.data} />;
}

function TaskForm({
  mode,
  project,
  user,
  board,
  lookups,
  editing,
  onClose,
  onDone,
  onStale,
}: TaskFormDialogProps & { editing: ApiResponse<ProjectTaskDetail> | null }): ReactElement {
  const { t, language } = useI18n();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction(taskProblemMessage);
  const canSearch = useUserSearch() ?? false;
  const parent = mode.kind === 'subtask' ? mode.parent : null;
  const before = editing?.data ?? null;
  const [values, setValues] = useState<TaskFormValues>(() =>
    before !== null
      ? taskFormValuesOf(before)
      : parent !== null
        ? emptyTaskForm(
            parent.plannedStartDate,
            parent.plannedFinishDate,
            parent.scheduleActivityId ?? '',
          )
        : emptyTaskForm('', ''),
  );
  const [owner, setOwner] = useState(() => assigneeValue(before?.assigneeUserId ?? null));
  const assignee = assigneeOf(owner, canSearch);
  const fields = useFieldErrors(
    { ...checkTaskForm(values), ...(before === null ? { assigneeUserId: assignee.code } : {}) },
    formRef,
    taskFieldMessage,
  );

  const set = (field: keyof TaskFormValues) => (value: string) => {
    setValues((current) => ({ ...current, [field]: value }));
    fields.clearServer();
  };

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (!fields.attempt()) {
      return;
    }
    const request = toTaskRequest(
      values,
      before === null ? assignee.userId : before.assigneeUserId,
      language,
      before,
    );
    const result = await save.run(async () =>
      before === null
        ? (
            await tasksApi.create({
              ...request,
              projectId: project.id,
              parentTaskId: parent?.id ?? null,
            })
          ).data
        : (await tasksApi.update(before.id, request, editing?.etag ?? null)).data,
    );
    if (result.ok) {
      onDone(result.value);
    } else if (isStale(result.error)) {
      onStale();
    } else {
      fields.showServer(result.error);
    }
  };

  // A task executes against a live leaf activity of the project's schedule (TASK-048 D-12); one it names already stays
  // listed, so an edit does not drop it unseen.
  const activities = board.activities ?? [];
  const activityOptions = activities
    .filter(
      (activity) => isLiveLeafActivity(activity) || activity.id === before?.scheduleActivityId,
    )
    .map((activity) => ({ value: activity.id, label: activityLabel(activity) }));
  const priorityOptions = [
    ...lookups.priorityOptions,
    ...(values.priorityItemId !== '' &&
    !lookups.priorityOptions.some((option) => option.value === values.priorityItemId)
      ? [
          {
            value: values.priorityItemId,
            label: lookups.priorityLabel(values.priorityItemId) ?? '',
          },
        ]
      : []),
  ];

  return (
    <form ref={formRef} className="form" noValidate onSubmit={(event) => void submit(event)}>
      {parent !== null && (
        <p className="form__note" dir="auto">
          {t('tasks.form.subtaskOf', { parent: parent.title.text })}
        </p>
      )}
      <FormAlert message={fields.summary ?? save.formError} />
      <TextField
        label={t('tasks.fields.title')}
        name="title"
        required
        value={values.title}
        onChange={set('title')}
        error={fields.errorOf('title')}
      />
      <TextAreaField
        label={t('tasks.fields.description')}
        name="description"
        rows={3}
        value={values.description}
        onChange={set('description')}
        error={fields.errorOf('description')}
      />
      {parent !== null ? (
        <p className="form__readonly">
          <span className="field__label">{t('tasks.fields.scheduleActivity')}</span>
          <span>
            {activityName(parent.scheduleActivityId, board.activities) ??
              t('tasks.detail.noActivity')}
          </span>
          <span className="field__hint">{t('tasks.form.subtaskActivityHint')}</span>
        </p>
      ) : board.activities === null ? (
        <p className="form__note">{t('tasks.form.activitiesUnreadable')}</p>
      ) : (
        <SelectField
          label={t('tasks.fields.scheduleActivity')}
          name="scheduleActivityId"
          value={values.scheduleActivityId}
          options={activityOptions}
          placeholder={t('tasks.form.noActivity')}
          hint={t('tasks.form.activityHint')}
          onChange={set('scheduleActivityId')}
          error={fields.errorOf('scheduleActivityId')}
        />
      )}
      {lookups.prioritiesReadable ? (
        <SelectField
          label={t('tasks.fields.priority')}
          name="priorityItemId"
          value={values.priorityItemId}
          options={priorityOptions}
          placeholder={t('tasks.form.noPriority')}
          onChange={set('priorityItemId')}
          error={fields.errorOf('priorityItemId')}
        />
      ) : (
        <p className="form__note">{t('tasks.form.prioritiesUnreadable')}</p>
      )}
      <div className="columns">
        <TextField
          label={t('tasks.fields.plannedStart')}
          name="plannedStartDate"
          type="date"
          required
          value={values.plannedStartDate}
          onChange={set('plannedStartDate')}
          error={fields.errorOf('plannedStartDate')}
        />
        <TextField
          label={t('tasks.fields.plannedFinish')}
          name="plannedFinishDate"
          type="date"
          required
          value={values.plannedFinishDate}
          onChange={set('plannedFinishDate')}
          error={fields.errorOf('plannedFinishDate')}
        />
      </div>
      {before === null && (
        <AssigneeField
          user={user}
          project={project}
          knownUserIds={board.tasks.map((task) => task.assigneeUserId)}
          value={owner}
          canSearch={canSearch}
          onChange={(value) => {
            setOwner(value);
            fields.clearServer();
          }}
          error={fields.errorOf('assigneeUserId')}
        />
      )}
      <div className="form__actions">
        <button type="submit" className="button button--primary" disabled={save.saving}>
          {save.saving ? t('common.states.saving') : t('common.actions.save')}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
