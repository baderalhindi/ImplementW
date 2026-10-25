import { type ReactElement, useCallback, useState } from 'react';
import { Link } from 'react-router';

import { PAGE_SIZE } from '@/shared/api/paging.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Pagination, TableContainer } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { integrationAdminApi } from '../api/integrationAdminApi.ts';
import { logsFilteredBy } from '../catalogue.ts';
import { syncRunTone } from '../presentation.ts';
import { integrationProblemMessage } from '../problems.ts';
import { useRefresh } from '../useRefresh.ts';

import { Code, Moment } from './Display.tsx';

interface SyncRunListProps {
  integrationId?: string;
  caption: string;
  /** Read again this often, in milliseconds (ADM-047, TASK-092). */
  refreshEvery?: number;
}

/**
 * Synchronisation and reconciliation runs, newest first, with their counts. A reconciliation applies nothing — its
 * applied count is always 0 — and reports mismatches as an alert (TASK-075 D-8). Each run opens its attempts in ADM-048
 * by correlation id.
 */
export function SyncRunList({
  integrationId,
  caption,
  refreshEvery,
}: SyncRunListProps): ReactElement {
  const { t, formatDateTime } = useI18n();
  const [page, setPage] = useState(1);
  const load = useCallback(
    (signal: AbortSignal) =>
      integrationAdminApi.syncRuns({ integrationId }, { page, pageSize: PAGE_SIZE }, signal),
    [integrationId, page],
  );
  const runs = useApiResource(load, { keepWhileReloading: true });
  useRefresh(runs.reload, refreshEvery);

  // A refresh keeps the rows shown until the next read lands; only a first read shows the loading state.
  if (runs.data === undefined && runs.error === null) {
    return <LoadingState />;
  }
  if (runs.error !== null || runs.data === undefined) {
    return <ErrorState error={runs.error} message={integrationProblemMessage(runs.error, t)} onRetry={runs.reload} />;
  }
  if (runs.data.items.length === 0) {
    return <EmptyState title={t('integrationAdmin.syncRuns.empty')} />;
  }
  return (
    <>
      <TableContainer caption={caption}>
        <thead>
          <tr>
            <th scope="col">{t('integrationAdmin.fields.startedAt')}</th>
            <th scope="col">{t('integrationAdmin.fields.integration')}</th>
            <th scope="col">{t('integrationAdmin.fields.runType')}</th>
            <th scope="col">{t('integrationAdmin.fields.runStatus')}</th>
            <th scope="col">{t('integrationAdmin.fields.recordsRead')}</th>
            <th scope="col">{t('integrationAdmin.fields.recordsApplied')}</th>
            <th scope="col">{t('integrationAdmin.fields.recordsRejected')}</th>
            <th scope="col">{t('integrationAdmin.fields.mismatches')}</th>
            <th scope="col">{t('common.table.actions')}</th>
          </tr>
        </thead>
        <tbody>
          {runs.data.items.map((run) => (
            <tr key={run.id} data-sync-run={run.id}>
              <td>
                <Moment value={run.startedAt} />
              </td>
              <td>
                <Code>{run.integrationCode}</Code>
              </td>
              <td>{t(`integrationAdmin.runType.${run.runType}`)}</td>
              <td>
                <StatusBadge
                  label={t(`integrationAdmin.runStatus.${run.status}`)}
                  tone={syncRunTone(run.status)}
                />
              </td>
              <td>{run.recordsRead}</td>
              <td>{run.recordsApplied}</td>
              <td>{run.recordsRejected}</td>
              <td>{run.mismatchCount}</td>
              <td className="cell--actions">
                <Link to={logsFilteredBy('correlationId', run.correlationId)}>
                  {t('integrationAdmin.syncRuns.openAttempts')}{' '}
                  <span className="visually-hidden">
                    {run.integrationCode} {formatDateTime(run.startedAt)}
                  </span>
                </Link>
              </td>
            </tr>
          ))}
        </tbody>
      </TableContainer>
      <Pagination
        page={runs.data.page}
        pageSize={runs.data.pageSize}
        totalCount={runs.data.totalCount}
        onPageChange={setPage}
      />
    </>
  );
}
