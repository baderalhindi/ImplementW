import { type ReactElement, type SyntheticEvent, useState } from 'react';

import { useSaveAction } from '@/features/identity-access/forms.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { TextField } from '@/shared/ui/FormFields.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { projectsApi } from '../api/projectsApi.ts';
import { type ProjectDetail } from '../api/types.ts';
import { projectProblemMessage } from '../problems.ts';

interface DeleteProjectDialogProps {
  open: boolean;
  project: ProjectDetail;
  etag: string | null;
  onClose: () => void;
  onDeleted: () => void;
  /** A holder of PROJECT_DELETE: any state, and the data stays. */
  anyState: boolean;
}

/** What the person types to confirm: the project's number, else (a draft has none) its title. */
function confirmationText(project: ProjectDetail): string {
  return project.formalProjectId ?? project.legacyReference ?? project.title.text;
}

/**
 * MOD-001 Delete Draft, and deleting any project for a holder of PROJECT_DELETE (client review, item 4). Review round 8, point 06: it names the project
 * and its number, says what is removed, and is confirmed by typing the project's number — a draft, which has none, by its title. A draft another
 * record names is refused (409 PROJECT_IN_USE).
 */
export function DeleteProjectDialog(props: DeleteProjectDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog
      open={props.open}
      title={t(props.anyState ? 'projects.delete.anyTitle' : 'projects.delete.title')}
      onClose={props.onClose}
    >
      {props.open && <DeleteProjectBody {...props} />}
    </Dialog>
  );
}

function DeleteProjectBody({
  project,
  etag,
  onClose,
  onDeleted,
  anyState,
}: DeleteProjectDialogProps): ReactElement {
  const { t } = useI18n();
  const save = useSaveAction(projectProblemMessage);
  const [typed, setTyped] = useState('');
  const expected = confirmationText(project);
  const matches = typed.trim() === expected;

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (!matches) {
      return;
    }
    const result = await save.run(() => projectsApi.remove(project.id, etag));
    if (result.ok) {
      onDeleted();
    }
  };

  return (
    <form className="form" noValidate onSubmit={(event) => void submit(event)}>
      <p>
        <strong dir="auto">{project.title.text}</strong>
        {(project.formalProjectId ?? project.legacyReference) != null && (
          <span className="details__aside" dir="ltr">
            {project.formalProjectId ?? project.legacyReference}
          </span>
        )}
      </p>
      <p>
        {t(anyState ? 'projects.delete.anyBody' : 'projects.delete.body', {
          title: project.title.text,
        })}
      </p>
      <FormAlert message={save.formError} />
      <TextField
        label={t('projects.delete.typeToConfirm', { text: expected })}
        name="confirmProject"
        dir="auto"
        autoComplete="off"
        value={typed}
        onChange={setTyped}
        required
      />
      <div className="form__actions">
        <button type="submit" className="button button--danger" disabled={!matches || save.saving}>
          {save.saving
            ? t('common.states.saving')
            : t(anyState ? 'projects.delete.anyConfirm' : 'projects.delete.confirm')}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
