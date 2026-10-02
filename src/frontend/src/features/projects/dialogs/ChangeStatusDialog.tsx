import { type ReactElement, type SyntheticEvent, useState } from 'react';

import { useSaveAction } from '@/features/identity-access/forms.ts';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { RadioGroupField } from '@/shared/ui/FormFields.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { type StatusCommand } from '../access.ts';
import { projectsApi } from '../api/projectsApi.ts';
import { type ProjectDetail } from '../api/types.ts';
import { projectProblemMessage } from '../problems.ts';

const COMMAND_LABELS: Record<StatusCommand, TranslationKey> = {
  withdraw: 'projects.changeStatus.withdraw.label',
  startReview: 'projects.changeStatus.startReview.label',
  activate: 'projects.changeStatus.activate.label',
};

const COMMAND_CONSEQUENCES: Record<StatusCommand, TranslationKey> = {
  withdraw: 'projects.changeStatus.withdraw.consequence',
  startReview: 'projects.changeStatus.startReview.consequence',
  activate: 'projects.changeStatus.activate.consequence',
};

const COMMAND_NOTICES: Record<StatusCommand, TranslationKey> = {
  withdraw: 'projects.done.withdrawn',
  startReview: 'projects.done.reviewStarted',
  activate: 'projects.done.activated',
};

interface ChangeStatusDialogProps {
  open: boolean;
  project: ProjectDetail;
  etag: string | null;
  /** The commands the state and the person allow (access.ts statusCommands). */
  commands: StatusCommand[];
  onClose: () => void;
  onChanged: (notice: TranslationKey) => void;
}

/**
 * MOD-003 Change Status: one lifecycle command at a time, each its own API call (R-4), with what it does said before
 * it is sent. Nothing here sets a state directly: there is no "set status" operation, and a project becomes ACTIVE
 * only by the activate command (TASK-041 D-7).
 */
export function ChangeStatusDialog(props: ChangeStatusDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog open={props.open} title={t('projects.changeStatus.title')} onClose={props.onClose}>
      {props.open && props.commands.length > 0 && <ChangeStatusBody {...props} />}
    </Dialog>
  );
}

function run(command: StatusCommand, project: ProjectDetail, etag: string | null) {
  switch (command) {
    case 'withdraw':
      return projectsApi.withdraw(project.id, etag);
    case 'startReview':
      return projectsApi.startReview(project.id, etag);
    case 'activate':
      return projectsApi.activate(project.id, etag);
  }
}

function ChangeStatusBody({
  project,
  etag,
  commands,
  onClose,
  onChanged,
}: ChangeStatusDialogProps): ReactElement {
  const { t } = useI18n();
  const save = useSaveAction(projectProblemMessage);
  const [command, setCommand] = useState<StatusCommand | null>(
    commands.length === 1 ? (commands[0] ?? null) : null,
  );

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (command === null) {
      return;
    }
    const result = await save.run(() => run(command, project, etag));
    if (result.ok) {
      onChanged(COMMAND_NOTICES[command]);
    }
  };

  return (
    <form className="form" noValidate onSubmit={(event) => void submit(event)}>
      <FormAlert message={save.formError} />
      <RadioGroupField
        label={t('projects.changeStatus.command')}
        name="command"
        required
        value={command ?? ''}
        options={commands.map((value) => ({ value, label: t(COMMAND_LABELS[value]) }))}
        onChange={(value) => {
          setCommand(commands.find((candidate) => candidate === value) ?? null);
        }}
      />
      {command !== null && <p className="form__note">{t(COMMAND_CONSEQUENCES[command])}</p>}
      <div className="form__actions">
        <button
          type="submit"
          className="button button--primary"
          disabled={command === null || save.saving}
        >
          {save.saving ? t('common.states.saving') : t('projects.changeStatus.confirm')}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
