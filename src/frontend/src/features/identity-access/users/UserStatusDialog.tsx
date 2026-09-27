import { type ReactElement } from 'react';

import { type ApiResponse } from '@/shared/api/httpClient.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { usersApi } from '../api/identityAccessApi.ts';
import { type UserDetail } from '../api/types.ts';
import { useSaveAction } from '../forms.ts';

interface UserStatusDialogProps {
  open: boolean;
  user: UserDetail;
  etag: string | null;
  onClose: () => void;
  onChanged: (response: ApiResponse<UserDetail>) => void;
}

/**
 * MOD-080 Activate/Disable User. Disabling changes the status and nothing else: the user's records and assignments
 * stay as they are (Appendix A.1, record D-3), and sign-in and every request are refused from then on.
 */
export function UserStatusDialog({
  open,
  user,
  etag,
  onClose,
  onChanged,
}: UserStatusDialogProps): ReactElement {
  const { t } = useI18n();
  const disabling = user.status === 'ACTIVE';
  return (
    <Dialog
      open={open}
      title={
        disabling
          ? t('identityAccess.userStatusDialog.disableTitle')
          : t('identityAccess.userStatusDialog.activateTitle')
      }
      onClose={onClose}
    >
      <UserStatusConfirmation user={user} etag={etag} onClose={onClose} onChanged={onChanged} />
    </Dialog>
  );
}

function UserStatusConfirmation({
  user,
  etag,
  onClose,
  onChanged,
}: Omit<UserStatusDialogProps, 'open'>): ReactElement {
  const { t } = useI18n();
  const save = useSaveAction();
  const disabling = user.status === 'ACTIVE';

  const confirm = async () => {
    const result = await save.run(() =>
      disabling ? usersApi.disable(user.id, etag) : usersApi.activate(user.id, etag),
    );
    if (result.ok) {
      onChanged(result.value);
    }
  };

  return (
    <>
      <p>
        {disabling
          ? t('identityAccess.userStatusDialog.disableBody', { name: user.displayName })
          : t('identityAccess.userStatusDialog.activateBody', { name: user.displayName })}
      </p>
      <p className="form__note">{t('identityAccess.stepUpNote')}</p>
      <FormAlert message={save.formError} />
      <div className="form__actions">
        <button
          type="button"
          className={disabling ? 'button button--danger' : 'button button--primary'}
          disabled={save.saving}
          onClick={() => void confirm()}
        >
          {save.saving
            ? t('common.states.saving')
            : disabling
              ? t('identityAccess.userStatusDialog.disable')
              : t('identityAccess.userStatusDialog.activate')}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </>
  );
}
