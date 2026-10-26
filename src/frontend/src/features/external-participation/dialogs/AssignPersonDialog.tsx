import { type ReactElement, type SyntheticEvent, useRef, useState } from 'react';

import { useSaveAction } from '@/features/identity-access/forms.ts';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { assigneeOf, assigneeValue, useUserSearch } from '@/features/tasks/assignee.ts';
import { useFieldErrors } from '@/shared/forms/useFieldErrors.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { externalRequestsApi } from '../api/externalParticipationApi.ts';
import { type ExternalUpdateRequestDetail } from '../api/types.ts';
import { PersonField } from '../components/PersonField.tsx';
import { isStale, participationProblemMessage, requestFieldMessage } from '../problems.ts';

interface AssignPersonDialogProps {
  role: 'responder' | 'reviewer';
  request: ExternalUpdateRequestDetail;
  etag: string | null;
  user: SessionUser;
  onClose: () => void;
  onDone: () => void;
  onStale: () => void;
}

/**
 * Replacing the responder or the reviewer of an issued request (WF-13 §9.4, MOD-112). Effective at once; earlier
 * revisions keep their contributor (BR-EXT-030). Someone must be named: there is no unassignment.
 */
export function AssignPersonDialog(props: AssignPersonDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog
      open
      title={t(
        props.role === 'responder'
          ? 'externalParticipation.assign.responderTitle'
          : 'externalParticipation.assign.reviewerTitle',
      )}
      onClose={props.onClose}
    >
      <AssignForm {...props} />
    </Dialog>
  );
}

function AssignForm({
  role,
  request,
  etag,
  user,
  onClose,
  onDone,
  onStale,
}: AssignPersonDialogProps): ReactElement {
  const { t } = useI18n();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction(participationProblemMessage);
  const canSearch = useUserSearch() ?? false;
  const field = role === 'responder' ? 'responsibleUserId' : 'reviewerUserId';
  const current =
    role === 'responder' ? request.responsibleUserId : (request.reviewerUserId ?? null);
  const [person, setPerson] = useState(() => assigneeValue(current));
  const choice = assigneeOf(person, canSearch);
  const fields = useFieldErrors(
    { [field]: choice.userId === null ? (choice.code ?? 'REQUIRED') : null },
    formRef,
    requestFieldMessage,
  );

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (!fields.attempt() || choice.userId === null) {
      return;
    }
    const userId = choice.userId;
    const result = await save.run(() =>
      role === 'responder'
        ? externalRequestsApi.assignResponder(request.id, userId, etag)
        : externalRequestsApi.assignReviewer(request.id, userId, etag),
    );
    if (result.ok) {
      onDone();
    } else if (isStale(result.error)) {
      onStale();
    } else {
      fields.showServer(result.error);
    }
  };

  return (
    <form ref={formRef} className="form" noValidate onSubmit={(event) => void submit(event)}>
      <FormAlert message={fields.summary ?? save.formError} />
      <PersonField
        label={t(
          role === 'responder'
            ? 'externalParticipation.fields.responder'
            : 'externalParticipation.fields.reviewer',
        )}
        name={field}
        userType={role === 'responder' ? 'EXTERNAL' : 'INTERNAL'}
        user={user}
        value={person}
        canSearch={canSearch}
        allowNone={false}
        hint={t(
          role === 'responder'
            ? 'externalParticipation.form.responderHint'
            : 'externalParticipation.form.reviewerHint',
        )}
        error={fields.errorOf(field)}
        onChange={(value) => {
          setPerson(value);
          fields.clearServer();
        }}
      />
      <div className="form__actions">
        <button type="submit" className="button button--primary" disabled={save.saving}>
          {save.saving ? t('common.states.saving') : t('externalParticipation.assign.confirm')}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
