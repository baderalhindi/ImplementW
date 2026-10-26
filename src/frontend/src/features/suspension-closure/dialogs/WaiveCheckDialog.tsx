import { type ReactElement, type SyntheticEvent, useState } from 'react';

import { checkText, useSaveAction } from '@/features/identity-access/forms.ts';
import { narrativeRequest } from '@/features/progress/progressUpdate.ts';
import { TEXT_LENGTH } from '@/features/projects/registration.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { TextAreaField } from '@/shared/ui/FormFields.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { type CloseoutStage, closeoutApi } from '../api/closeoutApi.ts';
import { type ReadinessCheckCode } from '../api/types.ts';
import { isStale, suspensionClosureProblemMessage } from '../problems.ts';

/**
 * A failed criterion waived with a reason (CLOSEOUT_WAIVE, AHDA's): the case reads Ready with conditions once every
 * failure is waived. The waiver is a further readiness record, kept with the case.
 */
export function WaiveCheckDialog({
  stage,
  caseId,
  checkCode,
  etag,
  onClose,
  onDone,
  onStale,
}: {
  stage: CloseoutStage;
  caseId: string;
  checkCode: ReadinessCheckCode;
  etag: string | null;
  onClose: () => void;
  onDone: () => void;
  onStale: () => void;
}): ReactElement {
  const { t, language } = useI18n();
  const save = useSaveAction(suspensionClosureProblemMessage);
  const [reason, setReason] = useState('');
  const [tried, setTried] = useState(false);
  const code = tried ? checkText(reason, { required: true, maxLength: TEXT_LENGTH }) : null;
  const criterion = t(`suspensionClosure.readiness.checks.${checkCode}`);

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    setTried(true);
    if (checkText(reason, { required: true, maxLength: TEXT_LENGTH }) !== null) {
      return;
    }
    const result = await save.run(() =>
      closeoutApi.waiveCheck(
        stage,
        caseId,
        { checkCode, reason: narrativeRequest(reason, language) },
        etag,
      ),
    );
    if (result.ok) {
      onDone();
    } else if (isStale(result.error)) {
      onStale();
    }
  };

  return (
    <Dialog open title={t('suspensionClosure.waive.title', { criterion })} onClose={onClose}>
      <form className="form" noValidate onSubmit={(event) => void submit(event)}>
        <FormAlert message={save.formError} />
        <p className="form__note">{t('suspensionClosure.waive.consequence')}</p>
        <TextAreaField
          label={t('suspensionClosure.waive.reason')}
          name="reason"
          required
          maxLength={TEXT_LENGTH}
          value={reason}
          onChange={setReason}
          error={code === null ? undefined : t('suspensionClosure.fieldErrors.reasonRequired')}
        />
        <div className="form__actions">
          <button type="submit" className="button button--primary" disabled={save.saving}>
            {save.saving ? t('common.states.saving') : t('suspensionClosure.waive.action')}
          </button>
          <button type="button" className="button" disabled={save.saving} onClick={onClose}>
            {t('common.actions.cancel')}
          </button>
        </div>
      </form>
    </Dialog>
  );
}
