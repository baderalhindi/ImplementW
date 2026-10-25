import { type ReactElement, useCallback, useId, useState } from 'react';
import { Link } from 'react-router';

import { Detail } from '@/features/projects/components/Detail.tsx';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
import { ErrorState, LoadingState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { auditEventPath, canReadSystemEvents } from './access.ts';
import { auditActivityApi } from './api/auditActivityApi.ts';
import {
  type ActivityProjectionReport,
  type AuditIngestionStatus,
  type AuditIntegrityReport,
} from './api/types.ts';
import { RequireAuditAccess } from './components/RequireAuditAccess.tsx';
import { auditProblemMessage } from './problems.ts';

function When({ value }: { value: string | null }): ReactElement {
  const { t, formatDateTime } = useI18n();
  return value === null ? (
    <>{t('common.values.none')}</>
  ) : (
    <time dateTime={value}>{formatDateTime(value)}</time>
  );
}

function PipelineStatus({ status }: { status: AuditIngestionStatus }): ReactElement {
  const { t } = useI18n();
  const healthy = status.failingMessages === 0;
  return (
    <dl className="details" data-pipeline-healthy={healthy}>
      <Detail term={t('auditActivity.system.state')}>
        <StatusBadge
          label={t(healthy ? 'auditActivity.system.healthy' : 'auditActivity.system.failing')}
          tone={healthy ? 'positive' : 'negative'}
        />
      </Detail>
      <Detail term={t('auditActivity.system.pendingMessages')}>{status.pendingMessages}</Detail>
      <Detail term={t('auditActivity.system.failingMessages')}>{status.failingMessages}</Detail>
      <Detail term={t('auditActivity.system.oldestPending')}>
        <When value={status.oldestPendingOccurredAt} />
      </Detail>
      <Detail term={t('auditActivity.system.lastError')}>
        {status.lastError === null ? (
          t('common.values.none')
        ) : (
          <code dir="ltr">{status.lastError}</code>
        )}
      </Detail>
      <Detail term={t('auditActivity.system.eventsStored')}>{status.eventsStored}</Detail>
      <Detail term={t('auditActivity.system.storeHead')}>
        <When value={status.storeHeadRecordedAt} />
      </Detail>
      <Detail term={t('auditActivity.system.projectionLag')}>{status.projectionLag}</Detail>
      <Detail term={t('auditActivity.system.projectionCheckpoint')}>
        <When value={status.projectionCheckpointRecordedAt} />
      </Detail>
    </dl>
  );
}

/** Each finding names its event; opening it is ADM-051 under the reader's own audit categories. */
function EventLinks({
  items,
  label,
}: {
  items: { auditEventId: string; kind: string }[];
  label: (kind: string) => string;
}): ReactElement | null {
  if (items.length === 0) {
    return null;
  }
  return (
    <ul className="link-list">
      {items.map((item) => (
        <li key={`${item.kind}-${item.auditEventId}`}>
          <Link to={auditEventPath(item.auditEventId)}>
            <code dir="ltr">{item.auditEventId}</code>
          </Link>{' '}
          <span className="cell__aside">{label(item.kind)}</span>
        </li>
      ))}
    </ul>
  );
}

function IntegrityResult({ report }: { report: AuditIntegrityReport }): ReactElement {
  const { t } = useI18n();
  return (
    <div data-check="integrity" data-intact={report.intact}>
      <StatusBadge
        label={t(report.intact ? 'auditActivity.system.intact' : 'auditActivity.system.tampered')}
        tone={report.intact ? 'positive' : 'negative'}
      />
      <dl className="details">
        <Detail term={t('auditActivity.system.eventsVerified')}>{report.eventsVerified}</Detail>
        <Detail term={t('auditActivity.system.brokenLinks')}>{report.brokenLinks}</Detail>
        <Detail term={t('auditActivity.system.digestMismatches')}>
          {report.attributeDigestMismatches}
        </Detail>
        <Detail term={t('auditActivity.system.withoutDigest')}>{report.eventsWithoutDigest}</Detail>
      </dl>
      <EventLinks
        items={report.findings}
        label={(kind) =>
          t(
            kind === 'BROKEN_LINK'
              ? 'auditActivity.system.finding.BROKEN_LINK'
              : 'auditActivity.system.finding.ATTRIBUTE_DIGEST_MISMATCH',
          )
        }
      />
    </div>
  );
}

function ProjectionResult({ report }: { report: ActivityProjectionReport }): ReactElement {
  const { t } = useI18n();
  return (
    <div data-check="projection" data-matches={report.matches}>
      <StatusBadge
        label={t(report.matches ? 'auditActivity.system.matches' : 'auditActivity.system.differs')}
        tone={report.matches ? 'positive' : 'negative'}
      />
      <dl className="details">
        <Detail term={t('auditActivity.system.eventsCompared')}>{report.eventsCompared}</Detail>
        <Detail term={t('auditActivity.system.missing')}>{report.missing}</Detail>
        <Detail term={t('auditActivity.system.unexpected')}>{report.unexpected}</Detail>
        <Detail term={t('auditActivity.system.changed')}>{report.changed}</Detail>
        <Detail term={t('auditActivity.system.pendingEvents')}>{report.pendingEvents}</Detail>
      </dl>
      <EventLinks
        items={report.differences}
        label={(kind) =>
          t(
            kind === 'MISSING'
              ? 'auditActivity.system.difference.MISSING'
              : kind === 'UNEXPECTED'
                ? 'auditActivity.system.difference.UNEXPECTED'
                : 'auditActivity.system.difference.CHANGED',
          )
        }
      />
    </div>
  );
}

type Running = 'integrity' | 'projection' | null;

function SystemEvents(): ReactElement {
  const { t } = useI18n();
  const load = useCallback((signal: AbortSignal) => auditActivityApi.ingestionStatus(signal), []);
  const status = useApiResource(load, { keepWhileReloading: true });
  const [running, setRunning] = useState<Running>(null);
  const [notice, setNotice] = useState<Notice | null>(null);
  const [integrity, setIntegrity] = useState<AuditIntegrityReport | null>(null);
  const [projection, setProjection] = useState<ActivityProjectionReport | null>(null);
  const scopeId = useId();

  const run = <T,>(which: Exclude<Running, null>, call: () => Promise<T>, done: (r: T) => void) => {
    setRunning(which);
    setNotice(null);
    call().then(
      (result) => {
        done(result);
        setRunning(null);
        status.reload();
      },
      (error: unknown) => {
        setNotice({ tone: 'warning', message: auditProblemMessage(error, t) });
        setRunning(null);
      },
    );
  };

  return (
    <>
      <PageHeader
        title={t('auditActivity.system.title')}
        description={t('auditActivity.system.description')}
      />
      <aside className="audit-boundary" data-scope="narrow-technical" aria-labelledby={scopeId}>
        <p id={scopeId} className="audit-boundary__title">
          {t('auditActivity.system.scopeTitle')}
        </p>
        <p>{t('auditActivity.system.scope')}</p>
      </aside>
      <PageNotice notice={notice} />

      <section className="section">
        <div className="section__header">
          <h2>{t('auditActivity.system.pipeline')}</h2>
          <button type="button" className="button" onClick={status.reload}>
            {t('auditActivity.system.refresh')}
          </button>
        </div>
        {status.data === undefined ? (
          status.loading ? (
            <LoadingState />
          ) : (
            <ErrorState error={status.error} message={auditProblemMessage(status.error, t)} onRetry={status.reload} />
          )
        ) : (
          <PipelineStatus status={status.data} />
        )}
      </section>

      <section className="section">
        <h2>{t('auditActivity.system.checks')}</h2>
        <p>{t('auditActivity.system.checksNote')}</p>
        <div className="section__header">
          <h3>{t('auditActivity.system.integrityTitle')}</h3>
          <button
            type="button"
            className="button"
            disabled={running !== null}
            onClick={() => {
              run('integrity', auditActivityApi.verifyIntegrity, setIntegrity);
            }}
          >
            {running === 'integrity'
              ? t('auditActivity.system.running')
              : t('auditActivity.system.verify')}
          </button>
        </div>
        {integrity !== null && <IntegrityResult report={integrity} />}
        <div className="section__header">
          <h3>{t('auditActivity.system.projectionTitle')}</h3>
          <button
            type="button"
            className="button"
            disabled={running !== null}
            onClick={() => {
              run('projection', auditActivityApi.checkProjection, setProjection);
            }}
          >
            {running === 'projection'
              ? t('auditActivity.system.running')
              : t('auditActivity.system.check')}
          </button>
        </div>
        {projection !== null && <ProjectionResult report={projection} />}
      </section>
    </>
  );
}

/**
 * ADM-052 System Event Logs (FG-06 §8.1, BR-AUD-043): a narrow technical view, labelled as such — the audit
 * subsystem's own state (§24.4: capture backlog and failures, store head, Activity lag) and its two read-only checks,
 * each finding linked to the formal event. It is not the Audit Trail and not an observability or SIEM console; the
 * searchable system-event list (AUD-API-055) has no API yet (TASK-073 F-4). AUDIT_OPERATIONS_MANAGE only.
 */
export function SystemEventLogsPage(): ReactElement {
  return (
    <RequireAuditAccess allows={canReadSystemEvents} deniedReason="auditActivity.denied.system">
      {() => <SystemEvents />}
    </RequireAuditAccess>
  );
}
