import { type ReactElement } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';

import { NotificationInbox } from './NotificationInbox.tsx';

/** SCR-150 All Notifications, with MOD-070 and MOD-071. */
export function AllNotificationsPage(): ReactElement {
  const { t } = useI18n();
  return (
    <NotificationInbox
      title={t('notifications.all.title')}
      description={t('notifications.all.description')}
      kind={null}
    />
  );
}

/** SCR-151 Alerts, with MOD-070. */
export function AlertsPage(): ReactElement {
  const { t } = useI18n();
  return (
    <NotificationInbox
      title={t('notifications.alert.title')}
      description={t('notifications.alert.description')}
      kind="alert"
    />
  );
}

/** SCR-152 Escalation Notifications, with MOD-070. */
export function EscalationsPage(): ReactElement {
  const { t } = useI18n();
  return (
    <NotificationInbox
      title={t('notifications.escalation.title')}
      description={t('notifications.escalation.description')}
      kind="escalation"
    />
  );
}
