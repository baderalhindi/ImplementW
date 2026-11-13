import { type ReactElement, type SyntheticEvent, useEffect, useRef, useState } from 'react';

import { ApiError } from '@/shared/api/httpClient.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { TextField } from '@/shared/ui/FormFields.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { checkNationalId, useFocusFirstError, useSaveAction } from '../forms.ts';
import { type ProblemDescriber, problemMessage } from '../problems.ts';
import { forgetDeviceUnlock, hasDeviceUnlock, rememberDeviceUnlock } from './deviceUnlockMemory.ts';
import { sessionApi, type SmsCodeChallenge } from './sessionApi.ts';
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

const describe: ProblemDescriber = (error, t) =>
  isPasskeyCancelled(error)
    ? t('identityAccess.signIn.passwordless.passkeyCancelled')
    : error instanceof ApiError && error.code === 'AUTHENTICATION_REQUIRED'
      ? t('identityAccess.signIn.passwordless.refused')
      : problemMessage(error, t);

type Stage = 'nationalId' | 'choose' | 'setup' | 'smsCode';

/**
 * Sign-in with biometrics (13 Nov 2026 review). The person gives their National ID or Iqama number, then proves it is
 * them: with Face ID / Touch ID on this device, or with a code by SMS to the mobile number verified on their account. The
 * first time on a device, Face ID / Touch ID is set up on the way: the SMS code proves who they are, then the device keeps
 * their Face ID / Touch ID for the account (a passkey, though the page never says the word), and from then on it is all
 * they need. The choice looks the same for any number, so the page says nothing about who has an account.
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
  /** `setUpForNationalId`: the person signed in to set Face ID / Touch ID up on this device for that National ID. */
  onSignedIn: (step: SignInStep, setUpForNationalId: string | null) => void;
}): ReactElement {
  const { t } = useI18n();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction(describe);
  useFocusFirstError(formRef, save.fieldErrors);
  const unlock = deviceUnlock();
  const [stage, setStage] = useState<Stage>('nationalId');
  const [nationalId, setNationalId] = useState('');
  const [code, setCode] = useState('');
  const [challenge, setChallenge] = useState<SmsCodeChallenge | null>(null);
  const [resendIn, setResendIn] = useState(0);
  const [deviceReady, setDeviceReady] = useState<boolean | null>(null);
  const [forSetup, setForSetup] = useState(false);
  const [deviceUnlockFailed, setDeviceUnlockFailed] = useState(false);
  const available = passkeyAvailable || smsAvailable;

  useEffect(() => {
    let current = true;
    void platformPasskeysAvailable().then((ready) => {
      if (current) {
        setDeviceReady(ready);
      }
    });
    return () => {
      current = false;
    };
  }, []);

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

  const id = nationalId.trim();

  /** With the National ID, only its owner's passkey verifies; without it, the passkey the device offers signs its owner in. */
  const signInWithPasskey = async (forNationalId: string | null) => {
    const result = await save.run(async () => {
      const { options, transaction } = await sessionApi.beginPasskeySignIn(forNationalId);
      const credential = (await navigator.credentials.get({
        publicKey: requestOptions(options),
      })) as PublicKeyCredential | null;
      if (credential === null) {
        throw new DOMException('No passkey was chosen.', 'NotAllowedError');
      }
      return nextStep(await sessionApi.signInWithPasskey(transaction, credentialJson(credential)));
    });
    if (result.ok) {
      if (forNationalId !== null) {
        await rememberDeviceUnlock(forNationalId);
      }
      onSignedIn(result.value, null);
    } else if (forNationalId !== null) {
      // Not set up here after all (removed, or another browser profile): offer the setup instead.
      await forgetDeviceUnlock(forNationalId);
      setDeviceUnlockFailed(true);
    }
  };

  /** Asks for Face ID / Touch ID at once where this device has it for the National ID; otherwise sets it up first. */
  const signInWithDeviceUnlock = async () => {
    if (await hasDeviceUnlock(id)) {
      await signInWithPasskey(id);
    } else {
      setStage('setup');
    }
  };

  const sendCode = async (setup: boolean) => {
    const result = await save.run(() => sessionApi.requestSmsCode(id));
    if (result.ok) {
      setForSetup(setup);
      setChallenge(result.value);
      setCode('');
      setResendIn(RESEND_SECONDS);
      setStage('smsCode');
    }
  };

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (stage === 'nationalId') {
      if (save.validate({ nationalId: checkNationalId(nationalId) })) {
        setStage('choose');
      }
      return;
    }
    if (stage !== 'smsCode' || challenge === null) {
      return;
    }
    const entered = code.trim();
    if (
      !save.validate({
        code: /^[0-9]{6}$/.test(entered) ? null : entered === '' ? 'REQUIRED' : 'MALFORMED',
      })
    ) {
      return;
    }
    const result = await save.run(async () =>
      nextStep(await sessionApi.signInWithSmsCode(challenge.challengeId, entered)),
    );
    if (result.ok) {
      onSignedIn(result.value, forSetup ? id : null);
    }
  };

  const startOver = () => {
    setStage('nationalId');
    setChallenge(null);
    setCode('');
    setDeviceUnlockFailed(false);
  };

  return (
    <form
      ref={formRef}
      className="form sign-in__form sign-in__passwordless"
      noValidate
      aria-label={t('identityAccess.signIn.passwordless.submit')}
      onSubmit={(event) => void submit(event)}
    >
      <p className="sign-in__note">{t('identityAccess.signIn.passwordless.instructions')}</p>
      {!available && !loading && (
        <p className="badge sign-in__soon">{t('identityAccess.signIn.comingSoon')}</p>
      )}
      <FormAlert message={save.formError} />

      {stage === 'nationalId' && (
        <>
          <TextField
            label={t('identityAccess.signIn.passwordless.nationalId')}
            name="nationalId"
            inputMode="numeric"
            dir="ltr"
            autoComplete="off"
            value={nationalId}
            onChange={setNationalId}
            error={save.fieldErrors.nationalId}
            disabled={!available}
          />
          <button type="submit" className="button sign-in__wide" disabled={!available}>
            {t('identityAccess.signIn.passwordless.submit')}
          </button>
          {passkeyAvailable && (
            <>
              <button
                type="button"
                className="button button--primary sign-in__wide"
                disabled={deviceReady !== true || save.saving}
                onClick={() => void signInWithPasskey(null)}
              >
                {save.saving
                  ? t('identityAccess.signIn.signingIn')
                  : t(`identityAccess.signIn.passwordless.direct.${unlock}`)}
              </button>
              {deviceReady === false && (
                <p className="sign-in__note">
                  {t('identityAccess.signIn.passwordless.noDeviceUnlock')}
                </p>
              )}
            </>
          )}
        </>
      )}

      {stage === 'choose' && (
        <>
          <p className="sign-in__subtitle">{t('identityAccess.signIn.passwordless.choose')}</p>
          {passkeyAvailable && (
            <>
              <button
                type="button"
                className="button button--primary sign-in__wide"
                disabled={deviceReady !== true || save.saving}
                onClick={() => void signInWithDeviceUnlock()}
              >
                {t(`identityAccess.signIn.passwordless.passkey.${unlock}`)}
              </button>
              {deviceUnlockFailed && smsAvailable && (
                <button
                  type="button"
                  className="button button--quiet sign-in__wide"
                  disabled={save.saving}
                  onClick={() => {
                    setStage('setup');
                  }}
                >
                  {t(`identityAccess.signIn.passwordless.setup.again.${unlock}`)}
                </button>
              )}
              {deviceReady === false && (
                <p className="sign-in__note">
                  {t('identityAccess.signIn.passwordless.noDeviceUnlock')}
                </p>
              )}
            </>
          )}
          {smsAvailable && (
            <button
              type="button"
              className="button sign-in__wide"
              disabled={save.saving}
              onClick={() => void sendCode(false)}
            >
              {t('identityAccess.signIn.passwordless.sendCode')}
            </button>
          )}
          <button
            type="button"
            className="button button--quiet sign-in__wide"
            disabled={save.saving}
            onClick={startOver}
          >
            {t('identityAccess.signIn.passwordless.changeNationalId')}
          </button>
        </>
      )}

      {stage === 'setup' && (
        <>
          <p className="sign-in__note">
            {t(`identityAccess.signIn.passwordless.setup.intro.${unlock}`)}
          </p>
          {smsAvailable ? (
            <button
              type="button"
              className="button button--primary sign-in__wide"
              disabled={save.saving}
              onClick={() => void sendCode(true)}
            >
              {t('identityAccess.signIn.passwordless.setup.sendCode')}
            </button>
          ) : (
            <p className="sign-in__note">{t('identityAccess.signIn.passwordless.setup.noSms')}</p>
          )}
          <button
            type="button"
            className="button button--quiet sign-in__wide"
            disabled={save.saving}
            onClick={() => void signInWithPasskey(id)}
          >
            {t(`identityAccess.signIn.passwordless.setup.already.${unlock}`)}
          </button>
          <button
            type="button"
            className="button button--quiet sign-in__wide"
            disabled={save.saving}
            onClick={() => {
              setStage('choose');
            }}
          >
            {t('identityAccess.signIn.passwordless.otherWay')}
          </button>
        </>
      )}

      {stage === 'smsCode' && (
        <>
          <p className="sign-in__note" role="status">
            {t('identityAccess.signIn.passwordless.codeSent')}
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
          <button
            type="submit"
            className="button button--primary sign-in__wide"
            disabled={save.saving}
          >
            {save.saving
              ? t('identityAccess.signIn.signingIn')
              : t('identityAccess.signIn.passwordless.signInWithCode')}
          </button>
          <div className="sign-in__sms-actions">
            <button
              type="button"
              className="button button--quiet"
              disabled={resendIn > 0 || save.saving}
              onClick={() => void sendCode(forSetup)}
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
                setStage('choose');
              }}
            >
              {t('identityAccess.signIn.passwordless.otherWay')}
            </button>
          </div>
        </>
      )}
    </form>
  );
}
