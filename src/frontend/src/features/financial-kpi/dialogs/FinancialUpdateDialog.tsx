import { type ReactElement, type SyntheticEvent, useEffect, useRef, useState } from 'react';

import { useSaveAction } from '@/features/identity-access/forms.ts';
import { TEXT_LENGTH } from '@/features/projects/registration.ts';
import { todayUtc } from '@/features/tasks/taskRules.ts';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { RadioGroupField, TextAreaField, TextField } from '@/shared/ui/FormFields.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { financialKpiApi } from '../api/financialKpiApi.ts';
import { type FinancialProgressUpdateDetail, type ValueStatus } from '../api/types.ts';
import {
  checkFinancialUpdate,
  financialValuesOf,
  type FinancialUpdateValues,
  type FormCodes,
  hasErrors,
  SOURCE_REFERENCE_LENGTH,
  toFinancialRequest,
} from '../financialKpiRules.ts';
import {
  financialKpiFieldMessage,
  financialKpiProblemMessage,
  isStale,
  serverCodesOf,
} from '../problems.ts';

interface FinancialUpdateDialogProps {
  open: boolean;
  update: FinancialProgressUpdateDetail;
  etag: string | null;
  periodLabel: string;
  /** The fields whose source is INTEGRATED: no figure for them is taken by hand (TASK-052 D-8). */
  integrated: { actual: boolean; forecast: boolean };
  onClose: () => void;
  onDone: (notice: TranslationKey) => void;
  onStale: () => void;
}

/**
 * MOD-023 Update Financial Progress: a period's actual expenditure to date and forecast at completion, or why there is
 * none. The figures are SAR (no currency is chosen, ADR-008) and recorded as a manual entry with its as-of date and
 * reference (ADR-008 extended). A status other than Measured sends no figure, so nothing Unknown is ever stored as 0.
 */
export function FinancialUpdateDialog(props: FinancialUpdateDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog open={props.open} title={t('financialKpi.update.title')} onClose={props.onClose}>
      {props.open && <FinancialUpdateBody {...props} />}
    </Dialog>
  );
}

const STATUSES: ValueStatus[] = ['MEASURED', 'MISSING', 'STALE', 'NOT_APPLICABLE'];

function FinancialUpdateBody({
  update,
  etag,
  periodLabel,
  integrated,
  onClose,
  onDone,
  onStale,
}: FinancialUpdateDialogProps): ReactElement {
  const { t, language } = useI18n();
  const today = todayUtc();
  const formRef = useRef<HTMLFormElement>(null);
  const intent = useRef<'save' | 'submit'>('submit');
  const save = useSaveAction(financialKpiProblemMessage);
  const [values, setValues] = useState<FinancialUpdateValues>(() =>
    financialValuesOf(update, today),
  );
  const [attempted, setAttempted] = useState(false);
  const [serverCodes, setServerCodes] = useState<FormCodes>({});
  const [focusRequest, setFocusRequest] = useState(0);

  const codes = checkFinancialUpdate(values, today);
  const measured = values.valueStatus === 'MEASURED';

  useEffect(() => {
    if (focusRequest > 0) {
      formRef.current?.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus();
    }
  }, [focusRequest]);

  const set = (field: keyof FinancialUpdateValues) => (value: string) => {
    setValues((current) => ({ ...current, [field]: value }));
    setServerCodes({});
  };

  // A value of the wrong shape is flagged as it is typed; an empty required one once saving is tried. The API's
  // refusals land on the input they name.
  const errorOf = (field: string): string | undefined => {
    const own = codes[field] ?? null;
    const code =
      own !== null && (attempted || own !== 'REQUIRED') ? own : (serverCodes[field] ?? null);
    return code === null ? undefined : financialKpiFieldMessage(field, code, t);
  };
  const invalidCount = attempted
    ? Object.values(codes).filter((code) => typeof code === 'string').length
    : 0;

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (hasErrors(codes)) {
      setAttempted(true);
      setFocusRequest((current) => current + 1);
      return;
    }
    const submitting = intent.current === 'submit';
    const result = await save.run(async () => {
      const saved = await financialKpiApi.saveUpdate(
        update.id,
        toFinancialRequest(values, language, update),
        etag,
      );
      if (submitting) {
        await financialKpiApi.submitUpdate(update.id, saved.etag);
      }
    });
    if (result.ok) {
      onDone(submitting ? 'financialKpi.done.updateSubmitted' : 'financialKpi.done.updateSaved');
    } else if (isStale(result.error)) {
      onStale();
    } else {
      setServerCodes(serverCodesOf(result.error));
      setFocusRequest((current) => current + 1);
    }
  };

  const offered = integrated.actual ? STATUSES.filter((status) => status !== 'MEASURED') : STATUSES;

  return (
    <form ref={formRef} className="form" noValidate onSubmit={(event) => void submit(event)}>
      <p className="form__note">
        {t('financialKpi.update.period', { period: periodLabel, revision: update.revisionNo })}
      </p>
      <p className="form__note">{t('financialKpi.update.manual')}</p>
      <FormAlert
        message={
          invalidCount > 0 ? t('common.form.fixErrors', { count: invalidCount }) : save.formError
        }
      />
      {integrated.actual && (
        <p className="form__note">{t('financialKpi.update.actualIntegrated')}</p>
      )}
      <RadioGroupField
        label={t('financialKpi.update.valueStatus')}
        name="valueStatus"
        required
        value={values.valueStatus}
        options={offered.map((status) => ({
          value: status,
          label: t(`financialKpi.update.status.${status}`),
        }))}
        error={errorOf('valueStatus')}
        onChange={set('valueStatus')}
      />
      {measured ? (
        <>
          <TextField
            label={t('financialKpi.update.actual')}
            name="actual"
            required
            value={values.actual}
            onChange={set('actual')}
            inputMode="decimal"
            dir="ltr"
            hint={t('financialKpi.update.amountHint')}
            error={errorOf('actualExpenditureToDateSar')}
          />
          {integrated.forecast ? (
            <p className="form__note">{t('financialKpi.update.forecastIntegrated')}</p>
          ) : (
            <TextField
              label={t('financialKpi.update.forecast')}
              name="forecast"
              value={values.forecast}
              onChange={set('forecast')}
              inputMode="decimal"
              dir="ltr"
              hint={t('financialKpi.update.forecastHint')}
              error={errorOf('forecastAtCompletionSar')}
            />
          )}
        </>
      ) : (
        values.valueStatus !== '' && (
          <p className="form__note">
            {t('financialKpi.update.noFigure', {
              state: t(`financialKpi.value.${values.valueStatus}`),
            })}
          </p>
        )
      )}
      <TextField
        label={t('financialKpi.update.asOfDate')}
        name="asOfDate"
        type="date"
        required
        value={values.asOfDate}
        onChange={set('asOfDate')}
        hint={t('financialKpi.update.asOfHint')}
        error={errorOf('asOfDate')}
      />
      <TextField
        label={t('financialKpi.update.sourceReference')}
        name="sourceReference"
        value={values.sourceReference}
        onChange={set('sourceReference')}
        hint={t('financialKpi.update.sourceReferenceHint', { max: SOURCE_REFERENCE_LENGTH })}
        error={errorOf('sourceReference')}
      />
      <TextAreaField
        label={t('financialKpi.update.narrative')}
        name="narrative"
        rows={3}
        maxLength={TEXT_LENGTH}
        value={values.narrative}
        onChange={set('narrative')}
        error={errorOf('narrative')}
      />
      <div className="form__actions">
        <button
          type="submit"
          className="button button--primary"
          disabled={save.saving}
          onClick={() => {
            intent.current = 'submit';
          }}
        >
          {save.saving ? t('common.states.saving') : t('financialKpi.update.saveAndSubmit')}
        </button>
        <button
          type="submit"
          className="button"
          disabled={save.saving}
          onClick={() => {
            intent.current = 'save';
          }}
        >
          {t('financialKpi.update.saveDraft')}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
