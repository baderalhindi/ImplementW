import { type ReactElement, useCallback, useState } from 'react';
import { Link, useSearchParams } from 'react-router';

import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader, Pagination, TableContainer } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { approvalTasksApi } from '../api/approvalsApi.ts';
import { type ApprovalDecision } from '../api/types.ts';
import { PageNotice, type Notice } from '../components/PageNotice.tsx';
import { SubjectSummary } from '../components/SubjectSummary.tsx';
import { DecisionDialog } from '../dialogs/DecisionDialog.tsx';
import { type TaskAction, type TaskActionTarget } from '../dialogs/taskActions.ts';
import { pageFrom, PAGE_SIZE } from '../paging.ts';
import { isOverdue } from '../presentation.ts';
import { approvalProblemMessage } from '../problems.ts';
import { useApprovalNames } from '../useApprovalNames.ts';

const DECISIONS: ApprovalDecision[] = ['approve', 'reject', 'return'];

/**
 * SCR-100 Approval Inbox. It lists exactly what the API returns: the tasks the caller may decide now, by FG-03
 * authority evaluated on the server for their own role and scope or a delegator's (TASK-035 D-4, D-5). Nothing is
 * filtered or added here, so what an approver sees is what their scope allows.
 */
export function ApprovalInboxPage(): ReactElement {
  const { t, formatDateTime } = useI18n();
  const [params, setParams] = useSearchParams();
  const page = pageFrom(params);
  const load = useCallback(
    (signal: AbortSignal) => approvalTasksApi.listInbox(page, PAGE_SIZE, signal),
    [page],
  );
  const inbox = useApiResource(load);
  const [target, setTarget] = useState<TaskActionTarget | null>(null);
  const [notice, setNotice] = useState<Notice | null>(null);
  const names = useApprovalNames(
    inbox.data?.items.flatMap((item) => [item.instance.requestedByUserId, item.onBehalfOfUserId]) ??
      [],
  );

  const settle = (next: Notice) => {
    setTarget(null);
    setNotice(next);
    inbox.reload();
  };

  const goToPage = (next: number) => {
    setParams(next === 1 ? {} : { page: String(next) });
  };

  return (
    <>
      <PageHeader
        title={t('approvals.inbox.title')}
        description={t('approvals.inbox.description')}
      />
      <PageNotice notice={notice} />

      {inbox.loading && <LoadingState label={t('approvals.inbox.loading')} />}
      {inbox.error !== null && (
        <ErrorState message={approvalProblemMessage(inbox.error, t)} onRetry={inbox.reload} />
      )}
      {inbox.data !== undefined &&
        (inbox.data.items.length === 0 ? (
          <EmptyState title={t('approvals.inbox.empty')}>
            <p>{t('approvals.inbox.emptyDescription')}</p>
            {page > 1 && <Link to="/approvals/inbox">{t('approvals.firstPage')}</Link>}
          </EmptyState>
        ) : (
          <>
            <TableContainer caption={t('approvals.inbox.caption')}>
              <thead>
                <tr>
                  <th scope="col">{t('approvals.fields.request')}</th>
                  <th scope="col">{t('approvals.fields.requestedBy')}</th>
                  <th scope="col">{t('approvals.fields.stage')}</th>
                  <th scope="col">{t('approvals.fields.dueAt')}</th>
                  <th scope="col">{t('approvals.fields.authority')}</th>
                  <th scope="col">{t('common.table.actions')}</th>
                </tr>
              </thead>
              <tbody>
                {inbox.data.items.map((item) => {
                  const subjectId = `subject-${item.taskId}`;
                  return (
                    <tr key={item.taskId}>
                      <td id={subjectId}>
                        <SubjectSummary
                          subject={item.instance.subject}
                          routingKey={item.instance.routingKey}
                        />
                        <Link
                          className="cell__link"
                          to={`/approvals/instances/${item.instance.id}`}
                        >
                          {t('approvals.viewHistory')}
                        </Link>
                      </td>
                      <td>
                        {names.user(item.instance.requestedByUserId)}
                        <span className="cell__aside">
                          {formatDateTime(item.instance.requestedAt)}
                        </span>
                      </td>
                      <td>
                        {t('approvals.stage', { stage: item.sequenceNo })}
                        <span className="cell__aside">{names.role(item.assignedRoleId)}</span>
                      </td>
                      <td>
                        {item.dueAt === null ? '—' : formatDateTime(item.dueAt)}
                        {isOverdue(item.dueAt) && (
                          <>
                            {' '}
                            <StatusBadge label={t('approvals.overdue')} tone="negative" />
                          </>
                        )}
                      </td>
                      <td>
                        {item.onBehalfOfUserId === null ? (
                          t('approvals.inbox.ownAuthority')
                        ) : (
                          <StatusBadge
                            label={t('approvals.inbox.onBehalfOf', {
                              name: names.user(item.onBehalfOfUserId),
                            })}
                            tone="info"
                          />
                        )}
                      </td>
                      <td className="cell--actions cell--actions-wrap">
                        <div className="actions-group">
                          {DECISIONS.map((decision) => (
                            <button
                              key={decision}
                              type="button"
                              className="button"
                              aria-describedby={subjectId}
                              onClick={() => {
                                setNotice(null);
                                setTarget({ taskId: item.taskId, action: decision });
                              }}
                            >
                              {t(`approvals.decision.${decision}.action`)}
                            </button>
                          ))}
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </TableContainer>
            <Pagination
              page={inbox.data.page}
              pageSize={inbox.data.pageSize}
              totalCount={inbox.data.totalCount}
              onPageChange={goToPage}
            />
          </>
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
    </>
  );
}
