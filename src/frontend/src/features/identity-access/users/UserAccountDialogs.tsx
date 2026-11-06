import { type ReactElement, type SyntheticEvent, useRef, useState } from 'react';

import { type ApiResponse } from '@/shared/api/httpClient.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { RadioGroupField, TextField } from '@/shared/ui/FormFields.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { usersApi } from '../api/identityAccessApi.ts';
import { type UserDetail } from '../api/types.ts';
import { checkPassword, useFocusFirstError, useSaveAction } from '../forms.ts';

export type PasswordMode = 'GENERATED' | 'TYPED';

/** How the temporary password is chosen, on creating a local account and on resetting one. */
export function PasswordModeField({
  mode,
  onChange,
}: {
  mode: PasswordMode;
  onChange: (mode: PasswordMode) => void;
}): ReactElement {
  const { t } = useI18n();
  return (
    <RadioGroupField
      label={t('identityAccess.users.form.passwordMode')}
      name="passwordMode"
      value={mode}
      options={[
        { value: 'GENERATED', label: t('identityAccess.users.form.passwordGenerated') },
        { value: 'TYPED', label: t('identityAccess.users.form.passwordTyped') },
      ]}
      onChange={(value) => {
        onChange(value === 'TYPED' ? 'TYPED' : 'GENERATED');
      }}
    />
  );
}

interface AccountDialogProps {
  open: boolean;
  user: UserDetail;
  etag: string | null;
  onClose: () => void;
  onDone: (response: ApiResponse<UserDetail>) => void;
}

/** Resets a local account's password; the new temporary one comes back in this response only. */
export function ResetPasswordDialog({ open, user, ...rest }: AccountDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog
      open={open}
      title={t('identityAccess.users.account.resetTitle', { name: user.displayName })}
      onClose={rest.onClose}
    >
      <ResetPasswordForm user={user} {...rest} />
    </Dialog>
  );
}

function ResetPasswordForm({
  user,
  etag,
  onClose,
  onDone,
}: Omit<AccountDialogProps, 'open'>): ReactElement {
  const { t } = useI18n();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction();
  useFocusFirstError(formRef, save.fieldErrors);
  const [mode, setMode] = useState<PasswordMode>('GENERATED');
  const [password, setPassword] = useState('');

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    const typed = mode === 'TYPED';
    if (!save.validate({ password: typed ? checkPassword(password, user.username) : null })) {
      return;
    }
    const result = await save.run(() =>
      usersApi.resetPassword(user.id, typed ? password : null, etag),
    );
    if (result.ok) {
      onDone(result.value);
    }
  };

  return (
    <form ref={formRef} className="form" noValidate onSubmit={(event) => void submit(event)}>
      <p>{t('identityAccess.users.account.resetBody')}</p>
      <p className="form__note">{t('identityAccess.stepUpNote')}</p>
      <FormAlert message={save.formError} />
      <PasswordModeField mode={mode} onChange={setMode} />
      {mode === 'TYPED' && (
        <TextField
          label={t('identityAccess.users.form.temporaryPassword')}
          name="password"
          required
          dir="ltr"
          autoComplete="off"
          hint={t('identityAccess.temporaryPasswordRules')}
          value={password}
          onChange={setPassword}
          error={save.fieldErrors.password}
        />
      )}
      <div className="form__actions">
        <button type="submit" className="button button--danger" disabled={save.saving}>
          {save.saving
            ? t('common.states.saving')
            : t('identityAccess.users.account.resetPassword')}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}

/** The account's last usable day, for either account type; empty makes it open-ended. */
export function SetEndDateDialog({ open, user, ...rest }: AccountDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog
      open={open}
      title={t('identityAccess.users.account.setEndDateTitle', { name: user.displayName })}
      onClose={rest.onClose}
    >
      <SetEndDateForm user={user} {...rest} />
    </Dialog>
  );
}

function SetEndDateForm({
  user,
  etag,
  onClose,
  onDone,
}: Omit<AccountDialogProps, 'open'>): ReactElement {
  const { t } = useI18n();
  const save = useSaveAction();
  const [endsOn, setEndsOn] = useState(user.accountEndsOn ?? '');

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    const result = await save.run(() =>
      usersApi.setEndDate(user.id, endsOn === '' ? null : endsOn, etag),
    );
    if (result.ok) {
      onDone(result.value);
    }
  };

  return (
    <form className="form" noValidate onSubmit={(event) => void submit(event)}>
      <FormAlert message={save.formError} />
      <TextField
        label={t('identityAccess.users.account.endsOn')}
        name="endsOn"
        type="date"
        hint={t('identityAccess.users.account.endsOnHint')}
        value={endsOn}
        onChange={setEndsOn}
        error={save.fieldErrors.endsOn}
      />
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
