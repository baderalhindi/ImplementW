import { type ReactElement, type SyntheticEvent, useState } from 'react';

import { checkText, useSaveAction } from '@/features/identity-access/forms.ts';
import { fieldMessage } from '@/features/identity-access/problems.ts';
import { narrativeRequest } from '@/features/progress/progressUpdate.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { RadioGroupField, TextAreaField } from '@/shared/ui/FormFields.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { projectsApi } from '../api/projectsApi.ts';
import { PROGRESS_METHODS, type ProgressMethod, type ProjectDetail } from '../api/types.ts';
import { projectProblemMessage } from '../problems.ts';
import { TEXT_LENGTH } from '../registration.ts';

interface ProgressMethodDialogProps {
  open: boolean;
  project: ProjectDetail;
  etag: string | null;
  onClose: () => void;
  onChanged: () => void;
}

/**
 * Review round 8, point 02: "طريقة احتساب الإنجاز", the schedule or the stages. Before the first approved progress update it changes freely; after it,
 * the change needs a reason, which the project's history keeps.
 */
export function ProgressMethodDialog(props: ProgressMethodDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog open={props.open} title={t('projects.progressMethod.title')} onClose={props.onClose}>
      {props.open && <ProgressMethodBody {...props} />}
    </Dialog>
  );
}

function ProgressMethodBody({
  project,
  etag,
  onClose,
  onChanged,
}: ProgressMethodDialogProps): ReactElement {
  const { t, language } = useI18n();
  const save = useSaveAction(projectProblemMessage);
  const progress = project.indicators?.progress ?? null;
  const current = progress?.method ?? null;
  const [method, setMethod] = useState<ProgressMethod>(current ?? 'STAGES');
  const [reason, setReason] = useState('');
  const [attempted, setAttempted] = useState(false);
  const reasonRequired = progress?.methodChangeNeedsReason ?? false;
  const reasonCode = checkText(reason, { required: reasonRequired, maxLength: TEXT_LENGTH });
  const unchanged = method === current;

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    setAttempted(true);
    if (unchanged || reasonCode !== null) {
      return;
    }
    const result = await save.run(() =>
      projectsApi.setProgressMethod(
        project.id,
        method,
        reason.trim() === '' ? null : narrativeRequest(reason, language),
        etag,
      ),
    );
    if (result.ok) {
      onChanged();
    }
  };

  return (
    <form className="form" noValidate onSubmit={(event) => void submit(event)}>
      <FormAlert message={save.formError} />
      <RadioGroupField
        label={t('projects.progressMethod.label')}
        name="progressMethod"
        required
        value={method}
        onChange={(value) => {
          setMethod(value === 'SCHEDULE' ? 'SCHEDULE' : 'STAGES');
        }}
        options={PROGRESS_METHODS.map((value) => ({
          value,
          label: t(`projects.progressMethod.methods.${value}`),
        }))}
        hint={t(`projects.progressMethod.explain.${method}`)}
        error={
          save.fieldErrors.progressMethod ??
          (attempted && unchanged ? t('projects.progressMethod.unchanged') : undefined)
        }
      />
      <TextAreaField
        label={t('projects.progressMethod.reason')}
        name="reason"
        required={reasonRequired}
        rows={3}
        maxLength={TEXT_LENGTH}
        value={reason}
        onChange={setReason}
        hint={
          reasonRequired
            ? t('projects.progressMethod.reasonRequired')
            : t('projects.progressMethod.reasonOptional')
        }
        error={
          save.fieldErrors['reason.text'] ??
          save.fieldErrors.reason ??
          (attempted && reasonCode !== null ? fieldMessage(reasonCode, t) : undefined)
        }
      />
      <div className="form__actions">
        <button type="submit" className="button button--primary" disabled={save.saving}>
          {save.saving ? t('common.states.saving') : t('projects.progressMethod.confirm')}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
