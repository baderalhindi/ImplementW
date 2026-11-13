import { type ReactElement, useState } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { FormAlert } from '@/shared/ui/States.tsx';

import { useSaveAction } from '../forms.ts';
import { problemMessage } from '../problems.ts';
import { rememberDeviceUnlock } from './deviceUnlockMemory.ts';
import { type Session } from './sessionApi.ts';
import { signInMethodsApi } from './signInMethodsApi.ts';
import {
  creationOptions,
  credentialJson,
  deviceName,
  deviceUnlock,
  isPasskeyCancelled,
} from './webAuthn.ts';

/**
 * The first Face ID / Touch ID on a device (13 Nov 2026 review). The person has just proved who they are with the code sent
 * to their verified mobile number (and their authenticator code, if their role needs one), so the device may now keep their
 * Face ID / Touch ID for this account: from then on it is all they need. The session is held here, not yet in the store,
 * so the page stays until they have saved it or chosen not to.
 */
export function DeviceUnlockSetup({
  session,
  nationalId,
  onDone,
}: {
  session: Session;
  nationalId: string;
  onDone: () => void;
}): ReactElement {
  const { t } = useI18n();
  const unlock = deviceUnlock();
  const save = useSaveAction((error, translate) =>
    isPasskeyCancelled(error)
      ? translate('identityAccess.signIn.passwordless.setup.cancelled')
      : problemMessage(error, translate),
  );
  const [saved, setSaved] = useState(false);

  const saveDeviceUnlock = async () => {
    const result = await save.run(async () => {
      const { options, transaction } = await signInMethodsApi.getRegistrationOptions(
        session.accessToken,
      );
      const credential = (await navigator.credentials.create({
        publicKey: creationOptions(options),
      })) as PublicKeyCredential | null;
      if (credential === null) {
        throw new DOMException('Nothing was saved.', 'NotAllowedError');
      }
      await signInMethodsApi.addPasskey(
        transaction,
        credentialJson(credential),
        deviceName(unlock),
        session.accessToken,
      );
      await rememberDeviceUnlock(nationalId);
    });
    if (result.ok) {
      setSaved(true);
      onDone();
    }
  };

  return (
    <section
      className="sign-in__passwordless"
      aria-label={t(`identityAccess.signIn.passwordless.setup.save.${unlock}`)}
    >
      <p className="sign-in__note" role="status">
        {t(`identityAccess.signIn.passwordless.setup.confirmed.${unlock}`)}
      </p>
      <FormAlert message={save.formError} />
      <button
        type="button"
        className="button button--primary sign-in__wide"
        disabled={save.saving || saved}
        onClick={() => void saveDeviceUnlock()}
      >
        {t(`identityAccess.signIn.passwordless.setup.save.${unlock}`)}
      </button>
      <button
        type="button"
        className="button button--quiet sign-in__wide"
        disabled={save.saving}
        onClick={onDone}
      >
        {t('identityAccess.signIn.passwordless.setup.notNow')}
      </button>
    </section>
  );
}
