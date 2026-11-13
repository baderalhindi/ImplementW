import { type ReactElement, useCallback, useState } from 'react';
import { Link, useSearchParams } from 'react-router';

import { pageFrom, PAGE_SIZE } from '@/shared/api/paging.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import {
  ConfirmDialog,
  type Confirmation,
} from '@/features/identity-access/components/ConfirmDialog.tsx';
import { isTranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { FilterBar } from '@/shared/ui/FilterBar.tsx';
import { PageHeader, Pagination, TableContainer } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';
import { PageNotice, type Notice } from '@/shared/ui/PageNotice.tsx';
import { SelectAllHeader, SelectRowCell } from '@/shared/ui/rowSelection.tsx';
import { useRowSelection } from '@/shared/ui/useRowSelection.ts';

import { approvalInstancesApi } from '../api/approvalsApi.ts';
import { type ApprovalInstanceSummary } from '../api/types.ts';
import { SubjectSummary } from '../components/SubjectSummary.tsx';
import { WithdrawDialog } from '../dialogs/WithdrawDialog.tsx';
import { INSTANCE_STATUSES, instanceTone } from '../presentation.ts';
import { approvalProblemMessage } from '../problems.ts';

/**
 * SCR-101 My Requests: the approval runs the caller requested, newest first. A request still PENDING can be withdrawn
 * here (MOD-044); an overdue task is escalated from the request's history (MOD-045), where the tasks are listed.
 */
export function MyRequestsPage(): ReactElement {
  const { t, formatDateTime } = useI18n();
  const [params, setParams] = useSearchParams();
  const page = pageFrom(params);
  const status = INSTANCE_STATUSES.find((candidate) => candidate === params.get('status'));
  const load = useCallback(
    (signal: AbortSignal) =>
      approvalInstancesApi.listMine({ status, page, pageSize: PAGE_SIZE }, signal),
    [status, page],
  );
  const requests = useApiResource(load);
  const [withdrawing, setWithdrawing] = useState<string | null>(null);
  const [notice, setNotice] = useState<Notice | null>(null);
  const [confirmation, setConfirmation] = useState<Confirmation | null>(null);
  const selection = useRowSelection();

  /** A request as its subject reads: the kind of request and its revision. */
  const requestName = (request: ApprovalInstanceSummary) => {
    const key = `approvals.routing.${request.routingKey}`;
    const kind = isTranslationKey(key) ? t(key) : request.routingKey;
    return `${kind} (${t('approvals.subject.revision', { revision: request.subject.revisionNo })})`;
  };

  /** The chosen requests on this page still PENDING, the only ones Withdraw applies to. */
  const chosenWithdrawable = (requests.data?.items ?? []).filter(
    (request) => selection.selected.has(request.id) && request.status === 'PENDING',
  );
  const withdrawMany = () => {
    setNotice(null);
    setConfirmation({
      title: t('approvals.withdraw.title'),
      body: t('approvals.withdraw.bodyMany', {
        names: chosenWithdrawable.map(requestName).join('، '),
      }),
      confirmLabel: t('approvals.withdraw.confirm'),
      destructive: true,
      action: async () => {
        for (const request of chosenWithdrawable) {
          await approvalInstancesApi.withdraw(request.id);
        }
        selection.clear();
      },
      describeProblem: approvalProblemMessage,
    });
  };

  const goToPage = (next: number) => {
    const updated = new URLSearchParams(params);
    updated.set('page', String(next));
    setParams(updated);
  };

  return (
    <>
      <PageHeader
        title={t('approvals.requests.title')}
        description={t('approvals.requests.description')}
      />
      <PageNotice notice={notice} />

      <FilterBar
        fields={[
          {
            name: 'status',
            label: t('approvals.fields.status'),
            all: t('common.filters.any'),
            options: INSTANCE_STATUSES.map((value) => ({
              value,
              label: t(`approvals.instanceStatus.${value}`),
            })),
          },
        ]}
        values={{ status }}
        onChange={(_name, value) => {
          setParams(value === undefined ? {} : { status: value });
        }}
        onClearAll={() => {
          setParams({});
        }}
      />

      {requests.loading && <LoadingState label={t('approvals.requests.loading')} />}
      {requests.error !== null && (
        <ErrorState
          error={requests.error}
          message={approvalProblemMessage(requests.error, t)}
          onRetry={requests.reload}
        />
      )}
      {requests.data !== undefined &&
        (requests.data.items.length === 0 ? (
          <EmptyState
            title={
              status === undefined
                ? t('approvals.requests.empty')
                : t('approvals.requests.emptyFiltered')
            }
          />
        ) : (
          <>
            <TableContainer
              caption={t('approvals.requests.caption')}
              selection={{
                selection,
                exportName: 'my-requests',
                actions: chosenWithdrawable.length > 0 && (
                  <button type="button" className="button" onClick={withdrawMany}>
                    {t('approvals.withdraw.action')}
                  </button>
                ),
              }}
            >
              <thead>
                <tr>
                  <SelectAllHeader
                    ids={requests.data.items.map((request) => request.id)}
                    selection={selection}
                  />
                  <th scope="col">{t('approvals.fields.request')}</th>
                  <th scope="col">{t('approvals.fields.requestedAt')}</th>
                  <th scope="col">{t('approvals.fields.status')}</th>
                  <th scope="col">{t('approvals.fields.completedAt')}</th>
                  <th scope="col">{t('common.table.actions')}</th>
                </tr>
              </thead>
              <tbody>
                {requests.data.items.map((request) => {
                  const subjectId = `subject-${request.id}`;
                  return (
                    <tr key={request.id}>
                      <SelectRowCell
                        id={request.id}
                        name={requestName(request)}
                        selection={selection}
                      />
                      <td id={subjectId}>
                        <SubjectSummary subject={request.subject} routingKey={request.routingKey} />
                      </td>
                      <td>{formatDateTime(request.requestedAt)}</td>
                      <td>
                        <StatusBadge
                          label={t(`approvals.instanceStatus.${request.status}`)}
                          tone={instanceTone(request.status)}
                        />
                      </td>
                      <td>
                        {request.completedAt === null ? '—' : formatDateTime(request.completedAt)}
                      </td>
                      <td className="cell--actions">
                        <Link
                          className="button"
                          to={`/approvals/instances/${request.id}`}
                          aria-describedby={subjectId}
                        >
                          {t('approvals.viewHistory')}
                        </Link>
                        {request.status === 'PENDING' && (
                          <button
                            type="button"
                            className="button"
                            aria-describedby={subjectId}
                            onClick={() => {
                              setNotice(null);
                              setWithdrawing(request.id);
                            }}
                          >
                            {t('approvals.withdraw.action')}
                          </button>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </TableContainer>
            <Pagination
              page={requests.data.page}
              pageSize={requests.data.pageSize}
              totalCount={requests.data.totalCount}
              onPageChange={goToPage}
            />
          </>
        ))}

      <WithdrawDialog
        instanceId={withdrawing}
        onClose={() => {
          setWithdrawing(null);
        }}
        onDone={() => {
          setWithdrawing(null);
          setNotice({ tone: 'success', message: t('approvals.done.withdraw') });
          requests.reload();
        }}
      />
      <ConfirmDialog
        confirmation={confirmation}
        onClose={() => {
          setConfirmation(null);
        }}
        onDone={() => {
          setConfirmation(null);
          setNotice({ tone: 'success', message: t('approvals.done.withdraw') });
          requests.reload();
        }}
      />
    </>
  );
}
