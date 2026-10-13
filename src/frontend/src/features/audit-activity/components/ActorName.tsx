import { type ReactElement } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';

import { type AuditEventSummary } from '../api/types.ts';

/**
 * Who acted: the person's current name (the id is the historical identity, FG-06 §7.3), or what kind of actor it was
 * when the event names no person — a service or an integration.
 */
export function ActorName({
  event,
}: {
  event: Pick<AuditEventSummary, 'actorType' | 'actorDisplayName' | 'actorUserId'>;
}): ReactElement {
  const { t } = useI18n();
  if (event.actorDisplayName !== null) {
    return <span dir="auto">{event.actorDisplayName}</span>;
  }
  return (
    <span className="cell__aside">
      {event.actorType === 'USER' && event.actorUserId === null
        ? t('auditActivity.actor.anonymous')
        : t(`auditActivity.actorType.${event.actorType}`)}
    </span>
  );
}
