import { type ReactElement, useCallback, useRef, useState } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';

import { ConfirmDialog } from '@/features/identity-access/components/ConfirmDialog.tsx';
import { Detail } from '@/features/projects/components/Detail.tsx';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { SelectField } from '@/shared/ui/FormFields.tsx';
import { PageHeader, TableContainer } from '@/shared/ui/Layout.tsx';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
import { ErrorState, LoadingState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { integrationAdminApi, type Versioned } from './api/integrationAdminApi.ts';
import { type IntegrationDetail, type InvocationDetail } from './api/types.ts';
import {
  hasConnectionTest,
  INTEGRATIONS_OVERVIEW_PATH,
  invocationPath,
  LIVE_ALERT_STATUSES,
  logsFilteredBy,
  METRICS_WINDOWS,
  type MetricsWindow,
  metricsWindowFrom,
  RECONCILE_OPERATION,
} from './catalogue.ts';
import { ActivationDialog } from './components/ActivationDialog.tsx';
import { AlertList } from './components/AlertList.tsx';
import { Code, EnvironmentBanner, Moment, Section } from './components/Display.tsx';
import { AvailabilityIndicator, FreshnessIndicator } from './components/HealthIndicators.tsx';
import { MetricsPanel } from './components/MetricsPanel.tsx';
import { SyncRunList } from './components/SyncRunList.tsx';
import { TestConnectionDialog } from './components/TestConnectionDialog.tsx';
import { activationTone, labelIn } from './presentation.ts';
import { integrationProblemMessage } from './problems.ts';
import { useCatalogue } from './useCatalogue.ts';

const DAY_MS = 24 * 60 * 60 * 1000;

/**
 * The measures window. One day is the API's own default, so nothing is sent; a longer one is sent as its bounds, taken
 * when the read starts so the loader does not change on every render.
 */
function windowBounds(days: MetricsWindow): { from?: string; to?: string } {
  if (days === 1) {
    return {};
  }
  const to = new Date();
  return { from: new Date(to.getTime() - days * DAY_MS).toISOString(), to: to.toISOString() };
}

/**
 * ADM-046 Integration Detail / Configuration (FG-05 §13): the definition and its retry policy, the environment, each
 * instance with availability and freshness apart and its endpoint's configuration key, ADR-004's measures over a chosen
 * window, live alerts, runs, and the actions the API allows — MOD-101 activation, MOD-100 for an identity connection,
 * and a reconciliation where FG-05 runs one. Secrets are excluded: the API returns none (TASK-075 D-11).
 */
export function IntegrationDetailPage(): ReactElement {
  const { t } = useI18n();
  const { integrationId = '' } = useParams();
  const [params, setParams] = useSearchParams();
  const days = metricsWindowFrom(params);
  const load = useCallback(
    (signal: AbortSignal) =>
      integrationAdminApi.integration(integrationId, windowBounds(days), signal),
    [integrationId, days],
  );
  const detail = useApiResource(load, { keepWhileReloading: true });
  const catalogue = useCatalogue();

  if (detail.data === undefined) {
    return (
      <>
        <PageHeader title={t('integrationAdmin.detail.title')} />
        {detail.error === null ? (
          <LoadingState />
        ) : (
          <ErrorState
            message={integrationProblemMessage(detail.error, t)}
            onRetry={detail.reload}
          />
        )}
      </>
    );
  }
  return (
    <DetailContent
      versioned={detail.data}
      environment={catalogue.data?.environment ?? null}
      days={days}
      onWindowChange={(value) => {
        const next = new URLSearchParams(params);
        if (value === 1) {
          next.delete('window');
        } else {
          next.set('window', String(value));
        }
        setParams(next);
      }}
      reload={detail.reload}
    />
  );
}

type OpenDialog = 'activation' | 'test' | 'reconcile' | null;

function DetailContent({
  versioned,
  environment,
  days,
  onWindowChange,
  reload,
}: {
  versioned: Versioned<IntegrationDetail>;
  environment: string | null;
  days: MetricsWindow;
  onWindowChange: (days: MetricsWindow) => void;
  reload: () => void;
}): ReactElement {
  const { t, language } = useI18n();
  const navigate = useNavigate();
  const [dialog, setDialog] = useState<OpenDialog>(null);
  const [notice, setNotice] = useState<Notice | null>(null);
  const started = useRef<InvocationDetail | null>(null);
  const { data: detail, etag } = versioned;
  const { integration } = detail;
  const name = labelIn(integration.name, language);
  const reconcilable = detail.operations.includes(RECONCILE_OPERATION);
  const close = () => {
    setDialog(null);
  };
  const open = (which: OpenDialog) => () => {
    setNotice(null);
    setDialog(which);
  };

  return (
    <article data-integration={integration.code}>
      <PageHeader
        title={name}
        description={t('integrationAdmin.detail.description')}
        actions={
          <>
            {integration.status !== 'RETIRED' && (
              <button
                type="button"
                className={integration.status === 'ACTIVE' ? 'button button--danger' : 'button'}
                onClick={open('activation')}
              >
                {t(
                  integration.status === 'ACTIVE'
                    ? 'integrationAdmin.actions.disable'
                    : 'integrationAdmin.actions.enable',
                )}
              </button>
            )}
            {hasConnectionTest(integration.code) && (
              <button type="button" className="button" onClick={open('test')}>
                {t('integrationAdmin.actions.testConnection')}
              </button>
            )}
            {reconcilable && (
              <button type="button" className="button" onClick={open('reconcile')}>
                {t('integrationAdmin.actions.reconcile')}
              </button>
            )}
            <Link className="button" to={INTEGRATIONS_OVERVIEW_PATH}>
              {t('integrationAdmin.detail.backToOverview')}
            </Link>
          </>
        }
      />
      {environment !== null && <EnvironmentBanner environment={environment} />}
      <PageNotice notice={notice} />

      <Section title={t('integrationAdmin.detail.definition')}>
        <dl className="details">
          <Detail term={t('integrationAdmin.fields.code')}>
            <Code>{integration.code}</Code>
          </Detail>
          <Detail term={t('integrationAdmin.fields.direction')}>
            {t(`integrationAdmin.direction.${integration.direction}`)}
          </Detail>
          <Detail term={t('integrationAdmin.fields.kind')}>
            {t(`integrationAdmin.kind.${integration.integrationKind}`)}
          </Detail>
          <Detail term={t('integrationAdmin.fields.activation')}>
            <StatusBadge
              label={t(`integrationAdmin.activationStatus.${integration.status}`)}
              tone={activationTone(integration.status)}
            />
          </Detail>
          <Detail term={t('integrationAdmin.fields.retryPolicy')}>
            {t('integrationAdmin.detail.retryPolicy', {
              attempts: detail.maxAttempts,
              seconds: detail.retryBackoffSeconds,
            })}
          </Detail>
          <Detail term={t('integrationAdmin.fields.operations')}>
            {detail.operations.length === 0
              ? t('integrationAdmin.detail.noOperations')
              : detail.operations.map((operation) => <Code key={operation}>{operation}</Code>)}
          </Detail>
        </dl>
        <p className="integration-note">{t('integrationAdmin.detail.noSecrets')}</p>
      </Section>

      <Section title={t('integrationAdmin.detail.instances')}>
        <TableContainer caption={t('integrationAdmin.detail.instances')}>
          <thead>
            <tr>
              <th scope="col">{t('integrationAdmin.fields.instance')}</th>
              <th scope="col">{t('integrationAdmin.fields.endpointKey')}</th>
              <th scope="col">{t('integrationAdmin.indicators.availability')}</th>
              <th scope="col">{t('integrationAdmin.indicators.freshness')}</th>
              <th scope="col">{t('integrationAdmin.fields.lastSuccess')}</th>
              <th scope="col">{t('integrationAdmin.fields.lastFailure')}</th>
              <th scope="col">{t('integrationAdmin.fields.evaluatedAt')}</th>
              <th scope="col">{t('integrationAdmin.fields.liveAlerts')}</th>
            </tr>
          </thead>
          <tbody>
            {integration.instances.map((instance) => (
              <tr key={instance.id} data-instance={instance.code}>
                <th scope="row">
                  <Code>{instance.code}</Code>
                </th>
                <td>
                  <Code>{instance.endpointConfigurationKey}</Code>
                </td>
                <td>
                  <AvailabilityIndicator availability={instance.availability} />
                </td>
                <td>
                  <FreshnessIndicator freshness={instance.freshness} />
                </td>
                <td>
                  <Moment value={instance.lastSuccessAt} />
                </td>
                <td>
                  <Moment value={instance.lastFailureAt} />
                </td>
                <td>
                  <Moment
                    value={instance.evaluatedAt}
                    absent={t('integrationAdmin.values.notEvaluated')}
                  />
                </td>
                <td>{instance.openAlertCount}</td>
              </tr>
            ))}
          </tbody>
        </TableContainer>
      </Section>

      <Section title={t('integrationAdmin.measures.title')}>
        <div className="filters filters--inline">
          <SelectField
            label={t('integrationAdmin.measures.windowChoice')}
            name="window"
            value={String(days)}
            options={METRICS_WINDOWS.map((value) => ({
              value: String(value),
              label: t('integrationAdmin.measures.lastDays', { days: value }),
            }))}
            onChange={(value) => {
              onWindowChange(Number(value) as MetricsWindow);
            }}
          />
        </div>
        <MetricsPanel metrics={detail.metrics} instances={integration.instances} />
      </Section>

      <Section
        title={t('integrationAdmin.alerts.liveTitle')}
        actions={
          <Link to={logsFilteredBy('integrationId', integration.id)}>
            {t('integrationAdmin.detail.openLogs')}
          </Link>
        }
      >
        <AlertList
          query={{ integrationId: integration.id, statuses: LIVE_ALERT_STATUSES }}
          caption={t('integrationAdmin.alerts.liveTitle')}
          emptyTitle={t('integrationAdmin.alerts.noneLive')}
        />
      </Section>

      <Section title={t('integrationAdmin.syncRuns.title')}>
        <SyncRunList
          integrationId={integration.id}
          caption={t('integrationAdmin.syncRuns.title')}
        />
      </Section>

      <ActivationDialog
        integration={dialog === 'activation' ? integration : null}
        etag={etag}
        onClose={close}
        onDone={() => {
          setNotice({
            tone: 'success',
            message: t(
              integration.status === 'ACTIVE'
                ? 'integrationAdmin.activation.disabled'
                : 'integrationAdmin.activation.enabled',
            ),
          });
          close();
          reload();
        }}
      />
      <TestConnectionDialog open={dialog === 'test'} onClose={close} />
      <ConfirmDialog
        confirmation={
          dialog === 'reconcile'
            ? {
                title: t('integrationAdmin.reconcile.title'),
                body: t('integrationAdmin.reconcile.body'),
                confirmLabel: t('integrationAdmin.actions.reconcile'),
                destructive: false,
                action: async () => {
                  started.current = await integrationAdminApi.reconcile(integration.id);
                },
                describeProblem: integrationProblemMessage,
              }
            : null
        }
        onClose={close}
        onDone={() => {
          const attempt = started.current;
          close();
          if (attempt !== null) {
            void navigate(invocationPath(attempt.invocation.id));
          }
        }}
      />
    </article>
  );
}
