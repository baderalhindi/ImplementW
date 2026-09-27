import { type ReactElement } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { useSaveAction } from '../forms.ts';

export interface Confirmation {
  title: string;
  body: string;
  confirmLabel: string;
  destructive: boolean;
  action: () => Promise<unknown>;
}

interface ConfirmDialogProps {
  confirmation: Confirmation | null;
  onClose: () => void;
  onDone: () => void;
}

/** A lifecycle command (activate, deactivate, suspend, retire) confirmed before it is sent. */
export function ConfirmDialog({ confirmation, onClose, onDone }: ConfirmDialogProps): ReactElement {
  return (
    <Dialog open={confirmation !== null} title={confirmation?.title ?? ''} onClose={onClose}>
      {confirmation !== null && (
        <ConfirmBody confirmation={confirmation} onClose={onClose} onDone={onDone} />
      )}
    </Dialog>
  );
}

function ConfirmBody({
  confirmation,
  onClose,
  onDone,
}: {
  confirmation: Confirmation;
  onClose: () => void;
  onDone: () => void;
}): ReactElement {
  const { t } = useI18n();
  const save = useSaveAction();

  const confirm = async () => {
    const result = await save.run(confirmation.action);
    if (result.ok) {
      onDone();
    }
  };

  return (
    <>
      <p>{confirmation.body}</p>
      <FormAlert message={save.formError} />
      <div className="form__actions">
        <button
          type="button"
          className={confirmation.destructive ? 'button button--danger' : 'button button--primary'}
          disabled={save.saving}
          onClick={() => void confirm()}
        >
          {save.saving ? t('common.states.saving') : confirmation.confirmLabel}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </>
  );
}
