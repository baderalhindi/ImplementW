import { type ReactElement, type SyntheticEvent, useEffect, useRef, useState } from 'react';

import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { TextField } from '@/shared/ui/FormFields.tsx';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { FormAlert, LoadingState } from '@/shared/ui/States.tsx';

import { useFocusFirstError, useSaveAction } from '../forms.ts';
import { problemMessage } from '../problems.ts';
import {
  type MobileNumberStatus,
  type MobileVerificationChallenge,
  type PasskeySummary,
  signInMethodsApi,
} from './signInMethodsApi.ts';
import {
  creationOptions,
  credentialJson,
  deviceName,
  deviceUnlock,
  isPasskeyCancelled,
  platformPasskeysAvailable,
} from './webAuthn.ts';

const loadPasskeys = (signal: AbortSignal) => signInMethodsApi.listPasskeys(signal);
const loadMobileNumber = (signal: AbortSignal) => signInMethodsApi.getMobileNumber(signal);

function AddPasskeyForm({ onAdded }: { onAdded: () => void }): ReactElement {
  const { t } = useI18n();
  const formRef = useRef<HTMLFormElement>(null);
  const unlock = deviceUnlock();
  const save = useSaveAction((error, translate) =>
    isPasskeyCancelled(error)
      ? translate('identityAccess.signInMethods.passkeyCancelled')
      : problemMessage(error, translate),
  );
  useFocusFirstError(formRef, save.fieldErrors);
  const [name, setName] = useState(deviceName(unlock));
  const [deviceReady, setDeviceReady] = useState<boolean | null>(null);

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

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (!save.validate({ name: name.trim() === '' ? 'REQUIRED' : null })) {
      return;
    }
    const result = await save.run(async () => {
      const { options, transaction } = await signInMethodsApi.getRegistrationOptions();
      const credential = (await navigator.credentials.create({
        publicKey: creationOptions(options),
      })) as PublicKeyCredential | null;
      if (credential === null) {
        throw new DOMException('No passkey was created.', 'NotAllowedError');
      }
      return signInMethodsApi.addPasskey(transaction, credentialJson(credential), name.trim());
    });
    if (result.ok) {
      onAdded();
    }
  };

  if (deviceReady === false) {
    return <p>{t('identityAccess.signInMethods.noDeviceUnlock')}</p>;
  }

  return (
    <form ref={formRef} className="form" noValidate onSubmit={(event) => void submit(event)}>
      <FormAlert message={save.formError} />
      <TextField
        label={t('identityAccess.signInMethods.deviceName')}
        hint={t('identityAccess.signInMethods.deviceNameHint')}
        name="name"
        required
        value={name}
        onChange={setName}
        error={save.fieldErrors.name}
      />
      <div className="form__actions">
        <button
          type="submit"
          className="button button--primary"
          disabled={deviceReady !== true || save.saving}
        >
          {save.saving
            ? t('common.states.saving')
            : t(`identityAccess.signInMethods.add.${unlock}`)}
        </button>
      </div>
    </form>
  );
}

function PasskeysSection(): ReactElement {
  const { t, formatDateTime } = useI18n();
  const passkeys = useApiResource(loadPasskeys, { keepWhileReloading: true });
  const remove = useSaveAction();
  const [removing, setRemoving] = useState<PasskeySummary | null>(null);

  const confirmRemoval = async () => {
    if (removing === null) {
      return;
    }
    const result = await remove.run(() => signInMethodsApi.removePasskey(removing.id));
    if (result.ok) {
      setRemoving(null);
      passkeys.reload();
    }
  };

  return (
    <section className="section" aria-labelledby="passkeys-title">
      <h2 id="passkeys-title">{t('identityAccess.signInMethods.passkeysTitle')}</h2>
      <p>{t('identityAccess.signInMethods.passkeysDescription')}</p>
      {passkeys.data === undefined ? (
        passkeys.error === null ? (
          <LoadingState />
        ) : (
          <FormAlert message={problemMessage(passkeys.error, t)} />
        )
      ) : passkeys.data.items.length === 0 ? (
        <p>{t('identityAccess.signInMethods.noPasskeys')}</p>
      ) : (
        <ul className="sign-in-methods__list">
          {passkeys.data.items.map((passkey) => (
            <li key={passkey.id} className="sign-in-methods__item">
              <span>
                <strong>{passkey.name}</strong>
                <span className="sign-in-methods__meta">
                  {t('identityAccess.signInMethods.added', {
                    date: formatDateTime(passkey.createdAt),
                  })}
                  {' · '}
                  {passkey.lastUsedAt === null
                    ? t('identityAccess.signInMethods.neverUsed')
                    : t('identityAccess.signInMethods.lastUsed', {
                        date: formatDateTime(passkey.lastUsedAt),
                      })}
                </span>
              </span>
              <button
                type="button"
                className="button button--quiet"
                onClick={() => {
                  setRemoving(passkey);
                }}
              >
                {t('identityAccess.signInMethods.remove')}
              </button>
            </li>
          ))}
        </ul>
      )}
      <AddPasskeyForm onAdded={passkeys.reload} />
      <Dialog
        open={removing !== null}
        title={t('identityAccess.signInMethods.removeTitle')}
        onClose={() => {
          setRemoving(null);
        }}
      >
        <p>{t('identityAccess.signInMethods.removeBody', { name: removing?.name ?? '' })}</p>
        <FormAlert message={remove.formError} />
        <div className="form__actions">
          <button
            type="button"
            className="button button--danger"
            disabled={remove.saving}
            onClick={() => void confirmRemoval()}
          >
            {t('identityAccess.signInMethods.remove')}
          </button>
          <button
            type="button"
            className="button"
            onClick={() => {
              setRemoving(null);
            }}
          >
            {t('common.actions.cancel')}
          </button>
        </div>
      </Dialog>
    </section>
  );
}

function VerifyMobileForm({
  status,
  onVerified,
}: {
  status: MobileNumberStatus;
  onVerified: () => void;
}): ReactElement {
  const { t } = useI18n();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction();
  useFocusFirstError(formRef, save.fieldErrors);
  const [challenge, setChallenge] = useState<MobileVerificationChallenge | null>(null);
  const [code, setCode] = useState('');

  const send = async () => {
    const result = await save.run(() => signInMethodsApi.startMobileVerification());
    if (result.ok) {
      setChallenge(result.value);
      setCode('');
    }
  };

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (challenge === null) {
      await send();
      return;
    }
    if (!save.validate({ code: code.trim() === '' ? 'REQUIRED' : null })) {
      return;
    }
    const result = await save.run(() =>
      signInMethodsApi.verifyMobileNumber(challenge.challengeId, code.trim()),
    );
    if (result.ok) {
      onVerified();
    }
  };

  if (!status.verificationAvailable) {
    return <p>{t('identityAccess.signInMethods.smsUnavailable')}</p>;
  }

  return (
    <form ref={formRef} className="form" noValidate onSubmit={(event) => void submit(event)}>
      <FormAlert message={save.formError} />
      {challenge !== null && (
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
      )}
      <div className="form__actions">
        <button type="submit" className="button button--primary" disabled={save.saving}>
          {challenge === null
            ? t('identityAccess.signInMethods.sendVerificationCode')
            : t('identityAccess.signInMethods.verify')}
        </button>
        {challenge !== null && (
          <button
            type="button"
            className="button button--quiet"
            disabled={save.saving}
            onClick={() => void send()}
          >
            {t('identityAccess.signIn.passwordless.resend')}
          </button>
        )}
      </div>
    </form>
  );
}

function MobileNumberSection(): ReactElement {
  const { t, formatDateTime } = useI18n();
  const status = useApiResource(loadMobileNumber, { keepWhileReloading: true });

  return (
    <section className="section" aria-labelledby="mobile-title">
      <h2 id="mobile-title">{t('identityAccess.signInMethods.mobileTitle')}</h2>
      <p>{t('identityAccess.signInMethods.mobileDescription')}</p>
      {status.data === undefined ? (
        status.error === null ? (
          <LoadingState />
        ) : (
          <FormAlert message={problemMessage(status.error, t)} />
        )
      ) : status.data.mobileNumber === null ? (
        <p>{t('identityAccess.signInMethods.noMobileNumber')}</p>
      ) : (
        <>
          <p>
            <bdi dir="ltr">{status.data.mobileNumber}</bdi>
            {' · '}
            {status.data.verifiedAt === null
              ? t('identityAccess.signInMethods.notVerified')
              : t('identityAccess.signInMethods.verifiedOn', {
                  date: formatDateTime(status.data.verifiedAt),
                })}
          </p>
          {status.data.verifiedAt === null && (
            <VerifyMobileForm status={status.data} onVerified={status.reload} />
          )}
        </>
      )}
    </section>
  );
}

/**
 * The person's own ways to sign in without a password (13 Nov 2026 review): passkeys on their devices (Face ID, Touch ID)
 * and the mobile number SMS codes go to, once they have verified it. Everyone reaches it from the account menu.
 */
export function SignInMethodsPage(): ReactElement {
  const { t } = useI18n();
  return (
    <>
      <PageHeader
        title={t('identityAccess.signInMethods.title')}
        description={t('identityAccess.signInMethods.description')}
      />
      <PasskeysSection />
      <MobileNumberSection />
    </>
  );
}
