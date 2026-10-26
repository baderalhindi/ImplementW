import { type ReactElement, type SyntheticEvent, useRef, useState } from 'react';

import { useSaveAction } from '@/features/identity-access/forms.ts';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { type ProjectSummary } from '@/features/projects/api/types.ts';
import { assigneeOf, assigneeValue, useUserSearch } from '@/features/tasks/assignee.ts';
import { AssigneeField } from '@/features/tasks/components/AssigneeField.tsx';
import { type ApiResponse } from '@/shared/api/httpClient.ts';
import { useFieldErrors } from '@/shared/forms/useFieldErrors.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { concernsApi } from '../api/concernsApi.ts';
import { type ConcernDetail } from '../api/types.ts';
import { concernFieldMessage, concernProblemMessage, isStale } from '../problems.ts';

interface AssignConcernDialogProps {
  concern: ApiResponse<ConcernDetail>;
  project: Pick<ProjectSummary, 'projectManagerUserId'>;
  user: SessionUser;
  onClose: () => void;
  onDone: (concern: ConcernDetail) => void;
  onStale: () => void;
}

/**
 * Assigning the concern: OPEN → ASSIGNED, or a reassignment while ASSIGNED or IN_PROGRESS. The assignee must hold a
 * role over the project now (422 CONCERN_ASSIGNEE_NOT_ELIGIBLE, shown on the field). Someone must be named: the API
 * has no unassignment.
 */
export function AssignConcernDialog(props: AssignConcernDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog
      open
      title={t('issuesChallenges.assign.title', { concern: props.concern.data.title.text })}
      onClose={props.onClose}
    >
      <AssignForm {...props} />
    </Dialog>
  );
}

function AssignForm({
  concern: { data: concern, etag },
  project,
  user,
  onClose,
  onDone,
  onStale,
}: AssignConcernDialogProps): ReactElement {
  const { t } = useI18n();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction(concernProblemMessage);
  const canSearch = useUserSearch() ?? false;
  const [assignee, setAssignee] = useState(() => assigneeValue(concern.assigneeUserId));
  const choice = assigneeOf(assignee, canSearch);
  const fields = useFieldErrors(
    { assigneeUserId: choice.userId === null ? (choice.code ?? 'REQUIRED') : null },
    formRef,
    concernFieldMessage,
  );

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (!fields.attempt() || choice.userId === null) {
      return;
    }
    const assigneeUserId = choice.userId;
    const result = await save.run(
      async () => (await concernsApi.assign(concern.id, { assigneeUserId }, etag)).data,
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
      <FormAlert message={fields.summary ?? save.formError} />
      <AssigneeField
        user={user}
        project={project}
        knownUserIds={[concern.assigneeUserId, concern.raisedByUserId]}
        value={assignee}
        canSearch={canSearch}
        name="assigneeUserId"
        onChange={(value) => {
          setAssignee(value);
          fields.clearServer();
        }}
        error={fields.errorOf('assigneeUserId')}
      />
      <div className="form__actions">
        <button type="submit" className="button button--primary" disabled={save.saving}>
          {save.saving ? t('common.states.saving') : t('issuesChallenges.assign.confirm')}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
