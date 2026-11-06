import { type ReactElement, type SyntheticEvent, useRef, useState } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { TextField } from '@/shared/ui/FormFields.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { useFocusFirstError, useSaveAction } from '../forms.ts';
import { isMultiFactorPending, type Session, sessionApi } from './sessionApi.ts';
import { describeRefusal, type PendingSecondFactor } from './signInSteps.ts';

interface SecondFactorFormProps {
  secondFactor: PendingSecondFactor;
  onSession: (session: Session) => void;
}

/** The second factor of a sign-in, by whichever method the first factor was (TASK-029). The session is issued only here. */
export function SecondFactorForm({ secondFactor, onSession }: SecondFactorFormProps): ReactElement {
  const { t } = useI18n();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction(describeRefusal('identityAccess.signIn.wrongCode'));
  useFocusFirstError(formRef, save.fieldErrors);
  const [code, setCode] = useState('');

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (!save.validate({ code: code.trim() === '' ? 'REQUIRED' : null })) {
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
      onSession(result.value);
    }
  };

  return (
    <form
      ref={formRef}
      className="form sign-in__form"
      noValidate
      onSubmit={(event) => void submit(event)}
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
      <button type="submit" className="button button--primary sign-in__wide" disabled={save.saving}>
        {save.saving ? t('identityAccess.signIn.verifying') : t('identityAccess.signIn.verify')}
      </button>
    </form>
  );
}
