import { type ReactElement, type SyntheticEvent, useCallback, useRef, useState } from 'react';

import { useSaveAction } from '@/features/identity-access/forms.ts';
import { type ProjectSummary } from '@/features/projects/api/types.ts';
import { activityLabel } from '@/features/schedule/presentation.ts';
import { type ApiResponse } from '@/shared/api/httpClient.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useFieldErrors } from '@/shared/forms/useFieldErrors.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { SelectField, TextField } from '@/shared/ui/FormFields.tsx';
import { ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';

import { milestonesApi } from '../api/milestonesApi.ts';
import { type ProjectMilestoneDetail } from '../api/types.ts';
import {
  checkMilestoneForm,
  EMPTY_MILESTONE_FORM,
  milestoneFormValuesOf,
  type MilestoneFormValues,
  toMilestoneRequest,
} from '../milestoneRules.ts';
import { isStale, milestoneFieldMessage, milestoneProblemMessage } from '../problems.ts';
import { type MilestoneLookups, useScheduleActivities } from '../useMilestoneData.ts';

interface MilestoneFormDialogProps {
  /** null: MOD-016 Create Milestone; an id: MOD-016 Edit Milestone. */
  milestoneId: string | null;
  project: ProjectSummary;
  lookups: MilestoneLookups;
  onClose: () => void;
  onDone: (milestone: ProjectMilestoneDetail) => void;
  onStale: () => void;
}

/**
 * MOD-016 Create/Edit Milestone: WF-03's side of the shared milestone (ICD-04) — its title, MILESTONE_CATEGORY item,
 * Current Forecast date, the schedule activity it completes, and its sort order. Its achievement is WF-05's, in MOD-019.
 */
export function MilestoneFormDialog(props: MilestoneFormDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog
      open
      title={t(
        props.milestoneId === null ? 'milestones.form.createTitle' : 'milestones.form.editTitle',
      )}
      onClose={props.onClose}
    >
      {props.milestoneId === null ? (
        <MilestoneForm {...props} editing={null} />
      ) : (
        <EditLoader {...props} milestoneId={props.milestoneId} />
      )}
    </Dialog>
  );
}

function EditLoader(props: MilestoneFormDialogProps & { milestoneId: string }): ReactElement {
  const { t } = useI18n();
  const { milestoneId } = props;
  const load = useCallback(
    (signal: AbortSignal) => milestonesApi.milestone(milestoneId, signal),
    [milestoneId],
  );
  const milestone = useApiResource(load);
  if (milestone.data === undefined) {
    return milestone.loading ? (
      <LoadingState />
    ) : (
      <ErrorState
        message={milestoneProblemMessage(milestone.error, t)}
        onRetry={milestone.reload}
      />
    );
  }
  return <MilestoneForm {...props} editing={milestone.data} />;
}

function MilestoneForm({
  project,
  lookups,
  editing,
  onClose,
  onDone,
  onStale,
}: MilestoneFormDialogProps & {
  editing: ApiResponse<ProjectMilestoneDetail> | null;
}): ReactElement {
  const { t, language } = useI18n();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction(milestoneProblemMessage);
  const activities = useScheduleActivities(project.id);
  const before = editing?.data ?? null;
  const [values, setValues] = useState<MilestoneFormValues>(() =>
    before === null ? EMPTY_MILESTONE_FORM : milestoneFormValuesOf(before),
  );
  const fields = useFieldErrors(checkMilestoneForm(values), formRef, milestoneFieldMessage);

  const set = (field: keyof MilestoneFormValues) => (value: string) => {
    setValues((current) => ({ ...current, [field]: value }));
    fields.clearServer();
  };

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (!fields.attempt()) {
      return;
    }
    const request = toMilestoneRequest(values, language, before);
    const result = await save.run(async () =>
      before === null
        ? (await milestonesApi.createMilestone({ ...request, projectId: project.id })).data
        : (await milestonesApi.updateMilestone(before.id, request, editing?.etag ?? null)).data,
    );
    if (result.ok) {
      onDone(result.value);
    } else if (isStale(result.error)) {
      onStale();
    } else {
      fields.showServer(result.error);
    }
  };

  // A milestone completes a live activity of the project's schedule (TASK-050 D-11); one it names already stays listed.
  const activityOptions = (activities.data ?? [])
    .filter(
      (activity) => activity.status !== 'CANCELLED' || activity.id === before?.scheduleActivityId,
    )
    .map((activity) => ({ value: activity.id, label: activityLabel(activity) }));
  // A category it already names stays listed, even if it is no longer PUBLISHED, so an edit does not drop it unseen.
  const categoryOptions = [
    ...lookups.categoryOptions,
    ...(values.milestoneCategoryItemId !== '' &&
    !lookups.categoryOptions.some((option) => option.value === values.milestoneCategoryItemId)
      ? [
          {
            value: values.milestoneCategoryItemId,
            label: lookups.itemLabel(values.milestoneCategoryItemId),
          },
        ]
      : []),
  ];

  return (
    <form ref={formRef} className="form" noValidate onSubmit={(event) => void submit(event)}>
      <p className="form__note">{t('milestones.form.intro')}</p>
      <FormAlert message={fields.summary ?? save.formError} />
      <TextField
        label={t('milestones.fields.title')}
        name="title"
        required
        value={values.title}
        onChange={set('title')}
        error={fields.errorOf('title')}
      />
      {lookups.readable || before !== null ? (
        <SelectField
          label={t('milestones.fields.category')}
          name="milestoneCategoryItemId"
          required
          value={values.milestoneCategoryItemId}
          options={categoryOptions}
          placeholder={t('milestones.form.chooseCategory')}
          hint={lookups.readable ? undefined : t('milestones.form.categoriesUnreadable')}
          onChange={set('milestoneCategoryItemId')}
          error={fields.errorOf('milestoneCategoryItemId')}
        />
      ) : (
        <p className="form__note">{t('milestones.form.categoriesUnreadableCreate')}</p>
      )}
      <TextField
        label={t('milestones.fields.forecastDate')}
        name="forecastDate"
        type="date"
        required
        hint={t('milestones.form.forecastHint')}
        value={values.forecastDate}
        onChange={set('forecastDate')}
        error={fields.errorOf('forecastDate')}
      />
      {activities.data === undefined ? (
        activities.loading && <LoadingState />
      ) : activities.data === null ? (
        <p className="form__note">{t('milestones.form.activitiesUnreadable')}</p>
      ) : (
        <SelectField
          label={t('milestones.fields.activity')}
          name="scheduleActivityId"
          value={values.scheduleActivityId}
          options={activityOptions}
          placeholder={t('milestones.form.noActivity')}
          hint={t('milestones.form.activityHint')}
          onChange={set('scheduleActivityId')}
          error={fields.errorOf('scheduleActivityId')}
        />
      )}
      <TextField
        label={t('milestones.fields.sortOrder')}
        name="sortOrder"
        inputMode="numeric"
        dir="ltr"
        hint={t('milestones.form.sortOrderHint')}
        value={values.sortOrder}
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
