import { type ReactElement, useCallback, useState } from 'react';

import { PAGE_SIZE } from '@/shared/api/paging.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useListParams } from '@/shared/api/useListParams.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { SelectField } from '@/shared/ui/FormFields.tsx';
import { PageHeader, Pagination, TableContainer } from '@/shared/ui/Layout.tsx';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { notificationsApi } from '../api/notificationsApi.ts';
import { type NotificationFamilyPreference, type NotificationSummary } from '../api/types.ts';
import { MarkAllReadDialog } from '../dialogs/MarkAllReadDialog.tsx';
import { NotificationDetailDialog } from '../dialogs/NotificationDetailDialog.tsx';
import {
  type FamilyKind,
  familyLabel,
  headline,
  isFamilyOfKind,
  isUnread,
} from '../presentation.ts';
import { isConfigurationMissing, notificationProblemMessage } from '../problems.ts';
import { unreadCountStore, useReloadOnUnreadChange, useUnreadCount } from '../unreadCount.ts';
import { useNotificationFamilies } from '../useNotificationFamilies.ts';

const READ_STATES = ['unread', 'read'] as const;
type ReadState = (typeof READ_STATES)[number];

interface NotificationInboxProps {
  title: string;
  description: string;
  /** SCR-151 and SCR-152 list one kind of family only; SCR-150 (null) lists every family. */
  kind: FamilyKind | null;
}

/**
 * SCR-150 All Notifications, SCR-151 Alerts and SCR-152 Escalations: the caller's in-app notifications, newest first,
 * filtered by read state and family (kept in the URL), each opened in MOD-070. SCR-150 also offers MOD-071. The page is
 * read again when the unread count moves, so a new notification appears within one poll.
 */
export function NotificationInbox({
  title,
  description,
  kind,
}: NotificationInboxProps): ReactElement {
  const { t } = useI18n();
  const families = useNotificationFamilies();
  const { count } = useUnreadCount();
  const [markingAll, setMarkingAll] = useState(false);
  const [notice, setNotice] = useState<Notice | null>(null);
  const [reloadKey, setReloadKey] = useState(0);

  const choosable =
    kind === null
      ? (families.data ?? [])
      : (families.data ?? []).filter((family) => isFamilyOfKind(family.eventFamilyCode, kind));

  return (
    <>
      <PageHeader
        title={title}
        description={description}
        actions={
          kind === null ? (
            <button
              type="button"
              className="button"
              disabled={count === 0}
              onClick={() => {
                setNotice(null);
                setMarkingAll(true);
              }}
            >
              {t('notifications.markAll.action')}
            </button>
          ) : undefined
        }
      />
      <PageNotice notice={notice} />

      {kind === null ? (
        <NotificationList
          key={reloadKey}
          caption={title}
          families={families.data}
          choosable={choosable}
          kind={null}
        />
      ) : (
        <>
          {families.loading && <LoadingState label={t('notifications.list.loading')} />}
          {families.error !== null &&
            (isConfigurationMissing(families.error) ? (
              <EmptyState title={t(`notifications.${kind}.noFamilies`)} />
            ) : (
              <ErrorState
                message={notificationProblemMessage(families.error, t)}
                onRetry={families.reload}
              />
            ))}
          {families.data !== undefined &&
            (choosable.length === 0 ? (
              <EmptyState title={t(`notifications.${kind}.noFamilies`)} />
            ) : (
              <NotificationList
                caption={title}
                families={families.data}
                choosable={choosable}
                kind={kind}
              />
            ))}
        </>
      )}

      {kind === null && (
        <MarkAllReadDialog
          open={markingAll}
          onClose={() => {
            setMarkingAll(false);
          }}
          onDone={(markedCount) => {
            setMarkingAll(false);
            setNotice({
              tone: 'success',
              message:
                markedCount === 0
                  ? t('notifications.markAll.doneNone')
                  : t('notifications.markAll.done', { count: markedCount }),
            });
            setReloadKey((key) => key + 1);
            void unreadCountStore.refresh();
          }}
        />
      )}
    </>
  );
}

interface NotificationListProps {
  caption: string;
  families: NotificationFamilyPreference[] | undefined;
  /** The families the filter offers. On SCR-151 and SCR-152 one of them is always chosen. */
  choosable: NotificationFamilyPreference[];
  kind: FamilyKind | null;
}

function NotificationList({
  caption,
  families,
  choosable,
  kind,
}: NotificationListProps): ReactElement {
  const { t, language, formatDateTime } = useI18n();
  const { params, page, setFilter, goToPage } = useListParams();
  const [openId, setOpenId] = useState<string | null>(null);
  const readState = READ_STATES.find((candidate) => candidate === params.get('state'));
  const requested = params.get('family');
  const family =
    kind === null
      ? (requested ?? undefined)
      : (choosable.find((candidate) => candidate.eventFamilyCode === requested) ?? choosable[0])
          ?.eventFamilyCode;

  const load = useCallback(
    (signal: AbortSignal) =>
      notificationsApi.list(
        {
          unread: readState === undefined ? undefined : readState === 'unread',
          eventFamilyCode: family,
          page,
          pageSize: PAGE_SIZE,
        },
        signal,
      ),
    [readState, family, page],
  );
  const notifications = useApiResource(load);
  useReloadOnUnreadChange(notifications.reload);

  const filtered = readState !== undefined || (kind === null && family !== undefined);
  return (
    <>
      <div className="filters" role="search" aria-label={t('common.filters.label')}>
        <SelectField
          label={t('notifications.filters.readState')}
          name="state"
          value={readState ?? ''}
          placeholder={t('common.filters.any')}
          options={READ_STATES.map((value: ReadState) => ({
            value,
            label: t(`notifications.readState.${value}`),
          }))}
          onChange={(value) => {
            setFilter('state', value);
          }}
        />
        {choosable.length > 0 && (
          <SelectField
            label={t('notifications.filters.family')}
            name="family"
            value={family ?? ''}
            placeholder={kind === null ? t('common.filters.any') : undefined}
            options={choosable.map((candidate) => ({
              value: candidate.eventFamilyCode,
              label: candidate.label[language],
            }))}
            onChange={(value) => {
              setFilter('family', value);
            }}
          />
        )}
      </div>

      {notifications.loading && <LoadingState label={t('notifications.list.loading')} />}
      {notifications.error !== null && (
        <ErrorState
          message={notificationProblemMessage(notifications.error, t)}
          onRetry={notifications.reload}
        />
      )}
      {notifications.data !== undefined &&
        (notifications.data.items.length === 0 ? (
          <EmptyState
            title={filtered ? t('notifications.list.emptyFiltered') : t('notifications.list.empty')}
          />
        ) : (
          <>
            <TableContainer caption={caption}>
              <thead>
                <tr>
                  <th scope="col">{t('notifications.fields.status')}</th>
                  <th scope="col">{t('notifications.fields.notification')}</th>
                  <th scope="col">{t('notifications.fields.family')}</th>
                  <th scope="col">{t('notifications.fields.received')}</th>
                </tr>
              </thead>
              <tbody>
                {notifications.data.items.map((notification) => (
                  <NotificationRow
                    key={notification.id}
                    notification={notification}
                    familyName={familyLabel(families, notification.eventFamilyCode, language)}
                    receivedAt={formatDateTime(notification.sentAt ?? notification.occurredAt)}
                    onOpen={() => {
                      setOpenId(notification.id);
                    }}
                  />
                ))}
              </tbody>
            </TableContainer>
            <Pagination
              page={notifications.data.page}
              pageSize={notifications.data.pageSize}
              totalCount={notifications.data.totalCount}
              onPageChange={goToPage}
            />
          </>
        ))}

      <NotificationDetailDialog
        notificationId={openId}
        families={families}
        onClose={() => {
          setOpenId(null);
          // Opening it may have read it: the badge, and through it this page, follow the API's count.
          void unreadCountStore.refresh();
        }}
      />
    </>
  );
}

function NotificationRow({
  notification,
  familyName,
  receivedAt,
  onOpen,
}: {
  notification: NotificationSummary;
  familyName: string;
  receivedAt: string;
  onOpen: () => void;
}): ReactElement {
  const { t } = useI18n();
  const unread = isUnread(notification);
  return (
    <tr className={unread ? 'row--unread' : undefined}>
      <td>
        <StatusBadge
          label={t(unread ? 'notifications.readState.unread' : 'notifications.readState.read')}
          tone={unread ? 'info' : 'neutral'}
        />
      </td>
      <td>
        <button type="button" className="button button--link" onClick={onOpen}>
          <span lang={notification.language} dir="auto">
            {headline(notification)}
          </span>
        </button>
      </td>
      <td>{familyName}</td>
      <td>{receivedAt}</td>
    </tr>
  );
}
