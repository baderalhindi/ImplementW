import { type ReactElement, type SyntheticEvent, useCallback, useRef, useState } from 'react';

import { useSaveAction } from '@/features/identity-access/forms.ts';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { type ProjectSummary } from '@/features/projects/api/types.ts';
import { TEXT_LENGTH } from '@/features/projects/registration.ts';
import { assigneeOf, assigneeValue, useUserSearch } from '@/features/tasks/assignee.ts';
import { AssigneeField } from '@/features/tasks/components/AssigneeField.tsx';
import { type ApiResponse } from '@/shared/api/httpClient.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useFieldErrors } from '@/shared/forms/useFieldErrors.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { SelectField, TextAreaField, TextField } from '@/shared/ui/FormFields.tsx';
import { ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';

import { risksApi } from '../api/risksApi.ts';
import { type RiskDetail, type RiskTreatmentActionDetail } from '../api/types.ts';
import { isStale, riskFieldMessage, riskProblemMessage } from '../problems.ts';
import {
  ACTION_TYPES,
  actionFormValuesOf,
  type ActionFormValues,
  checkActionForm,
  EMPTY_ACTION_FORM,
  toActionRequest,
} from '../riskRules.ts';

interface TreatmentActionDialogProps {
  risk: RiskDetail;
  /** null: MOD-034 Add Mitigation / Response; an id: edit that PLANNED or IN_PROGRESS action. */
  actionId: string | null;
  project: ProjectSummary;
  user: SessionUser;
  knownUserIds: (string | null)[];
  onClose: () => void;
  onDone: (action: RiskTreatmentActionDetail) => void;
  onStale: () => void;
}

/**
 * MOD-034 Add Mitigation / Response: the response strategy (mitigate, avoid, transfer, contingency), the action, its
 * owner and target date. It is planned, and the risk's rating does not change: only a new assessment rates it (WF-06
 * §8.8). An edit re-sends the action as a whole with its ETag; a completed or cancelled one is final.
 */
export function TreatmentActionDialog(props: TreatmentActionDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog
      open
      title={t(props.actionId === null ? 'risks.action.createTitle' : 'risks.action.editTitle')}
      onClose={props.onClose}
    >
      {props.actionId === null ? (
        <ActionForm {...props} editing={null} />
      ) : (
        <EditLoader {...props} actionId={props.actionId} />
      )}
    </Dialog>
  );
}

function EditLoader(props: TreatmentActionDialogProps & { actionId: string }): ReactElement {
  const { t } = useI18n();
  const { actionId } = props;
  const load = useCallback((signal: AbortSignal) => risksApi.action(actionId, signal), [actionId]);
  const action = useApiResource(load);
  if (action.data === undefined) {
    return action.loading ? (
      <LoadingState />
    ) : (
      <ErrorState message={riskProblemMessage(action.error, t)} onRetry={action.reload} />
    );
  }
  return <ActionForm {...props} editing={action.data} />;
}

function ActionForm({
  risk,
  project,
  user,
  knownUserIds,
  editing,
  onClose,
  onDone,
  onStale,
}: TreatmentActionDialogProps & {
  editing: ApiResponse<RiskTreatmentActionDetail> | null;
}): ReactElement {
  const { t, language } = useI18n();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction(riskProblemMessage);
  const canSearch = useUserSearch() ?? false;
  const before = editing?.data ?? null;
  const [values, setValues] = useState<ActionFormValues>(() =>
    before === null ? EMPTY_ACTION_FORM : actionFormValuesOf(before),
  );
  const [owner, setOwner] = useState(() =>
    assigneeValue(before === null ? risk.ownerUserId : before.ownerUserId),
  );
  const chosen = assigneeOf(owner, canSearch);
  const fields = useFieldErrors(
    { ...checkActionForm(values), ownerUserId: chosen.code },
    formRef,
    riskFieldMessage,
  );

  const set = (field: keyof ActionFormValues) => (value: string) => {
    setValues((current) => ({ ...current, [field]: value }));
    fields.clearServer();
  };

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (!fields.attempt()) {
      return;
    }
    const request = toActionRequest(values, chosen.userId, language, before);
    const result = await save.run(async () =>
      before === null
        ? (await risksApi.createAction({ ...request, riskId: risk.id })).data
        : (await risksApi.updateAction(before.id, request, editing?.etag ?? null)).data,
    );
    if (result.ok) {
      onDone(result.value);
    } else if (isStale(result.error)) {
      onStale();
    } else {
      fields.showServer(result.error);
    }
  };

  return (
    <form ref={formRef} className="form" noValidate onSubmit={(event) => void submit(event)}>
      <p className="form__note">{t('risks.action.intro')}</p>
      <FormAlert message={fields.summary ?? save.formError} />
      <SelectField
        label={t('risks.fields.actionType')}
        name="actionType"
        required
        value={values.actionType}
        options={ACTION_TYPES.map((type) => ({
          value: type,
          label: t(`risks.actionType.${type}`),
        }))}
        onChange={set('actionType')}
        error={fields.errorOf('actionType')}
      />
      <TextField
        label={t('risks.fields.actionTitle')}
        name="title"
        required
        dir="auto"
        value={values.title}
        onChange={set('title')}
        error={fields.errorOf('title')}
      />
      <TextAreaField
        label={t('risks.fields.actionDescription')}
        name="description"
        maxLength={TEXT_LENGTH}
        value={values.description}
        onChange={set('description')}
        error={fields.errorOf('description')}
      />
      <TextField
        label={t('risks.fields.dueDate')}
        name="dueDate"
        type="date"
        value={values.dueDate}
        onChange={set('dueDate')}
        error={fields.errorOf('dueDate')}
      />
      <AssigneeField
        user={user}
        project={project}
        knownUserIds={[...knownUserIds, risk.ownerUserId, before?.ownerUserId ?? null]}
        value={owner}
        canSearch={canSearch}
        name="ownerUserId"
        onChange={(value) => {
          setOwner(value);
          fields.clearServer();
        }}
        error={fields.errorOf('ownerUserId')}
      />
      <div className="form__actions">
        <button type="submit" className="button button--primary" disabled={save.saving}>
          {save.saving ? t('common.states.saving') : t('common.actions.save')}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
