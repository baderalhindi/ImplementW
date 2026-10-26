import { type ReactElement, type SyntheticEvent, useState } from 'react';

import { useSaveAction } from '@/features/identity-access/forms.ts';
import { fieldMessage } from '@/features/identity-access/problems.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { FormAlert } from '@/shared/ui/States.tsx';

import { isEditable } from '../access.ts';
import { projectsApi } from '../api/projectsApi.ts';
import { Detail } from '../components/Detail.tsx';
import { LocationFields } from '../components/LocationFields.tsx';
import { profileMandatoryFields } from '../governance.ts';
import { projectProblemMessage } from '../problems.ts';
import {
  checkRegistration,
  draftRequiredFields,
  hasErrors,
  type RegistrationField,
  type RegistrationValues,
  toRequest,
  valuesOf,
} from '../registration.ts';

import { useWorkspace } from './workspaceContext.ts';

/**
 * SCR-035 Project Location: the region, city and coordinates, changed here while the registrant holds the project.
 * The change is a PUT of the whole registration (R-5) with only the location replaced.
 *
 * No map is drawn: no map tile service is approved for the platform, and a public one would receive every project's
 * coordinates (F-5). The coordinates are shown as numbers until one is.
 */
export function LocationTab(): ReactElement {
  const { t } = useI18n();
  const { project, access, lookups } = useWorkspace();
  const [editing, setEditing] = useState(false);
  const editable = access.reached && isEditable(project.status);
  const none = t('common.values.none');

  if (editing) {
    return (
      <LocationForm
        onDone={() => {
          setEditing(false);
        }}
      />
    );
  }
  return (
    <>
      <dl className="details">
        <Detail term={t('projects.fields.region')}>
          {lookups.itemLabel(project.regionItemId) ?? none}
        </Detail>
        <Detail term={t('projects.fields.city')}>
          {lookups.itemLabel(project.cityItemId) ?? none}
        </Detail>
        <Detail term={t('projects.fields.coordinates')}>
          {project.latitude === null || project.longitude === null ? (
            none
          ) : (
            <span dir="ltr">
              {project.latitude}, {project.longitude}
            </span>
          )}
        </Detail>
      </dl>
      <p className="form__note">{t('projects.location.noMap')}</p>
      {editable && (
        <button
          type="button"
          className="button"
          onClick={() => {
            setEditing(true);
          }}
        >
          {t('projects.location.edit')}
        </button>
      )}
    </>
  );
}

function LocationForm({ onDone }: { onDone: () => void }): ReactElement {
  const { t, language } = useI18n();
  const { project, etag, lookups, reload, notify } = useWorkspace();
  const save = useSaveAction(projectProblemMessage);
  const [values, setValues] = useState<RegistrationValues>(valuesOf(project));
  const codes = checkRegistration(values, draftRequiredFields(values));

  const set = (field: RegistrationField, value: string) => {
    setValues((current) => ({ ...current, [field]: value }));
  };
  const errorOf = (field: RegistrationField) => {
    const code = codes[field];
    return code !== null && code !== undefined ? fieldMessage(code, t) : save.fieldErrors[field];
  };

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (hasErrors(codes)) {
      return;
    }
    const result = await save.run(() =>
      projectsApi.update(project.id, toRequest(values, language, project), etag),
    );
    if (result.ok) {
      notify({ tone: 'success', message: t('projects.done.locationUpdated') });
      reload();
      onDone();
    }
  };

  return (
    <form className="form" noValidate onSubmit={(event) => void submit(event)}>
      <FormAlert message={save.formError} />
      <LocationFields
        values={values}
        onChange={set}
        lookups={lookups}
        errorOf={errorOf}
        neededToSubmit={profileMandatoryFields(lookups.profiles, project.governanceProfileItemId)}
      />
      <div className="form__actions">
        <button
          type="submit"
          className="button button--primary"
          disabled={hasErrors(codes) || save.saving}
        >
          {save.saving ? t('common.states.saving') : t('common.actions.save')}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onDone}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
