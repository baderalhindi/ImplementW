import { type ReactElement, type SyntheticEvent, useRef, useState } from 'react';

import { checkText, UUID_PATTERN, useSaveAction } from '@/features/identity-access/forms.ts';
import { Detail } from '@/features/projects/components/Detail.tsx';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { TextField } from '@/shared/ui/FormFields.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { needsChangeAuthorization } from '../access.ts';
import { scheduleApi } from '../api/scheduleApi.ts';
import { type ProjectBaselineDetail, type ScheduleActivityDetail } from '../api/types.ts';
import { baselineName } from '../presentation.ts';
import { isLiveLeaf, latestFinish } from '../dependencyRules.ts';
import { isStale, scheduleProblemMessage } from '../problems.ts';
import { useFieldErrors } from '../useFieldErrors.ts';

interface SubmitBaselineDialogProps {
  baseline: ProjectBaselineDetail | null;
  active: ProjectBaselineDetail | null;
  activities: ScheduleActivityDetail[];
  onClose: () => void;
  onDone: (submitted: ProjectBaselineDetail) => void;
  onStale: () => void;
}

/**
 * MOD-017 Submit Baseline: a DRAFT or RETURNED candidate goes to AHDA's review through WF-11, the working schedule frozen
 * until it is decided, so what activates is what was reviewed (D-7, D-8). Under a governance profile that requires no
 * approval it becomes the ACTIVE baseline at once (ADR-015, D-10). The person is told both before sending: the profile's
 * switch is the API's to read.
 */
export function SubmitBaselineDialog(props: SubmitBaselineDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog
      open={props.baseline !== null}
      title={t('schedule.submit.title')}
      onClose={props.onClose}
    >
      {props.baseline !== null && <SubmitBaselineForm {...props} baseline={props.baseline} />}
    </Dialog>
  );
}

function SubmitBaselineForm({
  baseline,
  active,
  activities,
  onClose,
  onDone,
  onStale,
}: SubmitBaselineDialogProps & { baseline: ProjectBaselineDetail }): ReactElement {
  const { t } = useI18n();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction(scheduleProblemMessage);
  const [authorization, setAuthorization] = useState('');
  const rebaseline = needsChangeAuthorization(active);
  const fields = useFieldErrors(
    {
      changeAuthorizationId: rebaseline
        ? checkText(authorization, { required: true, pattern: UUID_PATTERN })
        : null,
    },
    formRef,
  );

  const leaves = activities.filter(isLiveLeaf);
  const plannedFinish = latestFinish(activities, 'plan');

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (!fields.attempt()) {
      return;
    }
    const result = await save.run(() =>
      scheduleApi.submitBaseline(baseline.id, rebaseline ? authorization.trim() : null),
    );
    if (result.ok) {
      onDone(result.value.data);
    } else if (isStale(result.error)) {
      onStale();
    } else {
      fields.showServer(result.error);
    }
  };

  return (
    <form ref={formRef} className="form" noValidate onSubmit={(event) => void submit(event)}>
      <dl className="details">
        <Detail term={t('schedule.baselines.candidate')}>{baselineName(baseline, t)}</Detail>
        <Detail term={t('schedule.submit.activities')}>{String(leaves.length)}</Detail>
        <Detail term={t('schedule.submit.plannedFinish')}>
          {plannedFinish ?? t('common.values.none')}
        </Detail>
        {active !== null && (
          <Detail term={t('schedule.submit.replaces')}>
            {t('schedule.summary.baselineFinish', {
              name: baselineName(active, t),
              date: active.baselineFinishDate,
            })}
          </Detail>
        )}
      </dl>
      <p className="form__note">{t('schedule.submit.consequence')}</p>
      <FormAlert message={fields.summary ?? save.formError} />
      {rebaseline && (
        <TextField
          label={t('schedule.submit.changeAuthorization')}
          name="changeAuthorizationId"
          required
          dir="ltr"
          value={authorization}
          hint={t('schedule.submit.changeAuthorizationHint')}
          onChange={(value) => {
            setAuthorization(value);
            fields.clearServer();
          }}
          error={fields.errorOf('changeAuthorizationId')}
        />
      )}
      <div className="form__actions">
        <button type="submit" className="button button--primary" disabled={save.saving}>
          {save.saving ? t('common.states.saving') : t('schedule.submit.confirm')}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
