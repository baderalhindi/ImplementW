import { type ReactElement, type SyntheticEvent, useState } from 'react';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { type PickedUser, UserPicker } from '@/features/identity-access/components/UserPicker.tsx';
import { checkText, useSaveAction } from '@/features/identity-access/forms.ts';
import { fieldMessage } from '@/features/identity-access/problems.ts';
import { narrativeRequest } from '@/features/progress/progressUpdate.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { TextAreaField } from '@/shared/ui/FormFields.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { projectsApi } from '../api/projectsApi.ts';
import { type ProjectDetail } from '../api/types.ts';
import { projectProblemMessage } from '../problems.ts';
import { TEXT_LENGTH } from '../registration.ts';

interface ChangeManagerDialogProps {
  open: boolean;
  project: ProjectDetail;
  etag: string | null;
  onClose: () => void;
  onChanged: () => void;
}

/**
 * Review round 7, item 2: assign or change the Project Manager, at any state but Closed. The new manager must hold the Project Manager role over the
 * project (the API checks); the project's open tasks and update requests pass to them, both managers are told, and the change is audited with its reason.
 */
export function ChangeManagerDialog(props: ChangeManagerDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog open={props.open} title={t('projects.changeManager.title')} onClose={props.onClose}>
      {props.open && <ChangeManagerBody {...props} />}
    </Dialog>
  );
}

function ChangeManagerBody({
  project,
  etag,
  onClose,
  onChanged,
}: ChangeManagerDialogProps): ReactElement {
  const { t, language } = useI18n();
  const save = useSaveAction(projectProblemMessage);
  const managerName = usePersonNames([project.projectManagerUserId]);
  const [manager, setManager] = useState<PickedUser | null>(null);
  const [reason, setReason] = useState('');
  const [attempted, setAttempted] = useState(false);
  const reasonCode = checkText(reason, { required: true, maxLength: TEXT_LENGTH });

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    setAttempted(true);
    if (manager === null || reasonCode !== null) {
      return;
    }
    const result = await save.run(() =>
      projectsApi.changeManager(project.id, manager.id, narrativeRequest(reason, language), etag),
    );
    if (result.ok) {
      onChanged();
    }
  };

  return (
    <form className="form" noValidate onSubmit={(event) => void submit(event)}>
      <p className="form__note">
        {project.projectManagerUserId === null
          ? t('projects.changeManager.none')
          : t('projects.changeManager.current', {
              name: managerName(project.projectManagerUserId),
            })}
      </p>
      <p className="form__note">{t('projects.changeManager.handover')}</p>
      <FormAlert message={save.formError} />
      <UserPicker
        label={t('projects.changeManager.newManager')}
        value={manager}
        onChange={setManager}
        required
        hint={t('projects.changeManager.newManagerHint')}
        error={
          save.fieldErrors.projectManagerUserId ??
          (attempted && manager === null ? fieldMessage('REQUIRED', t) : undefined)
        }
      />
      <TextAreaField
        label={t('projects.changeManager.reason')}
        name="reason"
        required
        rows={3}
        maxLength={TEXT_LENGTH}
        value={reason}
        onChange={setReason}
        error={
          save.fieldErrors['reason.text'] ??
          (attempted && reasonCode !== null ? fieldMessage(reasonCode, t) : undefined)
        }
      />
      <div className="form__actions">
        <button type="submit" className="button button--primary" disabled={save.saving}>
          {save.saving ? t('common.states.saving') : t('projects.changeManager.confirm')}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
