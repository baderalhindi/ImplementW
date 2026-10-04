import { type ReactElement } from 'react';
import { Link } from 'react-router';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { type ProjectDetail } from '@/features/projects/api/types.ts';
import { Detail } from '@/features/projects/components/Detail.tsx';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { TableContainer } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import {
  AssignmentStatusBadge,
  MeasurementStatusBadge,
  RagBadge,
  VersionStatusBadge,
} from './components/Badges.tsx';
import { AbsentFlag, KpiFigure } from './components/Figures.tsx';
import { KpiTrendChart } from './components/KpiTrendChart.tsx';
import { activeTarget, byPeriodDescending, figureOf, trendPoints } from './financialKpiRules.ts';
import { formatKpiValue } from './presentation.ts';
import { financialKpiProblemMessage, isForbidden } from './problems.ts';
import { useKpiEntry, useKpiLookups } from './useKpiData.ts';

/**
 * SCR-073 KPI History/Trend: one KPI's values over time, each beside the target version it was pinned to when recorded
 * (acceptance criterion 2), as a chart and as a table; and every version of its target, the superseded ones as they
 * were approved. Nothing here is recalculated against the target in force now.
 */
export function KpiHistory({
  project,
  assignmentId,
}: {
  project: ProjectDetail;
  assignmentId: string;
}): ReactElement {
  const { t, formatDateTime } = useI18n();
  const entry = useKpiEntry(assignmentId);
  const lookups = useKpiLookups();
  const personName = usePersonNames([
    entry.data?.assignment.ownerUserId ?? null,
    ...(entry.data?.measurements ?? []).flatMap((m) => [m.recordedByUserId, m.publishedByUserId]),
  ]);

  const heading = (
    <p>
      <Link to={`/projects/${project.id}/kpis`}>{t('financialKpi.actions.backToKpis')}</Link>
    </p>
  );

  // An assignment of another project is not this project's KPI: it is not shown under it.
  if (entry.data?.assignment.projectId !== project.id) {
    return (
      <>
        {heading}
        {entry.loading ? (
          <LoadingState />
        ) : isForbidden(entry.error) ? (
          <p className="state">{t('financialKpi.kpis.forbidden')}</p>
        ) : entry.data !== undefined ? (
          <p className="state">{t('financialKpi.trend.notFound')}</p>
        ) : (
          <ErrorState message={financialKpiProblemMessage(entry.error, t)} onRetry={entry.reload} />
        )}
      </>
    );
  }

  const { assignment, targets, measurements } = entry.data;
  const kpiName = lookups.kpiName(assignment.kpiDefinitionId);
  const unit = lookups.itemLabel(assignment.unitItemId);
  const direction = lookups.direction(assignment.kpiDefinitionId);
  const target = activeTarget(targets);
  const points = trendPoints(measurements);
  const when = (at: string | null, by: string | null) =>
    at === null ? t('common.values.none') : `${formatDateTime(at)} · ${personName(by)}`;

  return (
    <div className="progress">
      {heading}
      <h2>{t('financialKpi.trend.heading', { kpi: kpiName })}</h2>
      <dl className="details">
        <Detail term={t('financialKpi.trend.unit')}>{unit}</Detail>
        <Detail term={t('financialKpi.trend.direction')}>
          {direction === null ? t('common.values.none') : t(`financialKpi.direction.${direction}`)}
        </Detail>
        <Detail term={t('financialKpi.trend.frequency')}>
          {lookups.itemLabel(assignment.measurementFrequencyItemId)}
        </Detail>
        <Detail term={t('financialKpi.trend.owner')}>{personName(assignment.ownerUserId)}</Detail>
        <Detail term={t('financialKpi.kpis.status')}>
          <AssignmentStatusBadge status={assignment.status} />
        </Detail>
        <Detail term={t('financialKpi.kpis.target')}>
          {target === null ? (
            <AbsentFlag label={t('financialKpi.kpis.noTarget')} />
          ) : (
            t('financialKpi.kpis.targetVersion', {
              version: target.versionNo,
              target: formatKpiValue(target.targetValue),
            })
          )}
        </Detail>
      </dl>

      <section className="section" aria-labelledby="kpi-trend">
        <h3 id="kpi-trend">{t('financialKpi.trend.chartTitle')}</h3>
        {points.length === 0 ? (
          <EmptyState title={t('financialKpi.trend.empty')} />
        ) : (
          <>
            <p className="form__note">{t('financialKpi.trend.explanation')}</p>
            <KpiTrendChart points={points} kpiName={kpiName} unitLabel={unit} />
          </>
        )}
      </section>

      {measurements.length > 0 && (
        <section className="section" aria-labelledby="kpi-values">
          <h3 id="kpi-values">{t('financialKpi.trend.valuesTitle')}</h3>
          <TableContainer caption={t('financialKpi.trend.valuesCaption')}>
            <thead>
              <tr>
                <th scope="col">{t('financialKpi.kpis.period')}</th>
                <th scope="col">{t('financialKpi.kpis.value', { unit })}</th>
                <th scope="col">{t('financialKpi.kpis.pinnedTarget')}</th>
                <th scope="col">{t('financialKpi.kpis.rating')}</th>
                <th scope="col">{t('financialKpi.kpis.status')}</th>
                <th scope="col">{t('financialKpi.kpis.recorded')}</th>
                <th scope="col">{t('financialKpi.history.published')}</th>
              </tr>
            </thead>
            <tbody>
              {byPeriodDescending(measurements).map((measurement) => (
                <tr key={measurement.id} data-measurement={measurement.id}>
                  <td>
                    <span className="progress__period" dir="ltr">
                      {t('financialKpi.kpiValue.period', {
                        start: measurement.periodStart,
                        end: measurement.periodEnd,
                      })}
                    </span>
                  </td>
                  <td>
                    <KpiFigure
                      state={figureOf(measurement, 'measuredValue', measurement.valueStatus)}
                      absent="financialKpi.value.MISSING"
                    />
                  </td>
                  <td>
                    {t('financialKpi.kpis.targetVersion', {
                      version: measurement.targetVersionNo,
                      target: formatKpiValue(measurement.targetValue),
                    })}
                  </td>
                  <td>
                    <RagBadge rag={measurement.ragStatus} />
                  </td>
                  <td>
                    <MeasurementStatusBadge status={measurement.status} />
                  </td>
                  <td>
                    {t('financialKpi.kpis.recordedBy', {
                      name: personName(measurement.recordedByUserId),
                      date: measurement.asOfDate,
                    })}
                  </td>
                  <td>{when(measurement.publishedAt, measurement.publishedByUserId)}</td>
                </tr>
              ))}
            </tbody>
          </TableContainer>
        </section>
      )}

      <section className="section" aria-labelledby="kpi-targets">
        <h3 id="kpi-targets">{t('financialKpi.trend.targetsTitle')}</h3>
        {targets.length === 0 ? (
          <EmptyState title={t('financialKpi.kpis.noTarget')} />
        ) : (
          <TableContainer caption={t('financialKpi.trend.targetsCaption')}>
            <thead>
              <tr>
                <th scope="col">{t('financialKpi.history.version')}</th>
                <th scope="col">{t('financialKpi.history.status')}</th>
                <th scope="col">{t('financialKpi.trend.targetValue', { unit })}</th>
                <th scope="col">{t('financialKpi.trend.thresholds')}</th>
                <th scope="col">{t('financialKpi.history.effectiveFrom')}</th>
                <th scope="col">{t('financialKpi.history.activatedAt')}</th>
              </tr>
            </thead>
            <tbody>
              {targets.map((version) => (
                <tr key={version.id}>
                  <td>
                    {t('financialKpi.history.versionRevision', {
                      version: version.versionNo,
                      revision: version.revisionNo,
                    })}
                  </td>
                  <td>
                    <VersionStatusBadge status={version.status} />
                  </td>
                  <td>
                    <span className="figure" dir="ltr">
                      {formatKpiValue(version.targetValue)}
                    </span>
                  </td>
                  <td>
                    {version.greenThreshold === null || version.amberThreshold === null
                      ? t('financialKpi.trend.noThresholds')
                      : t('financialKpi.trend.thresholdValues', {
                          green: formatKpiValue(version.greenThreshold),
                          amber: formatKpiValue(version.amberThreshold),
                        })}
                  </td>
                  <td>{version.effectiveFrom ?? t('common.values.none')}</td>
                  <td>
                    {version.activatedAt === null
                      ? t('common.values.none')
                      : formatDateTime(version.activatedAt)}
                  </td>
                </tr>
              ))}
            </tbody>
          </TableContainer>
        )}
      </section>
    </div>
  );
}
