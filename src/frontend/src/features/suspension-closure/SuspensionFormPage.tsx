import { type ReactElement, useEffect, useRef, useState } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';

import { useOptionalProject } from '@/features/change-requests/useChangeRequestData.ts';
import { useSaveAction } from '@/features/identity-access/forms.ts';
import { useSession } from '@/features/identity-access/session/useSession.ts';
import { type ProjectDetail } from '@/features/projects/api/types.ts';
import { TEXT_LENGTH } from '@/features/projects/registration.ts';
import { type ApiResponse } from '@/shared/api/httpClient.ts';
import { type FieldCodes, serverCodesOf } from '@/shared/forms/useFieldErrors.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { ReadOnlyField, TextAreaField, TextField } from '@/shared/ui/FormFields.tsx';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';

import { suspensionApi } from './api/suspensionApi.ts';
import { type SuspensionRequestDetail, type SuspensionRequestType } from './api/types.ts';
import { isEditable } from './governedRequest.ts';
import { projectSuspensionPath, suspensionRequestPath } from './paths.ts';
import {
  isForbidden,
  suspensionClosureFieldMessage,
  suspensionClosureProblemMessage,
} from './problems.ts';
import {
  checkSuspensionDraft,
  checkSuspensionSubmission,
  emptySuspensionForm,
  sameSuspensionValues,
  SUSPENSION_REQUEST_TYPES,
  type SuspensionField,
  suspensionErrorCount,
  suspensionFormValuesOf,
  type SuspensionFormValues,
  toSuspensionRequest,
} from './suspensionRules.ts';
import { useSuspensionRecord } from './useSuspensionClosureData.ts';

/**
 * SCR-109 for a new request of the project named by `?projectId=`, of the type named by `?type=` (a suspension by
 * default). A person who cannot read the project still raises through the API, which decides (SUSPENSION_RAISE).
 */
export function CreateSuspensionRequestPage(): ReactElement {
  const { t } = useI18n();
  const [params] = useSearchParams();
  const projectId = params.get('projectId') ?? '';
  const type =
    SUSPENSION_REQUEST_TYPES.find((candidate) => candidate === params.get('type')) ?? 'SUSPEND';
  const project = useOptionalProject(projectId === '' ? null : projectId);

  if (projectId === '' || project.data === undefined) {
    return projectId !== '' && project.loading ? (
      <LoadingState />
    ) : (
      <>
        <PageHeader title={t(`suspensionClosure.suspension.form.createTitle.${type}`)} />
        {projectId === '' ? (
          <p className="state">{t('suspensionClosure.noProject')}</p>
        ) : (
          <ErrorState
            message={suspensionClosureProblemMessage(project.error, t)}
            onRetry={project.reload}
          />
        )}
      </>
    );
  }
  return (
    <SuspensionForm project={project.data} projectId={projectId} type={type} existing={null} />
  );
}

/** SCR-109 for a DRAFT or RETURNED request: corrected and (re)submitted. */
export function EditSuspensionRequestPage(): ReactElement {
  const { t } = useI18n();
  const { suspensionRequestId = '' } = useParams();
  const record = useSuspensionRecord(suspensionRequestId);
  const project = useOptionalProject(record.data?.data.projectId ?? null);

  if (record.data === undefined || project.loading) {
    return record.data === undefined && !record.loading ? (
      <>
        <PageHeader title={t('suspensionClosure.suspension.form.editTitle')} />
        {isForbidden(record.error) ? (
          <p className="state">{t('suspensionClosure.suspension.forbidden')}</p>
        ) : (
          <ErrorState
            message={suspensionClosureProblemMessage(record.error, t)}
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
        <PageHeader title={t('suspensionClosure.suspension.form.editTitle')} />
        <p className="state">{t('suspensionClosure.notEditable')}</p>
        <p>
          <Link to={suspensionRequestPath(request.id)}>
            {t('suspensionClosure.actions.openDetail')}
          </Link>
        </p>
      </>
    );
  }
  return (
    <SuspensionForm
      project={project.data ?? null}
      projectId={request.projectId}
      type={request.requestType}
      existing={record.data}
    />
  );
}

/** What was last attempted, which decides how strictly the form is checked: a draft may lack its effective date. */
type Attempt = 'none' | 'draft' | 'submission';

interface SuspensionFormProps {
  /** The request's project; null when the person cannot read it (403 or 404). */
  project: ProjectDetail | null;
  projectId: string;
  type: SuspensionRequestType;
  /** null: a new request; otherwise the DRAFT or RETURNED request with its ETag. */
  existing: ApiResponse<SuspensionRequestDetail> | null;
}

/**
 * SCR-109 Create Suspension Request (and its resumption counterpart): the reason and dates, saved as a draft or
 * submitted. Submission needs an effective date from today on; a suspension may state when it expects to resume, which
 * is planning information only: nothing resumes on it (BR-SUS-031) — a resumption is its own request.
 */
function SuspensionForm({ project, projectId, type, existing }: SuspensionFormProps): ReactElement {
  const { t, language } = useI18n();
  const { session } = useSession();
  const navigate = useNavigate();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction(suspensionClosureProblemMessage);
  const [saved, setSaved] = useState<ApiResponse<SuspensionRequestDetail> | null>(existing);
  const initial = existing === null ? emptySuspensionForm() : suspensionFormValuesOf(existing.data);
  const [values, setValues] = useState<SuspensionFormValues>(initial);
  const [savedValues, setSavedValues] = useState<SuspensionFormValues | null>(
    existing === null ? null : initial,
  );
  const [attempt, setAttempt] = useState<Attempt>('none');
  const [serverCodes, setServerCodes] = useState<FieldCodes>({});
  const [focusRequest, setFocusRequest] = useState(0);

  useEffect(() => {
    if (focusRequest > 0) {
      formRef.current?.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus();
    }
  }, [focusRequest]);

  const request = saved?.data ?? null;
  const dirty = savedValues === null || !sameSuspensionValues(values, savedValues);
  const codes =
    attempt === 'submission'
      ? checkSuspensionSubmission(values, type)
      : checkSuspensionDraft(values, type);
  const clientErrors = attempt === 'none' ? 0 : suspensionErrorCount(codes);

  const errorOf = (field: SuspensionField): string | undefined => {
    const client = codes[field] ?? null;
    const code =
      client !== null && (client !== 'REQUIRED' || attempt !== 'none')
        ? client
        : (serverCodes[field] ?? null);
    return code === null ? undefined : suspensionClosureFieldMessage(field, code, t);
  };

  const set = (field: SuspensionField, value: string) => {
    setValues((current) => ({ ...current, [field]: value }));
    setServerCodes({});
  };

  const passes = (strictness: Exclude<Attempt, 'none'>): boolean => {
    setAttempt(strictness);
    const found =
      strictness === 'submission'
        ? checkSuspensionSubmission(values, type)
        : checkSuspensionDraft(values, type);
    if (suspensionErrorCount(found) > 0) {
      setFocusRequest((current) => current + 1);
      return false;
    }
    return true;
  };

  /** Creates the request, or saves its changes; the saved request with its ETag, or null when refused. */
  const persist = async (): Promise<ApiResponse<SuspensionRequestDetail> | null> => {
    if (saved !== null && !dirty) {
      return saved;
    }
    const body = toSuspensionRequest(values, type, language, request);
    const result = await save.run(() =>
      saved === null
        ? suspensionApi.create({ ...body, projectId, requestType: type })
        : suspensionApi.update(saved.data.id, body, saved.etag),
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
      void navigate(suspensionRequestPath(persisted.data.id), { state: { notice: 'saved' } });
    }
  };

  const submit = async () => {
    if (!passes('submission')) {
      return;
    }
    const persisted = await persist();
    if (persisted === null) {
      return;
    }
    const result = await save.run(() =>
      suspensionApi.command(persisted.data.id, 'submit', persisted.etag),
    );
    if (result.ok) {
      void navigate(suspensionRequestPath(persisted.data.id), { state: { notice: 'submitted' } });
    } else {
      setServerCodes(serverCodesOf(result.error));
      setFocusRequest((current) => current + 1);
    }
  };

  const editing = existing !== null;
  // Navigation, not protection: a new request is the project's Project Manager's (SUSPENSION_RAISE at OWN).
  const raiser = editing || project === null || project.projectManagerUserId === session?.user.id;

  return (
    <>
      <PageHeader
        title={t(
          editing
            ? 'suspensionClosure.suspension.form.editTitle'
            : `suspensionClosure.suspension.form.createTitle.${type}`,
        )}
        description={
          project === null
            ? t('suspensionClosure.projectUnreadable')
            : t('suspensionClosure.forProject', { project: project.title.text })
        }
      />
      <p>
        <Link
          to={
            editing && request !== null
              ? suspensionRequestPath(request.id)
              : projectSuspensionPath(projectId)
          }
        >
          {t(
            editing
              ? 'suspensionClosure.actions.backToDetail'
              : 'suspensionClosure.actions.backToList',
          )}
        </Link>
      </p>
      {!raiser && <p className="form__note">{t('suspensionClosure.notRaiser')}</p>}
      {request?.status === 'RETURNED' && (
        <p className="form__note">
          {t('suspensionClosure.returnedNote', { revision: request.revisionNo + 1 })}
        </p>
      )}
      <form
        ref={formRef}
        className="form"
        noValidate
        onSubmit={(event) => {
          event.preventDefault();
          void submit();
        }}
      >
        <FormAlert
          message={
            clientErrors > 0 ? t('common.form.fixErrors', { count: clientErrors }) : save.formError
          }
        />
        <fieldset className="form__section">
          <legend>{t('suspensionClosure.suspension.form.section')}</legend>
          <ReadOnlyField
            label={t('suspensionClosure.suspension.fields.requestType')}
            name="requestType"
            value={t(`suspensionClosure.suspension.type.${type}`)}
            hint={t(`suspensionClosure.suspension.form.typeHint.${type}`)}
          />
          <TextAreaField
            label={t('suspensionClosure.suspension.fields.reason')}
            name="reason"
            required
            maxLength={TEXT_LENGTH}
            value={values.reason}
            onChange={(value) => {
              set('reason', value);
            }}
            error={errorOf('reason')}
          />
          <TextField
            label={t('suspensionClosure.suspension.fields.requestedEffectiveDate')}
            name="requestedEffectiveDate"
            type="date"
            dir="ltr"
            required
            hint={t('suspensionClosure.suspension.form.effectiveHint')}
            value={values.requestedEffectiveDate}
            onChange={(value) => {
              set('requestedEffectiveDate', value);
            }}
            error={errorOf('requestedEffectiveDate')}
          />
          {type === 'SUSPEND' && (
            <TextField
              label={t('suspensionClosure.suspension.fields.plannedResumptionDate')}
              name="plannedResumptionDate"
              type="date"
              dir="ltr"
              hint={t('suspensionClosure.suspension.form.plannedHint')}
              value={values.plannedResumptionDate}
              onChange={(value) => {
                set('plannedResumptionDate', value);
              }}
              error={errorOf('plannedResumptionDate')}
            />
          )}
        </fieldset>
        <p className="form__note">{t(`suspensionClosure.suspension.form.submitNote.${type}`)}</p>
        <div className="form__actions">
          <button
            type="button"
            className="button"
            disabled={save.saving}
            onClick={() => void saveDraft()}
          >
            {t('suspensionClosure.actions.saveDraft')}
          </button>
          <button type="submit" className="button button--primary" disabled={save.saving}>
            {save.saving ? t('common.states.saving') : t('suspensionClosure.actions.submit')}
          </button>
        </div>
      </form>
    </>
  );
}
