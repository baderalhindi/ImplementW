import { type ReactElement, type ReactNode, useCallback } from 'react';
import { Link } from 'react-router';

import { Detail } from '@/features/projects/components/Detail.tsx';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { TableContainer } from '@/shared/ui/Layout.tsx';
import { ErrorState, LoadingState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { AUDIT_TRAIL_PATH } from '../access.ts';
import { auditActivityApi } from '../api/auditActivityApi.ts';
import { type AuditEventDetail } from '../api/types.ts';
import { forwardingTone, outcomeTone, sourceRecordPath } from '../presentation.ts';
import { auditProblemMessage } from '../problems.ts';

import { ActorName } from './ActorName.tsx';

function trailFilteredBy(name: 'actorUserId' | 'correlationId', value: string): string {
  return `${AUDIT_TRAIL_PATH}?${new URLSearchParams({ [name]: value }).toString()}`;
}

function Section({ title, children }: { title: string; children: ReactNode }): ReactElement {
  return (
    <section className="section">
      <h2>{title}</h2>
      {children}
    </section>
  );
}

/** A technical identifier: left-to-right in either language, wrapped anywhere so a hash never widens the page. */
function Code({ children }: { children: string }): ReactElement {
  return (
    <code className="break-all" dir="ltr">
      {children}
    </code>
  );
}

/**
 * ADM-051 Audit Event Detail (FG-06 §8.3): the immutable event — what, when, outcome, actor, source record, project,
 * correlation, the attributes captured with it, the request context withheld from the reader, its place in the hash
 * chain and its SIEM forwarding. It offers no edit or delete: there is none (TASK-073 D-7). An event the reader may not
 * see answers 404, said as "not found or not available to you", never whether it exists.
 */
export function AuditEventDetailView({ auditEventId }: { auditEventId: string }): ReactElement {
  const { t } = useI18n();
  const load = useCallback(
    (signal: AbortSignal) => auditActivityApi.event(auditEventId, signal),
    [auditEventId],
  );
  const detail = useApiResource(load);

  if (detail.loading) {
    return <LoadingState />;
  }
  if (detail.error !== null || detail.data === undefined) {
    return <ErrorState message={auditProblemMessage(detail.error, t)} onRetry={detail.reload} />;
  }
  return <EventDetail detail={detail.data} />;
}

function EventDetail({ detail }: { detail: AuditEventDetail }): ReactElement {
  const { t, formatDateTime } = useI18n();
  const { event, attributes, maskedAttributes, integrity, forwarding } = detail;
  const none = t('common.values.none');
  const recordPath = sourceRecordPath(event, event.scopeProjectId);

  return (
    <article className="audit-evidence" data-audit-event={event.id}>
      <p className="audit-evidence__seal" role="note">
        {t('auditActivity.detail.immutable')}
      </p>

      <Section title={t('auditActivity.detail.event')}>
        <dl className="details">
          <Detail term={t('auditActivity.fields.eventId')}>
            <Code>{event.id}</Code>
          </Detail>
          <Detail term={t('auditActivity.fields.eventType')}>
            <Code>{event.eventType}</Code>
          </Detail>
          <Detail term={t('auditActivity.fields.eventClass')}>
            {t(`auditActivity.eventClass.${event.eventClass}`)}
          </Detail>
          <Detail term={t('auditActivity.fields.category')}>
            {t(`auditActivity.category.${event.category}`)}
          </Detail>
          <Detail term={t('auditActivity.fields.outcome')}>
            <StatusBadge
              label={t(`auditActivity.outcome.${event.outcome}`)}
              tone={outcomeTone(event.outcome)}
            />
          </Detail>
          <Detail term={t('auditActivity.fields.occurredAt')}>
            <time dateTime={event.occurredAt}>{formatDateTime(event.occurredAt)}</time>
          </Detail>
          <Detail term={t('auditActivity.fields.recordedAt')}>
            <time dateTime={event.recordedAt}>{formatDateTime(event.recordedAt)}</time>
          </Detail>
          <Detail term={t('auditActivity.fields.classification')}>
            {event.dataClassificationItemId === null
              ? t('auditActivity.detail.unclassified')
              : t('auditActivity.detail.classified')}
          </Detail>
        </dl>
      </Section>

      <Section title={t('auditActivity.detail.actor')}>
        <dl className="details">
          <Detail term={t('auditActivity.fields.actorType')}>
            {t(`auditActivity.actorType.${event.actorType}`)}
          </Detail>
          <Detail term={t('auditActivity.fields.actor')}>
            <ActorName event={event} />
          </Detail>
          <Detail term={t('auditActivity.fields.actorUserId')}>
            {event.actorUserId === null ? (
              none
            ) : (
              <>
                <Code>{event.actorUserId}</Code>{' '}
                <Link to={trailFilteredBy('actorUserId', event.actorUserId)}>
                  {t('auditActivity.detail.actorEvents')}
                </Link>
              </>
            )}
          </Detail>
        </dl>
      </Section>

      <Section title={t('auditActivity.detail.source')}>
        <dl className="details">
          <Detail term={t('auditActivity.fields.subject')}>
            {event.subjectModule === null ? (
              none
            ) : (
              <Code>{`${event.subjectModule}.${event.subjectType ?? ''}`}</Code>
            )}
          </Detail>
          <Detail term={t('auditActivity.fields.subjectId')}>
            {event.subjectId === null ? (
              none
            ) : (
              <>
                <Code>{event.subjectId}</Code>
                {recordPath !== null && (
                  <>
                    {' '}
                    <Link to={recordPath}>{t('auditActivity.detail.openRecord')}</Link>
                  </>
                )}
              </>
            )}
          </Detail>
          <Detail term={t('auditActivity.fields.projectId')}>
            {event.scopeProjectId === null ? (
              none
            ) : (
              <>
                <Code>{event.scopeProjectId}</Code>{' '}
                <Link to={`/projects/${event.scopeProjectId}`}>
                  {t('auditActivity.detail.openProject')}
                </Link>
              </>
            )}
          </Detail>
          <Detail term={t('auditActivity.fields.externalEntityId')}>
            {event.scopeExternalEntityId === null ? (
              none
            ) : (
              <Code>{event.scopeExternalEntityId}</Code>
            )}
          </Detail>
          <Detail term={t('auditActivity.fields.correlationId')}>
            <Code>{event.correlationId}</Code>{' '}
            <Link to={trailFilteredBy('correlationId', event.correlationId)}>
              {t('auditActivity.detail.correlatedEvents')}
            </Link>
          </Detail>
        </dl>
      </Section>

      <Section title={t('auditActivity.detail.attributes')}>
        {attributes.length === 0 ? (
          <p>{t('auditActivity.detail.noAttributes')}</p>
        ) : (
          <TableContainer caption={t('auditActivity.detail.attributes')}>
            <thead>
              <tr>
                <th scope="col">{t('auditActivity.fields.attribute')}</th>
                <th scope="col">{t('auditActivity.fields.oldValue')}</th>
                <th scope="col">{t('auditActivity.fields.newValue')}</th>
              </tr>
            </thead>
            <tbody>
              {attributes.map((attribute) => (
                <tr key={attribute.name} data-attribute={attribute.name}>
                  <th scope="row">
                    <Code>{attribute.name}</Code>
                  </th>
                  <td dir="auto">{attribute.oldValue ?? none}</td>
                  <td dir="auto">{attribute.newValue ?? none}</td>
                </tr>
              ))}
            </tbody>
          </TableContainer>
        )}
        {maskedAttributes.length > 0 && (
          <div className="audit-evidence__masked" data-masked="true">
            <p>{t('auditActivity.detail.masked')}</p>
            <ul>
              {maskedAttributes.map((name) => (
                <li key={name}>
                  <Code>{name}</Code>
                </li>
              ))}
            </ul>
          </div>
        )}
      </Section>

      <Section title={t('auditActivity.detail.integrity')}>
        <dl className="details">
          <Detail term={t('auditActivity.fields.eventHash')}>
            <Code>{integrity.eventHash}</Code>
          </Detail>
          <Detail term={t('auditActivity.fields.previousEventHash')}>
            {integrity.previousEventHash === null ? (
              t('auditActivity.detail.chainStart')
            ) : (
              <Code>{integrity.previousEventHash}</Code>
            )}
          </Detail>
          <Detail term={t('auditActivity.fields.attributeDigest')}>
            {integrity.attributeDigest === null ? (
              t('auditActivity.detail.noDigest')
            ) : (
              <Code>{integrity.attributeDigest}</Code>
            )}
          </Detail>
          <Detail term={t('auditActivity.fields.forwarding')}>
            {forwarding === null ? (
              t('auditActivity.detail.notForwarded')
            ) : (
              <>
                <StatusBadge
                  label={t(`auditActivity.forwarding.${forwarding.status}`)}
                  tone={forwardingTone(forwarding.status)}
                />
                {forwarding.forwardedAt !== null && (
                  <span className="details__aside">{formatDateTime(forwarding.forwardedAt)}</span>
                )}
              </>
            )}
          </Detail>
        </dl>
      </Section>
    </article>
  );
}
