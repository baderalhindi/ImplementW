import { type ReactElement, type SyntheticEvent } from 'react';

import { useSaveAction } from '@/features/identity-access/forms.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { riskProblemMessage, isStale } from '../problems.ts';

interface ConfirmCommandDialogProps {
  title: string;
  /** What the command does, in a sentence. */
  consequence: string;
  confirmLabel: string;
  /** Sends the command with the ETag it was read with. */
  run: () => Promise<unknown>;
  onClose: () => void;
  onDone: () => void;
  onStale: () => void;
}

/**
 * A risk or treatment-action command with no input (start treatment, monitor, revoke an acceptance, reopen; start,
 * complete or cancel an action): said in words and confirmed, never sent by one stray click. A refusal stays in the
 * dialog; a record that moved on is read again.
 */
export function ConfirmCommandDialog({
  title,
  consequence,
  confirmLabel,
  run,
  onClose,
  onDone,
  onStale,
}: ConfirmCommandDialogProps): ReactElement {
  const { t } = useI18n();
  const save = useSaveAction(riskProblemMessage);

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    const result = await save.run(run);
    if (result.ok) {
      onDone();
    } else if (isStale(result.error)) {
      onStale();
    }
  };

  return (
    <Dialog open title={title} onClose={onClose}>
      <form className="form" noValidate onSubmit={(event) => void submit(event)}>
        <FormAlert message={save.formError} />
        <p className="form__note">{consequence}</p>
        <div className="form__actions">
          <button type="submit" className="button button--primary" disabled={save.saving}>
            {save.saving ? t('common.states.saving') : confirmLabel}
          </button>
          <button type="button" className="button" disabled={save.saving} onClick={onClose}>
            {t('common.actions.cancel')}
          </button>
        </div>
      </form>
    </Dialog>
  );
}
