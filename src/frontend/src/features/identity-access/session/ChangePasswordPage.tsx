import { type ReactElement, type SyntheticEvent, useRef, useState } from 'react';
import { useNavigate } from 'react-router';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { TextField } from '@/shared/ui/FormFields.tsx';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { useFocusFirstError, useSaveAction } from '../forms.ts';
import { DirectoryPasswordHelp, NewPasswordFields } from './passwordSelfService.tsx';
import { checkNewPassword, PASSWORD_UPDATED } from './passwordUpdate.ts';
import { sessionApi } from './sessionApi.ts';
import { useSession } from './useSession.ts';

function LocalPasswordForm({ username }: { username: string }): ReactElement {
  const { t } = useI18n();
  const navigate = useNavigate();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction();
  useFocusFirstError(formRef, save.fieldErrors);
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    const valid = save.validate({
      currentPassword: currentPassword === '' ? 'REQUIRED' : null,
      ...checkNewPassword(newPassword, confirmPassword, username),
    });
    if (!valid) {
      return;
    }
    const result = await save.run(() => sessionApi.changeOwnPassword(currentPassword, newPassword));
    if (result.ok) {
      await navigate('/password-updated', { replace: true, state: PASSWORD_UPDATED });
    }
  };

  return (
    <form ref={formRef} className="form" noValidate onSubmit={(event) => void submit(event)}>
      <FormAlert message={save.formError} />
      <TextField
        label={t('identityAccess.passwordSelfService.currentPassword')}
        name="currentPassword"
        type="password"
        required
        dir="ltr"
        autoComplete="current-password"
        value={currentPassword}
        onChange={setCurrentPassword}
        error={save.fieldErrors.currentPassword}
      />
      <NewPasswordFields
        newPassword={newPassword}
        confirmPassword={confirmPassword}
        onNewPassword={setNewPassword}
        onConfirmPassword={setConfirmPassword}
        errors={save.fieldErrors}
      />
      <button type="submit" className="button button--primary" disabled={save.saving}>
        {save.saving
          ? t('common.states.saving')
          : t('identityAccess.passwordSelfService.changeSubmit')}
      </button>
    </form>
  );
}

/**
 * Every person reaches this page from the account menu. A local account changes its password here; a directory or Microsoft account's
 * password is not the platform's to change (ADR-007), so the page says where it is changed.
 */
export function ChangePasswordPage(): ReactElement | null {
  const { t } = useI18n();
  const { session } = useSession();
  if (session === null) {
    return null;
  }
  const local = session.user.authenticationMethod === 'LOCAL_PASSWORD';
  return (
    <>
      <PageHeader
        title={t('identityAccess.passwordSelfService.changeTitle')}
        description={
          local
            ? t('identityAccess.passwordSelfService.changeDescription')
            : t('identityAccess.passwordSelfService.managedBody')
        }
      />
      {local ? (
        <LocalPasswordForm username={session.user.username} />
      ) : (
        <p>
          <DirectoryPasswordHelp
            linkLabel={t('identityAccess.passwordSelfService.managedLink')}
            contact={t('identityAccess.passwordSelfService.managedContact')}
          />
        </p>
      )}
    </>
  );
}
