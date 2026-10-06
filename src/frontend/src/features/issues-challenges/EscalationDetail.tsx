import { type ReactElement, useState } from 'react';
import { Link, useParams } from 'react-router';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { useSession } from '@/features/identity-access/session/useSession.ts';
import { Detail } from '@/features/projects/components/Detail.tsx';
import { isForbidden } from '@/features/risks/problems.ts';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
import { ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { canResolveEscalation, canWithdrawEscalation } from './access.ts';
import {
  ConcernStatusBadge,
  EscalationStatusBadge,
  SeverityBadge,
} from './components/ConcernBadges.tsx';
import { escalationAgeDays } from './concernRules.ts';
import {
  ResolveEscalationDialog,
  WithdrawEscalationDialog,
} from './dialogs/EscalationCommandDialogs.tsx';
import { concernPath } from './paths.ts';
import { concernProblemMessage } from './problems.ts';
import { useConcernLookups, useEscalationRecord, useRoleNames } from './useConcernData.ts';

/**
 * SCR-088 Escalation Detail: the escalation (number, status, addressee, reason, who raised it and when, how it ended
 * and the management direction) and the concern it escalates as it stands now — the API keeps no snapshot from the
 * moment of escalation. Read from the escalation and the concern only, so the role it is addressed to can open it
 * without reading the project. The addressee resolves it; its escalator withdraws it.
 */
export function EscalationDetail(): ReactElement | null {
  const { t, formatDateTime } = useI18n();
  const { session } = useSession();
  const { escalationId = '' } = useParams();
  const record = useEscalationRecord(escalationId);
  const lookups = useConcernLookups();
  const roles = useRoleNames();
  const [notice, setNotice] = useState<Notice | null>(null);
  const [dialog, setDialog] = useState<'resolve' | 'withdraw' | null>(null);
  const data = record.data;
  const personName = usePersonNames([
    data?.escalation.escalatedByUserId ?? null,
    data?.escalation.resolvedByUserId ?? null,
    data?.concern.assigneeUserId ?? null,
  ]);

  const back = (
    <p>
      <Link to="/issues-challenges/escalations">
        {t('issuesChallenges.actions.backToEscalations')}
      </Link>
    </p>
  );
  if (session === null) {
    return null;
  }
  if (data === undefined) {
    return (
      <>
        {back}
        {record.loading ? (
          <LoadingState />
        ) : isForbidden(record.error) ? (
          <p className="state">{t('issuesChallenges.forbidden')}</p>
        ) : (
          <ErrorState message={concernProblemMessage(record.error, t)} onRetry={record.reload} />
        )}
      </>
    );
  }

  const { escalation, concern, project } = data;
  const user = session.user;
  const open = escalation.status === 'OPEN';
  const resolver = canResolveEscalation(user, escalation, roles.code(escalation.escalatedToRoleId));
  const withdrawer = canWithdrawEscalation(user, escalation);
  const changed = (message: TranslationKey, tone: Notice['tone'] = 'success') => {
    setDialog(null);
    setNotice({ tone, message: t(message) });
    record.reload();
  };

  return (
    <>
      {back}
      <PageHeader
        title={t('issuesChallenges.escalationDetail.title', {
          number: escalation.escalationNo,
          concern: concern.title.text,
        })}
        actions={
          <>
            {resolver && (
              <button
                type="button"
                className="button button--primary"
                onClick={() => {
                  setDialog('resolve');
                }}
              >
                {t('issuesChallenges.resolveEscalation.confirm')}
              </button>
            )}
            {withdrawer && (
              <button
                type="button"
                className="button"
                onClick={() => {
                  setDialog('withdraw');
                }}
              >
                {t('issuesChallenges.withdrawEscalation.confirm')}
              </button>
            )}
          </>
        }
      />
      <PageNotice notice={notice} />
      <div className="progress risk-detail">
        <dl className="details">
          <Detail term={t('issuesChallenges.table.status')}>
            <EscalationStatusBadge status={escalation.status} />
          </Detail>
          <Detail term={t('issuesChallenges.escalations.table.addressedTo')}>
            {roles.name(escalation.escalatedToRoleId)}
          </Detail>
          <Detail term={t('issuesChallenges.escalations.table.raised')}>
            {formatDateTime(escalation.escalatedAt)}
            <span className="cell__aside">{personName(escalation.escalatedByUserId)}</span>
          </Detail>
          <Detail term={t('issuesChallenges.escalationDetail.age')}>
            {t(
              open
                ? 'issuesChallenges.escalations.openFor'
                : 'issuesChallenges.escalations.wasOpenFor',
              { days: escalationAgeDays(escalation) },
            )}
          </Detail>
          {!open && escalation.resolvedAt !== null && (
            <Detail term={t('issuesChallenges.escalationDetail.ended')}>
              {formatDateTime(escalation.resolvedAt)}
              <span className="cell__aside">{personName(escalation.resolvedByUserId)}</span>
            </Detail>
          )}
        </dl>

        <section className="section" aria-labelledby="escalation-reason">
          <h2 id="escalation-reason">{t('issuesChallenges.fields.reason')}</h2>
          <p dir="auto" className="risk-detail__text">
            {escalation.reason.text}
          </p>
        </section>

        {escalation.resolution !== null && (
          <section className="section" aria-labelledby="escalation-direction">
            <h2 id="escalation-direction">{t('issuesChallenges.fields.direction')}</h2>
            <p dir="auto" className="risk-detail__text">
              {escalation.resolution.text}
            </p>
          </section>
        )}

        <section className="section" aria-labelledby="escalation-concern">
          <h2 id="escalation-concern">{t('issuesChallenges.escalationDetail.concernTitle')}</h2>
          <p className="form__note">{t('issuesChallenges.escalationDetail.concernNow')}</p>
          <dl className="details">
            <Detail term={t(`issuesChallenges.type.${concern.concernType}`)}>
              <Link to={concernPath(concern.projectId, concern.id)} dir="auto">
                {concern.title.text}
              </Link>
            </Detail>
            <Detail term={t('issuesChallenges.table.project')}>
              {project === null ? (
                t('issuesChallenges.escalationDetail.projectUnreadable')
              ) : (
                <span dir="auto">{project.title.text}</span>
              )}
            </Detail>
            <Detail term={t('issuesChallenges.table.status')}>
              <ConcernStatusBadge status={concern.status} />
            </Detail>
            <Detail term={t('issuesChallenges.fields.severity')}>
              <SeverityBadge concern={concern} itemLabel={lookups.itemLabel} />
            </Detail>
            <Detail term={t('issuesChallenges.fields.priority')}>
              {lookups.itemLabel(concern.priorityItemId)}
            </Detail>
            <Detail term={t('issuesChallenges.table.assignee')}>
              {concern.assigneeUserId === null
                ? t('issuesChallenges.table.unassigned')
                : personName(concern.assigneeUserId)}
            </Detail>
            <Detail term={t('issuesChallenges.fields.targetResolutionDate')}>
              {concern.targetResolutionDate === null ? (
                t('issuesChallenges.table.noTarget')
              ) : (
                <span dir="ltr">{concern.targetResolutionDate}</span>
              )}
            </Detail>
          </dl>
        </section>
      </div>

      {dialog === 'resolve' && (
        <ResolveEscalationDialog
          escalation={escalation}
          onClose={() => {
            setDialog(null);
          }}
          onDone={() => {
            changed('issuesChallenges.done.escalationResolved');
          }}
          onStale={() => {
            changed('issuesChallenges.done.stale', 'warning');
          }}
        />
      )}
      {dialog === 'withdraw' && (
        <WithdrawEscalationDialog
          escalation={escalation}
          onClose={() => {
            setDialog(null);
          }}
          onDone={() => {
            changed('issuesChallenges.done.escalationWithdrawn');
          }}
          onStale={() => {
            changed('issuesChallenges.done.stale', 'warning');
          }}
        />
      )}
    </>
  );
}
