import { type ReactElement, type SyntheticEvent, useRef, useState } from 'react';

import { useSaveAction } from '@/features/identity-access/forms.ts';
import { TEXT_LENGTH } from '@/features/projects/registration.ts';
import { type ApiResponse } from '@/shared/api/httpClient.ts';
import { useFieldErrors } from '@/shared/forms/useFieldErrors.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { TextAreaField } from '@/shared/ui/FormFields.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { risksApi } from '../api/risksApi.ts';
import { type RiskDetail } from '../api/types.ts';
import { isStale, riskFieldMessage, riskProblemMessage } from '../problems.ts';
import { checkClosure, toCloseCommand } from '../riskRules.ts';

interface CloseRiskDialogProps {
  risk: ApiResponse<RiskDetail>;
  /** Its PLANNED or IN_PROGRESS treatment actions: closing leaves them as they are. */
  liveActions: number;
  onClose: () => void;
  onDone: (risk: RiskDetail) => void;
  onStale: () => void;
}

/**
 * MOD-035 Close Risk: a controlled closure with its rationale (acceptance criterion 2). A blank rationale is refused
 * here before anything is sent, and the API refuses it too (400 REQUIRED on `rationale`), which lands on the same
 * field. The person is told what closing does: an ACTIVE acceptance ends, open actions stay, history is kept.
 */
export function CloseRiskDialog(props: CloseRiskDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog
      open
      title={t('risks.close.title', { risk: props.risk.data.title.text })}
      onClose={props.onClose}
    >
      <CloseForm {...props} />
    </Dialog>
  );
}

function CloseForm({
  risk: { data: risk, etag },
  liveActions,
  onClose,
  onDone,
  onStale,
}: CloseRiskDialogProps): ReactElement {
  const { t, language } = useI18n();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction(riskProblemMessage);
  const [rationale, setRationale] = useState('');
  const fields = useFieldErrors(checkClosure(rationale), formRef, riskFieldMessage);

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (!fields.attempt()) {
      return;
    }
    const result = await save.run(
      async () => (await risksApi.close(risk.id, toCloseCommand(rationale, language), etag)).data,
    );
    if (result.ok) {
      onDone(result.value);
    } else if (isStale(result.error)) {
      onStale();
    } else {
      fields.showServer(result.error);
    }
  };

  return (
    <form ref={formRef} className="form" noValidate onSubmit={(event) => void submit(event)}>
      <FormAlert message={fields.summary ?? save.formError} />
      <ul className="form__note risk-close__warnings">
        {risk.acceptedUntil !== null && (
          <li>{t('risks.close.acceptanceEnds', { date: risk.acceptedUntil })}</li>
        )}
        {liveActions > 0 && <li>{t('risks.close.openActions', { count: liveActions })}</li>}
        <li>{t('risks.close.history')}</li>
      </ul>
      <TextAreaField
        label={t('risks.fields.closureRationale')}
        name="rationale"
        required
        maxLength={TEXT_LENGTH}
        hint={t('risks.close.rationaleHint')}
        value={rationale}
        onChange={(value) => {
          setRationale(value);
          fields.clearServer();
        }}
        error={fields.errorOf('rationale')}
      />
      <div className="form__actions">
        <button type="submit" className="button button--primary" disabled={save.saving}>
          {save.saving ? t('common.states.saving') : t('risks.close.confirm')}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
