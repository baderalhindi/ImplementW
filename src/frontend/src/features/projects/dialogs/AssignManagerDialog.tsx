import { type ReactElement, type SyntheticEvent, useState } from 'react';
import { Link } from 'react-router';

import { type UserSummary } from '@/features/identity-access/api/types.ts';
import { UserPicker } from '@/features/identity-access/components/UserPicker.tsx';
import { useSaveAction } from '@/features/identity-access/forms.ts';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { RadioGroupField } from '@/shared/ui/FormFields.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { projectsApi } from '../api/projectsApi.ts';
import { type ProjectDetail } from '../api/types.ts';
import { profileMandatoryFields } from '../governance.ts';
import { projectProblemMessage } from '../problems.ts';
import {
  FIELD_LABELS,
  missingFields,
  SUBMISSION_REQUIRED_FIELDS,
  valuesOf,
} from '../registration.ts';
import { type ProjectLookups } from '../useProjectLookups.ts';

/** The Project Manager a submission names: the person submitting, the one named before, or someone found. */
type ManagerChoice = 'self' | 'current' | 'other';

interface AssignManagerDialogProps {
  open: boolean;
  user: SessionUser;
  project: ProjectDetail;
  etag: string | null;
  lookups: ProjectLookups;
  onClose: () => void;
  onSubmitted: () => void;
}

/**
 * MOD-002 Assign Project Manager: submitting the registration for AHDA's review names its Project Manager (TASK-041
 * D-9). Eligible is an R04 holder over the project, internal or of its delivering entity (ADR-013); the API decides
 * (422 PROJECT_MANAGER_INVALID). Submission is enabled only once the budget, both planned dates and the governance
 * profile's mandatory fields are filled (TASK-041 D-8, ADR-015).
 */
export function AssignManagerDialog(props: AssignManagerDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog open={props.open} title={t('projects.submit.title')} onClose={props.onClose}>
      {props.open && <AssignManagerBody {...props} />}
    </Dialog>
  );
}

function AssignManagerBody({
  user,
  project,
  etag,
  lookups,
  onClose,
  onSubmitted,
}: AssignManagerDialogProps): ReactElement {
  const { t } = useI18n();
  const save = useSaveAction(projectProblemMessage);
  const current = project.projectManagerUserId;
  const [choice, setChoice] = useState<ManagerChoice>(
    current !== null && current !== user.id ? 'current' : 'self',
  );
  const [other, setOther] = useState<UserSummary | null>(null);

  const missing = missingFields(valuesOf(project), [
    ...SUBMISSION_REQUIRED_FIELDS,
    ...profileMandatoryFields(lookups.profiles, project.governanceProfileItemId),
  ]);
  const managerId =
    choice === 'self' ? user.id : choice === 'current' ? current : (other?.id ?? null);
  const ready = missing.length === 0 && managerId !== null;

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (!ready) {
      return;
    }
    const result = await save.run(() => projectsApi.submit(project.id, managerId, etag));
    if (result.ok) {
      onSubmitted();
    }
  };

  const options = [
    { value: 'self', label: t('projects.submit.self', { name: user.displayName }) },
    ...(current !== null && current !== user.id
      ? [{ value: 'current', label: t('projects.submit.current') }]
      : []),
    { value: 'other', label: t('projects.submit.other') },
  ];

  return (
    <form className="form" noValidate onSubmit={(event) => void submit(event)}>
      <p>{t('projects.submit.body')}</p>
      {missing.length > 0 && (
        <div className="notice notice--warning" role="status">
          <p>{t('projects.overview.missingForSubmission')}</p>
          <ul>
            {missing.map((field) => (
              <li key={field}>{t(FIELD_LABELS[field])}</li>
            ))}
          </ul>
          <Link to={`/projects/${project.id}/edit`}>{t('projects.actions.edit')}</Link>
        </div>
      )}
      <FormAlert message={save.formError} />
      <RadioGroupField
        label={t('projects.fields.projectManager')}
        name="projectManager"
        required
        value={choice}
        options={options}
        hint={t('projects.submit.managerHint')}
        error={save.fieldErrors.projectManagerUserId}
        onChange={(value) => {
          setChoice(value === 'current' || value === 'other' ? value : 'self');
        }}
      />
      {choice === 'other' && (
        <UserPicker label={t('projects.submit.find')} value={other} onChange={setOther} required />
      )}
      <div className="form__actions">
        <button type="submit" className="button button--primary" disabled={!ready || save.saving}>
          {save.saving ? t('common.states.saving') : t('projects.submit.confirm')}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
