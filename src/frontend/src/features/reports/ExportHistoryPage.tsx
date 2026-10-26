import { type ReactElement, useCallback, useEffect, useState } from 'react';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { useSession } from '@/features/identity-access/session/useSession.ts';
import { PAGE_SIZE } from '@/shared/api/paging.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useListParams } from '@/shared/api/useListParams.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader, Pagination, TableContainer } from '@/shared/ui/Layout.tsx';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { reportsApi } from './api/reportsApi.ts';
import { type ReportCatalogueEntry, type ReportJobSummary } from './api/types.ts';
import { JobDownloadButton } from './components/JobDownloadButton.tsx';
import { failureMessage, isJobActive, isJobCancellable, jobTone } from './presentation.ts';
import { reportProblemMessage } from './problems.ts';

// SCR-140 Export History (FG-02 §13.5; acceptance criterion 2): the person's own report jobs — who generated each
// output and when, its state, format, classification and expiry — with the file of a completed one. The API lists the
// requester's jobs only; anyone else's job does not exist for them (R-47). Every download is authorised again.

/** How often the list is read again while one of its jobs is still running. */
export const HISTORY_POLL_MS = 5000;

export function ExportHistoryPage(): ReactElement {
  const { t, language, formatDateTime } = useI18n();
  const { session } = useSession();
  const { page, goToPage } = useListParams();
  const [notice, setNotice] = useState<Notice | null>(null);

  const load = useCallback(
    async (signal: AbortSignal) => {
      const [jobs, catalogue] = await Promise.all([
        reportsApi.jobs(page, PAGE_SIZE, signal),
        reportsApi.catalogue(signal),
      ]);
      return { jobs, catalogue };
    },
    [page],
  );
  const { data, error, loading, reload } = useApiResource(load, { keepWhileReloading: true });
  const nameOf = usePersonNames(data?.jobs.items.map((job) => job.requestedByUserId) ?? []);

  const running = data?.jobs.items.some((job) => isJobActive(job.status)) ?? false;
  useEffect(() => {
    if (!running || loading) {
      return;
    }
    const timer = setTimeout(reload, HISTORY_POLL_MS);
    return () => {
      clearTimeout(timer);
    };
  }, [running, loading, reload]);

  const title = t('reports.history.title');
  const outputName = (job: ReportJobSummary, catalogue: ReportCatalogueEntry[]) =>
    job.kind === 'EXPLORER'
      ? t('reports.explorer.title')
      : (catalogue.find((entry) => entry.code === job.reportCode)?.name[language] ??
        job.reportCode ??
        '—');
  /** The requester by name: the signed-in person's own, the API's id read through the directory otherwise. */
  const generatedBy = (userId: string) =>
    userId === session?.user.id
      ? t('reports.history.you', { name: session.user.displayName })
      : nameOf(userId);

  return (
    <>
      <PageHeader title={title} description={t('reports.history.description')} />
      <PageNotice notice={notice} />
      {loading && data === undefined ? (
        <LoadingState />
      ) : error !== null || data === undefined ? (
        <ErrorState error={error} message={reportProblemMessage(error, t)} onRetry={reload} />
      ) : data.jobs.items.length === 0 ? (
        <EmptyState title={t('reports.history.empty')} />
      ) : (
        <>
          <TableContainer caption={title}>
            <thead>
              <tr>
                <th scope="col">{t('reports.history.output')}</th>
                <th scope="col">{t('reports.history.format')}</th>
                <th scope="col">{t('reports.history.status')}</th>
                <th scope="col">{t('reports.history.generatedBy')}</th>
                <th scope="col">{t('reports.history.requestedAt')}</th>
                <th scope="col">{t('reports.history.generatedAt')}</th>
                <th scope="col">{t('reports.history.classification')}</th>
                <th scope="col">{t('reports.history.expiresAt')}</th>
                <th scope="col">{t('common.table.actions')}</th>
              </tr>
            </thead>
            <tbody>
              {data.jobs.items.map((job) => {
                const name = outputName(job, data.catalogue);
                return (
                  <tr key={job.id} data-job={job.id} data-job-status={job.status}>
                    <td>{name}</td>
                    <td>{t(`reports.formats.${job.exportFormat}`)}</td>
                    <td>
                      <StatusBadge
                        label={t(`reports.jobStatus.${job.status}`)}
                        tone={jobTone(job.status)}
                      />
                      {job.status === 'FAILED' && (
                        <span className="cell__aside">{failureMessage(job.failureCode, t)}</span>
                      )}
                    </td>
                    <td data-generated-by={job.requestedByUserId}>
                      {generatedBy(job.requestedByUserId)}
                    </td>
                    <td>{formatDateTime(job.requestedAt)}</td>
                    <td>
                      {job.completedAt === null
                        ? t('reports.history.notGenerated')
                        : formatDateTime(job.completedAt)}
                    </td>
                    <td>
                      {job.sensitivity === null ? (
                        '—'
                      ) : (
                        <StatusBadge
                          label={t(`reports.sensitivity.${job.sensitivity}`)}
                          tone={job.sensitivity === 'SENSITIVE' ? 'warning' : 'neutral'}
                        />
                      )}
                    </td>
                    <td>{job.expiresAt === null ? '—' : formatDateTime(job.expiresAt)}</td>
                    <td className="cell--actions cell--actions-wrap">
                      {job.status === 'COMPLETED' && (
                        <JobDownloadButton jobId={job.id} format={job.exportFormat} name={name} />
                      )}
                      {isJobCancellable(job.status) && (
                        <button
                          type="button"
                          className="button"
                          onClick={() => {
                            reportsApi.cancelJob(job.id).then(
                              () => {
                                setNotice({
                                  tone: 'success',
                                  message: t('reports.history.cancelled', { name }),
                                });
                                reload();
                              },
                              (failure: unknown) => {
                                setNotice({
                                  tone: 'warning',
                                  message: reportProblemMessage(failure, t),
                                });
                                reload();
                              },
                            );
                          }}
                        >
                          {t('reports.history.cancel')}{' '}
                          <span className="visually-hidden">{name}</span>
                        </button>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </TableContainer>
          <Pagination
            page={data.jobs.page}
            pageSize={data.jobs.pageSize}
            totalCount={data.jobs.totalCount}
            onPageChange={goToPage}
          />
        </>
      )}
    </>
  );
}
