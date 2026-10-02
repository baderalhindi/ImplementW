import { type ReactElement, useCallback } from 'react';
import { Link } from 'react-router';

import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { notificationsApi } from '../api/notificationsApi.ts';
import { type NotificationFamilyPreference } from '../api/types.ts';
import { familyLabel, isAppRoute, isUnread } from '../presentation.ts';
import { notificationProblemMessage } from '../problems.ts';

interface NotificationDetailDialogProps {
  /** The notification to show; null while closed. */
  notificationId: string | null;
  families: NotificationFamilyPreference[] | undefined;
  onClose: () => void;
}

/**
 * MOD-070 Notification Detail. Opening an unread notification reads it (SENT → READ); a read one is only shown. The
 * text is plain, as rendered for the recipient in their language (TASK-039 D-7, D-8), and the link is the SPA route the
 * source gave.
 */
export function NotificationDetailDialog({
  notificationId,
  families,
  onClose,
}: NotificationDetailDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog
      open={notificationId !== null}
      title={t('notifications.detail.title')}
      onClose={onClose}
    >
      {notificationId !== null && (
        <NotificationDetail notificationId={notificationId} families={families} onClose={onClose} />
      )}
    </Dialog>
  );
}

function NotificationDetail({
  notificationId,
  families,
  onClose,
}: {
  notificationId: string;
  families: NotificationFamilyPreference[] | undefined;
  onClose: () => void;
}): ReactElement {
  const { t, language, formatDateTime } = useI18n();
  const load = useCallback(
    async (signal: AbortSignal) => {
      const notification = await notificationsApi.get(notificationId, signal);
      return isUnread(notification) ? notificationsApi.markRead(notificationId) : notification;
    },
    [notificationId],
  );
  const detail = useApiResource(load);
  const notification = detail.data;

  return (
    <>
      {detail.loading && <LoadingState label={t('notifications.detail.loading')} />}
      {detail.error !== null && (
        <ErrorState message={notificationProblemMessage(detail.error, t)} onRetry={detail.reload} />
      )}
      {notification !== undefined && (
        <article className="notification" lang={notification.language}>
          {notification.subject !== null && (
            <h3 className="notification__subject" dir="auto">
              {notification.subject}
            </h3>
          )}
          <p className="notification__body" dir="auto">
            {notification.body}
          </p>
          <dl className="details" lang={language}>
            <div className="details__row">
              <dt>{t('notifications.fields.family')}</dt>
              <dd>{familyLabel(families, notification.eventFamilyCode, language)}</dd>
            </div>
            <div className="details__row">
              <dt>{t('notifications.fields.received')}</dt>
              <dd>{formatDateTime(notification.sentAt ?? notification.occurredAt)}</dd>
            </div>
            {notification.readAt !== null && (
              <div className="details__row">
                <dt>{t('notifications.fields.readAt')}</dt>
                <dd>{formatDateTime(notification.readAt)}</dd>
              </div>
            )}
          </dl>
          {isAppRoute(notification.deepLink) && (
            <p lang={language}>
              <Link to={notification.deepLink} onClick={onClose}>
                {t('notifications.detail.openRecord')}
              </Link>
            </p>
          )}
        </article>
      )}
      <div className="form__actions">
        <button type="button" className="button" onClick={onClose}>
          {t('notifications.detail.close')}
        </button>
      </div>
    </>
  );
}
