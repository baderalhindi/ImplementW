import { type ReactElement, type SyntheticEvent, useEffect, useRef, useState } from 'react';
import { Link, Navigate, useLocation, useNavigate } from 'react-router';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { TextField } from '@/shared/ui/FormFields.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { checkText, useFocusFirstError, useSaveAction } from '../forms.ts';
import { DirectoryPasswordHelp, NewPasswordFields, PasswordCard } from './passwordSelfService.tsx';
import { checkNewPassword, PASSWORD_UPDATED } from './passwordUpdate.ts';
import { sessionApi } from './sessionApi.ts';
import { sessionStore } from './sessionStore.ts';
import { describeRefusal } from './signInSteps.ts';
import { useSession } from './useSession.ts';

function BackToSignIn(): ReactElement {
  const { t } = useI18n();
  return <Link to="/sign-in">{t('identityAccess.signIn.backToSignIn')}</Link>;
}

/** Asks for a reset link. The answer is the same whether or not the username names a local account. */
export function ForgotPasswordPage(): ReactElement {
  const { t } = useI18n();
  const { session } = useSession();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction();
  useFocusFirstError(formRef, save.fieldErrors);
  const [username, setUsername] = useState('');
  const [sent, setSent] = useState(false);
  if (session !== null) {
    return <Navigate to="/" replace />;
  }

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (!save.validate({ username: checkText(username, { required: true }) })) {
      return;
    }
    const result = await save.run(() => sessionApi.requestPasswordReset(username.trim()));
    if (result.ok) {
      setSent(true);
    }
  };

  const directoryHelp = (
    <p>
      {t('identityAccess.passwordSelfService.directoryAccount')}{' '}
      <DirectoryPasswordHelp
        linkLabel={t('identityAccess.passwordSelfService.managedLink')}
        contact={t('identityAccess.passwordSelfService.managedContact')}
      />
    </p>
  );

  return (
    <PasswordCard title={t('identityAccess.passwordSelfService.forgotTitle')}>
      {sent ? (
        <p role="status">{t('identityAccess.passwordSelfService.forgotSent')}</p>
      ) : (
        <form
          ref={formRef}
          className="form sign-in__form"
          noValidate
          onSubmit={(event) => void submit(event)}
        >
          <p>{t('identityAccess.passwordSelfService.forgotDescription')}</p>
          <FormAlert message={save.formError} />
          <TextField
            label={t('identityAccess.signIn.username')}
            name="username"
            required
            dir="ltr"
            autoComplete="username"
            value={username}
            onChange={setUsername}
            error={save.fieldErrors.username}
          />
          <button
            type="submit"
            className="button button--primary sign-in__wide"
            disabled={save.saving}
          >
            {save.saving
              ? t('common.states.saving')
              : t('identityAccess.passwordSelfService.forgotSubmit')}
          </button>
        </form>
      )}
      {directoryHelp}
      <BackToSignIn />
    </PasswordCard>
  );
}

/** The page a reset e-mail links to. The token rides in the URL's fragment, which no server, log or Referer header sees. */
export function ResetPasswordPage(): ReactElement {
  const { t } = useI18n();
  const location = useLocation();
  const navigate = useNavigate();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction(describeRefusal('identityAccess.passwordSelfService.linkInvalid'));
  useFocusFirstError(formRef, save.fieldErrors);
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const token = new URLSearchParams(location.hash.slice(1)).get('token');

  const requestNewLink = (
    <Link to="/forgot-password">{t('identityAccess.passwordSelfService.requestNewLink')}</Link>
  );

  if (token === null) {
    return (
      <PasswordCard title={t('identityAccess.passwordSelfService.resetTitle')}>
        <FormAlert message={t('identityAccess.passwordSelfService.linkInvalid')} />
        {requestNewLink}
      </PasswordCard>
    );
  }

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (!save.validate(checkNewPassword(newPassword, confirmPassword, ''))) {
      return;
    }
    const result = await save.run(() => sessionApi.completePasswordReset(token, newPassword));
    if (result.ok) {
      await navigate('/password-updated', { replace: true, state: PASSWORD_UPDATED });
    }
  };

  return (
    <PasswordCard title={t('identityAccess.passwordSelfService.resetTitle')}>
      <form
        ref={formRef}
        className="form sign-in__form"
        noValidate
        onSubmit={(event) => void submit(event)}
      >
        <FormAlert message={save.formError} />
        <NewPasswordFields
          newPassword={newPassword}
          confirmPassword={confirmPassword}
          onNewPassword={setNewPassword}
          onConfirmPassword={setConfirmPassword}
          errors={save.fieldErrors}
        />
        <button
          type="submit"
          className="button button--primary sign-in__wide"
          disabled={save.saving}
        >
          {save.saving
            ? t('common.states.saving')
            : t('identityAccess.passwordSelfService.resetSubmit')}
        </button>
      </form>
      {requestNewLink}
    </PasswordCard>
  );
}

/**
 * Where changing or resetting a password ends. The API has ended the account's sessions, so the page ends this browser's: after it has mounted, because signing
 * out on the page that asked would send that page's guard to sign-in first. It acts only when a password update led here, so a link cannot sign anyone out. It holds no
 * state: signing out swaps the app's shell, which remounts it.
 */
export function PasswordUpdatedPage(): ReactElement {
  const { t } = useI18n();
  const location = useLocation();
  const updated = location.state === PASSWORD_UPDATED;
  useEffect(() => {
    if (updated) {
      sessionStore.signOut();
    }
  }, [updated]);
  if (!updated) {
    return <Navigate to="/" replace />;
  }
  return (
    <PasswordCard title={t('identityAccess.passwordSelfService.updatedTitle')}>
      <p role="status">{t('identityAccess.passwordSelfService.updatedBody')}</p>
      <Link className="button button--primary sign-in__wide" to="/sign-in">
        {t('identityAccess.passwordSelfService.updatedSignIn')}
      </Link>
    </PasswordCard>
  );
}
