import { type ReactElement, type ReactNode } from 'react';

import { Detail } from '@/features/projects/components/Detail.tsx';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { ValueStateFlag } from '@/shared/ui/ValueState.tsx';

import { type IntegrationInstanceView, type IntegrationMetrics } from '../api/types.ts';
import { formatMilliseconds, formatRate } from '../presentation.ts';

import { Moment } from './Display.tsx';

/**
 * ADR-004's measures for each instance over the API's window (TASK-075 D-13): attempts and the failure rate, operations
 * delivered and undelivered and the delivery rate, attempt latency, and receipt latency. Every figure is the API's; a
 * measure over no sample is null and reads "No sample", never 0 (Blueprint: missing ≠ zero).
 */
export function MetricsPanel({
  metrics,
  instances,
}: {
  metrics: readonly IntegrationMetrics[];
  instances: readonly IntegrationInstanceView[];
}): ReactElement {
  const { t } = useI18n();
  if (metrics.length === 0) {
    return (
      <ValueStateFlag
        icon="missing"
        state="no-instance"
        label={t('integrationAdmin.values.noInstance')}
      />
    );
  }
  return (
    <>
      {metrics.map((measures) => (
        <InstanceMeasures
          key={measures.integrationInstanceId}
          measures={measures}
          instanceCode={
            instances.find((instance) => instance.id === measures.integrationInstanceId)?.code ??
            null
          }
        />
      ))}
    </>
  );
}

function InstanceMeasures({
  measures,
  instanceCode,
}: {
  measures: IntegrationMetrics;
  instanceCode: string | null;
}): ReactElement {
  const { t } = useI18n();
  const noSample = (
    <ValueStateFlag
      icon="missing"
      state="no-sample"
      label={t('integrationAdmin.values.noSample')}
    />
  );
  const measure = (value: number | null, format: (value: number) => string): ReactNode =>
    value === null ? noSample : format(value);
  return (
    <div className="integration-measures" data-measures={measures.integrationInstanceId}>
      <p className="integration-measures__window">
        {instanceCode !== null && <strong dir="ltr">{instanceCode}</strong>}{' '}
        {t('integrationAdmin.measures.window')} <Moment value={measures.from} /> –{' '}
        <Moment value={measures.to} />
      </p>
      <dl className="details">
        <Detail term={t('integrationAdmin.measures.attempts')}>{measures.attempts}</Detail>
        <Detail term={t('integrationAdmin.measures.failedAttempts')}>
          {measures.failedAttempts}
        </Detail>
        <Detail term={t('integrationAdmin.measures.failureRate')}>
          {measure(measures.failureRate, formatRate)}
        </Detail>
        <Detail term={t('integrationAdmin.measures.delivered')}>
          {measures.deliveredLineages}
        </Detail>
        <Detail term={t('integrationAdmin.measures.undelivered')}>
          {measures.undeliveredLineages}
        </Detail>
        <Detail term={t('integrationAdmin.measures.deliveryRate')}>
          {measure(measures.deliveryRate, formatRate)}
        </Detail>
        <Detail term={t('integrationAdmin.measures.medianLatency')}>
          {measure(measures.medianLatencyMilliseconds, formatMilliseconds)}
        </Detail>
        <Detail term={t('integrationAdmin.measures.p95Latency')}>
          {measure(measures.p95LatencyMilliseconds, formatMilliseconds)}
        </Detail>
        <Detail term={t('integrationAdmin.measures.receipts')}>{measures.receipts}</Detail>
        <Detail term={t('integrationAdmin.measures.receiptLatency')}>
          {measure(measures.medianReceiptLatencyMilliseconds, formatMilliseconds)}
        </Detail>
      </dl>
    </div>
  );
}
