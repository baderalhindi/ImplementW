import {
  type ReactElement,
  type SyntheticEvent,
  useCallback,
  useId,
  useRef,
  useState,
} from 'react';
import { useNavigate } from 'react-router';

import { useFocusFirstError, useSaveAction } from '@/features/identity-access/forms.ts';
import { fieldMessage } from '@/features/identity-access/problems.ts';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import {
  RadioGroupField,
  SelectField,
  TextAreaField,
  AmountField,
  TextField,
} from '@/shared/ui/FormFields.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';
import { CancelLink } from '@/shared/ui/CancelLink.tsx';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { UserPicker } from '@/features/identity-access/components/UserPicker.tsx';

import { isInternal, ownEntityId } from '../access.ts';
import { projectsApi } from '../api/projectsApi.ts';
import { FUNDING_SOURCES, type ParticipationMode, type ProjectDetail } from '../api/types.ts';
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
import {
  checkStagePlan,
  hasStagePlanErrors,
  rowsOf,
  stagePlanChanged,
  type StagePlanRow,
  toEntries,
} from '../stagePlan/stagePlan.ts';
import { stagePlanApi, type StagePlanStage } from '../stagePlan/stagePlanApi.ts';
import { StagePlanFields } from '../stagePlan/StagePlanFields.tsx';
import { useApiResource } from '@/shared/api/useApiResource.ts';

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

  // Review round 7, item 1: the stage plan, pre-filled from the stages in force, saved with the registration.
  const projectId = existing?.project.id;
  const loadPlan = useCallback(
    async (
      signal: AbortSignal,
    ): Promise<{ stages: StagePlanStage[]; acceptsStartingPercent: boolean }> =>
      projectId === undefined
        ? { stages: await stagePlanApi.stages(signal), acceptsStartingPercent: false }
        : (await stagePlanApi.get(projectId, signal)).data,
    [projectId],
  );
  const plan = useApiResource(loadPlan);
  // The rows as edited; until the first edit, the plan as read.
  const [editedRows, setStageRows] = useState<StagePlanRow[] | null>(null);
  const stageRows = editedRows ?? (plan.data === undefined ? null : rowsOf(plan.data.stages));
  const acceptsStartingPercent = plan.data?.acceptsStartingPercent ?? false;
  const stageCodes = checkStagePlan(stageRows ?? [], {
    projectStart: values.plannedStartDate,
    projectEnd: values.plannedEndDate,
    requireDates: false,
    acceptsStartingPercent,
  });

  const internal = isInternal(user);
  const entityFixed = !internal && ownEntityId(user) !== null;
  const required = draftRequiredFields(values);
  const neededToSubmit = [
    ...SUBMISSION_REQUIRED_FIELDS,
    ...profileMandatoryFields(lookups.profiles, values.governanceProfileItemId),
  ];
  const codes = checkRegistration(values, required);
  const missing = missingFields(values, required);
  const ready = !hasErrors(codes) && !hasStagePlanErrors(stageCodes);

  const set = (field: RegistrationField, value: string) => {
    // An initiative belongs to one portfolio: another portfolio drops the initiative chosen under the last one.
    setValues((current) => ({
      ...current,
      [field]: value,
      ...(field === 'strategicPortfolioItemId' && value !== current.strategicPortfolioItemId
        ? { strategicInitiativeItemId: '' }
        : {}),
    }));
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
    if (!result.ok) {
      return;
    }
    const saved = result.value;
    let notice = existing === undefined ? 'created' : 'updated';
    if (
      stageRows !== null &&
      stageRows.length > 0 &&
      (existing === undefined ||
        stagePlanChanged(stageRows, plan.data?.stages ?? [], acceptsStartingPercent))
    ) {
      // The registration is saved; a plan the API refuses is said so on the project, where it is set again.
      try {
        await stagePlanApi.update(
          saved.data.id,
          {
            stages: toEntries(stageRows, acceptsStartingPercent),
            changeAuthorizationId: null,
            reason: null,
          },
          saved.etag,
        );
      } catch {
        notice = 'stagePlanNotSaved';
      }
    }
    void navigate(`/projects/${saved.data.id}`, { state: { notice } });
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

      {internal && (
        <fieldset className="form__section">
          <legend>{t('projects.form.sections.strategy')}</legend>
          <SelectField
            label={t('projects.fields.strategicPortfolio')}
            name="strategicPortfolioItemId"
            value={values.strategicPortfolioItemId}
            placeholder={t('common.values.none')}
            options={lookups.options('STRATEGIC_PORTFOLIO')}
            hint={t('projects.form.portfolioHint')}
            onChange={setter('strategicPortfolioItemId')}
            error={errorOf('strategicPortfolioItemId')}
          />
          <SelectField
            label={t('projects.fields.strategicInitiative')}
            name="strategicInitiativeItemId"
            value={values.strategicInitiativeItemId}
            placeholder={
              values.strategicPortfolioItemId === ''
                ? t('projects.form.choosePortfolioFirst')
                : t('common.values.none')
            }
            options={
              values.strategicPortfolioItemId === ''
                ? []
                : lookups
                    .options('STRATEGIC_INITIATIVE', values.strategicPortfolioItemId)
                    .filter(
                      (option) =>
                        lookups.parentOf(option.value) === values.strategicPortfolioItemId,
                    )
            }
            onChange={setter('strategicInitiativeItemId')}
            error={errorOf('strategicInitiativeItemId')}
          />
          <SelectField
            label={t('projects.fields.priority')}
            name="priorityItemId"
            value={values.priorityItemId}
            placeholder={t('common.values.none')}
            options={lookups.options('PRIORITY')}
            onChange={setter('priorityItemId')}
            error={errorOf('priorityItemId')}
          />
          <SelectField
            label={t('projects.fields.fundingSource')}
            name="fundingSource"
            value={values.fundingSource}
            placeholder={t('common.values.none')}
            options={FUNDING_SOURCES.map((source) => ({
              value: source,
              label: t(`projects.fundingSource.${source}`),
            }))}
            onChange={setter('fundingSource')}
            error={errorOf('fundingSource')}
          />
          <TextField
            label={t('projects.fields.legacyReference')}
            name="legacyReference"
            dir="ltr"
            value={values.legacyReference}
            hint={t('projects.form.legacyReferenceHint')}
            onChange={setter('legacyReference')}
            error={errorOf('legacyReference')}
          />
          <FollowUpOwnerField
            value={values.followUpOwnerUserId}
            onChange={setter('followUpOwnerUserId')}
            error={errorOf('followUpOwnerUserId')}
          />
        </fieldset>
      )}

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
        <AmountField
          label={t('projects.fields.registrationBudget')}
          name="registrationBudgetSar"
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

      {stageRows !== null && stageRows.length > 0 && (
        <fieldset className="form__section">
          <legend>{t('projects.form.sections.stagePlan')}</legend>
          <p className="form__note">{t('projects.stagePlan.intro')}</p>
          <StagePlanFields
            rows={stageRows}
            onChange={setStageRows}
            codes={stageCodes}
            acceptsStartingPercent={acceptsStartingPercent}
          />
        </fieldset>
      )}

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
        <CancelLink
          fallback={existing === undefined ? '/projects' : `/projects/${existing.project.id}`}
        />
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

/** The RPMO staff member who follows the project up: an active internal user, found by name, or none. */
function FollowUpOwnerField({
  value,
  onChange,
  error,
}: {
  value: string;
  onChange: (value: string) => void;
  error: string | undefined;
}): ReactElement {
  const { t } = useI18n();
  const personName = usePersonNames(value === '' ? [] : [value]);
  const [changing, setChanging] = useState(false);
  if (value !== '' && !changing) {
    return (
      <p className="form__readonly">
        <span className="field__label">{t('projects.fields.followUpOwner')}</span>
        {personName(value)}{' '}
        <button
          type="button"
          className="button button--quiet"
          onClick={() => {
            setChanging(true);
          }}
        >
          {t('projects.form.changeOwner')}
        </button>
        <button
          type="button"
          className="button button--quiet"
          onClick={() => {
            onChange('');
          }}
        >
          {t('projects.form.clearOwner')}
        </button>
      </p>
    );
  }
  return (
    <UserPicker
      label={t('projects.fields.followUpOwner')}
      value={null}
      userType="INTERNAL"
      hint={t('projects.form.followUpOwnerHint')}
      error={error}
      onChange={(user) => {
        onChange(user?.id ?? '');
        setChanging(false);
      }}
    />
  );
}
