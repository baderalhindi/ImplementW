import { type ReactElement, type SyntheticEvent, useEffect, useRef, useState } from 'react';

import { ApiError } from '@/shared/api/httpClient.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { TextField } from '@/shared/ui/FormFields.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { useFocusFirstError, useSaveAction } from '../forms.ts';
import { type ProblemDescriber, problemMessage } from '../problems.ts';
import { sessionApi, type SmsCodeChallenge } from './sessionApi.ts';
import { MOBILE_PATTERN, toE164 } from './mobileNumber.ts';
import { nextStep } from './signInSteps.ts';
import {
  credentialJson,
  deviceUnlock,
  isPasskeyCancelled,
  platformPasskeysAvailable,
  requestOptions,
} from './webAuthn.ts';

export type SignInStep = Awaited<ReturnType<typeof nextStep>>;

/** The wait before another code can be sent: the API's resend cooldown. */
const RESEND_SECONDS = 60;

const describe =
  (
    refusedKey:
      'identityAccess.signIn.passwordless.passkeyRefused' | 'identityAccess.signIn.wrongCode',
  ): ProblemDescriber =>
  (error, t) =>
    isPasskeyCancelled(error)
      ? t('identityAccess.signIn.passwordless.passkeyCancelled')
      : error instanceof ApiError && error.code === 'AUTHENTICATION_REQUIRED'
        ? t(refusedKey)
        : problemMessage(error, t);

function PasskeySignIn({ onSignedIn }: { onSignedIn: (step: SignInStep) => void }): ReactElement {
  const { t } = useI18n();
  const save = useSaveAction(describe('identityAccess.signIn.passwordless.passkeyRefused'));
  const [deviceReady, setDeviceReady] = useState<boolean | null>(null);
  const unlock = deviceUnlock();

  useEffect(() => {
    let current = true;
    void platformPasskeysAvailable().then((available) => {
      if (current) {
        setDeviceReady(available);
      }
    });
    return () => {
      current = false;
    };
  }, []);

  const signIn = async () => {
    const result = await save.run(async () => {
      const { options, transaction } = await sessionApi.getPasskeySignInOptions();
      const credential = (await navigator.credentials.get({
        publicKey: requestOptions(options),
      })) as PublicKeyCredential | null;
      if (credential === null) {
        throw new DOMException('No passkey was chosen.', 'NotAllowedError');
      }
      return nextStep(await sessionApi.signInWithPasskey(transaction, credentialJson(credential)));
    });
    if (result.ok) {
      onSignedIn(result.value);
    }
  };

  return (
    <div className="sign-in__passwordless-option">
      <FormAlert message={save.formError} />
      <button
        type="button"
        className="button button--primary sign-in__wide"
        disabled={deviceReady !== true || save.saving}
        onClick={() => void signIn()}
      >
        {save.saving
          ? t('identityAccess.signIn.signingIn')
          : t(`identityAccess.signIn.passwordless.passkey.${unlock}`)}
      </button>
      <p className="sign-in__note">
        {deviceReady === false
          ? t('identityAccess.signIn.passwordless.noDeviceUnlock')
          : t('identityAccess.signIn.passwordless.passkeyHint')}
      </p>
    </div>
  );
}

function SmsSignIn({ onSignedIn }: { onSignedIn: (step: SignInStep) => void }): ReactElement {
  const { t } = useI18n();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction(describe('identityAccess.signIn.wrongCode'));
  useFocusFirstError(formRef, save.fieldErrors);
  const [mobileNumber, setMobileNumber] = useState('');
  const [code, setCode] = useState('');
  const [challenge, setChallenge] = useState<SmsCodeChallenge | null>(null);
  const [resendIn, setResendIn] = useState(0);

  useEffect(() => {
    if (resendIn <= 0) {
      return undefined;
    }
    const timer = window.setTimeout(() => {
      setResendIn((seconds) => seconds - 1);
    }, 1000);
    return () => {
      window.clearTimeout(timer);
    };
  }, [resendIn]);

  const sendCode = async () => {
    const number = toE164(mobileNumber);
    if (
      !save.validate({
        mobileNumber: MOBILE_PATTERN.test(number) ? null : number === '' ? 'REQUIRED' : 'MALFORMED',
      })
    ) {
      return;
    }
    const result = await save.run(() => sessionApi.requestSmsCode(number));
    if (result.ok) {
      setChallenge(result.value);
      setCode('');
      setResendIn(RESEND_SECONDS);
    }
  };

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (challenge === null) {
      await sendCode();
      return;
    }
    if (
      !save.validate({
        code: /^[0-9]{6}$/.test(code.trim()) ? null : code.trim() === '' ? 'REQUIRED' : 'MALFORMED',
      })
    ) {
      return;
    }
    const result = await save.run(async () =>
      nextStep(await sessionApi.signInWithSmsCode(challenge.challengeId, code.trim())),
    );
    if (result.ok) {
      onSignedIn(result.value);
    }
  };

  return (
    <form
      ref={formRef}
      className="form sign-in__form sign-in__passwordless-option"
      noValidate
      aria-label={t('identityAccess.signIn.passwordless.smsTitle')}
      onSubmit={(event) => void submit(event)}
    >
      <FormAlert message={save.formError} />
      {challenge === null ? (
        <TextField
          label={t('identityAccess.signIn.passwordless.mobileNumber')}
          hint={t('identityAccess.signIn.passwordless.mobileNumberHint')}
          name="mobileNumber"
          type="tel"
          inputMode="tel"
          dir="ltr"
          autoComplete="tel"
          value={mobileNumber}
          onChange={setMobileNumber}
          error={save.fieldErrors.mobileNumber}
        />
      ) : (
        <>
          <p className="sign-in__note" role="status">
            {t('identityAccess.signIn.passwordless.codeSent', { number: toE164(mobileNumber) })}
          </p>
          <TextField
            label={t('identityAccess.signIn.code')}
            name="code"
            inputMode="numeric"
            dir="ltr"
            autoComplete="one-time-code"
            value={code}
            onChange={setCode}
            error={save.fieldErrors.code}
          />
        </>
      )}
      <button type="submit" className="button sign-in__wide" disabled={save.saving}>
        {challenge === null
          ? t('identityAccess.signIn.passwordless.sendCode')
          : save.saving
            ? t('identityAccess.signIn.signingIn')
            : t('identityAccess.signIn.passwordless.signInWithCode')}
      </button>
      {challenge !== null && (
        <div className="sign-in__sms-actions">
          <button
            type="button"
            className="button button--quiet"
            disabled={resendIn > 0 || save.saving}
            onClick={() => void sendCode()}
          >
            {resendIn > 0
              ? t('identityAccess.signIn.passwordless.resendIn', { seconds: resendIn })
              : t('identityAccess.signIn.passwordless.resend')}
          </button>
          <button
            type="button"
            className="button button--quiet"
            disabled={save.saving}
            onClick={() => {
              setChallenge(null);
              setCode('');
            }}
          >
            {t('identityAccess.signIn.passwordless.changeNumber')}
          </button>
        </div>
      )}
    </form>
  );
}

/**
 * Sign-in without a password (13 Nov 2026 review), in two ways: the passkey on this device (Face ID, Touch ID or its own
 * unlock), or a code by SMS to the person's verified mobile number. Each is offered only where the API says it can work.
 */
export function PasswordlessSignIn({
  passkeyAvailable,
  smsAvailable,
  loading,
  onSignedIn,
}: {
  passkeyAvailable: boolean;
  smsAvailable: boolean;
  loading: boolean;
  onSignedIn: (step: SignInStep) => void;
}): ReactElement {
  const { t } = useI18n();
  if (!passkeyAvailable && !smsAvailable) {
    return (
      <section
        className="sign-in__passwordless"
        aria-label={t('identityAccess.signIn.passwordless.title')}
      >
        <p className="sign-in__note">{t('identityAccess.signIn.passwordless.title')}</p>
        {!loading && <p className="badge sign-in__soon">{t('identityAccess.signIn.comingSoon')}</p>}
      </section>
    );
  }
  return (
    <section
      className="sign-in__passwordless"
      aria-label={t('identityAccess.signIn.passwordless.title')}
    >
      <p className="sign-in__note">{t('identityAccess.signIn.passwordless.title')}</p>
      {passkeyAvailable && <PasskeySignIn onSignedIn={onSignedIn} />}
      {passkeyAvailable && smsAvailable && (
        <p className="sign-in__divider">{t('identityAccess.signIn.or')}</p>
      )}
      {smsAvailable && <SmsSignIn onSignedIn={onSignedIn} />}
    </section>
  );
}
