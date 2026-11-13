import { type ReactElement, type SyntheticEvent, useEffect, useRef, useState } from 'react';

import { ApiError } from '@/shared/api/httpClient.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { TextField } from '@/shared/ui/FormFields.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { checkNationalId, useFocusFirstError, useSaveAction } from '../forms.ts';
import { type ProblemDescriber, problemMessage } from '../problems.ts';
import {
  forgetDeviceUnlock,
  hasAnyDeviceUnlock,
  hasDeviceUnlock,
  rememberDeviceUnlock,
} from './deviceUnlockMemory.ts';
import { MOBILE_PATTERN, toE164 } from './mobileNumber.ts';
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

/** At the code step, a refusal is about the code. */
const describeCode: ProblemDescriber = (error, t) =>
  error instanceof ApiError && error.code === 'AUTHENTICATION_REQUIRED'
    ? t('identityAccess.signIn.wrongCode')
    : problemMessage(error, t);

type Stage = 'start' | 'nationalId' | 'mobileNumber' | 'smsCode';

/**
 * Sign-in with biometrics (the client's flow, 13 Nov 2026):
 *
 * 1. One button. Where this device has Face ID / Touch ID saved for an account, it is asked for at once and signs that
 *    account in: nothing is typed.
 * 2. Otherwise the National ID or Iqama number, which must be registered to a person who may sign in. Where the device
 *    has Face ID / Touch ID for that number, it is asked for then.
 * 3. Otherwise (the first time on this device), the person types the mobile number on their profile; a code goes to it, and
 *    once it is entered (with the authenticator code where the role needs one) the device saves their Face ID / Touch ID,
 *    so next time step 2 is all they need. Another device starts at step 3 again.
 *
 * The device keeps the Face ID / Touch ID as a passkey; the page never says the word.
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
  /** `setUpForNationalId`: save Face ID / Touch ID on this device for that National ID once the session is issued. */
  onSignedIn: (step: SignInStep, setUpForNationalId: string | null) => void;
}): ReactElement {
  const { t } = useI18n();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction(describe);
  const codeSave = useSaveAction(describeCode);
  useFocusFirstError(formRef, save.fieldErrors);
  const unlock = deviceUnlock();
  const [stage, setStage] = useState<Stage>('start');
  /** Face ID / Touch ID was just tried and did not sign in: do not ask for it again before the code. */
  const [deviceUnlockFailed, setDeviceUnlockFailed] = useState(false);
  const [nationalId, setNationalId] = useState('');
  const [mobileNumber, setMobileNumber] = useState('');
  const [code, setCode] = useState('');
  const [challenge, setChallenge] = useState<SmsCodeChallenge | null>(null);
  const [resendIn, setResendIn] = useState(0);
  const [deviceReady, setDeviceReady] = useState<boolean | null>(null);
  const available = passkeyAvailable || smsAvailable;
  const canSetUp = passkeyAvailable && deviceReady === true;

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

  // The National ID field takes the focus when it appears, so the person can type at once.
  useEffect(() => {
    if (stage === 'nationalId') {
      formRef.current?.querySelector<HTMLInputElement>('input[name="nationalId"]')?.focus();
    }
  }, [stage]);

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

  /** With the National ID, only its owner's Face ID / Touch ID verifies; without it, whoever the device's belongs to signs in. */
  const signInWithDeviceUnlock = async (forNationalId: string | null): Promise<boolean> => {
    const result = await save.run(async () => {
      const { options, transaction } = await sessionApi.beginPasskeySignIn(forNationalId);
      const credential = (await navigator.credentials.get({
        publicKey: requestOptions(options),
      })) as PublicKeyCredential | null;
      if (credential === null) {
        throw new DOMException('Nothing was chosen.', 'NotAllowedError');
      }
      return nextStep(await sessionApi.signInWithPasskey(transaction, credentialJson(credential)));
    });
    if (result.ok) {
      if (forNationalId !== null) {
        await rememberDeviceUnlock(forNationalId);
      }
      onSignedIn(result.value, null);
      return true;
    }
    if (forNationalId !== null) {
      await forgetDeviceUnlock(forNationalId);
    }
    return false;
  };

  /** The one button: Face ID / Touch ID at once where this device has it, the National ID otherwise. */
  const start = async () => {
    if (canSetUp && hasAnyDeviceUnlock()) {
      if (await signInWithDeviceUnlock(null)) {
        return;
      }
      setDeviceUnlockFailed(true);
    }
    setStage('nationalId');
  };

  const checkId = async () => {
    if (!save.validate({ nationalId: checkNationalId(nationalId) })) {
      return;
    }
    const checked = await save.run(() => sessionApi.checkNationalId(id));
    if (!checked.ok) {
      return;
    }
    if (!checked.value.registered) {
      save.validate({ nationalId: 'NOT_REGISTERED' });
      return;
    }
    if (
      canSetUp &&
      !deviceUnlockFailed &&
      (await hasDeviceUnlock(id)) &&
      (await signInWithDeviceUnlock(id))
    ) {
      return;
    }
    setStage('mobileNumber');
  };

  const sendCode = async () => {
    const number = toE164(mobileNumber);
    if (
      !save.validate({
        mobileNumber: MOBILE_PATTERN.test(number) ? null : number === '' ? 'REQUIRED' : 'MALFORMED',
      })
    ) {
      return;
    }
    const result = await save.run(() => sessionApi.requestSmsCode(id, number));
    if (result.ok) {
      setChallenge(result.value);
      setCode('');
      setResendIn(RESEND_SECONDS);
      setStage('smsCode');
    }
  };

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (stage === 'start') {
      await start();
      return;
    }
    if (stage === 'nationalId') {
      await checkId();
      return;
    }
    if (stage === 'mobileNumber') {
      await sendCode();
      return;
    }
    if (challenge === null) {
      return;
    }
    const entered = code.trim();
    if (
      !codeSave.validate({
        code: /^[0-9]{6}$/.test(entered) ? null : entered === '' ? 'REQUIRED' : 'MALFORMED',
      })
    ) {
      return;
    }
    const result = await codeSave.run(async () =>
      nextStep(await sessionApi.signInWithSmsCode(challenge.challengeId, entered)),
    );
    if (result.ok) {
      onSignedIn(result.value, canSetUp ? id : null);
    }
  };

  const startOver = () => {
    setStage('nationalId');
    setChallenge(null);
    setCode('');
    setMobileNumber('');
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
      <FormAlert
        message={stage === 'smsCode' ? (codeSave.formError ?? save.formError) : save.formError}
      />

      {stage === 'start' && (
        <>
          <button
            type="submit"
            className="button button--primary sign-in__wide"
            disabled={!available || deviceReady === null || save.saving}
          >
            {save.saving
              ? t('identityAccess.signIn.signingIn')
              : t('identityAccess.signIn.passwordless.submit')}
          </button>
          {passkeyAvailable && deviceReady === false && (
            <p className="sign-in__note">
              {t('identityAccess.signIn.passwordless.noDeviceUnlock')}
            </p>
          )}
        </>
      )}

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
          />
          <button
            type="submit"
            className="button button--primary sign-in__wide"
            disabled={save.saving}
          >
            {t('identityAccess.signIn.passwordless.continue')}
          </button>
        </>
      )}

      {stage === 'mobileNumber' && (
        <>
          <p className="sign-in__note">
            {canSetUp
              ? t(`identityAccess.signIn.passwordless.firstTime.${unlock}`)
              : t('identityAccess.signIn.passwordless.mobileOnly')}
          </p>
          {smsAvailable ? (
            <>
              <TextField
                label={t('identityAccess.signIn.passwordless.mobileNumber')}
                name="mobileNumber"
                type="tel"
                inputMode="tel"
                dir="ltr"
                autoComplete="tel"
                value={mobileNumber}
                onChange={setMobileNumber}
                error={save.fieldErrors.mobileNumber}
              />
              <button
                type="submit"
                className="button button--primary sign-in__wide"
                disabled={save.saving}
              >
                {t('identityAccess.signIn.passwordless.sendCode')}
              </button>
            </>
          ) : (
            <p className="sign-in__note">{t('identityAccess.signIn.passwordless.noSms')}</p>
          )}
          {canSetUp && (
            <button
              type="button"
              className="button button--quiet sign-in__wide"
              disabled={save.saving}
              onClick={() => void signInWithDeviceUnlock(id)}
            >
              {t(`identityAccess.signIn.passwordless.already.${unlock}`)}
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

      {stage === 'smsCode' && (
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
            error={codeSave.fieldErrors.code}
          />
          <button
            type="submit"
            className="button button--primary sign-in__wide"
            disabled={save.saving || codeSave.saving}
          >
            {codeSave.saving
              ? t('identityAccess.signIn.signingIn')
              : t('identityAccess.signIn.passwordless.verifyCode')}
          </button>
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
              disabled={save.saving || codeSave.saving}
              onClick={startOver}
            >
              {t('identityAccess.signIn.passwordless.changeNationalId')}
            </button>
          </div>
        </>
      )}
    </form>
  );
}
