import { type SyntheticEvent, type ReactElement, useCallback, useRef, useState } from 'react';

import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { TextField } from '@/shared/ui/FormFields.tsx';
import { ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';

import { useFocusFirstError, useSaveAction } from '../forms.ts';
import { problemMessage } from '../problems.ts';
import { sessionApi } from './sessionApi.ts';
import { sessionStore } from './sessionStore.ts';
import { useSession } from './useSession.ts';

/**
 * ADR-010 step-up: activating or disabling a user and making or ending an assignment need a second factor entered
 * within the last few minutes (record D-11). When the API answers STEP_UP_REQUIRED, this dialog collects a fresh
 * code and the action is sent again.
 */
export function StepUpDialog(): ReactElement {
  const { t } = useI18n();
  const { stepUpPending } = useSession();
  return (
    <Dialog
      open={stepUpPending}
      title={t('identityAccess.stepUp.title')}
      onClose={() => {
        sessionStore.completeStepUp(null);
      }}
    >
      <StepUpForm />
    </Dialog>
  );
}

function StepUpForm(): ReactElement {
  const { t } = useI18n();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction();
  useFocusFirstError(formRef, save.fieldErrors);
  const [code, setCode] = useState('');

  const begin = useCallback((_signal: AbortSignal) => {
    const refreshToken = sessionStore.getSnapshot().session?.refreshToken ?? '';
    return sessionApi.beginStepUp(refreshToken);
  }, []);
  const challenge = useApiResource(begin);

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    const refreshToken = sessionStore.getSnapshot().session?.refreshToken;
    if (
      challenge.data === undefined ||
      refreshToken === undefined ||
      !save.validate({ code: code.trim() === '' ? 'REQUIRED' : null })
    ) {
      return;
    }
    const challengeId = challenge.data.challengeId;
    const result = await save.run(() =>
      sessionApi.completeStepUp(refreshToken, challengeId, code.trim()),
    );
    if (result.ok) {
      sessionStore.completeStepUp(result.value);
    }
  };

  const cancel = (
    <button
      type="button"
      className="button"
      disabled={save.saving}
      onClick={() => {
        sessionStore.completeStepUp(null);
      }}
    >
      {t('common.actions.cancel')}
    </button>
  );

  if (challenge.loading) {
    return <LoadingState />;
  }
  if (challenge.error !== null) {
    return (
      <>
        <ErrorState message={problemMessage(challenge.error, t)} onRetry={challenge.reload} />
        <div className="form__actions">{cancel}</div>
      </>
    );
  }
  return (
    <form ref={formRef} className="form" noValidate onSubmit={(event) => void submit(event)}>
      <p>{t('identityAccess.stepUp.instructions')}</p>
      <FormAlert message={save.formError} />
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
          {save.saving ? t('identityAccess.signIn.verifying') : t('identityAccess.stepUp.confirm')}
        </button>
        {cancel}
      </div>
    </form>
  );
}
