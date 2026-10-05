import {
  type ReactElement,
  type SyntheticEvent,
  useCallback,
  useEffect,
  useRef,
  useState,
} from 'react';

import { useSaveAction } from '@/features/identity-access/forms.ts';
import { TEXT_LENGTH } from '@/features/projects/registration.ts';
import { todayUtc } from '@/features/tasks/taskRules.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { RadioGroupField, TextAreaField, TextField } from '@/shared/ui/FormFields.tsx';
import { ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';

import { financialKpiApi } from '../api/financialKpiApi.ts';
import {
  type KpiMeasurementDetail,
  type KpiTargetVersionDetail,
  type ValueStatus,
} from '../api/types.ts';
import {
  checkKpiValueForm,
  type FormCodes,
  hasErrors,
  kpiValuesOf,
  type KpiValueValues,
  toKpiCreateRequest,
  toKpiRequest,
} from '../financialKpiRules.ts';
import { formatKpiValue } from '../presentation.ts';
import {
  financialKpiFieldMessage,
  financialKpiProblemMessage,
  isStale,
  serverCodesOf,
} from '../problems.ts';

interface KpiValueDialogProps {
  open: boolean;
  kpiAssignmentId: string;
  /** The DRAFT to edit, or null to record a new value. */
  measurementId: string | null;
  /** The target version in force, which a new value is pinned to; null when none is approved. */
  target: KpiTargetVersionDetail | null;
  kpiName: string;
  unitLabel: string;
  onClose: () => void;
  onDone: (notice: TranslationKey) => void;
  onStale: () => void;
}

/**
 * MOD-024 Update KPI Value: a period's value, or why there is none (Missing, Stale, Not applicable), its as-of date and
 * a narrative. A new value is pinned to the target version in force and rated against it for good; a DRAFT keeps the
 * version it was pinned to (TASK-052 D-13). A status other than Measured sends no value, never 0.
 */
export function KpiValueDialog(props: KpiValueDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog
      open={props.open}
      title={t('financialKpi.kpiValue.title', { kpi: props.kpiName })}
      onClose={props.onClose}
    >
      {props.open &&
        (props.measurementId === null ? (
          <KpiValueForm {...props} measurement={null} etag={null} />
        ) : (
          <DraftLoader {...props} measurementId={props.measurementId} />
        ))}
    </Dialog>
  );
}

/** A DRAFT is read again on its own, for the ETag its PUT sends back (R-21). */
function DraftLoader(props: KpiValueDialogProps & { measurementId: string }): ReactElement {
  const { t } = useI18n();
  const { measurementId } = props;
  const load = useCallback(
    (signal: AbortSignal) => financialKpiApi.measurement(measurementId, signal),
    [measurementId],
  );
  const draft = useApiResource(load);
  if (draft.data === undefined) {
    return draft.loading ? (
      <LoadingState />
    ) : (
      <ErrorState message={financialKpiProblemMessage(draft.error, t)} onRetry={draft.reload} />
    );
  }
  return <KpiValueForm {...props} measurement={draft.data.data} etag={draft.data.etag} />;
}

const STATUSES: ValueStatus[] = ['MEASURED', 'MISSING', 'STALE', 'NOT_APPLICABLE'];

function KpiValueForm({
  kpiAssignmentId,
  measurement,
  etag,
  target,
  unitLabel,
  onClose,
  onDone,
  onStale,
}: KpiValueDialogProps & {
  measurement: KpiMeasurementDetail | null;
  etag: string | null;
}): ReactElement {
  const { t, language } = useI18n();
  const today = todayUtc();
  const isNew = measurement === null;
  const formRef = useRef<HTMLFormElement>(null);
  const intent = useRef<'save' | 'submit'>('submit');
  const save = useSaveAction(financialKpiProblemMessage);
  const [values, setValues] = useState<KpiValueValues>(() => kpiValuesOf(measurement, today));
  const [attempted, setAttempted] = useState(false);
  const [serverCodes, setServerCodes] = useState<FormCodes>({});
  const [focusRequest, setFocusRequest] = useState(0);

  const codes = checkKpiValueForm(values, today, isNew);
  // The version the value is rated against: its own once recorded, the one in force for a new value.
  const pinned =
    measurement === null
      ? target === null
        ? null
        : { versionNo: target.versionNo, targetValue: target.targetValue }
      : { versionNo: measurement.targetVersionNo, targetValue: measurement.targetValue };

  useEffect(() => {
    if (focusRequest > 0) {
      formRef.current?.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus();
    }
  }, [focusRequest]);

  const set = (field: keyof KpiValueValues) => (value: string) => {
    setValues((current) => ({ ...current, [field]: value }));
    setServerCodes({});
  };

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
      const saved =
        measurement === null
          ? await financialKpiApi.recordMeasurement(
              toKpiCreateRequest(values, language, kpiAssignmentId),
            )
          : await financialKpiApi.saveMeasurement(
              measurement.id,
              toKpiRequest(values, language, measurement),
              etag,
            );
      if (submitting) {
        await financialKpiApi.submitMeasurement(saved.data.id, saved.etag);
      }
    });
    if (result.ok) {
      onDone(submitting ? 'financialKpi.done.valueSubmitted' : 'financialKpi.done.valueSaved');
    } else if (isStale(result.error)) {
      onStale();
    } else {
      setServerCodes(serverCodesOf(result.error));
      setFocusRequest((current) => current + 1);
    }
  };

  return (
    <form ref={formRef} className="form" noValidate onSubmit={(event) => void submit(event)}>
      {pinned === null ? (
        <p className="form-alert" role="alert">
          {t('financialKpi.kpiValue.noTarget')}
        </p>
      ) : (
        <p className="form__note" data-pinned-version={pinned.versionNo}>
          {t(isNew ? 'financialKpi.kpiValue.pinsTarget' : 'financialKpi.kpiValue.pinnedTarget', {
            version: pinned.versionNo,
            target: formatKpiValue(pinned.targetValue),
            unit: unitLabel,
          })}
        </p>
      )}
      <FormAlert
        message={
          invalidCount > 0 ? t('common.form.fixErrors', { count: invalidCount }) : save.formError
        }
      />
      {isNew ? (
        <>
          <TextField
            label={t('financialKpi.kpiValue.periodStart')}
            name="periodStart"
            type="date"
            required
            value={values.periodStart}
            onChange={set('periodStart')}
            error={errorOf('periodStart')}
          />
          <TextField
            label={t('financialKpi.kpiValue.periodEnd')}
            name="periodEnd"
            type="date"
            required
            value={values.periodEnd}
            onChange={set('periodEnd')}
            hint={t('financialKpi.kpiValue.periodHint')}
            error={errorOf('periodEnd')}
          />
        </>
      ) : (
        <p className="form__note">
          {t('financialKpi.kpiValue.period', { start: values.periodStart, end: values.periodEnd })}
        </p>
      )}
      <RadioGroupField
        label={t('financialKpi.kpiValue.valueStatus')}
        name="valueStatus"
        required
        value={values.valueStatus}
        options={STATUSES.map((status) => ({
          value: status,
          label: t(`financialKpi.kpiValue.status.${status}`),
        }))}
        error={errorOf('valueStatus')}
        onChange={set('valueStatus')}
      />
      {values.valueStatus === 'MEASURED' ? (
        <TextField
          label={t('financialKpi.kpiValue.value', { unit: unitLabel })}
          name="measuredValue"
          required
          value={values.value}
          onChange={set('value')}
          inputMode="decimal"
          dir="ltr"
          hint={t('financialKpi.kpiValue.valueHint')}
          error={errorOf('measuredValue')}
        />
      ) : (
        values.valueStatus !== '' && (
          <p className="form__note">
            {t('financialKpi.kpiValue.noValue', {
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
          disabled={save.saving || pinned === null}
          onClick={() => {
            intent.current = 'submit';
          }}
        >
          {save.saving ? t('common.states.saving') : t('financialKpi.update.saveAndSubmit')}
        </button>
        <button
          type="submit"
          className="button"
          disabled={save.saving || pinned === null}
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
