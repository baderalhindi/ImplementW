import { type ReactElement, type SyntheticEvent, useRef, useState } from 'react';

import { useSaveAction } from '@/features/identity-access/forms.ts';
import { TEXT_LENGTH } from '@/features/projects/registration.ts';
import { todayUtc } from '@/features/tasks/taskRules.ts';
import { type ApiResponse } from '@/shared/api/httpClient.ts';
import { useFieldErrors } from '@/shared/forms/useFieldErrors.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { TextAreaField, TextField } from '@/shared/ui/FormFields.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { risksApi } from '../api/risksApi.ts';
import { type RiskDetail } from '../api/types.ts';
import { isStale, riskFieldMessage, riskProblemMessage } from '../problems.ts';
import { type AcceptanceValues, checkAcceptance, toAcceptCommand } from '../riskRules.ts';

interface AcceptRiskDialogProps {
  risk: ApiResponse<RiskDetail>;
  onClose: () => void;
  onDone: (risk: RiskDetail) => void;
  onStale: () => void;
}

/**
 * Accepting a rated risk until an expiry (TASK-055 D-7), AHDA's decision (RISK_ACCEPT, internal only). The risk moves
 * to Monitoring with its next review on the expiry; on that day the acceptance lapses and the risk returns to Assessed.
 * An acceptance is never extended: a later decision is a new one.
 */
export function AcceptRiskDialog(props: AcceptRiskDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog
      open
      title={t('risks.accept.title', { risk: props.risk.data.title.text })}
      onClose={props.onClose}
    >
      <AcceptForm {...props} />
    </Dialog>
  );
}

function AcceptForm({
  risk: { data: risk, etag },
  onClose,
  onDone,
  onStale,
}: AcceptRiskDialogProps): ReactElement {
  const { t, language } = useI18n();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction(riskProblemMessage);
  const [values, setValues] = useState<AcceptanceValues>({ expiresOn: '', rationale: '' });
  const fields = useFieldErrors(checkAcceptance(values, todayUtc()), formRef, riskFieldMessage);

  const set = (field: keyof AcceptanceValues) => (value: string) => {
    setValues((current) => ({ ...current, [field]: value }));
    fields.clearServer();
  };

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (!fields.attempt()) {
      return;
    }
    const result = await save.run(
      async () => (await risksApi.accept(risk.id, toAcceptCommand(values, language), etag)).data,
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
      <p className="form__note">{t('risks.accept.intro')}</p>
      <FormAlert message={fields.summary ?? save.formError} />
      <TextField
        label={t('risks.fields.expiresOn')}
        name="expiresOn"
        type="date"
        required
        hint={t('risks.accept.expiresHint')}
        value={values.expiresOn}
        onChange={set('expiresOn')}
        error={fields.errorOf('expiresOn')}
      />
      <TextAreaField
        label={t('risks.fields.acceptanceRationale')}
        name="rationale"
        required
        maxLength={TEXT_LENGTH}
        value={values.rationale}
        onChange={set('rationale')}
        error={fields.errorOf('rationale')}
      />
      <div className="form__actions">
        <button type="submit" className="button button--primary" disabled={save.saving}>
          {save.saving ? t('common.states.saving') : t('risks.accept.confirm')}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
