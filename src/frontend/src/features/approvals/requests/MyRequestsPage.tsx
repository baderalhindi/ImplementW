import { type ReactElement, useCallback, useState } from 'react';
import { Link, useSearchParams } from 'react-router';

import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { SelectField } from '@/shared/ui/FormFields.tsx';
import { PageHeader, Pagination, TableContainer } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { approvalInstancesApi } from '../api/approvalsApi.ts';
import { PageNotice, type Notice } from '../components/PageNotice.tsx';
import { SubjectSummary } from '../components/SubjectSummary.tsx';
import { WithdrawDialog } from '../dialogs/WithdrawDialog.tsx';
import { pageFrom, PAGE_SIZE } from '../paging.ts';
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

      <div className="filters" role="search" aria-label={t('common.filters.label')}>
        <SelectField
          label={t('approvals.fields.status')}
          name="status"
          value={status ?? ''}
          placeholder={t('common.filters.any')}
          options={INSTANCE_STATUSES.map((value) => ({
            value,
            label: t(`approvals.instanceStatus.${value}`),
          }))}
          onChange={(value) => {
            setParams(value === '' ? {} : { status: value });
          }}
        />
      </div>

      {requests.loading && <LoadingState label={t('approvals.requests.loading')} />}
      {requests.error !== null && (
        <ErrorState message={approvalProblemMessage(requests.error, t)} onRetry={requests.reload} />
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
            <TableContainer caption={t('approvals.requests.caption')}>
              <thead>
                <tr>
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
    </>
  );
}
