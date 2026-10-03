import { type ReactElement, type SyntheticEvent, useCallback, useRef, useState } from 'react';

import { useSaveAction } from '@/features/identity-access/forms.ts';
import { Detail } from '@/features/projects/components/Detail.tsx';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { TextField } from '@/shared/ui/FormFields.tsx';
import { ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';

import { checkDate } from '../activityForm.ts';
import { scheduleApi } from '../api/scheduleApi.ts';
import { type ScheduleActivityDetail, type ScheduleDependencyDetail } from '../api/types.ts';
import { dayNumber, daySpan, startConstraint } from '../dependencyRules.ts';
import { activityLabel, constraintMessage } from '../presentation.ts';
import { isStale, scheduleProblemMessage } from '../problems.ts';
import { type FieldCodes, useFieldErrors } from '../useFieldErrors.ts';

interface ReforecastDialogProps {
  activityId: string | null;
  activities: ScheduleActivityDetail[];
  dependencies: ScheduleDependencyDetail[];
  onClose: () => void;
  onDone: () => void;
  onStale: () => void;
}

/**
 * A live leaf's Current Forecast, once the project has an ACTIVE baseline (D-5): when it is now expected to start and
 * finish. The Approved Baseline is shown beside it and does not move. The start is held to what the predecessors'
 * forecasts allow, the same rule a drag on SCR-045 obeys.
 */
export function ReforecastDialog(props: ReforecastDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog
      open={props.activityId !== null}
      title={t('schedule.reforecast.title')}
      onClose={props.onClose}
    >
      {props.activityId !== null && <ReforecastLoader {...props} activityId={props.activityId} />}
    </Dialog>
  );
}

function ReforecastLoader(props: ReforecastDialogProps & { activityId: string }): ReactElement {
  const { t } = useI18n();
  const { activityId } = props;
  const load = useCallback(
    (signal: AbortSignal) => scheduleApi.activity(activityId, signal),
    [activityId],
  );
  const activity = useApiResource(load);
  if (activity.loading) {
    return <LoadingState />;
  }
  if (activity.data === undefined) {
    return (
      <ErrorState message={scheduleProblemMessage(activity.error, t)} onRetry={activity.reload} />
    );
  }
  return <ReforecastForm {...props} activity={activity.data.data} etag={activity.data.etag} />;
}

function ReforecastForm({
  activity,
  etag,
  activities,
  dependencies,
  onClose,
  onDone,
  onStale,
}: ReforecastDialogProps & {
  activity: ScheduleActivityDetail;
  etag: string | null;
}): ReactElement {
  const { t } = useI18n();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction(scheduleProblemMessage);
  const [start, setStart] = useState(activity.forecastStartDate);
  const [finish, setFinish] = useState(activity.forecastFinishDate);

  const startCode = checkDate(start);
  const finishCode = checkDate(finish);
  const ordered = startCode === null && finishCode === null;
  const constraint = ordered
    ? startConstraint(
        activity.id,
        Math.max(1, daySpan(start, finish)),
        activities,
        dependencies,
        'forecast',
      )
    : null;
  const held = constraint !== null && dayNumber(start) < dayNumber(constraint.earliest);
  const codes: FieldCodes = {
    forecastStartDate: startCode ?? (held ? 'BEFORE_PREDECESSOR' : null),
    forecastFinishDate:
      finishCode ?? (ordered && dayNumber(finish) < dayNumber(start) ? 'DATE_BEFORE_START' : null),
  };
  const byId = new Map(activities.map((item) => [item.id, item]));
  const fields = useFieldErrors(
    codes,
    formRef,
    held ? { forecastStartDate: constraintMessage(constraint, byId, t) } : {},
  );

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (!fields.attempt()) {
      return;
    }
    const result = await save.run(() =>
      scheduleApi.reforecast(
        activity.id,
        { forecastStartDate: start, forecastFinishDate: finish },
        etag,
      ),
    );
    if (result.ok) {
      onDone();
    } else if (isStale(result.error)) {
      onStale();
    } else {
      fields.showServer(result.error);
    }
  };

  return (
    <form ref={formRef} className="form" noValidate onSubmit={(event) => void submit(event)}>
      <p className="form__note">{activityLabel(activity)}</p>
      <dl className="details">
        <Detail term={t('schedule.legend.baseline')}>
          {activity.baselineStartDate === null || activity.baselineFinishDate === null
            ? t('schedule.activities.notBaselined')
            : t('schedule.dates.range', {
                start: activity.baselineStartDate,
                finish: activity.baselineFinishDate,
              })}
        </Detail>
        <Detail term={t('schedule.activities.planned')}>
          {t('schedule.dates.range', {
            start: activity.plannedStartDate,
            finish: activity.plannedFinishDate,
          })}
        </Detail>
      </dl>
      <p className="form__note">{t('schedule.reforecast.intro')}</p>
      <FormAlert message={fields.summary ?? save.formError} />
      <TextField
        label={t('schedule.reforecast.start')}
        name="forecastStartDate"
        type="date"
        required
        value={start}
        onChange={(value) => {
          setStart(value);
          fields.clearServer();
        }}
        error={fields.errorOf('forecastStartDate')}
      />
      <TextField
        label={t('schedule.reforecast.finish')}
        name="forecastFinishDate"
        type="date"
        required
        value={finish}
        onChange={(value) => {
          setFinish(value);
          fields.clearServer();
        }}
        error={fields.errorOf('forecastFinishDate')}
      />
      <div className="form__actions">
        <button type="submit" className="button button--primary" disabled={save.saving}>
          {save.saving ? t('common.states.saving') : t('schedule.reforecast.confirm')}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
