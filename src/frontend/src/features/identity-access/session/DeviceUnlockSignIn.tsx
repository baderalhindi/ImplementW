import { type ReactElement, useEffect, useState } from 'react';

import { ApiError } from '@/shared/api/httpClient.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { FormAlert } from '@/shared/ui/States.tsx';

import { useSaveAction } from '../forms.ts';
import { forgetAllDeviceUnlocks, hasAnyDeviceUnlock } from './deviceUnlockMemory.ts';
import { describePasskeyRefusal, type SignInStep, signInWithPasskey } from './signInSteps.ts';
import { deviceUnlock, platformPasskeysAvailable } from './webAuthn.ts';

/**
 * "Or sign in with Face ID / Touch ID" under the Microsoft sign-in (20 Nov 2026). Shown only where this device has Face ID / Touch ID
 * saved for an account (set up at a biometric sign-in or from Sign-in methods) and can use it; one press signs that account in, with
 * nothing typed. A key no account knows any more (it was removed) is forgotten here and the button goes away.
 */
export function DeviceUnlockSignIn({
  passkeyAvailable,
  onSignedIn,
}: {
  passkeyAvailable: boolean;
  onSignedIn: (step: SignInStep) => void;
}): ReactElement | null {
  const { t } = useI18n();
  const save = useSaveAction(describePasskeyRefusal);
  const unlock = deviceUnlock();
  const [saved, setSaved] = useState(hasAnyDeviceUnlock);
  const [deviceReady, setDeviceReady] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);

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

  const signIn = async () => {
    setNotice(null);
    const result = await save.run(() => signInWithPasskey(null));
    if (result.ok) {
      onSignedIn(result.value);
    } else if (
      result.error instanceof ApiError &&
      result.error.code === 'AUTHENTICATION_REQUIRED'
    ) {
      forgetAllDeviceUnlocks();
      setSaved(false);
      setNotice(t(`identityAccess.signIn.passwordless.notRecognised.${unlock}`));
    }
  };

  if (notice !== null) {
    return <FormAlert message={notice} />;
  }
  if (!passkeyAvailable || !deviceReady || !saved) {
    return null;
  }
  return (
    <>
      <p className="sign-in__divider">{t('identityAccess.signIn.or')}</p>
      <FormAlert message={save.formError} />
      <button
        type="button"
        className="button button--primary sign-in__wide"
        disabled={save.saving}
        onClick={() => void signIn()}
      >
        {save.saving
          ? t('identityAccess.signIn.signingIn')
          : t(`identityAccess.signIn.deviceUnlock.${unlock}`)}
      </button>
    </>
  );
}
