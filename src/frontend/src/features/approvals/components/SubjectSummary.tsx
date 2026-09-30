import { type ReactElement } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';

import { type ApprovalSubject } from '../api/types.ts';

/**
 * What a request is about, as its source module names it: module, record type and revision, with the routing key that
 * chose its approvers. Approval holds no title for the record (M-8); the source screens that do are later tasks.
 */
export function SubjectSummary({
  subject,
  routingKey,
}: {
  subject: ApprovalSubject;
  routingKey: string;
}): ReactElement {
  const { t } = useI18n();
  return (
    <span className="subject">
      <span className="subject__name" dir="ltr">
        {subject.module} · {subject.type}
      </span>
      <span className="subject__meta">
        {t('approvals.subject.revision', { revision: subject.revisionNo })} ·{' '}
        <span dir="ltr">{routingKey}</span>
      </span>
    </span>
  );
}
