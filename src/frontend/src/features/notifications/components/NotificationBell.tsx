import { type ReactElement, useEffect } from 'react';
import { Link } from 'react-router';

import { useI18n } from '@/shared/i18n/i18n.ts';

import { unreadCountStore, useUnreadCount } from '../unreadCount.ts';

function BellIcon(): ReactElement {
  return (
    <svg className="bell__icon" viewBox="0 0 24 24" width="20" height="20" aria-hidden="true" focusable="false">
      <path
        d="M6 16V11a6 6 0 1 1 12 0v5l1.5 2h-15zM10 20.5a2 2 0 0 0 4 0"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.75"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

/**
 * The header's link to SCR-150: a bell with the unread badge on its corner. It polls while it is mounted, which is
 * while someone is signed in. The number shown is the API's, unrounded; the word is visually hidden, so the accessible
 * name is still "Notifications 3 unread", and it is the tooltip.
 */
export function NotificationBell(): ReactElement {
  const { t } = useI18n();
  const { count } = useUnreadCount();

  useEffect(() => unreadCountStore.watch(), []);

  return (
    <Link className="bell" to="/notifications" title={t('notifications.bell.text')}>
      <BellIcon />
      <span className="visually-hidden">{t('notifications.bell.text')}</span>
      {count !== null && count > 0 && (
        <span className="bell__count">
          <span aria-hidden="true">{count}</span>
          <span className="visually-hidden">{` ${t('notifications.bell.unread', { count })}`}</span>
        </span>
      )}
    </Link>
  );
}
