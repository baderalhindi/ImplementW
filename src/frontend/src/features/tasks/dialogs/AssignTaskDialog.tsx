import { type ReactElement, type SyntheticEvent, useCallback, useRef, useState } from 'react';

import { useSaveAction } from '@/features/identity-access/forms.ts';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { type ProjectSummary } from '@/features/projects/api/types.ts';
import { type ApiResponse } from '@/shared/api/httpClient.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useFieldErrors } from '@/shared/forms/useFieldErrors.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';

import { tasksApi } from '../api/tasksApi.ts';
import { type ProjectTaskDetail } from '../api/types.ts';
import { assigneeOf, assigneeValue, useUserSearch } from '../assignee.ts';
import { AssigneeField } from '../components/AssigneeField.tsx';
import { isStale, taskFieldMessage, taskProblemMessage } from '../problems.ts';
import { withAssignee } from '../taskRules.ts';

interface AssignTaskDialogProps {
  taskId: string;
  project: ProjectSummary;
  user: SessionUser;
  /** Owners of the project's tasks: offered by name. */
  knownUserIds: (string | null)[];
  onClose: () => void;
  onDone: (task: ProjectTaskDetail) => void;
  onStale: () => void;
}

/**
 * MOD-014 Assign Task: the task's plan re-sent with another owner (R-5, If-Match). Whether that person may own it — a
 * role over the project now (TASK-048 D-12) — is decided by the API alone; its refusal, 422
 * TASK_ASSIGNEE_NOT_ELIGIBLE on `assigneeUserId`, is shown on the owner field and in the form's alert, and nothing
 * changes (acceptance criterion 2).
 */
export function AssignTaskDialog(props: AssignTaskDialogProps): ReactElement {
  const { t } = useI18n();
  const { taskId } = props;
  const load = useCallback((signal: AbortSignal) => tasksApi.task(taskId, signal), [taskId]);
  const task = useApiResource(load);
  return (
    <Dialog open title={t('tasks.assign.title')} onClose={props.onClose}>
      {task.data === undefined ? (
        task.loading ? (
          <LoadingState />
        ) : (
          <ErrorState message={taskProblemMessage(task.error, t)} onRetry={task.reload} />
        )
      ) : (
        <AssignForm {...props} task={task.data} />
      )}
    </Dialog>
  );
}

function AssignForm({
  task: { data: task, etag },
  project,
  user,
  knownUserIds,
  onClose,
  onDone,
  onStale,
}: AssignTaskDialogProps & { task: ApiResponse<ProjectTaskDetail> }): ReactElement {
  const { t } = useI18n();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction(taskProblemMessage);
  const canSearch = useUserSearch() ?? false;
  const [owner, setOwner] = useState(() => assigneeValue(task.assigneeUserId));
  const assignee = assigneeOf(owner, canSearch);
  const fields = useFieldErrors({ assigneeUserId: assignee.code }, formRef, taskFieldMessage);

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (!fields.attempt()) {
      return;
    }
    const result = await save.run(
      async () => (await tasksApi.update(task.id, withAssignee(task, assignee.userId), etag)).data,
    );
    if (result.ok) {
      onDone(result.value);
    } else if (isStale(result.error)) {
      onStale();
    } else {
      fields.showServer(result.error);
    }
  };

  return (
    <form ref={formRef} className="form" noValidate onSubmit={(event) => void submit(event)}>
      <p className="form__note" dir="auto">
        {t('tasks.assign.intro', { task: task.title.text })}
      </p>
      <FormAlert message={fields.summary ?? save.formError} />
      <AssigneeField
        user={user}
        project={project}
        knownUserIds={[...knownUserIds, task.assigneeUserId]}
        value={owner}
        canSearch={canSearch}
        onChange={(value) => {
          setOwner(value);
          fields.clearServer();
        }}
        error={fields.errorOf('assigneeUserId')}
      />
      <div className="form__actions">
        <button type="submit" className="button button--primary" disabled={save.saving}>
          {save.saving ? t('common.states.saving') : t('tasks.assign.confirm')}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
