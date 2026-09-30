import { type ReactElement, type ReactNode, useCallback, useState } from 'react';
import { Link, useParams } from 'react-router';

import { useSession } from '@/features/identity-access/session/useSession.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader, TableContainer } from '@/shared/ui/Layout.tsx';
import { ErrorState, LoadingState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { approvalInstancesApi } from '../api/approvalsApi.ts';
import { type ApprovalInstanceDetail, type ApprovalTaskDetail } from '../api/types.ts';
import { PageNotice, type Notice } from '../components/PageNotice.tsx';
import { SubjectSummary } from '../components/SubjectSummary.tsx';
import { DecisionDialog } from '../dialogs/DecisionDialog.tsx';
import { type TaskAction, type TaskActionTarget } from '../dialogs/taskActions.ts';
import { WithdrawDialog } from '../dialogs/WithdrawDialog.tsx';
import {
  currentStage,
  instanceTone,
  isOverdue,
  languageTag,
  tasksByStage,
  taskTone,
} from '../presentation.ts';
import { approvalProblemMessage } from '../problems.ts';
import { type ApprovalNames, useApprovalNames } from '../useApprovalNames.ts';

function DetailRow({ term, children }: { term: string; children: ReactNode }): ReactElement {
  return (
    <div className="details__row">
      <dt>{term}</dt>
      <dd>{children}</dd>
    </div>
  );
}

/** Who decided: the person who acted, and whose authority they used when it was delegated (TASK-035 D-5). */
function DecidedBy({
  task,
  names,
}: {
  task: ApprovalTaskDetail;
  names: ApprovalNames;
}): ReactElement {
  const { t } = useI18n();
  if (task.actingUserId === null) {
    return <>—</>;
  }
  if (task.approvalDelegationId === null || task.assignedUserId === task.actingUserId) {
    return <>{names.user(task.actingUserId)}</>;
  }
  return (
    <>
      {names.user(task.actingUserId)}
      <span className="cell__aside">
        {t('approvals.history.onBehalfOf', { name: names.user(task.assignedUserId) })}
      </span>
    </>
  );
}

/**
 * SCR-115 Workflow/Approval History: the run and every task of its route, in stage order, including escalated and
 * cancelled ones, with each decision's reason. The requester may withdraw a pending run (MOD-044) and escalate an
 * overdue task of the current stage (MOD-045); decisions are made from the inbox, which lists only decidable tasks.
 */
export function ApprovalHistoryPage(): ReactElement {
  const { t } = useI18n();
  const { instanceId = '' } = useParams();
  const load = useCallback(
    (signal: AbortSignal) => approvalInstancesApi.get(instanceId, signal),
    [instanceId],
  );
  const run = useApiResource(load);
  // Held here, not in ApprovalHistory: reloading after an action shows the loading state, which unmounts it.
  const [notice, setNotice] = useState<Notice | null>(null);

  if (run.data === undefined) {
    return (
      <>
        <PageHeader title={t('approvals.history.title')} />
        <PageNotice notice={notice} />
        {run.loading ? (
          <LoadingState />
        ) : (
          <ErrorState message={approvalProblemMessage(run.error, t)} onRetry={run.reload} />
        )}
      </>
    );
  }
  return (
    <ApprovalHistory
      run={run.data}
      notice={notice}
      onNotice={setNotice}
      onSettled={(next) => {
        setNotice(next);
        run.reload();
      }}
    />
  );
}

interface ApprovalHistoryProps {
  run: ApprovalInstanceDetail;
  notice: Notice | null;
  onNotice: (notice: Notice | null) => void;
  /** An action succeeded or found the run moved on: show the notice and read the run again. */
  onSettled: (notice: Notice) => void;
}

function ApprovalHistory({ run, notice, onNotice, onSettled }: ApprovalHistoryProps): ReactElement {
  const { t, formatDateTime } = useI18n();
  const { session } = useSession();
  const names = useApprovalNames([
    run.requestedByUserId,
    ...run.tasks.flatMap((task) => [task.actingUserId, task.assignedUserId]),
  ]);
  const [target, setTarget] = useState<TaskActionTarget | null>(null);
  const [withdrawing, setWithdrawing] = useState(false);

  const isRequester = session?.user.id === run.requestedByUserId;
  const stage = currentStage(run.tasks);
  const canEscalate = (task: ApprovalTaskDetail) =>
    isRequester && task.status === 'PENDING' && task.sequenceNo === stage && isOverdue(task.dueAt);

  const settle = (next: Notice) => {
    setTarget(null);
    setWithdrawing(false);
    onSettled(next);
  };

  return (
    <>
      <PageHeader
        title={t('approvals.history.title')}
        actions={
          isRequester && run.status === 'PENDING' ? (
            <button
              type="button"
              className="button"
              onClick={() => {
                onNotice(null);
                setWithdrawing(true);
              }}
            >
              {t('approvals.withdraw.action')}
            </button>
          ) : undefined
        }
      />
      <PageNotice notice={notice} />

      <dl className="details">
        <DetailRow term={t('approvals.fields.request')}>
          <SubjectSummary subject={run.subject} routingKey={run.routingKey} />
        </DetailRow>
        <DetailRow term={t('approvals.fields.status')}>
          <StatusBadge
            label={t(`approvals.instanceStatus.${run.status}`)}
            tone={instanceTone(run.status)}
          />
        </DetailRow>
        <DetailRow term={t('approvals.fields.requestedBy')}>
          {names.user(run.requestedByUserId)}
          <span className="details__aside">{formatDateTime(run.requestedAt)}</span>
        </DetailRow>
        <DetailRow term={t('approvals.fields.completedAt')}>
          {run.completedAt === null ? '—' : formatDateTime(run.completedAt)}
        </DetailRow>
        {run.status !== 'PENDING' && (
          <DetailRow term={t('approvals.history.outcomeDelivered')}>
            {run.outcomeDeliveredAt === null
              ? t('approvals.history.outcomePending')
              : formatDateTime(run.outcomeDeliveredAt)}
          </DetailRow>
        )}
        {run.previousInstanceId !== null && (
          <DetailRow term={t('approvals.history.previous')}>
            <Link to={`/approvals/instances/${run.previousInstanceId}`}>
              {t('approvals.history.previousLink')}
            </Link>
          </DetailRow>
        )}
      </dl>

      {tasksByStage(run.tasks).map(([sequenceNo, tasks]) => (
        <section
          key={sequenceNo}
          className="section"
          aria-labelledby={`stage-${String(sequenceNo)}`}
        >
          <div className="section__header">
            <h2 id={`stage-${String(sequenceNo)}`}>
              {t('approvals.stage', { stage: sequenceNo })}
            </h2>
            {sequenceNo === stage && (
              <StatusBadge label={t('approvals.history.currentStage')} tone="info" />
            )}
          </div>
          <TableContainer caption={t('approvals.history.stageCaption', { stage: sequenceNo })}>
            <thead>
              <tr>
                <th scope="col">{t('approvals.fields.role')}</th>
                <th scope="col">{t('approvals.fields.status')}</th>
                <th scope="col">{t('approvals.fields.dueAt')}</th>
                <th scope="col">{t('approvals.fields.decidedBy')}</th>
                <th scope="col">{t('approvals.fields.decidedAt')}</th>
                <th scope="col">{t('approvals.fields.reason')}</th>
                {isRequester && <th scope="col">{t('common.table.actions')}</th>}
              </tr>
            </thead>
            <tbody>
              {tasks.map((task) => (
                <tr key={task.id}>
                  <td>{names.role(task.assignedRoleId)}</td>
                  <td>
                    <StatusBadge
                      label={t(`approvals.taskStatus.${task.status}`)}
                      tone={taskTone(task.status)}
                    />
                    {task.status === 'PENDING' && isOverdue(task.dueAt) && (
                      <>
                        {' '}
                        <StatusBadge label={t('approvals.overdue')} tone="negative" />
                      </>
                    )}
                  </td>
                  <td>
                    {task.dueAt === null
                      ? t('approvals.history.notReached')
                      : formatDateTime(task.dueAt)}
                  </td>
                  <td>
                    <DecidedBy task={task} names={names} />
                  </td>
                  <td>{task.decidedAt === null ? '—' : formatDateTime(task.decidedAt)}</td>
                  <td>
                    {task.decisionReason === null ? (
                      '—'
                    ) : (
                      <span
                        className="reason"
                        lang={languageTag(task.decisionReason.language)}
                        dir="auto"
                      >
                        {task.decisionReason.text}
                      </span>
                    )}
                  </td>
                  {isRequester && (
                    <td className="cell--actions">
                      {canEscalate(task) && (
                        <button
                          type="button"
                          className="button"
                          onClick={() => {
                            onNotice(null);
                            setTarget({ taskId: task.id, action: 'escalate' });
                          }}
                        >
                          {t('approvals.decision.escalate.action')}
                        </button>
                      )}
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </TableContainer>
        </section>
      ))}

      <DecisionDialog
        target={target}
        onClose={() => {
          setTarget(null);
        }}
        onDone={(action: TaskAction) => {
          settle({ tone: 'success', message: t(`approvals.done.${action}`) });
        }}
        onStale={(message) => {
          settle({ tone: 'warning', message });
        }}
      />
      <WithdrawDialog
        instanceId={withdrawing ? run.id : null}
        onClose={() => {
          setWithdrawing(false);
        }}
        onDone={() => {
          settle({ tone: 'success', message: t('approvals.done.withdraw') });
        }}
      />
    </>
  );
}
