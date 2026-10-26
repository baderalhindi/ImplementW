import { type ReactElement } from 'react';

import { Detail } from '@/features/projects/components/Detail.tsx';
import { useI18n } from '@/shared/i18n/i18n.ts';

import { type KpiMeasurementDetail } from '../api/types.ts';
import { figureOf } from '../financialKpiRules.ts';
import { formatKpiValue } from '../presentation.ts';

import { MeasurementStatusBadge, RagBadge } from './Badges.tsx';
import { KpiFigure } from './Figures.tsx';

/** One measurement as a `.details` list: its period, value or state, pinned target and rating, and who recorded it. */
export function MeasurementDetails({
  measurement,
  unitLabel,
  personName,
}: {
  measurement: KpiMeasurementDetail;
  unitLabel: string;
  personName: (id: string | null) => string;
}): ReactElement {
  const { t } = useI18n();
  return (
    <dl className="details">
      <Detail term={t('financialKpi.kpis.period')}>
        <span dir="ltr">
          {t('financialKpi.kpiValue.period', {
            start: measurement.periodStart,
            end: measurement.periodEnd,
          })}
        </span>
      </Detail>
      <Detail term={t('financialKpi.kpis.value', { unit: unitLabel })}>
        <KpiFigure
          state={figureOf(measurement, 'measuredValue', measurement.valueStatus)}
          absent="financialKpi.value.MISSING"
        />
      </Detail>
      <Detail term={t('financialKpi.kpis.pinnedTarget')}>
        {t('financialKpi.kpis.targetVersion', {
          version: measurement.targetVersionNo,
          target: formatKpiValue(measurement.targetValue),
        })}
      </Detail>
      <Detail term={t('financialKpi.kpis.rating')}>
        <RagBadge rag={measurement.ragStatus} />
      </Detail>
      <Detail term={t('financialKpi.kpis.status')}>
        <MeasurementStatusBadge status={measurement.status} />
      </Detail>
      <Detail term={t('financialKpi.kpis.recorded')}>
        {t('financialKpi.kpis.recordedBy', {
          name: personName(measurement.recordedByUserId),
          date: measurement.asOfDate,
        })}
      </Detail>
      {measurement.narrative !== null && (
        <Detail term={t('financialKpi.update.narrative')}>
          <span dir="auto">{measurement.narrative.text}</span>
        </Detail>
      )}
    </dl>
  );
}
