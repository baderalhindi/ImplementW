import { type ReactElement, type SyntheticEvent } from 'react';

import { useSaveAction } from '@/features/identity-access/forms.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { isStale, scheduleProblemMessage } from '../problems.ts';

interface ConfirmDialogProps {
  open: boolean;
  title: string;
  /** What happens, said before it is sent. */
  body: string;
  confirmLabel: string;
  danger?: boolean;
  action: () => Promise<unknown>;
  onClose: () => void;
  onDone: () => void;
  /** The record moved on since it was read: the screen reads it again. */
  onStale: () => void;
}

/** One command with no input, confirmed: cancelling an activity, removing a dependency, deleting a draft baseline. */
export function ConfirmDialog(props: ConfirmDialogProps): ReactElement {
  return (
    <Dialog open={props.open} title={props.title} onClose={props.onClose}>
      {props.open && <ConfirmBody {...props} />}
    </Dialog>
  );
}

function ConfirmBody({
  body,
  confirmLabel,
  danger = false,
  action,
  onClose,
  onDone,
  onStale,
}: ConfirmDialogProps): ReactElement {
  const { t } = useI18n();
  const save = useSaveAction(scheduleProblemMessage);

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    const result = await save.run(action);
    if (result.ok) {
      onDone();
    } else if (isStale(result.error)) {
      onStale();
    }
  };

  return (
    <form className="form" noValidate onSubmit={(event) => void submit(event)}>
      <p>{body}</p>
      <FormAlert message={save.formError} />
      <div className="form__actions">
        <button
          type="submit"
          className={danger ? 'button button--danger' : 'button button--primary'}
          disabled={save.saving}
        >
          {save.saving ? t('common.states.saving') : confirmLabel}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
