import { type SyntheticEvent, type ReactElement, useRef, useState } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { TextField } from '@/shared/ui/FormFields.tsx';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { checkText, useFocusFirstError, useSaveAction } from '../forms.ts';
import {
  isMultiFactorPending,
  type MultiFactorChallenge,
  type MultiFactorPending,
  type Session,
  sessionApi,
} from './sessionApi.ts';
import { sessionStore } from './sessionStore.ts';
import { useSession } from './useSession.ts';

interface PendingSecondFactor {
  pending: MultiFactorPending;
  challenge: MultiFactorChallenge;
}

function redirectTarget(state: unknown): string {
  const from = (state as { from?: unknown } | null)?.from;
  return typeof from === 'string' && from.startsWith('/') && !from.startsWith('//') ? from : '/';
}

/**
 * Directory sign-in with the second factor (TASK-028, TASK-029): the minimum needed to reach the administration
 * screens. R01 always requires MFA, so an administrator always passes the code step.
 */
export function SignInPage(): ReactElement {
  const { t } = useI18n();
  const { session } = useSession();
  const location = useLocation();
  const navigate = useNavigate();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction();
  useFocusFirstError(formRef, save.fieldErrors);

  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [code, setCode] = useState('');
  const [secondFactor, setSecondFactor] = useState<PendingSecondFactor | null>(null);

  const target = redirectTarget(location.state);
  if (session !== null) {
    return <Navigate to={target} replace />;
  }

  const finish = (session: Session) => {
    sessionStore.setSession(session);
    void navigate(target, { replace: true });
  };

  const submitCredentials = async (event: SyntheticEvent) => {
    event.preventDefault();
    const valid = save.validate({
      username: checkText(username, { required: true }),
      password: password === '' ? 'REQUIRED' : null,
    });
    if (!valid) {
      return;
    }
    const result = await save.run(async () => {
      const signedIn = await sessionApi.signIn(username.trim(), password);
      if (!isMultiFactorPending(signedIn)) {
        return { session: signedIn };
      }
      const challenge = await sessionApi.beginMultiFactor(signedIn.mfaToken);
      return { secondFactor: { pending: signedIn, challenge } };
    });
    if (!result.ok) {
      return;
    }
    setPassword('');
    if ('session' in result.value) {
      finish(result.value.session);
    } else {
      setSecondFactor(result.value.secondFactor);
    }
  };

  const submitCode = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (secondFactor === null || !save.validate({ code: code.trim() === '' ? 'REQUIRED' : null })) {
      return;
    }
    const result = await save.run(() =>
      sessionApi.completeMultiFactor(
        secondFactor.pending.mfaToken,
        secondFactor.challenge.challengeId,
        code.trim(),
      ),
    );
    if (result.ok && !isMultiFactorPending(result.value)) {
      finish(result.value);
    }
  };

  return (
    <div className="sign-in">
      <PageHeader title={t('identityAccess.signIn.title')} />
      {secondFactor === null ? (
        <form
          ref={formRef}
          className="form"
          noValidate
          onSubmit={(event) => void submitCredentials(event)}
        >
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
          <TextField
            label={t('identityAccess.signIn.password')}
            name="password"
            type="password"
            required
            dir="ltr"
            autoComplete="current-password"
            value={password}
            onChange={setPassword}
            error={save.fieldErrors.password}
          />
          <div className="form__actions">
            <button type="submit" className="button button--primary" disabled={save.saving}>
              {save.saving
                ? t('identityAccess.signIn.signingIn')
                : t('identityAccess.signIn.submit')}
            </button>
          </div>
        </form>
      ) : (
        <form
          ref={formRef}
          className="form"
          noValidate
          onSubmit={(event) => void submitCode(event)}
        >
          <FormAlert message={save.formError} />
          <p>
            {secondFactor.pending.enrolmentRequired
              ? t('identityAccess.signIn.enrolInstructions')
              : t('identityAccess.signIn.codeInstructions')}
          </p>
          {secondFactor.challenge.provisioningUri !== null && (
            <p className="form__readonly">
              <span className="field__label">{t('identityAccess.signIn.provisioningUri')}</span>
              <code dir="ltr" className="break-all">
                {secondFactor.challenge.provisioningUri}
              </code>
            </p>
          )}
          <TextField
            label={t('identityAccess.signIn.code')}
            name="code"
            required
            dir="ltr"
            inputMode="numeric"
            autoComplete="one-time-code"
            value={code}
            onChange={setCode}
            error={save.fieldErrors.code}
          />
          <div className="form__actions">
            <button type="submit" className="button button--primary" disabled={save.saving}>
              {save.saving
                ? t('identityAccess.signIn.verifying')
                : t('identityAccess.signIn.verify')}
            </button>
          </div>
        </form>
      )}
    </div>
  );
}
