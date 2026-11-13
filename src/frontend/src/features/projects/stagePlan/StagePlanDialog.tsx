import { type ReactElement, type SyntheticEvent, useCallback, useState } from 'react';

import { readIssuedAuthorizations } from '@/features/change-requests/api/issuedAuthorizations.ts';
import { checkText, useSaveAction } from '@/features/identity-access/forms.ts';
import { fieldMessage } from '@/features/identity-access/problems.ts';
import { narrativeRequest } from '@/features/progress/progressUpdate.ts';
import { ApiError } from '@/shared/api/httpClient.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { SelectField, TextAreaField } from '@/shared/ui/FormFields.tsx';
import { ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';

import { type ProjectDetail } from '../api/types.ts';
import { projectProblemMessage } from '../problems.ts';
import { TEXT_LENGTH } from '../registration.ts';

import {
  checkStagePlan,
  hasStagePlanErrors,
  rowsOf,
  stagePlanChanged,
  type StagePlanCodes,
  type StagePlanRow,
  toEntries,
} from './stagePlan.ts';
import { stagePlanApi } from './stagePlanApi.ts';
import { StagePlanFields } from './StagePlanFields.tsx';

interface StagePlanDialogProps {
  open: boolean;
  project: ProjectDetail;
  /** Whether the person may change the plan: the API decides the rest (state, revision, authorisation). */
  canChange: boolean;
  onClose: () => void;
  onSaved: () => void;
}

/**
 * The project's stage plan (review round 7, item 1). A project with no plan yet — registered before stages, or imported — sets it here once; the
 * progress update asks for it first. Once the registration is approved and the plan set, changing a stage's dates or whether it applies implements an
 * approved schedule change request, chosen here by its title, with the reason.
 */
export function StagePlanDialog(props: StagePlanDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog open={props.open} title={t('projects.stagePlan.title')} onClose={props.onClose} wide>
      {props.open && <StagePlanBody {...props} />}
    </Dialog>
  );
}

function codesOf(error: unknown): StagePlanCodes {
  const codes: StagePlanCodes = {};
  if (error instanceof ApiError) {
    for (const issue of error.fieldIssues) {
      codes[issue.field.replace(/\.(text|language)$/, '')] ??= issue.code;
    }
  }
  return codes;
}

function StagePlanBody({
  project,
  canChange,
  onClose,
  onSaved,
}: StagePlanDialogProps): ReactElement {
  const { t, language, formatDateTime } = useI18n();
  const load = useCallback(
    (signal: AbortSignal) => stagePlanApi.get(project.id, signal),
    [project.id],
  );
  const plan = useApiResource(load);
  const save = useSaveAction(projectProblemMessage);
  // The rows as edited; until the first edit, the plan as read.
  const [editedRows, setRows] = useState<StagePlanRow[] | null>(null);
  const [authorization, setAuthorization] = useState('');
  const [reason, setReason] = useState('');
  const [attempted, setAttempted] = useState(false);
  const [serverCodes, setServerCodes] = useState<StagePlanCodes>({});

  const requiresAuthorization = plan.data?.data.requiresChangeAuthorization ?? false;
  const loadAuthorizations = useCallback(
    (signal: AbortSignal) =>
      requiresAuthorization && canChange
        ? readIssuedAuthorizations(project.id, 'STAGE_PLAN_CHANGE', signal)
        : Promise.resolve([]),
    [project.id, requiresAuthorization, canChange],
  );
  const authorizations = useApiResource(loadAuthorizations);

  if (plan.data === undefined) {
    return plan.loading ? (
      <LoadingState />
    ) : (
      <ErrorState
        error={plan.error}
        message={projectProblemMessage(plan.error, t)}
        onRetry={plan.reload}
      />
    );
  }

  const current = plan.data.data;
  const rows = editedRows ?? rowsOf(current.stages);
  if (current.stages.length === 0) {
    return <p className="state">{t('projects.problems.stagesNotConfigured')}</p>;
  }
  const locked =
    project.status === 'SUBMITTED' ||
    project.status === 'UNDER_REVIEW' ||
    project.status === 'CLOSED';
  const editable = canChange && !locked;
  const draft = project.status === 'DRAFT' || project.status === 'RETURNED';
  const codes: StagePlanCodes = {
    ...checkStagePlan(rows, {
      projectStart: project.plannedStartDate ?? '',
      projectEnd: project.plannedEndDate ?? '',
      requireDates: !draft,
      acceptsStartingPercent: current.acceptsStartingPercent,
    }),
    ...(requiresAuthorization
      ? {
          changeAuthorizationId: authorization === '' ? 'REQUIRED' : undefined,
          reason: checkText(reason, { required: true, maxLength: TEXT_LENGTH }) ?? undefined,
        }
      : {}),
  };
  const shown: StagePlanCodes = attempted ? { ...serverCodes, ...codes } : serverCodes;

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    setAttempted(true);
    if (hasStagePlanErrors(codes)) {
      return;
    }
    if (
      current.revisionNo > 0 &&
      !stagePlanChanged(rows, current.stages, current.acceptsStartingPercent)
    ) {
      onClose();
      return;
    }
    const result = await save.run(() =>
      stagePlanApi.update(
        project.id,
        {
          stages: toEntries(rows, current.acceptsStartingPercent),
          changeAuthorizationId: requiresAuthorization ? authorization : null,
          reason: requiresAuthorization ? narrativeRequest(reason, language) : null,
        },
        plan.data?.etag ?? null,
      ),
    );
    if (result.ok) {
      onSaved();
    } else {
      setServerCodes(codesOf(result.error));
    }
  };

  return (
    <form className="form" noValidate onSubmit={(event) => void submit(event)}>
      <p className="form__note">
        {current.revisionNo === 0
          ? t('projects.stagePlan.notSet')
          : t('projects.stagePlan.revision', { revision: current.revisionNo })}
        {current.stageVersionNo !== null &&
          ` ${t('projects.stagePlan.version', { version: current.stageVersionNo })}`}
      </p>
      {editable && requiresAuthorization && (
        <p className="form__note">{t('projects.stagePlan.changeNeedsRequest')}</p>
      )}
      {locked && <p className="form__note">{t('projects.problems.stagePlanUnderReview')}</p>}
      <FormAlert message={save.formError} />
      <StagePlanFields
        rows={rows}
        onChange={(next) => {
          setRows(next);
          setServerCodes({});
        }}
        codes={shown}
        acceptsStartingPercent={current.acceptsStartingPercent}
        disabled={!editable}
      />
      {editable && requiresAuthorization && (
        <>
          <SelectField
            label={t('projects.stagePlan.changeAuthorization')}
            name="changeAuthorizationId"
            required
            value={authorization}
            placeholder={
              authorizations.loading ? t('common.states.loading') : t('common.form.choose')
            }
            options={(authorizations.data ?? []).map((option) => ({
              value: option.id,
              label: t('projects.stagePlan.authorizationOption', {
                title: option.title,
                date: formatDateTime(option.issuedAt),
              }),
            }))}
            hint={
              authorizations.data?.length === 0
                ? t('projects.stagePlan.noAuthorization')
                : t('projects.stagePlan.changeAuthorizationHint')
            }
            onChange={(value) => {
              setAuthorization(value);
              setServerCodes({});
            }}
            error={
              shown.changeAuthorizationId === undefined
                ? undefined
                : fieldMessage(shown.changeAuthorizationId, t)
            }
          />
          <TextAreaField
            label={t('projects.stagePlan.reason')}
            name="reason"
            required
            rows={3}
            maxLength={TEXT_LENGTH}
            value={reason}
            onChange={setReason}
            error={shown.reason === undefined ? undefined : fieldMessage(shown.reason, t)}
          />
        </>
      )}
      <div className="form__actions">
        {editable && (
          <button type="submit" className="button button--primary" disabled={save.saving}>
            {save.saving ? t('common.states.saving') : t('projects.stagePlan.save')}
          </button>
        )}
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {editable ? t('common.actions.cancel') : t('common.actions.close')}
        </button>
      </div>
    </form>
  );
}
