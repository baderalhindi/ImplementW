import { type ReactElement, useEffect } from 'react';
import { Link } from 'react-router';

import { useI18n } from '@/shared/i18n/i18n.ts';

import { unreadCountStore, useUnreadCount } from '../unreadCount.ts';

/**
 * The header's link to SCR-150 with the unread badge. It polls while it is mounted, which is while someone is signed
 * in. The number shown is the API's, unrounded; its accessible name is "Notifications 3 unread".
 */
export function NotificationBell(): ReactElement {
  const { t } = useI18n();
  const { count } = useUnreadCount();

  useEffect(() => unreadCountStore.watch(), []);

  return (
    <Link className="bell" to="/notifications">
      {t('notifications.bell.text')}
      {count !== null && count > 0 && (
        <span className="bell__count">
          <span aria-hidden="true">{count}</span>
          <span className="visually-hidden">{` ${t('notifications.bell.unread', { count })}`}</span>
        </span>
      )}
    </Link>
  );
}
