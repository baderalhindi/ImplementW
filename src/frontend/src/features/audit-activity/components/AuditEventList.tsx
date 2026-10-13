import { type ReactElement, useCallback } from 'react';
import { Link } from 'react-router';

import { PAGE_SIZE } from '@/shared/api/paging.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useListParams } from '@/shared/api/useListParams.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Pagination, TableContainer } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { auditActivityApi } from '../api/auditActivityApi.ts';
import { type AuditEventQuery, type AuditEventSummary } from '../api/types.ts';
import { outcomeTone } from '../presentation.ts';
import { auditProblemMessage } from '../problems.ts';

import { ActorName } from './ActorName.tsx';

interface AuditEventListProps {
  query: AuditEventQuery;
  caption: string;
  emptyTitle: string;
  /** Where each event's detail (ADM-051) opens. */
  detailPath: (event: AuditEventSummary) => string;
}

/**
 * Formal audit events as evidence, newest first: when, category, the event's code and class, outcome and actor, each
 * opening its immutable detail. The page is the API's: the categories, scope and clearance were applied before
 * counting (AUD-CC-20), so the total never counts what the reader may not see.
 */
export function AuditEventList({
  query,
  caption,
  emptyTitle,
  detailPath,
}: AuditEventListProps): ReactElement {
  const { t, formatDateTime } = useI18n();
  const { page, goToPage } = useListParams();
  // Compared by value: each screen builds its query from the URL on every render.
  const key = JSON.stringify(query);
  const load = useCallback(
    (signal: AbortSignal) =>
      auditActivityApi.events(
        JSON.parse(key) as AuditEventQuery,
        { page, pageSize: PAGE_SIZE },
        signal,
      ),
    [key, page],
  );
  const events = useApiResource(load);

  if (events.loading) {
    return <LoadingState />;
  }
  if (events.error !== null || events.data === undefined) {
    return <ErrorState message={auditProblemMessage(events.error, t)} onRetry={events.reload} />;
  }
  if (events.data.items.length === 0) {
    return <EmptyState title={emptyTitle} />;
  }
  return (
    <>
      <TableContainer caption={caption}>
        <thead>
          <tr>
            <th scope="col">{t('auditActivity.fields.occurredAt')}</th>
            <th scope="col">{t('auditActivity.fields.category')}</th>
            <th scope="col">{t('auditActivity.fields.event')}</th>
            <th scope="col">{t('auditActivity.fields.outcome')}</th>
            <th scope="col">{t('auditActivity.fields.actor')}</th>
            <th scope="col">{t('common.table.actions')}</th>
          </tr>
        </thead>
        <tbody>
          {events.data.items.map((event) => (
            <tr key={event.id} data-audit-event={event.id} data-category={event.category}>
              <td>
                <time dateTime={event.occurredAt}>{formatDateTime(event.occurredAt)}</time>
              </td>
              <td>{t(`auditActivity.category.${event.category}`)}</td>
              <td>
                <code dir="ltr">{event.eventType}</code>
                <span className="cell__aside">
                  {t(`auditActivity.eventClass.${event.eventClass}`)}
                </span>
              </td>
              <td>
                <StatusBadge
                  label={t(`auditActivity.outcome.${event.outcome}`)}
                  tone={outcomeTone(event.outcome)}
                />
              </td>
              <td>
                <ActorName event={event} />
              </td>
              <td className="cell--actions">
                <Link to={detailPath(event)}>
                  {t('auditActivity.list.open')}{' '}
                  <span className="visually-hidden">{event.eventType}</span>
                </Link>
              </td>
            </tr>
          ))}
        </tbody>
      </TableContainer>
      <Pagination
        page={events.data.page}
        pageSize={events.data.pageSize}
        totalCount={events.data.totalCount}
        onPageChange={goToPage}
      />
    </>
  );
}
