import { type ReactElement, type SyntheticEvent, useRef, useState } from 'react';

import {
  checkText,
  optionalText,
  useFocusFirstError,
  useSaveAction,
} from '@/features/identity-access/forms.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { TextAreaField } from '@/shared/ui/FormFields.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { approvalTasksApi } from '../api/approvalsApi.ts';
import { type ApprovalInstanceDetail } from '../api/types.ts';
import { approvalProblemMessage, isStaleApproval } from '../problems.ts';

import {
  REASON_LENGTH,
  reasonRequired,
  type TaskAction,
  type TaskActionTarget,
} from './taskActions.ts';

interface DecisionDialogProps {
  target: TaskActionTarget | null;
  onClose: () => void;
  onDone: (action: TaskAction, run: ApprovalInstanceDetail) => void;
  /** The task moved on since it was listed (decided elsewhere, retried, no longer visible): the screen reloads. */
  onStale: (message: string) => void;
}

export function DecisionDialog({
  target,
  onClose,
  onDone,
  onStale,
}: DecisionDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog
      open={target !== null}
      title={target === null ? '' : t(`approvals.decision.${target.action}.title`)}
      onClose={onClose}
    >
      {target !== null && (
        <DecisionForm target={target} onClose={onClose} onDone={onDone} onStale={onStale} />
      )}
    </Dialog>
  );
}

function DecisionForm({
  target,
  onClose,
  onDone,
  onStale,
}: DecisionDialogProps & { target: TaskActionTarget }): ReactElement {
  const { t, language } = useI18n();
  const save = useSaveAction(approvalProblemMessage);
  const formRef = useRef<HTMLFormElement>(null);
  const [reason, setReason] = useState('');
  const required = reasonRequired(target.action);
  useFocusFirstError(formRef, save.fieldErrors);

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (
      !save.validate({ 'reason.text': checkText(reason, { required, maxLength: REASON_LENGTH }) })
    ) {
      return;
    }
    const text = optionalText(reason);
    // Tagged with the interface language: the language the person is writing in (ADR-012 extension, ERD D-7).
    const narrative = text === null ? null : { text, language };
    const result = await save.run(() =>
      target.action === 'escalate'
        ? approvalTasksApi.escalate(target.taskId, narrative)
        : approvalTasksApi.decide(target.taskId, target.action, narrative),
    );
    if (result.ok) {
      onDone(target.action, result.value);
    } else if (isStaleApproval(result.error)) {
      onStale(approvalProblemMessage(result.error, t));
    }
  };

  const reasonError = save.fieldErrors['reason.text'] ?? save.fieldErrors.reason;
  return (
    <form ref={formRef} className="form" noValidate onSubmit={(event) => void submit(event)}>
      <p>{t(`approvals.decision.${target.action}.body`)}</p>
      <FormAlert message={save.formError} />
      <TextAreaField
        label={t('approvals.decision.reason')}
        name="reason"
        value={reason}
        onChange={setReason}
        required={required}
        maxLength={REASON_LENGTH}
        hint={
          required
            ? t('approvals.decision.reasonRequiredHint')
            : t('approvals.decision.reasonOptionalHint')
        }
        error={reasonError}
      />
      <div className="form__actions">
        <button
          type="submit"
          className={
            target.action === 'reject' ? 'button button--danger' : 'button button--primary'
          }
          disabled={save.saving}
        >
          {save.saving
            ? t('common.states.saving')
            : t(`approvals.decision.${target.action}.confirm`)}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
