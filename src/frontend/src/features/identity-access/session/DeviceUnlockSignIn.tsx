import { type ReactElement, useEffect, useState } from 'react';

import { ApiError } from '@/shared/api/httpClient.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { FormAlert } from '@/shared/ui/States.tsx';

import { useSaveAction } from '../forms.ts';
import { forgetSavedPasskeys, savedPasskeyIds } from './deviceUnlockMemory.ts';
import { describePasskeyRefusal, type SignInStep, signInWithPasskey } from './signInSteps.ts';
import { deviceUnlock, platformPasskeysAvailable } from './webAuthn.ts';

/**
 * "Or sign in with Face ID / Touch ID" under the Microsoft sign-in (20 Nov 2026). Shown only where Face ID / Touch ID was saved from
 * Sign-in methods in this browser and the device can use it; one press asks the device for those passkeys alone (never another the
 * device keeps for the site) and signs their account in, with nothing typed. Keys no account knows any more are forgotten and the
 * button goes away.
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
  const [saved, setSaved] = useState(savedPasskeyIds);
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
    const result = await save.run(() => signInWithPasskey(null, saved));
    if (result.ok) {
      onSignedIn(result.value);
    } else if (
      result.error instanceof ApiError &&
      result.error.code === 'AUTHENTICATION_REQUIRED'
    ) {
      forgetSavedPasskeys();
      setSaved([]);
      setNotice(t('identityAccess.signIn.deviceUnlockGone'));
    }
  };

  if (notice !== null) {
    return <FormAlert message={notice} />;
  }
  if (!passkeyAvailable || !deviceReady || saved.length === 0) {
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
