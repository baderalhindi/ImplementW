import { type ReactElement, useCallback } from 'react';

import { PAGE_SIZE } from '@/shared/api/paging.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useListParams } from '@/shared/api/useListParams.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { SelectField } from '@/shared/ui/FormFields.tsx';
import { PageHeader, Pagination, TableContainer } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { notificationsApi } from '../api/notificationsApi.ts';
import { type NotificationHistoryItem } from '../api/types.ts';
import {
  CHANNELS,
  DELIVERY_STATUSES,
  deliveryTone,
  familyLabel,
  isKnownSuppressionReason,
} from '../presentation.ts';
import { notificationProblemMessage } from '../problems.ts';
import { useNotificationFamilies } from '../useNotificationFamilies.ts';

/**
 * SCR-153 Notification History: every delivery to the caller on every channel, newest first, with what became of it —
 * sent, delivered, read, retried, not delivered, or not sent and why (TASK-039 D-12). Only the subject is shown; what
 * an e-mail or SMS said is not part of this read.
 */
export function NotificationHistoryPage(): ReactElement {
  const { t, language, formatDateTime } = useI18n();
  const { params, page, setFilter, goToPage } = useListParams();
  const channel = CHANNELS.find((candidate) => candidate === params.get('channel'));
  const status = DELIVERY_STATUSES.find((candidate) => candidate === params.get('status'));
  const load = useCallback(
    (signal: AbortSignal) =>
      notificationsApi.listHistory({ channel, status, page, pageSize: PAGE_SIZE }, signal),
    [channel, status, page],
  );
  const history = useApiResource(load);
  const families = useNotificationFamilies();

  const outcomeNote = (item: NotificationHistoryItem): string | null => {
    if (item.suppressionReason !== null) {
      return isKnownSuppressionReason(item.suppressionReason)
        ? t(`notifications.suppressionReason.${item.suppressionReason}`)
        : t('notifications.suppressionReason.other', { reason: item.suppressionReason });
    }
    if (item.readAt !== null) {
      return t('notifications.values.readAt', { at: formatDateTime(item.readAt) });
    }
    if (item.deliveredAt !== null) {
      return t('notifications.values.deliveredAt', { at: formatDateTime(item.deliveredAt) });
    }
    return null;
  };

  const filtered = channel !== undefined || status !== undefined;
  return (
    <>
      <PageHeader
        title={t('notifications.history.title')}
        description={t('notifications.history.description')}
      />
      <div className="filters" role="search" aria-label={t('common.filters.label')}>
        <SelectField
          label={t('notifications.filters.channel')}
          name="channel"
          value={channel ?? ''}
          placeholder={t('common.filters.any')}
          options={CHANNELS.map((value) => ({
            value,
            label: t(`notifications.channel.${value}`),
          }))}
          onChange={(value) => {
            setFilter('channel', value);
          }}
        />
        <SelectField
          label={t('notifications.fields.status')}
          name="status"
          value={status ?? ''}
          placeholder={t('common.filters.any')}
          options={DELIVERY_STATUSES.map((value) => ({
            value,
            label: t(`notifications.status.${value}`),
          }))}
          onChange={(value) => {
            setFilter('status', value);
          }}
        />
      </div>

      {history.loading && <LoadingState label={t('notifications.history.loading')} />}
      {history.error !== null && (
        <ErrorState
          message={notificationProblemMessage(history.error, t)}
          onRetry={history.reload}
        />
      )}
      {history.data !== undefined &&
        (history.data.items.length === 0 ? (
          <EmptyState
            title={
              filtered ? t('notifications.history.emptyFiltered') : t('notifications.history.empty')
            }
          />
        ) : (
          <>
            <TableContainer caption={t('notifications.history.title')}>
              <thead>
                <tr>
                  <th scope="col">{t('notifications.fields.notification')}</th>
                  <th scope="col">{t('notifications.fields.channel')}</th>
                  <th scope="col">{t('notifications.fields.family')}</th>
                  <th scope="col">{t('notifications.fields.outcome')}</th>
                  <th scope="col">{t('notifications.fields.created')}</th>
                  <th scope="col">{t('notifications.fields.sent')}</th>
                </tr>
              </thead>
              <tbody>
                {history.data.items.map((item) => {
                  const note = outcomeNote(item);
                  return (
                    <tr key={item.id}>
                      <td dir="auto">{item.subject ?? item.eventType}</td>
                      <td>{t(`notifications.channel.${item.channel}`)}</td>
                      <td>{familyLabel(families.data, item.eventFamilyCode, language)}</td>
                      <td>
                        <StatusBadge
                          label={t(`notifications.status.${item.status}`)}
                          tone={deliveryTone(item.status)}
                        />
                        {note !== null && <span className="cell__aside">{note}</span>}
                      </td>
                      <td>{formatDateTime(item.createdAt)}</td>
                      <td>{item.sentAt === null ? '—' : formatDateTime(item.sentAt)}</td>
                    </tr>
                  );
                })}
              </tbody>
            </TableContainer>
            <Pagination
              page={history.data.page}
              pageSize={history.data.pageSize}
              totalCount={history.data.totalCount}
              onPageChange={goToPage}
            />
          </>
        ))}
    </>
  );
}
