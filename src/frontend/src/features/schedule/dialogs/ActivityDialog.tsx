import { type ReactElement, type SyntheticEvent, useCallback, useRef, useState } from 'react';

import { CODE_LENGTH, useSaveAction } from '@/features/identity-access/forms.ts';
import { TEXT_LENGTH } from '@/features/projects/registration.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { SelectField, TextField } from '@/shared/ui/FormFields.tsx';
import { ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';

import {
  type ActivityValues,
  activityValuesOf,
  checkActivity,
  emptyActivity,
  parentChoices,
  toActivityRequest,
} from '../activityForm.ts';
import { scheduleApi } from '../api/scheduleApi.ts';
import { type ScheduleActivityDetail, type ScheduleDependencyDetail } from '../api/types.ts';
import { activityLabel } from '../presentation.ts';
import { isStale, scheduleProblemMessage } from '../problems.ts';
import { useFieldErrors } from '../useFieldErrors.ts';

interface ActivityDialogProps {
  /** The activity to edit; null opens a new one. */
  activityId: string | null;
  open: boolean;
  projectId: string;
  activities: ScheduleActivityDetail[];
  dependencies: ScheduleDependencyDetail[];
  /** A new activity's requested start: the project's planned start, or today. */
  defaultStart: string;
  onClose: () => void;
  onDone: (created: boolean) => void;
  onStale: () => void;
}

/**
 * An activity's inputs on SCR-060: its place in the work breakdown, WBS code, name and, for a leaf, the requested start
 * and the duration in working days. The planned and forecast dates are calculated by the backend from these and the
 * dependencies, and shown, never typed (D-3). An edit is read again first, for the ETag its PUT requires.
 */
export function ActivityDialog(props: ActivityDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog
      open={props.open}
      title={t(
        props.activityId === null ? 'schedule.activity.createTitle' : 'schedule.activity.editTitle',
      )}
      onClose={props.onClose}
    >
      {props.open &&
        (props.activityId === null ? (
          <ActivityForm {...props} existing={null} etag={null} />
        ) : (
          <EditActivity {...props} activityId={props.activityId} />
        ))}
    </Dialog>
  );
}

function EditActivity(props: ActivityDialogProps & { activityId: string }): ReactElement {
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
  return <ActivityForm {...props} existing={activity.data.data} etag={activity.data.etag} />;
}

function ActivityForm({
  projectId,
  activities,
  dependencies,
  defaultStart,
  existing,
  etag,
  onClose,
  onDone,
  onStale,
}: ActivityDialogProps & {
  existing: ScheduleActivityDetail | null;
  etag: string | null;
}): ReactElement {
  const { t, language } = useI18n();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction(scheduleProblemMessage);
  const [values, setValues] = useState<ActivityValues>(() =>
    existing === null
      ? emptyActivity(activities.length + 1, defaultStart)
      : activityValuesOf(existing),
  );
  const fields = useFieldErrors(checkActivity(values), formRef);
  const parents = parentChoices(activities, dependencies, existing).map((activity) => ({
    value: activity.id,
    label: activityLabel(activity),
  }));

  const set = (field: keyof ActivityValues) => (value: string) => {
    setValues((current) => ({ ...current, [field]: value }));
    fields.clearServer();
  };

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (!fields.attempt()) {
      return;
    }
    const request = toActivityRequest(values, language, existing);
    const result = await save.run(() =>
      existing === null
        ? scheduleApi.createActivity({ ...request, projectId })
        : scheduleApi.updateActivity(existing.id, request, etag),
    );
    if (result.ok) {
      onDone(existing === null);
    } else if (isStale(result.error)) {
      onStale();
    } else {
      fields.showServer(result.error);
    }
  };

  return (
    <form ref={formRef} className="form" noValidate onSubmit={(event) => void submit(event)}>
      <p className="form__note">{t('schedule.activity.intro')}</p>
      {existing?.activityKind === 'SUMMARY' && (
        <p className="form__note">{t('schedule.activity.summaryNote')}</p>
      )}
      <FormAlert message={fields.summary ?? save.formError} />
      <SelectField
        label={t('schedule.activity.parent')}
        name="parentActivityId"
        value={values.parentActivityId}
        options={parents}
        placeholder={t('schedule.activity.noParent')}
        hint={t('schedule.activity.parentHint')}
        onChange={set('parentActivityId')}
        error={fields.errorOf('parentActivityId')}
      />
      <TextField
        label={t('schedule.activity.wbsCode')}
        name="wbsCode"
        required
        dir="ltr"
        value={values.wbsCode}
        hint={t('schedule.activity.wbsCodeHint', { max: CODE_LENGTH })}
        onChange={set('wbsCode')}
        error={fields.errorOf('wbsCode')}
      />
      <TextField
        label={t('schedule.activity.name')}
        name="name"
        required
        dir="auto"
        value={values.name}
        hint={t('schedule.activity.nameHint', { max: TEXT_LENGTH })}
        onChange={set('name')}
        error={fields.errorOf('name')}
      />
      <TextField
        label={t('schedule.activity.requestedStart')}
        name="requestedStartDate"
        type="date"
        required
        value={values.requestedStartDate}
        hint={t('schedule.activity.requestedStartHint')}
        onChange={set('requestedStartDate')}
        error={fields.errorOf('requestedStartDate')}
      />
      <TextField
        label={t('schedule.activity.duration')}
        name="plannedDurationDays"
        required
        inputMode="numeric"
        dir="ltr"
        value={values.plannedDurationDays}
        hint={t('schedule.activity.durationHint')}
        onChange={set('plannedDurationDays')}
        error={fields.errorOf('plannedDurationDays')}
      />
      <TextField
        label={t('schedule.activity.sortOrder')}
        name="sortOrder"
        inputMode="numeric"
        dir="ltr"
        value={values.sortOrder}
        hint={t('schedule.activity.sortOrderHint')}
        onChange={set('sortOrder')}
        error={fields.errorOf('sortOrder')}
      />
      <div className="form__actions">
        <button type="submit" className="button button--primary" disabled={save.saving}>
          {save.saving ? t('common.states.saving') : t('common.actions.save')}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
