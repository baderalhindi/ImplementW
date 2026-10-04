import { type ReactElement, type SyntheticEvent, useCallback } from 'react';

import { useSaveAction } from '@/features/identity-access/forms.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';

import { financialKpiApi } from '../api/financialKpiApi.ts';
import { MeasurementDetails } from '../components/MeasurementDetails.tsx';
import { financialKpiProblemMessage, isStale } from '../problems.ts';

interface PublishMeasurementDialogProps {
  open: boolean;
  measurementId: string;
  kpiName: string;
  unitLabel: string;
  personName: (id: string | null) => string;
  onClose: () => void;
  onDone: (notice: TranslationKey) => void;
  onStale: () => void;
}

/**
 * Publishing a submitted KPI value: AHDA's gate (ADR-013), never the person who recorded it (TASK-052 D-15). The
 * reviewer sees the value or its state, the target version it is pinned to and the rating against it.
 */
export function PublishMeasurementDialog(props: PublishMeasurementDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog
      open={props.open}
      title={t('financialKpi.publish.title', { kpi: props.kpiName })}
      onClose={props.onClose}
    >
      {props.open && <PublishBody {...props} />}
    </Dialog>
  );
}

function PublishBody({
  measurementId,
  unitLabel,
  personName,
  onClose,
  onDone,
  onStale,
}: PublishMeasurementDialogProps): ReactElement {
  const { t } = useI18n();
  const save = useSaveAction(financialKpiProblemMessage);
  const load = useCallback(
    (signal: AbortSignal) => financialKpiApi.measurement(measurementId, signal),
    [measurementId],
  );
  const measurement = useApiResource(load);

  if (measurement.data === undefined) {
    return measurement.loading ? (
      <LoadingState />
    ) : (
      <ErrorState
        message={financialKpiProblemMessage(measurement.error, t)}
        onRetry={measurement.reload}
      />
    );
  }
  const { data, etag } = measurement.data;

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    const result = await save.run(() => financialKpiApi.publishMeasurement(data.id, etag));
    if (result.ok) {
      onDone('financialKpi.done.valuePublished');
    } else if (isStale(result.error)) {
      onStale();
    }
  };

  return (
    <form className="form" noValidate onSubmit={(event) => void submit(event)}>
      <FormAlert message={save.formError} />
      <MeasurementDetails measurement={data} unitLabel={unitLabel} personName={personName} />
      <p className="form__note">{t('financialKpi.publish.consequence')}</p>
      <div className="form__actions">
        <button type="submit" className="button button--primary" disabled={save.saving}>
          {save.saving ? t('common.states.saving') : t('financialKpi.publish.confirm')}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
