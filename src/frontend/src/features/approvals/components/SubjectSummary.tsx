import { type ReactElement } from 'react';
import { Link } from 'react-router';

import { changeRequestPath } from '@/features/change-requests/paths.ts';
import { closeoutCasePath, suspensionRequestPath } from '@/features/suspension-closure/paths.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';

import { type ApprovalSubject } from '../api/types.ts';

/**
 * The source screen a subject opens, where its module has one readable without the project: WF-08's SCR-107, WF-09's
 * SCR-110 and WF-10's SCR-113.
 */
function subjectPath(subject: ApprovalSubject): string | null {
  switch (`${subject.module}.${subject.type}`) {
    case 'ChangeRequest.ChangeRequest':
      return changeRequestPath(subject.id);
    case 'Suspension.SuspensionRequest':
      return suspensionRequestPath(subject.id);
    case 'Closure.CompletionCase':
      return closeoutCasePath('completion', subject.id);
    case 'Closure.ClosureCase':
      return closeoutCasePath('closure', subject.id);
    default:
      return null;
  }
}

/**
 * What a request is about, as its source module names it: module, record type and revision, with the routing key that
 * chose its approvers. Approval holds no title for the record (M-8); a change request, suspension request or closeout
 * case links to its detail, where the approver reads it before deciding.
 */
export function SubjectSummary({
  subject,
  routingKey,
}: {
  subject: ApprovalSubject;
  routingKey: string;
}): ReactElement {
  const { t } = useI18n();
  const path = subjectPath(subject);
  const name = `${subject.module} · ${subject.type}`;
  return (
    <span className="subject">
      <span className="subject__name" dir="ltr">
        {path === null ? name : <Link to={path}>{name}</Link>}
      </span>
      <span className="subject__meta">
        {t('approvals.subject.revision', { revision: subject.revisionNo })} ·{' '}
        <span dir="ltr">{routingKey}</span>
      </span>
    </span>
  );
}
