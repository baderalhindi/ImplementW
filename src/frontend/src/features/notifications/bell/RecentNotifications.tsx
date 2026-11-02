import { type ReactElement, useCallback } from 'react';
import { Link, useNavigate } from 'react-router';

import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { LoadingState } from '@/shared/ui/States.tsx';

import { notificationsApi } from '../api/notificationsApi.ts';
import { type NotificationSummary } from '../api/types.ts';
import { headline, isAppRoute, isUnread } from '../presentation.ts';
import { unreadCountStore } from '../unreadCount.ts';

/** How many of the newest notifications the top bar lists. */
export const RECENT_COUNT = 6;

/**
 * The top bar's notification list (the 27 Oct 2026 review): the newest notifications, unread ones marked. Choosing one reads it and opens
 * the record it is about, or the inbox when it names none. The Notifications menu keeps every screen; this is the quick look.
 */
export function RecentNotifications({ onClose }: { onClose: () => void }): ReactElement {
  const { t, formatDateTime } = useI18n();
  const navigate = useNavigate();
  const recent = useApiResource(
    useCallback(
      (signal: AbortSignal) => notificationsApi.list({ page: 1, pageSize: RECENT_COUNT }, signal),
      [],
    ),
  );

  const open = async (notification: NotificationSummary): Promise<void> => {
    onClose();
    if (isUnread(notification)) {
      try {
        await notificationsApi.markRead(notification.id);
      } catch {
        // Not read on the server: it stays unread in the inbox, and the record still opens.
      }
      void unreadCountStore.refresh();
    }
    void navigate(isAppRoute(notification.deepLink) ? notification.deepLink : '/notifications');
  };

  return (
    <div className="notification-menu">
      <p className="notification-menu__title">{t('notifications.bell.title')}</p>
      {recent.data === undefined ? (
        recent.loading ? (
          <LoadingState label={t('notifications.list.loading')} />
        ) : (
          <p className="notification-menu__empty">{t('notifications.bell.unavailable')}</p>
        )
      ) : recent.data.items.length === 0 ? (
        <p className="notification-menu__empty">{t('notifications.list.empty')}</p>
      ) : (
        <ul className="notification-menu__list">
          {recent.data.items.map((notification) => {
            const unread = isUnread(notification);
            return (
              <li key={notification.id}>
                <button
                  type="button"
                  className={
                    unread
                      ? 'notification-menu__item notification-menu__item--unread'
                      : 'notification-menu__item'
                  }
                  onClick={() => {
                    void open(notification);
                  }}
                >
                  <span className="notification-menu__dot" aria-hidden="true" />
                  <span className="notification-menu__text">
                    <span lang={notification.language} dir="auto">
                      {headline(notification)}
                    </span>
                    <span className="notification-menu__time">
                      {formatDateTime(notification.sentAt ?? notification.occurredAt)}
                      {unread && (
                        <span className="visually-hidden">
                          {` · ${t('notifications.readState.unread')}`}
                        </span>
                      )}
                    </span>
                  </span>
                </button>
              </li>
            );
          })}
        </ul>
      )}
      <Link className="notification-menu__all" to="/notifications" onClick={onClose}>
        {t('notifications.bell.all')}
      </Link>
    </div>
  );
}
