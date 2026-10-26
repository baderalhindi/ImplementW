import { type ReactElement, type SyntheticEvent, useRef, useState } from 'react';

import { useSaveAction } from '@/features/identity-access/forms.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { RadioGroupField, SelectField, TextField } from '@/shared/ui/FormFields.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { scheduleApi } from '../api/scheduleApi.ts';
import {
  type ScheduleActivityDetail,
  type ScheduleDependencyDetail,
  type ScheduleDependencyType,
} from '../api/types.ts';
import { checkDependency, type DependencyValues, isLiveLeaf } from '../dependencyRules.ts';
import { activityCode, activityLabel } from '../presentation.ts';
import { isStale, scheduleProblemMessage } from '../problems.ts';
import { useFieldErrors } from '../useFieldErrors.ts';

const TYPES: ScheduleDependencyType[] = ['FS', 'SS', 'FF'];

const EMPTY: DependencyValues = {
  predecessorActivityId: '',
  successorActivityId: '',
  dependencyType: 'FS',
  lagDays: '0',
};

interface AddDependencyDialogProps {
  open: boolean;
  activities: ScheduleActivityDetail[];
  dependencies: ScheduleDependencyDetail[];
  onClose: () => void;
  onDone: () => void;
  onStale: () => void;
}

/**
 * MOD-015 Add Dependency: two live leaf activities (D-4), FS, SS or FF, and a lag of 0 or more working days. A
 * dependency that would close a cycle is refused here with the chain it would close, before any request is made
 * (acceptance criterion 2); the API refuses it too, for a cycle closed by someone else meanwhile.
 */
export function AddDependencyDialog(props: AddDependencyDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog open={props.open} title={t('schedule.dependency.title')} onClose={props.onClose}>
      {props.open && <AddDependencyBody {...props} />}
    </Dialog>
  );
}

function AddDependencyBody({
  activities,
  dependencies,
  onClose,
  onDone,
  onStale,
}: AddDependencyDialogProps): ReactElement {
  const { t } = useI18n();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction(scheduleProblemMessage);
  const [values, setValues] = useState<DependencyValues>(EMPTY);
  const { codes, cycle } = checkDependency(values, dependencies);

  const byId = new Map(activities.map((activity) => [activity.id, activity]));
  const chain =
    cycle === null
      ? null
      : [...cycle, cycle[0] ?? ''].map((id) => activityCode(id, byId)).join(' → ');
  const fields = useFieldErrors(codes, formRef, {
    ...(chain === null
      ? {}
      : { successorActivityId: t('schedule.dependency.circular', { chain }) }),
  });

  const leaves = activities.filter(isLiveLeaf).map((activity) => ({
    value: activity.id,
    label: activityLabel(activity),
  }));
  const set = (field: keyof DependencyValues) => (value: string) => {
    setValues((current) => ({ ...current, [field]: value }));
    fields.clearServer();
  };

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (!fields.attempt() || values.dependencyType === '') {
      return;
    }
    const dependencyType = values.dependencyType;
    const result = await save.run(() =>
      scheduleApi.createDependency({
        predecessorActivityId: values.predecessorActivityId,
        successorActivityId: values.successorActivityId,
        dependencyType,
        lagDays: values.lagDays.trim() === '' ? 0 : Number(values.lagDays),
      }),
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
      <p className="form__note">{t('schedule.dependency.intro')}</p>
      <FormAlert message={fields.summary ?? save.formError} />
      <SelectField
        label={t('schedule.dependency.predecessor')}
        name="predecessorActivityId"
        required
        value={values.predecessorActivityId}
        options={leaves}
        placeholder={t('common.form.choose')}
        onChange={set('predecessorActivityId')}
        error={fields.errorOf('predecessorActivityId')}
      />
      <SelectField
        label={t('schedule.dependency.successor')}
        name="successorActivityId"
        required
        value={values.successorActivityId}
        options={leaves}
        placeholder={t('common.form.choose')}
        onChange={set('successorActivityId')}
        error={fields.errorOf('successorActivityId')}
      />
      <RadioGroupField
        label={t('schedule.dependency.type')}
        name="dependencyType"
        required
        value={values.dependencyType}
        options={TYPES.map((type) => ({
          value: type,
          label: t(`schedule.dependencyType.${type}`),
        }))}
        onChange={set('dependencyType')}
        error={fields.errorOf('dependencyType')}
      />
      <TextField
        label={t('schedule.dependency.lag')}
        name="lagDays"
        value={values.lagDays}
        inputMode="numeric"
        dir="ltr"
        hint={t('schedule.dependency.lagHint')}
        onChange={set('lagDays')}
        error={fields.errorOf('lagDays')}
      />
      <div className="form__actions">
        <button type="submit" className="button button--primary" disabled={save.saving}>
          {save.saving ? t('common.states.saving') : t('schedule.dependency.confirm')}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
