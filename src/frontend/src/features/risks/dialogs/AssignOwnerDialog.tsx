import { type ReactElement, type SyntheticEvent, useRef, useState } from 'react';

import { useSaveAction } from '@/features/identity-access/forms.ts';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { type ProjectSummary } from '@/features/projects/api/types.ts';
import { assigneeOf, assigneeValue, useUserSearch } from '@/features/tasks/assignee.ts';
import { AssigneeField } from '@/features/tasks/components/AssigneeField.tsx';
import { type ApiResponse } from '@/shared/api/httpClient.ts';
import { useFieldErrors } from '@/shared/forms/useFieldErrors.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { risksApi } from '../api/risksApi.ts';
import { type RiskDetail } from '../api/types.ts';
import { isStale, riskFieldMessage, riskProblemMessage } from '../problems.ts';
import { withOwner } from '../riskRules.ts';

interface AssignOwnerDialogProps {
  risk: ApiResponse<RiskDetail>;
  project: ProjectSummary;
  user: SessionUser;
  knownUserIds: (string | null)[];
  onClose: () => void;
  onDone: (risk: RiskDetail) => void;
  onStale: () => void;
}

/**
 * MOD-033 Assign Risk Owner: the risk's register fields re-sent with another owner (R-5, If-Match); the API has no
 * owner command of its own (TASK-055 §4). Whether that person may own it — a role over the project now — is the API's
 * decision; its refusal, 422 RISK_OWNER_NOT_ELIGIBLE on `ownerUserId`, is shown on the owner field.
 */
export function AssignOwnerDialog(props: AssignOwnerDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog open title={t('risks.owner.title')} onClose={props.onClose}>
      <AssignForm {...props} />
    </Dialog>
  );
}

function AssignForm({
  risk: { data: risk, etag },
  project,
  user,
  knownUserIds,
  onClose,
  onDone,
  onStale,
}: AssignOwnerDialogProps): ReactElement {
  const { t, language } = useI18n();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction(riskProblemMessage);
  const canSearch = useUserSearch() ?? false;
  const [owner, setOwner] = useState(() => assigneeValue(risk.ownerUserId));
  const chosen = assigneeOf(owner, canSearch);
  const fields = useFieldErrors({ ownerUserId: chosen.code }, formRef, riskFieldMessage);

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (!fields.attempt()) {
      return;
    }
    const result = await save.run(
      async () =>
        (await risksApi.update(risk.id, withOwner(risk, chosen.userId, language), etag)).data,
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
      <p className="form__note" dir="auto">
        {t('risks.owner.intro', { risk: risk.title.text })}
      </p>
      <FormAlert message={fields.summary ?? save.formError} />
      <AssigneeField
        user={user}
        project={project}
        knownUserIds={[...knownUserIds, risk.ownerUserId]}
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
          {save.saving ? t('common.states.saving') : t('risks.owner.confirm')}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
