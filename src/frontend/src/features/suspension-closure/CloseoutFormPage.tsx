import { type ReactElement, type SyntheticEvent, useEffect, useRef, useState } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';

import { useOptionalProject } from '@/features/change-requests/useChangeRequestData.ts';
import { useSaveAction } from '@/features/identity-access/forms.ts';
import { useSession } from '@/features/identity-access/session/useSession.ts';
import { type ProjectDetail } from '@/features/projects/api/types.ts';
import { TEXT_LENGTH } from '@/features/projects/registration.ts';
import { type ApiResponse } from '@/shared/api/httpClient.ts';
import { type FieldCodes, serverCodesOf } from '@/shared/forms/useFieldErrors.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { TextAreaField, TextField } from '@/shared/ui/FormFields.tsx';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';

import { closeoutApi, type CloseoutCaseOf, type CloseoutStage } from './api/closeoutApi.ts';
import { type ClosureCaseDetail, type CompletionCaseDetail } from './api/types.ts';
import {
  CLOSEOUT_STAGES,
  checkCloseoutForm,
  closeoutErrorCount,
  closeoutFormValuesOf,
  type CloseoutFormValues,
  emptyCloseoutForm,
  NARRATIVE_FIELDS,
  sameCloseoutValues,
  toClosureRequest,
  toCompletionRequest,
} from './closeoutRules.ts';
import { isEditable } from './governedRequest.ts';
import { closeoutCasePath, projectCloseoutPath } from './paths.ts';
import {
  isForbidden,
  suspensionClosureFieldMessage,
  suspensionClosureProblemMessage,
} from './problems.ts';
import { useCloseoutRecord } from './useSuspensionClosureData.ts';

function stageOf(value: string | null | undefined): CloseoutStage | null {
  return CLOSEOUT_STAGES.find((candidate) => candidate === value) ?? null;
}

/** SCR-112 for a new case of the project named by `?projectId=`, at the stage named by `?stage=`. */
export function CreateCloseoutCasePage(): ReactElement {
  const { t } = useI18n();
  const [params] = useSearchParams();
  const projectId = params.get('projectId') ?? '';
  const stage = stageOf(params.get('stage')) ?? 'completion';
  const project = useOptionalProject(projectId === '' ? null : projectId);

  if (projectId === '' || project.data === undefined) {
    return projectId !== '' && project.loading ? (
      <LoadingState />
    ) : (
      <>
        <PageHeader title={t(`suspensionClosure.closeout.form.createTitle.${stage}`)} />
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
    <CloseoutForm stage={stage} project={project.data} projectId={projectId} existing={null} />
  );
}

/** SCR-112 for a DRAFT or RETURNED case of either stage. */
export function EditCloseoutCasePage(): ReactElement {
  const { stage: stageParam, caseId = '' } = useParams();
  const stage = stageOf(stageParam);
  const { t } = useI18n();
  if (stage === null) {
    return <p className="state">{t('common.notFound.title')}</p>;
  }
  return <EditCloseoutCase key={`${stage}/${caseId}`} stage={stage} caseId={caseId} />;
}

function EditCloseoutCase({
  stage,
  caseId,
}: {
  stage: CloseoutStage;
  caseId: string;
}): ReactElement {
  const { t } = useI18n();
  const record = useCloseoutRecord(stage, caseId);
  const project = useOptionalProject(record.data?.data.projectId ?? null);

  if (record.data === undefined || project.loading) {
    return record.data === undefined && !record.loading ? (
      <>
        <PageHeader title={t('suspensionClosure.closeout.form.editTitle')} />
        {isForbidden(record.error) ? (
          <p className="state">{t('suspensionClosure.closeout.forbidden')}</p>
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
  const closeoutCase = record.data.data;
  if (!isEditable(closeoutCase.status)) {
    return (
      <>
        <PageHeader title={t('suspensionClosure.closeout.form.editTitle')} />
        <p className="state">{t('suspensionClosure.notEditable')}</p>
        <p>
          <Link to={closeoutCasePath(stage, closeoutCase.id)}>
            {t('suspensionClosure.actions.openDetail')}
          </Link>
        </p>
      </>
    );
  }
  return (
    <CloseoutForm
      stage={stage}
      project={project.data ?? null}
      projectId={closeoutCase.projectId}
      existing={record.data}
    />
  );
}

interface CloseoutFormProps {
  stage: CloseoutStage;
  /** The case's project; null when the person cannot read it (403 or 404). */
  project: ProjectDetail | null;
  projectId: string;
  existing: ApiResponse<CloseoutCaseOf[CloseoutStage]> | null;
}

/**
 * SCR-112 Create Closure Request, reused for both stages (WF-10 §14): Stage 1 raises the completion case of an ACTIVE
 * project, with the date its works actually finished; Stage 2 raises the closure case of a COMPLETED project, or of a
 * SUSPENDED one that closes without completion. The form saves the case as a draft; its readiness is evaluated and it is
 * submitted on its own page (SCR-113), so the person sees what still blocks it first.
 */
function CloseoutForm({ stage, project, projectId, existing }: CloseoutFormProps): ReactElement {
  const { t, language } = useI18n();
  const { session } = useSession();
  const navigate = useNavigate();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction(suspensionClosureProblemMessage);
  const initial = existing === null ? emptyCloseoutForm() : closeoutFormValuesOf(existing.data);
  const [values, setValues] = useState<CloseoutFormValues>(initial);
  const [tried, setTried] = useState(false);
  const [serverCodes, setServerCodes] = useState<FieldCodes>({});
  const [focusRequest, setFocusRequest] = useState(0);

  useEffect(() => {
    if (focusRequest > 0) {
      formRef.current?.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus();
    }
  }, [focusRequest]);

  const codes = checkCloseoutForm(values, stage);
  const clientErrors = tried ? closeoutErrorCount(codes) : 0;
  const narrativeField = NARRATIVE_FIELDS[stage];

  const errorOf = (field: keyof CloseoutFormValues): string | undefined => {
    const serverField = field === 'narrative' ? narrativeField : field;
    const code = codes[field] ?? serverCodes[serverField] ?? null;
    return code === null ? undefined : suspensionClosureFieldMessage(field, code, t);
  };

  const set = (field: keyof CloseoutFormValues, value: string) => {
    setValues((current) => ({ ...current, [field]: value }));
    setServerCodes({});
  };

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    setTried(true);
    if (closeoutErrorCount(codes) > 0) {
      setFocusRequest((current) => current + 1);
      return;
    }
    if (existing !== null && sameCloseoutValues(values, closeoutFormValuesOf(existing.data))) {
      void navigate(closeoutCasePath(stage, existing.data.id));
      return;
    }
    const result = await save.run(async () => {
      if (stage === 'completion') {
        const before = existing?.data as CompletionCaseDetail | undefined;
        const body = toCompletionRequest(values, language, before ?? null);
        return existing === null
          ? closeoutApi.create('completion', { ...body, projectId })
          : closeoutApi.update('completion', existing.data.id, body, existing.etag);
      }
      const before = existing?.data as ClosureCaseDetail | undefined;
      const body = toClosureRequest(values, language, before ?? null);
      return existing === null
        ? closeoutApi.create('closure', { ...body, projectId })
        : closeoutApi.update('closure', existing.data.id, body, existing.etag);
    });
    if (result.ok) {
      void navigate(closeoutCasePath(stage, result.value.data.id), { state: { notice: 'saved' } });
    } else {
      setServerCodes(serverCodesOf(result.error));
      setFocusRequest((current) => current + 1);
    }
  };

  const editing = existing !== null;
  const terminal =
    stage === 'closure' &&
    (existing === null
      ? project?.status === 'SUSPENDED'
      : (existing.data as ClosureCaseDetail).outcome === 'TERMINATED_WITHOUT_COMPLETION');
  // Navigation, not protection: a new case is the project's Project Manager's (CLOSEOUT_RAISE at OWN).
  const raiser = editing || project === null || project.projectManagerUserId === session?.user.id;
  const stageNumber = stage === 'completion' ? 1 : 2;

  return (
    <>
      <PageHeader
        title={t(
          editing
            ? 'suspensionClosure.closeout.form.editTitle'
            : `suspensionClosure.closeout.form.createTitle.${stage}`,
        )}
        description={
          project === null
            ? t('suspensionClosure.projectUnreadable')
            : t('suspensionClosure.forProject', { project: project.title.text })
        }
      />
      <p>
        <Link
          to={editing ? closeoutCasePath(stage, existing.data.id) : projectCloseoutPath(projectId)}
        >
          {t(
            editing
              ? 'suspensionClosure.actions.backToDetail'
              : 'suspensionClosure.actions.backToCloseout',
          )}
        </Link>
      </p>
      <p className="closeout-stage-label" data-stage={stage}>
        {t('suspensionClosure.stages.numbered', {
          number: stageNumber,
          name: t(`suspensionClosure.stages.name.${stage}`),
        })}
      </p>
      <p className="form__note">
        {t(
          terminal
            ? 'suspensionClosure.closeout.form.intro.terminal'
            : `suspensionClosure.closeout.form.intro.${stage}`,
        )}
      </p>
      {!raiser && <p className="form__note">{t('suspensionClosure.notRaiser')}</p>}
      {existing?.data.status === 'RETURNED' && (
        <p className="form__note">
          {t('suspensionClosure.returnedNote', { revision: existing.data.revisionNo + 1 })}
        </p>
      )}
      <form ref={formRef} className="form" noValidate onSubmit={(event) => void submit(event)}>
        <FormAlert
          message={
            clientErrors > 0 ? t('common.form.fixErrors', { count: clientErrors }) : save.formError
          }
        />
        <fieldset className="form__section">
          <legend>{t(`suspensionClosure.closeout.form.section.${stage}`)}</legend>
          {stage === 'completion' && (
            <TextField
              label={t('suspensionClosure.closeout.fields.actualProjectCompletionDate')}
              name="actualProjectCompletionDate"
              type="date"
              dir="ltr"
              required
              hint={t('suspensionClosure.closeout.form.completionDateHint')}
              value={values.actualProjectCompletionDate}
              onChange={(value) => {
                set('actualProjectCompletionDate', value);
              }}
              error={errorOf('actualProjectCompletionDate')}
            />
          )}
          <TextAreaField
            label={t(`suspensionClosure.closeout.fields.narrative.${stage}`)}
            name="narrative"
            required
            maxLength={TEXT_LENGTH}
            hint={t(`suspensionClosure.closeout.form.narrativeHint.${stage}`)}
            value={values.narrative}
            onChange={(value) => {
              set('narrative', value);
            }}
            error={errorOf('narrative')}
          />
        </fieldset>
        <p className="form__note">{t('suspensionClosure.closeout.form.nextStep')}</p>
        <div className="form__actions">
          <button type="submit" className="button button--primary" disabled={save.saving}>
            {save.saving ? t('common.states.saving') : t('suspensionClosure.actions.saveDraft')}
          </button>
        </div>
      </form>
    </>
  );
}
