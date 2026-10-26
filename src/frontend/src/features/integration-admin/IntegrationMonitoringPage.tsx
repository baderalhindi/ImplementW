import { type ReactElement, useState } from 'react';
import { useSearchParams } from 'react-router';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { SelectField } from '@/shared/ui/FormFields.tsx';

import { type IntegrationCatalogueView, type OperationalAlertStatus } from './api/types.ts';
import { ALERT_STATUSES, LIVE_ALERT_STATUSES } from './catalogue.ts';
import { AlertList } from './components/AlertList.tsx';
import { CataloguePage } from './components/CataloguePage.tsx';
import { Section } from './components/Display.tsx';
import { IndicatorLegend } from './components/HealthIndicators.tsx';
import { IntegrationTable } from './components/IntegrationTable.tsx';
import { InvocationList } from './components/InvocationList.tsx';
import { QueueTable } from './components/QueueTable.tsx';
import { SyncRunList } from './components/SyncRunList.tsx';

const DEAD_LETTERS = { statuses: ['DEAD_LETTER'] } as const;

/** FG-05's health pass runs every 30 s (`IntegrationMonitoring:Worker:PollInterval`); reading more often shows nothing new. */
export const MONITORING_REFRESH_MS = 30_000;

/**
 * How many instances are down, and how many stale — counted per dimension, never combined into one health figure
 * (acceptance criterion 2) — then the work queued and the dead letters across every integration (TASK-092). Counting is
 * display; each state is the API's.
 */
function DimensionSummary({ catalogue }: { catalogue: IntegrationCatalogueView }): ReactElement {
  const { t } = useI18n();
  const instances = catalogue.items.flatMap((item) => item.instances);
  const count = (matches: (instance: (typeof instances)[number]) => boolean) =>
    instances.filter(matches).length;
  return (
    <dl className="integration-summary">
      <div data-summary="availability">
        <dt>{t('integrationAdmin.monitoring.down')}</dt>
        <dd>{count((instance) => instance.availability === 'DOWN')}</dd>
      </div>
      <div data-summary="freshness">
        <dt>{t('integrationAdmin.monitoring.stale')}</dt>
        <dd>{count((instance) => instance.freshness === 'STALE')}</dd>
      </div>
      <div data-summary="alerts">
        <dt>{t('integrationAdmin.monitoring.liveAlerts')}</dt>
        <dd>{instances.reduce((sum, instance) => sum + instance.openAlertCount, 0)}</dd>
      </div>
      <div data-summary="queued">
        <dt>{t('integrationAdmin.monitoring.queued')}</dt>
        <dd>{instances.reduce((sum, instance) => sum + instance.queue.depth, 0)}</dd>
      </div>
      <div data-summary="deadLetters">
        <dt>{t('integrationAdmin.monitoring.deadLetters')}</dt>
        <dd>{instances.reduce((sum, instance) => sum + instance.queue.deadLetters, 0)}</dd>
      </div>
    </dl>
  );
}

/** Live alerts by default; one status when chosen, so resolved and dismissed alerts stay reachable. */
function alertStatusesFrom(params: URLSearchParams): readonly OperationalAlertStatus[] {
  const chosen = ALERT_STATUSES.find((status) => status === params.get('alertStatus'));
  return chosen === undefined ? LIVE_ALERT_STATUSES : [chosen];
}

/**
 * ADM-047 Integration Monitoring (FG-05 §13): the operator's workspace and the operational dashboard of TASK-092
 * (docs/operations/operational-dashboards.md OD-1). Each integration's availability and freshness are two indicators
 * in two columns, with a legend stating what each means first (Blueprint §17.1: availability is "distinct from
 * freshness"); then every integration's queue depth and dead letters; then the alerts to handle, the dead letters, and
 * the runs. Every part is read again every 30 seconds, so the figures and the lists stay live.
 */
export function IntegrationMonitoringPage(): ReactElement {
  const { t } = useI18n();
  const [params, setParams] = useSearchParams();
  const [deadLetterPage, setDeadLetterPage] = useState(1);
  const alertStatus = params.get('alertStatus') ?? '';
  const title = t('integrationAdmin.monitoring.title');
  return (
    <CataloguePage
      title={title}
      description={t('integrationAdmin.monitoring.description')}
      refreshEvery={MONITORING_REFRESH_MS}
    >
      {(catalogue) => (
        <>
          <IndicatorLegend />
          <DimensionSummary catalogue={catalogue} />
          <Section title={t('integrationAdmin.monitoring.health')}>
            <IntegrationTable
              items={catalogue.items}
              caption={t('integrationAdmin.monitoring.health')}
              emptyTitle={t('integrationAdmin.overview.empty')}
            />
          </Section>
          <Section
            title={t('integrationAdmin.queue.title')}
            description={t('integrationAdmin.queue.note')}
          >
            <QueueTable
              items={catalogue.items}
              caption={t('integrationAdmin.queue.title')}
              emptyTitle={t('integrationAdmin.overview.empty')}
            />
          </Section>
          <Section title={t('integrationAdmin.alerts.title')}>
            <div className="filters filters--inline">
              <SelectField
                label={t('integrationAdmin.fields.alertStatus')}
                name="alertStatus"
                value={alertStatus}
                placeholder={t('integrationAdmin.alerts.live')}
                options={ALERT_STATUSES.map((status) => ({
                  value: status,
                  label: t(`integrationAdmin.alertStatus.${status}`),
                }))}
                onChange={(value) => {
                  const next = new URLSearchParams(params);
                  if (value === '') {
                    next.delete('alertStatus');
                  } else {
                    next.set('alertStatus', value);
                  }
                  setParams(next);
                }}
              />
            </div>
            <AlertList
              key={alertStatus}
              query={{ statuses: alertStatusesFrom(params) }}
              caption={t('integrationAdmin.alerts.title')}
              emptyTitle={t('integrationAdmin.alerts.none')}
              refreshEvery={MONITORING_REFRESH_MS}
            />
          </Section>
          <Section
            title={t('integrationAdmin.monitoring.deadLetters')}
            description={t('integrationAdmin.monitoring.deadLettersNote')}
          >
            <InvocationList
              query={DEAD_LETTERS}
              caption={t('integrationAdmin.monitoring.deadLetters')}
              emptyTitle={t('integrationAdmin.monitoring.noDeadLetters')}
              page={deadLetterPage}
              onPageChange={setDeadLetterPage}
              refreshEvery={MONITORING_REFRESH_MS}
            />
          </Section>
          <Section title={t('integrationAdmin.syncRuns.title')}>
            <SyncRunList
              caption={t('integrationAdmin.syncRuns.title')}
              refreshEvery={MONITORING_REFRESH_MS}
            />
          </Section>
        </>
      )}
    </CataloguePage>
  );
}
