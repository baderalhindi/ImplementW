import { type ReactElement, type SyntheticEvent, useEffect, useId, useRef, useState } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';

import { useSaveAction } from '@/features/identity-access/forms.ts';
import { useSession } from '@/features/identity-access/session/useSession.ts';
import { type ProjectDetail } from '@/features/projects/api/types.ts';
import { TEXT_LENGTH } from '@/features/projects/registration.ts';
import { useProjectLookups } from '@/features/projects/useProjectLookups.ts';
import { ApiError, type ApiResponse } from '@/shared/api/httpClient.ts';
import { type FieldCodes, serverCodesOf } from '@/shared/forms/useFieldErrors.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import {
  CheckboxField,
  ReadOnlyField,
  SelectField,
  TextAreaField,
  TextField,
} from '@/shared/ui/FormFields.tsx';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';

import { canRaiseChangeRequests } from './access.ts';
import { changeRequestsApi } from './api/changeRequestsApi.ts';
import {
  type ChangeRequestDetail,
  type ChangeType,
  type MaterialityAssessment,
} from './api/types.ts';
import {
  CHANGE_TYPES,
  changeRequestFormValuesOf,
  type ChangeRequestFormValues,
  checkDraft,
  checkForSubmission,
  emptyChangeRequestForm,
  isEditable,
  requiredImpactOf,
  toChangeRequestRequest,
} from './changeRequestRules.ts';
import { MaterialityPanel } from './components/MaterialityPanel.tsx';
import { changeRequestPath, projectChangeRequestsPath } from './paths.ts';
import { changeRequestFieldMessage, changeRequestProblemMessage, isForbidden } from './problems.ts';
import { useChangeRequestRecord, useOptionalProject } from './useChangeRequestData.ts';

/**
 * SCR-106 for a new request of the project named by `?projectId=`. The project is read for its title and profile; a
 * person who cannot read it still raises through the API, which decides (CHANGE_REQUEST_RAISE).
 */
export function CreateChangeRequestPage(): ReactElement {
  const { t } = useI18n();
  const [params] = useSearchParams();
  const projectId = params.get('projectId') ?? '';
  const project = useOptionalProject(projectId === '' ? null : projectId);

  if (projectId === '' || project.data === undefined) {
    return projectId !== '' && project.loading ? (
      <LoadingState />
    ) : (
      <>
        <PageHeader title={t('changeRequests.form.createTitle')} />
        {projectId === '' ? (
          <p className="state">{t('changeRequests.form.noProject')}</p>
        ) : (
          <ErrorState
            message={changeRequestProblemMessage(project.error, t)}
            onRetry={project.reload}
          />
        )}
      </>
    );
  }
  return <ChangeRequestForm project={project.data} projectId={projectId} existing={null} />;
}

/** SCR-106 for a DRAFT or RETURNED request: corrected, classified again and (re)submitted. */
export function EditChangeRequestPage(): ReactElement {
  const { t } = useI18n();
  const { changeRequestId = '' } = useParams();
  const record = useChangeRequestRecord(changeRequestId);
  const project = useOptionalProject(record.data?.data.projectId ?? null);

  if (record.data === undefined || project.loading) {
    return record.data === undefined && !record.loading ? (
      <>
        <PageHeader title={t('changeRequests.form.editTitle')} />
        {isForbidden(record.error) ? (
          <p className="state">{t('changeRequests.forbidden')}</p>
        ) : (
          <ErrorState
            message={changeRequestProblemMessage(record.error, t)}
            onRetry={record.reload}
          />
        )}
      </>
    ) : (
      <LoadingState />
    );
  }
  const request = record.data.data;
  if (!isEditable(request.status)) {
    return (
      <>
        <PageHeader title={t('changeRequests.form.editTitle')} />
        <p className="state">{t('changeRequests.form.notEditable')}</p>
        <p>
          <Link to={changeRequestPath(request.id)}>{t('changeRequests.actions.openDetail')}</Link>
        </p>
      </>
    );
  }
  return (
    <ChangeRequestForm
      project={project.data ?? null}
      projectId={request.projectId}
      existing={record.data}
    />
  );
}

/** What was last attempted, which decides how strictly the form is checked: a draft may be incomplete. */
type Attempt = 'none' | 'draft' | 'submission';

const FIELDS = [
  'changeType',
  'title',
  'justification',
  'costImpactSar',
  'scheduleImpactDays',
  'scopeImpact',
  'isContractualObligation',
  'requestedGovernanceProfileItemId',
] as const;

function sameValues(a: ChangeRequestFormValues, b: ChangeRequestFormValues): boolean {
  return FIELDS.every((field) => a[field] === b[field]);
}

interface ChangeRequestFormProps {
  /** The request's project; null when the person cannot read it (403 or 404). */
  project: ProjectDetail | null;
  projectId: string;
  /** null: a new request; otherwise the DRAFT or RETURNED request with its ETag. */
  existing: ApiResponse<ChangeRequestDetail> | null;
}

/**
 * SCR-106 Create Change Request (acceptance criterion 1): the change, its impacts, and — before it can be submitted — the
 * materiality classification the server computes for it, with the approval path that band enters. "Submit for review" is
 * enabled only while the classification shown is the one computed for exactly what is saved: any edit after it disables
 * submission until the request is classified again. The SPA computes no band (CHG-GP-08) and sends none (TASK-060 D-4).
 */
function ChangeRequestForm({ project, projectId, existing }: ChangeRequestFormProps): ReactElement {
  const { t, language } = useI18n();
  const { session } = useSession();
  const navigate = useNavigate();
  const formRef = useRef<HTMLFormElement>(null);
  const readyNoteId = useId();
  const lookups = useProjectLookups();
  const save = useSaveAction(changeRequestProblemMessage);
  const [saved, setSaved] = useState<ApiResponse<ChangeRequestDetail> | null>(existing);
  const initial =
    existing === null ? emptyChangeRequestForm() : changeRequestFormValuesOf(existing.data);
  const [values, setValues] = useState<ChangeRequestFormValues>(initial);
  const [savedValues, setSavedValues] = useState<ChangeRequestFormValues | null>(
    existing === null ? null : initial,
  );
  const [preview, setPreview] = useState<MaterialityAssessment | null>(null);
  const [attempt, setAttempt] = useState<Attempt>('none');
  const [serverCodes, setServerCodes] = useState<FieldCodes>({});
  const [focusRequest, setFocusRequest] = useState(0);

  useEffect(() => {
    if (focusRequest > 0) {
      formRef.current?.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus();
    }
  }, [focusRequest]);

  const request = saved?.data ?? null;
  const dirty = savedValues === null || !sameValues(values, savedValues);
  const classified = preview !== null && !dirty;
  const changeType = values.changeType;
  const requiredImpact = requiredImpactOf(changeType);
  const codes = attempt === 'submission' ? checkForSubmission(values) : checkDraft(values);
  const clientErrors = attempt === 'none' ? 0 : FIELDS.filter((field) => codes[field]).length;

  // A value of the wrong shape is flagged as it is typed; a missing one once saving or classifying was tried. The API's
  // refusals land on the field they name.
  const errorOf = (field: (typeof FIELDS)[number]): string | undefined => {
    const client = codes[field] ?? null;
    const code =
      client !== null && (client !== 'REQUIRED' || attempt !== 'none')
        ? client
        : (serverCodes[field] ?? null);
    return code === null ? undefined : changeRequestFieldMessage(field, code, t);
  };

  const set = <K extends keyof ChangeRequestFormValues>(
    field: K,
    value: ChangeRequestFormValues[K],
  ) => {
    setValues((current) => ({ ...current, [field]: value }));
    setServerCodes({});
  };

  /** Shows the client-side errors of a check; false when there are any. */
  const passes = (strictness: Exclude<Attempt, 'none'>): boolean => {
    setAttempt(strictness);
    const found = strictness === 'submission' ? checkForSubmission(values) : checkDraft(values);
    if (FIELDS.some((field) => found[field])) {
      setFocusRequest((current) => current + 1);
      return false;
    }
    return true;
  };

  /** Creates the request, or saves its changes; the saved request with its ETag, or null when refused. */
  const persist = async (): Promise<ApiResponse<ChangeRequestDetail> | null> => {
    if (saved !== null && !dirty) {
      return saved;
    }
    const body = toChangeRequestRequest(values, language, request);
    const result = await save.run(() =>
      saved === null
        ? changeRequestsApi.create({
            ...body,
            projectId,
            changeType: values.changeType as ChangeType,
          })
        : changeRequestsApi.update(saved.data.id, body, saved.etag),
    );
    if (!result.ok) {
      setServerCodes(serverCodesOf(result.error));
      setFocusRequest((current) => current + 1);
      return null;
    }
    setSaved(result.value);
    setSavedValues(values);
    return result.value;
  };

  const saveDraft = async () => {
    if (!passes('draft')) {
      return;
    }
    const persisted = await persist();
    if (persisted !== null) {
      void navigate(changeRequestPath(persisted.data.id), { state: { notice: 'saved' } });
    }
  };

  const classify = async () => {
    setPreview(null);
    if (!passes('submission')) {
      return;
    }
    const persisted = await persist();
    if (persisted === null) {
      return;
    }
    const result = await save.run(() => changeRequestsApi.previewMateriality(persisted.data.id));
    if (result.ok) {
      setPreview(result.value);
    }
  };

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (!classified || saved === null) {
      return;
    }
    const result = await save.run(() =>
      changeRequestsApi.command(saved.data.id, 'submit', saved.etag),
    );
    if (result.ok) {
      void navigate(changeRequestPath(saved.data.id), { state: { notice: 'submitted' } });
    } else {
      setServerCodes(serverCodesOf(result.error));
      if (result.error instanceof ApiError && result.error.fieldIssues.length > 0) {
        setPreview(null);
        setFocusRequest((current) => current + 1);
      }
    }
  };

  const profileOptions = lookups
    .options('GOVERNANCE_PROFILE')
    .filter((option) => option.value !== project?.governanceProfileItemId);
  const profilesUnreadable = lookups.catalogueError !== null;
  const editing = existing !== null;
  // Navigation, not protection: a new request is the project's Project Manager's (CHANGE_REQUEST_RAISE at OWN).
  const raiser =
    editing ||
    project === null ||
    (session !== null && canRaiseChangeRequests(session.user, project));

  return (
    <>
      <PageHeader
        title={t(editing ? 'changeRequests.form.editTitle' : 'changeRequests.form.createTitle')}
        description={
          project === null
            ? t('changeRequests.detail.projectUnreadable')
            : t('changeRequests.form.forProject', { project: project.title.text })
        }
      />
      <p>
        <Link
          to={
            editing && request !== null
              ? changeRequestPath(request.id)
              : projectChangeRequestsPath(projectId)
          }
        >
          {t(editing ? 'changeRequests.actions.backToDetail' : 'changeRequests.actions.backToList')}
        </Link>
      </p>
      {!raiser && <p className="form__note">{t('changeRequests.form.notRaiser')}</p>}
      {request?.status === 'RETURNED' && (
        <p className="form__note">
          {t('changeRequests.form.returnedNote', { revision: request.revisionNo + 1 })}
        </p>
      )}
      <form
        ref={formRef}
        className="form change-request-form"
        noValidate
        onSubmit={(event) => void submit(event)}
      >
        <FormAlert
          message={
            clientErrors > 0 ? t('common.form.fixErrors', { count: clientErrors }) : save.formError
          }
        />
        <fieldset className="form__section">
          <legend>{t('changeRequests.form.sections.change')}</legend>
          {editing && request !== null ? (
            <ReadOnlyField
              label={t('changeRequests.fields.changeType')}
              name="changeType"
              value={t(`changeRequests.changeType.${request.changeType}`)}
              hint={t('changeRequests.form.changeTypeFixed')}
            />
          ) : (
            <SelectField
              label={t('changeRequests.fields.changeType')}
              name="changeType"
              required
              value={values.changeType}
              placeholder={t('common.form.choose')}
              options={CHANGE_TYPES.map((type) => ({
                value: type,
                label: t(`changeRequests.changeType.${type}`),
              }))}
              hint={t('changeRequests.form.changeTypeHint')}
              onChange={(value) => {
                const type = CHANGE_TYPES.find((candidate) => candidate === value) ?? '';
                setValues((current) => ({
                  ...current,
                  changeType: type,
                  // A contractual-obligation change is flagged as one (TASK-060 D-12).
                  isContractualObligation:
                    type === 'CONTRACTUAL_OBLIGATION' ? true : current.isContractualObligation,
                }));
                setServerCodes({});
              }}
              error={errorOf('changeType')}
            />
          )}
          <TextField
            label={t('changeRequests.fields.title')}
            name="title"
            required
            dir="auto"
            value={values.title}
            onChange={(value) => {
              set('title', value);
            }}
            error={errorOf('title')}
          />
          <TextAreaField
            label={t('changeRequests.fields.justification')}
            name="justification"
            required
            maxLength={TEXT_LENGTH}
            hint={t('changeRequests.form.justificationHint')}
            value={values.justification}
            onChange={(value) => {
              set('justification', value);
            }}
            error={errorOf('justification')}
          />
        </fieldset>

        <fieldset className="form__section">
          <legend>{t('changeRequests.form.sections.impacts')}</legend>
          <p className="form__note">{t('changeRequests.form.impactsNote')}</p>
          <TextField
            label={t('changeRequests.fields.costImpactSar')}
            name="costImpactSar"
            dir="ltr"
            inputMode="decimal"
            required={requiredImpact === 'costImpactSar'}
            hint={t('changeRequests.form.costHint')}
            value={values.costImpactSar}
            onChange={(value) => {
              set('costImpactSar', value);
            }}
            error={errorOf('costImpactSar')}
          />
          <TextField
            label={t('changeRequests.fields.scheduleImpactDays')}
            name="scheduleImpactDays"
            dir="ltr"
            inputMode="numeric"
            required={requiredImpact === 'scheduleImpactDays'}
            hint={t('changeRequests.form.daysHint')}
            value={values.scheduleImpactDays}
            onChange={(value) => {
              set('scheduleImpactDays', value);
            }}
            error={errorOf('scheduleImpactDays')}
          />
          <TextAreaField
            label={t('changeRequests.fields.scopeImpact')}
            name="scopeImpact"
            rows={3}
            maxLength={TEXT_LENGTH}
            required={requiredImpact === 'scopeImpact'}
            hint={t('changeRequests.form.scopeHint')}
            value={values.scopeImpact}
            onChange={(value) => {
              set('scopeImpact', value);
            }}
            error={errorOf('scopeImpact')}
          />
          <CheckboxField
            label={t('changeRequests.fields.isContractualObligation')}
            name="isContractualObligation"
            required={requiredImpact === 'isContractualObligation'}
            hint={t('changeRequests.form.contractualHint')}
            checked={values.isContractualObligation}
            onChange={(checked) => {
              set('isContractualObligation', checked);
            }}
            error={errorOf('isContractualObligation')}
          />
          {changeType === 'GOVERNANCE_PROFILE' &&
            (profilesUnreadable ? (
              <p className="form__note">{t('changeRequests.form.profilesUnreadable')}</p>
            ) : (
              <SelectField
                label={t('changeRequests.fields.requestedGovernanceProfile')}
                name="requestedGovernanceProfileItemId"
                required
                value={values.requestedGovernanceProfileItemId}
                placeholder={lookups.loading ? t('common.states.loading') : t('common.form.choose')}
                options={profileOptions}
                hint={t('changeRequests.form.profileHint')}
                onChange={(value) => {
                  set('requestedGovernanceProfileItemId', value);
                }}
                error={errorOf('requestedGovernanceProfileItemId')}
              />
            ))}
        </fieldset>

        <section className="section materiality-step" aria-labelledby="materiality-step">
          <h2 id="materiality-step">{t('changeRequests.form.sections.materiality')}</h2>
          {classified && request !== null ? (
            <MaterialityPanel assessment={preview} impacts={request} headingLevel={3} />
          ) : (
            <p className="form__note">
              {preview === null
                ? t('changeRequests.form.materialityIntro')
                : t('changeRequests.form.materialityStale')}
            </p>
          )}
        </section>

        <p id={readyNoteId} className="form__note" data-field="submitReadiness">
          {classified
            ? t('changeRequests.form.readyToSubmit', {
                band: preview.resultingBandNo,
                revision:
                  request?.status === 'RETURNED'
                    ? request.revisionNo + 1
                    : (request?.revisionNo ?? 1),
              })
            : t('changeRequests.form.classifyFirst')}
        </p>
        <div className="form__actions">
          <button
            type="button"
            className="button"
            disabled={save.saving}
            onClick={() => void saveDraft()}
          >
            {t('changeRequests.actions.saveDraft')}
          </button>
          <button
            type="button"
            className={classified ? 'button' : 'button button--primary'}
            disabled={save.saving}
            onClick={() => void classify()}
          >
            {save.saving ? t('common.states.saving') : t('changeRequests.actions.classify')}
          </button>
          <button
            type="submit"
            className="button button--primary"
            disabled={!classified || save.saving}
            aria-describedby={readyNoteId}
          >
            {t('changeRequests.actions.submit')}
          </button>
        </div>
      </form>
    </>
  );
}
