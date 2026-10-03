import { type ReactElement, type SyntheticEvent, useRef, useState } from 'react';
import { Link } from 'react-router';

import { approvalTasksApi } from '@/features/approvals/api/approvalsApi.ts';
import { type ApprovalDecision, type ApprovalInboxItem } from '@/features/approvals/api/types.ts';
import { REASON_LENGTH, reasonRequired } from '@/features/approvals/dialogs/taskActions.ts';
import { approvalProblemMessage, isStaleApproval } from '@/features/approvals/problems.ts';
import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { checkText, optionalText, useSaveAction } from '@/features/identity-access/forms.ts';
import { Detail } from '@/features/projects/components/Detail.tsx';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { RadioGroupField, TextAreaField } from '@/shared/ui/FormFields.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { type ProjectBaselineDetail } from '../api/types.ts';
import { baselineName } from '../presentation.ts';
import { useFieldErrors } from '../useFieldErrors.ts';

const DECISIONS: ApprovalDecision[] = ['approve', 'return', 'reject'];

interface ApproveBaselineDialogProps {
  baseline: ProjectBaselineDetail | null;
  /** The person's inbox task deciding the baseline. */
  task: ApprovalInboxItem | null;
  active: ProjectBaselineDetail | null;
  plannedFinish: string | null;
  projectId: string;
  onClose: () => void;
  onDone: (decision: ApprovalDecision) => void;
  onStale: () => void;
}

/**
 * MOD-018 Approve Baseline: AHDA's decision on a submitted candidate, taken on the person's inbox task of its WF-11 run
 * (D-7). Approving makes it the single ACTIVE baseline, superseding the one before atomically, once WF-11 hands the
 * outcome to the schedule; returning sends it back for another revision, with the reason; rejecting ends it. The
 * reviewer sees what they decide on: the candidate, who submitted it, and the planned finish against the baseline it
 * would replace, with the working schedule (frozen while under review) one link away.
 */
export function ApproveBaselineDialog(props: ApproveBaselineDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog
      open={props.baseline !== null && props.task !== null}
      title={t('schedule.approve.title')}
      onClose={props.onClose}
    >
      {props.baseline !== null && props.task !== null && (
        <ApproveForm {...props} baseline={props.baseline} task={props.task} />
      )}
    </Dialog>
  );
}

function ApproveForm({
  baseline,
  task,
  active,
  plannedFinish,
  projectId,
  onClose,
  onDone,
  onStale,
}: ApproveBaselineDialogProps & {
  baseline: ProjectBaselineDetail;
  task: ApprovalInboxItem;
}): ReactElement {
  const { t, language, formatDateTime } = useI18n();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction(approvalProblemMessage);
  const personName = usePersonNames([task.instance.requestedByUserId, task.onBehalfOfUserId]);
  const [decision, setDecision] = useState<ApprovalDecision | ''>('');
  const [reason, setReason] = useState('');
  const required = decision !== '' && reasonRequired(decision);
  const fields = useFieldErrors(
    {
      decision: decision === '' ? 'REQUIRED' : null,
      reason: checkText(reason, { required, maxLength: REASON_LENGTH }),
    },
    formRef,
  );

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (!fields.attempt() || decision === '') {
      return;
    }
    const text = optionalText(reason);
    const result = await save.run(() =>
      approvalTasksApi.decide(task.taskId, decision, text === null ? null : { text, language }),
    );
    if (result.ok) {
      onDone(decision);
    } else if (isStaleApproval(result.error)) {
      onStale();
    } else {
      fields.showServer(result.error);
    }
  };

  return (
    <form ref={formRef} className="form" noValidate onSubmit={(event) => void submit(event)}>
      <dl className="details">
        <Detail term={t('schedule.baselines.candidate')}>{baselineName(baseline, t)}</Detail>
        <Detail term={t('schedule.approve.submitted')}>
          {`${formatDateTime(task.instance.requestedAt)} · ${personName(task.instance.requestedByUserId)}`}
        </Detail>
        <Detail term={t('schedule.submit.plannedFinish')}>
          {plannedFinish ?? t('common.values.none')}
        </Detail>
        {active !== null && (
          <Detail term={t('schedule.submit.replaces')}>
            {t('schedule.summary.baselineFinish', {
              name: baselineName(active, t),
              date: active.baselineFinishDate,
            })}
          </Detail>
        )}
      </dl>
      {task.onBehalfOfUserId !== null && (
        <p className="form__note">
          {t('schedule.approve.onBehalfOf', { person: personName(task.onBehalfOfUserId) })}
        </p>
      )}
      <p>
        <Link to={`/projects/${projectId}/schedule/gantt`}>{t('schedule.approve.inspect')}</Link>
      </p>
      <FormAlert message={fields.summary ?? save.formError} />
      <RadioGroupField
        label={t('schedule.approve.decision')}
        name="decision"
        required
        value={decision}
        options={DECISIONS.map((value) => ({
          value,
          label: t(`schedule.approve.decisions.${value}`),
        }))}
        onChange={(value) => {
          setDecision(DECISIONS.find((item) => item === value) ?? '');
          fields.clearServer();
        }}
        error={fields.errorOf('decision')}
      />
      {decision !== '' && (
        <p className="form__note">{t(`schedule.approve.consequences.${decision}`)}</p>
      )}
      <TextAreaField
        label={t('schedule.approve.reason')}
        name="reason"
        required={required}
        rows={3}
        maxLength={REASON_LENGTH}
        value={reason}
        hint={
          required
            ? t('approvals.decision.reasonRequiredHint')
            : t('approvals.decision.reasonOptionalHint')
        }
        onChange={(value) => {
          setReason(value);
          fields.clearServer();
        }}
        error={fields.errorOf('reason')}
      />
      <div className="form__actions">
        <button
          type="submit"
          className={decision === 'reject' ? 'button button--danger' : 'button button--primary'}
          disabled={save.saving}
        >
          {save.saving ? t('common.states.saving') : t('schedule.approve.confirm')}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
