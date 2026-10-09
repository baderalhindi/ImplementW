import {
  type ReactElement,
  type SyntheticEvent,
  useCallback,
  useEffect,
  useRef,
  useState,
} from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';

import { useOptionalProject } from '@/features/change-requests/useChangeRequestData.ts';
import { externalEntitiesApi } from '@/features/identity-access/api/identityAccessApi.ts';
import { useSaveAction } from '@/features/identity-access/forms.ts';
import { useSession } from '@/features/identity-access/session/useSession.ts';
import { isInternal } from '@/features/projects/access.ts';
import { type ProjectDetail } from '@/features/projects/api/types.ts';
import { TEXT_LENGTH } from '@/features/projects/registration.ts';
import { useUserSearch } from '@/features/tasks/assignee.ts';
import { tasksApi } from '@/features/tasks/api/tasksApi.ts';
import { isLiveLeaf, todayUtc } from '@/features/tasks/taskRules.ts';
import { ApiError, type ApiResponse } from '@/shared/api/httpClient.ts';
import { isPublished, readCatalogueItems } from '@/shared/api/masterData.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { type FieldCodes, serverCodesOf } from '@/shared/forms/useFieldErrors.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import {
  ReadOnlyField,
  SelectField,
  type SelectOption,
  TextAreaField,
  TextField,
} from '@/shared/ui/FormFields.tsx';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';

import { canCreateRequest } from './access.ts';
import { externalRequestsApi } from './api/externalParticipationApi.ts';
import { type ExternalUpdateRequestDetail } from './api/types.ts';
import { PersonField } from './components/PersonField.tsx';
import { externalRequestPath, projectExternalRequestsPath } from './paths.ts';
import { isNotFound, participationProblemMessage, requestFieldMessage } from './problems.ts';
import {
  checkRequestForm,
  emptyRequestForm,
  REQUEST_FORM_FIELDS,
  type RequestFormValues,
  requestFormValuesOf,
  toRequestBody,
} from './requestForm.ts';
import { schemaNeedsTarget } from './rules.ts';
import { useRequestRecord } from './useExternalParticipationData.ts';

/** SCR-161 for a new request of the project named by `?projectId=`, from its workspace. */
export function CreateExternalRequestPage(): ReactElement {
  const { t } = useI18n();
  const [params] = useSearchParams();
  const projectId = params.get('projectId') ?? '';
  const project = useOptionalProject(projectId === '' ? null : projectId);

  if (projectId === '' || project.data === undefined) {
    return projectId !== '' && project.loading ? (
      <LoadingState />
    ) : (
      <>
        <PageHeader title={t('externalParticipation.form.createTitle')} />
        {projectId === '' ? (
          <p className="state">{t('externalParticipation.form.noProject')}</p>
        ) : (
          <ErrorState
            message={participationProblemMessage(project.error, t)}
            onRetry={project.reload}
          />
        )}
      </>
    );
  }
  return <RequestForm project={project.data} projectId={projectId} existing={null} />;
}

/** SCR-161 for a DRAFT: only a draft is edited (409 EXTERNAL_REQUEST_NOT_EDITABLE). */
export function EditExternalRequestPage(): ReactElement {
  const { t } = useI18n();
  const { requestId = '' } = useParams();
  const record = useRequestRecord(requestId);
  const project = useOptionalProject(record.data?.data.projectId ?? null);

  if (record.data === undefined || project.loading) {
    return record.data === undefined && !record.loading ? (
      <>
        <PageHeader title={t('externalParticipation.form.editTitle')} />
        {isNotFound(record.error) ? (
          <p className="state">{t('externalParticipation.detail.notFound')}</p>
        ) : (
          <ErrorState
            message={participationProblemMessage(record.error, t)}
            onRetry={record.reload}
          />
        )}
      </>
    ) : (
      <LoadingState />
    );
  }
  const request = record.data.data;
  if (request.status !== 'DRAFT') {
    return (
      <>
        <PageHeader title={t('externalParticipation.form.editTitle')} />
        <p className="state">{t('externalParticipation.form.notEditable')}</p>
        <p>
          <Link to={externalRequestPath(request.id)}>
            {t('externalParticipation.actions.openDetail')}
          </Link>
        </p>
      </>
    );
  }
  return (
    <RequestForm
      project={project.data ?? null}
      projectId={request.projectId}
      existing={record.data}
    />
  );
}

/** The CONTRIBUTION_TYPE items, PUBLISHED, with their codes; null without MASTER_DATA_VIEW (R01 only, TASK-038 F-1). */
async function readContributionTypes(signal: AbortSignal) {
  try {
    return (await readCatalogueItems(['CONTRIBUTION_TYPE'], signal)).CONTRIBUTION_TYPE.filter(
      isPublished,
    );
  } catch (error) {
    if (error instanceof ApiError && error.status === 403) {
      return null;
    }
    throw error;
  }
}

/** ACTIVE entities; null without ORGANIZATION_VIEW (R01 only). */
async function readActiveEntities(signal: AbortSignal) {
  try {
    return (await externalEntitiesApi.listAll(signal)).filter(
      (entity) => entity.status === 'ACTIVE',
    );
  } catch (error) {
    if (error instanceof ApiError && error.status === 403) {
      return null;
    }
    throw error;
  }
}

/** The project's live leaf tasks, the only ones that take a percentage; null when they cannot be read. */
function useProjectTasks(projectId: string) {
  const load = useCallback(
    async (signal: AbortSignal) => {
      try {
        return (await tasksApi.tasks(projectId, signal)).filter(isLiveLeaf);
      } catch (error) {
        if (error instanceof ApiError && (error.status === 403 || error.status === 404)) {
          return null;
        }
        throw error;
      }
    },
    [projectId],
  );
  return useApiResource(load);
}

interface RequestFormProps {
  /** The request's project; null when the person cannot read it (403 or 404). */
  project: ProjectDetail | null;
  projectId: string;
  /** null: a new request; otherwise the DRAFT with its ETag. */
  existing: ApiResponse<ExternalUpdateRequestDetail> | null;
}

/**
 * SCR-161 Create External Update Request (WF-13 §12.3): one project, one entity, one typed purpose from a published
 * contribution type, the source record its schema names, instructions, a due date, the entity's responder and AHDA's
 * reviewer. Saved as a DRAFT the entity cannot see, or saved and issued at once. A list the person may not read
 * (contribution types, entities, people, the project's tasks) is offered as an id instead; the API decides.
 */
function RequestForm({ project, projectId, existing }: RequestFormProps): ReactElement | null {
  const { t, language } = useI18n();
  const { session } = useSession();
  const navigate = useNavigate();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction(participationProblemMessage);
  const canSearch = useUserSearch() ?? false;
  const types = useApiResource(readContributionTypes);
  const entities = useApiResource(readActiveEntities);
  const tasks = useProjectTasks(projectId);
  const [saved, setSaved] = useState<ApiResponse<ExternalUpdateRequestDetail> | null>(existing);
  const [values, setValues] = useState<RequestFormValues>(() =>
    existing === null
      ? emptyRequestForm(
          project?.externalEntityId ?? null,
          session !== null && isInternal(session.user) ? session.user.id : null,
        )
      : requestFormValuesOf(existing.data),
  );
  const [attempt, setAttempt] = useState<'none' | 'draft' | 'issue'>('none');
  const [serverCodes, setServerCodes] = useState<FieldCodes>({});
  const [focusRequest, setFocusRequest] = useState(0);

  useEffect(() => {
    if (focusRequest > 0) {
      formRef.current?.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus();
    }
  }, [focusRequest]);

  if (session === null) {
    return null;
  }

  const editing = existing !== null;
  // The chosen type's code: from the catalogue, or the draft's own pinned schema while its type is unchanged.
  const typeCode =
    types.data?.find((item) => item.id === values.contributionTypeItemId)?.code ??
    (existing !== null && existing.data.contributionTypeItemId === values.contributionTypeItemId
      ? existing.data.contributionSchemaCode
      : null);
  // Unknown for a type given by id (the catalogue unreadable): the source is then offered, optional.
  const needsTarget =
    typeCode !== null
      ? schemaNeedsTarget(typeCode)
      : types.data === null && values.contributionTypeItemId !== ''
        ? undefined
        : false;
  const context = { needsTarget, canSearch, issuing: attempt === 'issue', today: todayUtc() };
  const codes = checkRequestForm(values, context);

  const errorOf = (field: (typeof REQUEST_FORM_FIELDS)[number]): string | undefined => {
    const client = codes[field] ?? null;
    const code =
      client !== null && (client !== 'REQUIRED' || attempt !== 'none')
        ? client
        : (serverCodes[field] ?? null);
    return code === null ? undefined : requestFieldMessage(field, code, t);
  };
  const clientErrors =
    attempt === 'none' ? 0 : REQUEST_FORM_FIELDS.filter((field) => codes[field]).length;

  const set = <K extends keyof RequestFormValues>(field: K, value: RequestFormValues[K]) => {
    setValues((current) => ({ ...current, [field]: value }));
    setServerCodes({});
  };

  /** Creates the draft or saves its changes; the saved request with its ETag, or null when refused. */
  const persist = async (): Promise<ApiResponse<ExternalUpdateRequestDetail> | null> => {
    const body = toRequestBody(values, context, language);
    const result = await save.run(() =>
      saved === null
        ? externalRequestsApi.create({
            ...body,
            projectId,
            externalEntityId: values.externalEntityId.trim().toLowerCase(),
          })
        : externalRequestsApi.update(saved.data.id, body, saved.etag),
    );
    if (!result.ok) {
      setServerCodes(serverCodesOf(result.error));
      setFocusRequest((current) => current + 1);
      return null;
    }
    setSaved(result.value);
    return result.value;
  };

  const run = async (event: SyntheticEvent, issuing: boolean) => {
    event.preventDefault();
    setAttempt(issuing ? 'issue' : 'draft');
    const found = checkRequestForm(values, { ...context, issuing });
    if (REQUEST_FORM_FIELDS.some((field) => found[field])) {
      setFocusRequest((current) => current + 1);
      return;
    }
    const persisted = await persist();
    if (persisted === null) {
      return;
    }
    if (!issuing) {
      void navigate(externalRequestPath(persisted.data.id), { state: { notice: 'saved' } });
      return;
    }
    const issued = await save.run(() =>
      externalRequestsApi.command(persisted.data.id, 'issue', persisted.etag),
    );
    if (issued.ok) {
      void navigate(externalRequestPath(persisted.data.id), { state: { notice: 'issued' } });
    } else {
      // Saved, not issued: the form stays, now editing the saved draft, with the refusal on the fields it names.
      setServerCodes(serverCodesOf(issued.error));
      setFocusRequest((current) => current + 1);
    }
  };

  const typeOptions: SelectOption[] | null =
    types.data === undefined || types.data === null
      ? null
      : types.data.map((item) => ({
          value: item.id,
          label: `${item.label[language]} (${item.code})`,
        }));
  const entityOptions: SelectOption[] | null =
    entities.data === undefined || entities.data === null
      ? null
      : entities.data.map((entity) => ({ value: entity.id, label: entity.name[language] }));
  const taskOptions: SelectOption[] | null =
    tasks.data === undefined || tasks.data === null
      ? null
      : tasks.data.map((task) => ({ value: task.id, label: task.title.text }));
  const creator = editing || project === null || canCreateRequest(session.user, project);
  const formError =
    clientErrors > 0 ? t('common.form.fixErrors', { count: clientErrors }) : save.formError;

  return (
    <>
      <PageHeader
        title={t(
          editing
            ? 'externalParticipation.form.editTitle'
            : 'externalParticipation.form.createTitle',
        )}
        description={
          project === null
            ? t('externalParticipation.form.projectUnreadable')
            : t('externalParticipation.form.forProject', { project: project.title.text })
        }
      />
      <p>
        <Link
          to={
            saved === null
              ? projectExternalRequestsPath(projectId)
              : externalRequestPath(saved.data.id)
          }
        >
          {t(
            saved === null
              ? 'externalParticipation.actions.backToProject'
              : 'externalParticipation.actions.openDetail',
          )}
        </Link>
      </p>
      {!creator && <p className="form__note">{t('externalParticipation.form.notCreator')}</p>}
      <p className="form__note">{t('externalParticipation.form.draftNote')}</p>
      <form
        ref={formRef}
        className="form form--wide"
        noValidate
        onSubmit={(event) => void run(event, false)}
      >
        <FormAlert message={formError} />
        {editing ? (
          <ReadOnlyField
            label={t('externalParticipation.fields.entity')}
            name="externalEntityId"
            value={
              entities.data?.find((entity) => entity.id === values.externalEntityId)?.name[
                language
              ] ?? values.externalEntityId
            }
            hint={t('externalParticipation.form.entityFixed')}
          />
        ) : (
          <ReferenceField
            label={t('externalParticipation.fields.entity')}
            name="externalEntityId"
            options={entityOptions}
            loading={entities.loading}
            value={values.externalEntityId}
            required
            hint={t('externalParticipation.form.entityHint')}
            idHint={t('externalParticipation.form.entityIdHint')}
            error={errorOf('externalEntityId')}
            onChange={(value) => {
              set('externalEntityId', value);
            }}
          />
        )}
        <ReferenceField
          label={t('externalParticipation.fields.contributionType')}
          name="contributionTypeItemId"
          options={typeOptions}
          loading={types.loading}
          value={values.contributionTypeItemId}
          required
          hint={t('externalParticipation.form.typeHint')}
          idHint={t('externalParticipation.form.typeIdHint')}
          error={errorOf('contributionTypeItemId')}
          onChange={(value) => {
            set('contributionTypeItemId', value);
          }}
        />
        {needsTarget !== false && (
          <ReferenceField
            label={t('externalParticipation.fields.source')}
            name="targetId"
            options={taskOptions}
            loading={tasks.loading}
            value={values.targetId}
            required={needsTarget === true}
            hint={t(
              needsTarget === true
                ? 'externalParticipation.form.taskHint'
                : 'externalParticipation.form.sourceUnknownHint',
            )}
            idHint={t('externalParticipation.form.taskIdHint')}
            error={errorOf('targetId')}
            onChange={(value) => {
              set('targetId', value);
            }}
          />
        )}
        <TextAreaField
          label={t('externalParticipation.fields.instructions')}
          name="instructions"
          value={values.instructions}
          required
          maxLength={TEXT_LENGTH}
          hint={t('externalParticipation.form.instructionsHint')}
          error={errorOf('instructions')}
          onChange={(value) => {
            set('instructions', value);
          }}
        />
        <TextField
          label={t('externalParticipation.fields.dueDate')}
          name="dueDate"
          type="date"
          dir="ltr"
          value={values.dueDate}
          hint={t('externalParticipation.form.dueDateHint')}
          error={errorOf('dueDate')}
          onChange={(value) => {
            set('dueDate', value);
          }}
        />
        <PersonField
          label={t('externalParticipation.fields.responder')}
          name="responsibleUserId"
          userType="EXTERNAL"
          user={session.user}
          value={values.responder}
          canSearch={canSearch}
          allowNone
          hint={t('externalParticipation.form.responderHint')}
          error={errorOf('responsibleUserId')}
          onChange={(value) => {
            set('responder', value);
          }}
        />
        <PersonField
          label={t('externalParticipation.fields.reviewer')}
          name="reviewerUserId"
          userType="INTERNAL"
          user={session.user}
          value={values.reviewer}
          canSearch={canSearch}
          allowNone
          hint={t('externalParticipation.form.reviewerHint')}
          error={errorOf('reviewerUserId')}
          onChange={(value) => {
            set('reviewer', value);
          }}
        />
        <div className="form__actions">
          <button type="submit" className="button" disabled={save.saving}>
            {save.saving ? t('common.states.saving') : t('externalParticipation.actions.saveDraft')}
          </button>
          <button
            type="button"
            className="button button--primary"
            disabled={save.saving}
            onClick={(event) => void run(event, true)}
          >
            {t('externalParticipation.actions.saveAndIssue')}
          </button>
        </div>
      </form>
    </>
  );
}

interface ReferenceFieldProps {
  label: string;
  name: string;
  /** The choices, or null when the person may not list them: an id is typed instead. */
  options: SelectOption[] | null;
  loading: boolean;
  value: string;
  required: boolean;
  hint: string;
  /** What to type when the list cannot be read, and where to find it. */
  idHint: string;
  error: string | undefined;
  onChange: (value: string) => void;
}

/** A reference chosen from a list the person may read, or given by its id when they may not. */
function ReferenceField({
  label,
  name,
  options,
  loading,
  value,
  required,
  hint,
  idHint,
  error,
  onChange,
}: ReferenceFieldProps): ReactElement {
  const { t } = useI18n();
  if (loading && options === null) {
    return (
      <LoadingState label={t('externalParticipation.form.loadingChoices', { field: label })} />
    );
  }
  return options === null ? (
    <TextField
      label={label}
      name={name}
      value={value}
      dir="ltr"
      required={required}
      hint={`${hint} ${idHint}`}
      error={error}
      onChange={onChange}
    />
  ) : (
    <SelectField
      label={label}
      name={name}
      value={value}
      options={options}
      placeholder={t('common.form.choose')}
      required={required}
      hint={hint}
      error={error}
      onChange={onChange}
    />
  );
}
