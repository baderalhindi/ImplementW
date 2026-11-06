import { type ReactElement, useCallback } from 'react';
import { Link } from 'react-router';

import { useCan } from '@/features/identity-access/permissions/permissions.ts';
import { PAGE_SIZE } from '@/shared/api/paging.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useListParams } from '@/shared/api/useListParams.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader, Pagination, TableContainer } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { updateRequestBatchesApi } from '../api/monthlyUpdatesApi.ts';
import { NEW_UPDATE_BATCH_PATH, updateBatchPath } from '../paths.ts';
import { participationProblemMessage } from '../problems.ts';

/** The monthly update batches the person may see (the 5 Nov 2026 review §2.2), newest first as the API orders them. */
export function BatchesPage(): ReactElement {
  const { t, formatDateTime } = useI18n();
  const { page, goToPage } = useListParams();
  const maySend = useCan('UPDATE_REQUEST_SEND');
  const batches = useApiResource(
    useCallback(
      (signal: AbortSignal) => updateRequestBatchesApi.list(page, PAGE_SIZE, signal),
      [page],
    ),
  );

  return (
    <>
      <PageHeader
        title={t('externalParticipation.nav.batches')}
        description={t('externalParticipation.batch.listDescription')}
        actions={
          maySend ? (
            <Link className="button button--primary" to={NEW_UPDATE_BATCH_PATH}>
              {t('externalParticipation.batch.new')}
            </Link>
          ) : undefined
        }
      />
      {batches.data === undefined ? (
        batches.loading ? (
          <LoadingState />
        ) : (
          <ErrorState
            error={batches.error}
            message={participationProblemMessage(batches.error, t)}
            onRetry={batches.reload}
          />
        )
      ) : batches.data.items.length === 0 ? (
        <EmptyState title={t('externalParticipation.batch.empty')} />
      ) : (
        <>
          <TableContainer caption={t('externalParticipation.nav.batches')}>
            <thead>
              <tr>
                <th scope="col">{t('externalParticipation.batch.sentAt')}</th>
                <th scope="col">{t('externalParticipation.fields.dueDate')}</th>
                <th scope="col">{t('externalParticipation.batch.projects')}</th>
                <th scope="col">{t('externalParticipation.batch.entities')}</th>
                <th scope="col">{t('externalParticipation.batch.open')}</th>
                <th scope="col">{t('externalParticipation.batch.repeat')}</th>
              </tr>
            </thead>
            <tbody>
              {batches.data.items.map((batch) => (
                <tr key={batch.id}>
                  <td>
                    <Link to={updateBatchPath(batch.id)}>{formatDateTime(batch.createdAt)}</Link>
                    {batch.withdrawnAt !== null && (
                      <>
                        {' '}
                        <StatusBadge
                          label={t('externalParticipation.batch.withdrawn')}
                          tone="neutral"
                        />
                      </>
                    )}
                  </td>
                  <td dir="ltr" className="cell--ltr">
                    {batch.dueDate}
                  </td>
                  <td>{batch.projectCount}</td>
                  <td>{batch.entityCount}</td>
                  <td>{batch.openCount}</td>
                  <td>
                    {batch.repeatDay === null
                      ? t('externalParticipation.batch.noRepeat')
                      : t('externalParticipation.batch.repeatsOn', { day: batch.repeatDay })}
                  </td>
                </tr>
              ))}
            </tbody>
          </TableContainer>
          <Pagination
            page={batches.data.page}
            pageSize={batches.data.pageSize}
            totalCount={batches.data.totalCount}
            onPageChange={goToPage}
          />
        </>
      )}
    </>
  );
}
