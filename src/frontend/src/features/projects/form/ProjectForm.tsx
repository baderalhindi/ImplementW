import { type ReactElement, type SyntheticEvent, useId, useRef, useState } from 'react';
import { Link, useNavigate } from 'react-router';

import { useFocusFirstError, useSaveAction } from '@/features/identity-access/forms.ts';
import { fieldMessage } from '@/features/identity-access/problems.ts';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { RadioGroupField, SelectField, TextAreaField, TextField } from '@/shared/ui/FormFields.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { isInternal, ownEntityId } from '../access.ts';
import { projectsApi } from '../api/projectsApi.ts';
import { type ParticipationMode, type ProjectDetail } from '../api/types.ts';
import { LocationFields } from '../components/LocationFields.tsx';
import { profileMandatoryFields } from '../governance.ts';
import { projectProblemMessage } from '../problems.ts';
import {
  checkRegistration,
  draftRequiredFields,
  EMPTY_REGISTRATION,
  FIELD_LABELS,
  hasErrors,
  missingFields,
  type RegistrationField,
  type RegistrationValues,
  SUBMISSION_REQUIRED_FIELDS,
  TEXT_LENGTH,
  toRequest,
  valuesOf,
} from '../registration.ts';
import { type ProjectLookups, useProjectLookups } from '../useProjectLookups.ts';

const PARTICIPATION_MODES: ParticipationMode[] = ['AHDA_MANAGED', 'ENTITY_MANAGED'];

/** The API names a narrative field's text `title.text`; the form names the input `title`. */
function serverError(
  fieldErrors: Partial<Record<string, string>>,
  field: RegistrationField,
): string | undefined {
  return fieldErrors[field] ?? fieldErrors[`${field}.text`];
}

/** An external user registers for their own entity (ADR-013), so a new form starts with it. */
function initialValues(user: SessionUser): RegistrationValues {
  const entity = ownEntityId(user);
  return entity === null
    ? EMPTY_REGISTRATION
    : { ...EMPTY_REGISTRATION, externalEntityId: entity, participationMode: 'ENTITY_MANAGED' };
}

/** Says which reference sets this person cannot read, so an unusable form is explained, not just empty (F-1). */
function UnreadableLookups({ lookups }: { lookups: ProjectLookups }): ReactElement | null {
  const { t } = useI18n();
  const missing: TranslationKey[] = [
    ...(lookups.catalogueError === null ? [] : ['projects.form.unreadable.catalogues' as const]),
    ...(lookups.organizationError === null
      ? []
      : ['projects.form.unreadable.organization' as const]),
  ];
  if (missing.length === 0) {
    return null;
  }
  return (
    <div className="notice notice--warning" role="status">
      <p>{t('projects.form.unreadable.title')}</p>
      <ul>
        {missing.map((key) => (
          <li key={key}>{t(key)}</li>
        ))}
      </ul>
    </div>
  );
}

interface ProjectFormProps {
  user: SessionUser;
  /** Absent on SCR-033 Create; the project and its ETag on SCR-034 Edit. */
  existing?: { project: ProjectDetail; etag: string | null };
}

/**
 * SCR-033 Create Project and SCR-034 Edit Project Draft. Saving is enabled once every field a draft needs is filled
 * and each value has the shape the API accepts (acceptance criterion 1); the API then checks the references
 * themselves and its refusals land on the same inputs. What submission adds — the budget, the planned dates and the
 * governance profile's mandatory fields (ADR-015) — is marked here and enforced by MOD-002.
 */
export function ProjectForm({ user, existing }: ProjectFormProps): ReactElement {
  const { t, language } = useI18n();
  const navigate = useNavigate();
  const formRef = useRef<HTMLFormElement>(null);
  const missingId = useId();
  const lookups = useProjectLookups();
  const save = useSaveAction(projectProblemMessage);
  useFocusFirstError(formRef, save.fieldErrors);
  const [values, setValues] = useState<RegistrationValues>(
    existing === undefined ? initialValues(user) : valuesOf(existing.project),
  );

  const internal = isInternal(user);
  const entityFixed = !internal && ownEntityId(user) !== null;
  const required = draftRequiredFields(values);
  const neededToSubmit = [
    ...SUBMISSION_REQUIRED_FIELDS,
    ...profileMandatoryFields(lookups.profiles, values.governanceProfileItemId),
  ];
  const codes = checkRegistration(values, required);
  const missing = missingFields(values, required);
  const ready = !hasErrors(codes);

  const set = (field: RegistrationField, value: string) => {
    setValues((current) => ({ ...current, [field]: value }));
  };
  const setter = (field: RegistrationField) => (value: string) => {
    set(field, value);
  };

  // A value of the wrong shape is flagged as it is typed. An empty required field is not: the save button stays
  // disabled and the note beside it lists what is still needed. The API's refusals land on the field they name.
  const errorOf = (field: RegistrationField): string | undefined => {
    const code = codes[field];
    return code !== null && code !== undefined && code !== 'REQUIRED'
      ? fieldMessage(code, t)
      : serverError(save.fieldErrors, field);
  };
  const needed = (field: RegistrationField) =>
    neededToSubmit.includes(field) ? t('projects.form.neededToSubmit') : undefined;

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (!ready) {
      return;
    }
    const request = toRequest(values, language, existing?.project);
    const result = await save.run(() =>
      existing === undefined
        ? projectsApi.create(request)
        : projectsApi.update(existing.project.id, request, existing.etag),
    );
    if (result.ok) {
      void navigate(`/projects/${result.value.data.id}`, {
        state: { notice: existing === undefined ? 'created' : 'updated' },
      });
    }
  };

  return (
    <form ref={formRef} className="form" noValidate onSubmit={(event) => void submit(event)}>
      <UnreadableLookups lookups={lookups} />
      <FormAlert message={save.formError} />

      <fieldset className="form__section">
        <legend>{t('projects.form.sections.project')}</legend>
        <TextField
          label={t('projects.fields.title')}
          name="title"
          required
          dir="auto"
          value={values.title}
          onChange={setter('title')}
          error={errorOf('title')}
        />
        <TextAreaField
          label={t('projects.fields.description')}
          name="description"
          maxLength={TEXT_LENGTH}
          value={values.description}
          onChange={setter('description')}
          hint={needed('description')}
          error={errorOf('description')}
        />
        <SelectField
          label={t('projects.fields.classification')}
          name="classificationItemId"
          required
          value={values.classificationItemId}
          placeholder={lookups.loading ? t('common.states.loading') : t('common.form.choose')}
          options={lookups.options('PROJECT_CLASSIFICATION')}
          hint={
            !lookups.loading &&
            lookups.catalogueError === null &&
            lookups.options('PROJECT_CLASSIFICATION').length === 0
              ? t('projects.form.nonePublished')
              : undefined
          }
          onChange={setter('classificationItemId')}
          error={errorOf('classificationItemId')}
        />
        <SelectField
          label={t('projects.fields.governanceProfile')}
          name="governanceProfileItemId"
          required
          value={values.governanceProfileItemId}
          placeholder={lookups.loading ? t('common.states.loading') : t('common.form.choose')}
          options={lookups.options('GOVERNANCE_PROFILE')}
          hint={t('projects.form.governanceProfileHint')}
          onChange={setter('governanceProfileItemId')}
          error={errorOf('governanceProfileItemId')}
        />
      </fieldset>

      <fieldset className="form__section">
        <legend>{t('projects.form.sections.organization')}</legend>
        <RadioGroupField
          label={t('projects.fields.participation')}
          name="participationMode"
          required
          value={values.participationMode}
          options={PARTICIPATION_MODES.map((mode) => ({
            value: mode,
            label: t(`projects.participationMode.${mode}`),
          }))}
          hint={t('projects.form.participationHint')}
          onChange={setter('participationMode')}
          error={errorOf('participationMode')}
        />
        <SelectField
          label={t('projects.fields.department')}
          name="departmentId"
          required
          value={values.departmentId}
          placeholder={lookups.loading ? t('common.states.loading') : t('common.form.choose')}
          options={lookups.departmentOptions}
          hint={t('projects.form.departmentHint')}
          onChange={setter('departmentId')}
          error={errorOf('departmentId')}
        />
        {entityFixed ? (
          <p className="form__readonly">
            <span className="field__label">{t('projects.fields.externalEntity')}</span>
            {lookups.organizationError === null
              ? lookups.entityName(values.externalEntityId)
              : t('projects.form.ownEntity')}
          </p>
        ) : (
          <SelectField
            label={t('projects.fields.externalEntity')}
            name="externalEntityId"
            required={values.participationMode === 'ENTITY_MANAGED'}
            value={values.externalEntityId}
            placeholder={t('common.values.none')}
            options={lookups.entityOptions}
            hint={needed('externalEntityId')}
            onChange={setter('externalEntityId')}
            error={errorOf('externalEntityId')}
          />
        )}
      </fieldset>

      <fieldset className="form__section">
        <legend>{t('projects.form.sections.budgetSchedule')}</legend>
        <TextField
          label={t('projects.fields.registrationBudget')}
          name="registrationBudgetSar"
          dir="ltr"
          inputMode="decimal"
          value={values.registrationBudgetSar}
          hint={`${t('projects.form.budgetHint')} ${t('projects.form.neededToSubmit')}`}
          onChange={setter('registrationBudgetSar')}
          error={errorOf('registrationBudgetSar')}
        />
        <TextField
          label={t('projects.fields.plannedStartDate')}
          name="plannedStartDate"
          type="date"
          dir="ltr"
          value={values.plannedStartDate}
          hint={t('projects.form.neededToSubmit')}
          onChange={setter('plannedStartDate')}
          error={errorOf('plannedStartDate')}
        />
        <TextField
          label={t('projects.fields.plannedEndDate')}
          name="plannedEndDate"
          type="date"
          dir="ltr"
          value={values.plannedEndDate}
          hint={t('projects.form.neededToSubmit')}
          onChange={setter('plannedEndDate')}
          error={errorOf('plannedEndDate')}
        />
      </fieldset>

      <fieldset className="form__section">
        <legend>{t('projects.form.sections.location')}</legend>
        <LocationFields
          values={values}
          onChange={set}
          lookups={lookups}
          errorOf={errorOf}
          neededToSubmit={neededToSubmit}
        />
      </fieldset>

      <div className="form__actions">
        <button
          type="submit"
          className="button button--primary"
          disabled={!ready || save.saving}
          aria-describedby={ready ? undefined : missingId}
        >
          {save.saving
            ? t('common.states.saving')
            : existing === undefined
              ? t('projects.create.submit')
              : t('common.actions.save')}
        </button>
        <Link
          className="button"
          to={existing === undefined ? '/projects' : `/projects/${existing.project.id}`}
        >
          {t('common.actions.cancel')}
        </Link>
      </div>
      {!ready && (
        <p id={missingId} className="form__note">
          {missing.length > 0
            ? t('projects.form.stillNeeded', {
                fields: missing.map((field) => t(FIELD_LABELS[field])).join(', '),
              })
            : t('projects.form.correctValues')}
        </p>
      )}
    </form>
  );
}
