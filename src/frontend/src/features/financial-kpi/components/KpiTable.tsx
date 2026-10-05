import { type ReactElement, type ReactNode } from 'react';
import { Link } from 'react-router';

import { type ProjectSummary } from '@/features/projects/api/types.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { TableContainer } from '@/shared/ui/Layout.tsx';

import {
  activeTarget,
  currentMeasurement,
  figureOf,
  latestPublished,
} from '../financialKpiRules.ts';
import { formatKpiValue } from '../presentation.ts';
import { type KpiEntry, type KpiLookups } from '../useKpiData.ts';

import { AssignmentStatusBadge, MeasurementStatusBadge, RagBadge } from './Badges.tsx';
import { AbsentFlag, KpiFigure } from './Figures.tsx';

export interface KpiRow {
  entry: KpiEntry;
  /** Named on the register, which lists every project's KPIs. */
  project?: ProjectSummary;
}

/**
 * A KPI per row with where it stands for the period today falls in: its value, or "No data for this period" with a
 * grey "Not rated" — never a 0 and never green without a value (acceptance criterion 1, the workbook's validation
 * check). The last published value is beside it, with its period.
 */
export function KpiTable({
  caption,
  rows,
  today,
  lookups,
  showTarget,
  personName,
  actions,
}: {
  caption: string;
  rows: KpiRow[];
  today: string;
  lookups: KpiLookups;
  /** The target version in force, on screens that read the targets. */
  showTarget: boolean;
  personName?: (id: string | null) => string;
  actions?: (entry: KpiEntry) => ReactNode;
}): ReactElement {
  const { t } = useI18n();
  const showProject = rows.some((row) => row.project !== undefined);
  return (
    <TableContainer caption={caption}>
      <thead>
        <tr>
          <th scope="col">{t('financialKpi.kpis.kpi')}</th>
          {showProject && <th scope="col">{t('financialKpi.kpis.project')}</th>}
          {showTarget && <th scope="col">{t('financialKpi.kpis.target')}</th>}
          <th scope="col">{t('financialKpi.kpis.thisPeriod')}</th>
          <th scope="col">{t('financialKpi.kpis.rating')}</th>
          <th scope="col">{t('financialKpi.kpis.status')}</th>
          <th scope="col">{t('financialKpi.kpis.lastPublished')}</th>
          {actions !== undefined && <th scope="col">{t('financialKpi.kpis.actions')}</th>}
        </tr>
      </thead>
      <tbody>
        {rows.map(({ entry, project }) => {
          const { assignment, measurements, targets } = entry;
          const current = currentMeasurement(measurements, today);
          const published = latestPublished(measurements);
          const target = activeTarget(targets);
          return (
            <tr key={assignment.id} data-kpi-assignment={assignment.id}>
              <td>
                <Link to={`/projects/${assignment.projectId}/kpis/${assignment.id}`}>
                  {lookups.kpiName(assignment.kpiDefinitionId)}
                </Link>
                <span className="cell__aside">
                  {t('financialKpi.kpis.unitFrequency', {
                    unit: lookups.itemLabel(assignment.unitItemId),
                    frequency: lookups.itemLabel(assignment.measurementFrequencyItemId),
                  })}
                </span>
                {personName !== undefined && assignment.ownerUserId !== null && (
                  <span className="cell__aside">
                    {t('financialKpi.kpis.owner', { name: personName(assignment.ownerUserId) })}
                  </span>
                )}
                {assignment.status !== 'ACTIVE' && (
                  <AssignmentStatusBadge status={assignment.status} />
                )}
              </td>
              {project !== undefined && (
                <td>
                  <Link to={`/projects/${project.id}/kpis`} dir="auto">
                    {project.title.text}
                  </Link>
                </td>
              )}
              {showTarget && (
                <td>
                  {target === null ? (
                    <AbsentFlag label={t('financialKpi.kpis.noTarget')} />
                  ) : (
                    t('financialKpi.kpis.targetVersion', {
                      version: target.versionNo,
                      target: formatKpiValue(target.targetValue),
                    })
                  )}
                </td>
              )}
              <td>
                {current === null ? (
                  <AbsentFlag label={t('financialKpi.kpis.noDataThisPeriod')} />
                ) : (
                  <KpiFigure
                    state={figureOf(current, 'measuredValue', current.valueStatus)}
                    absent="financialKpi.value.MISSING"
                  />
                )}
              </td>
              <td>
                <RagBadge rag={current === null ? 'UNKNOWN' : current.ragStatus} />
              </td>
              <td>
                {current === null ? (
                  t('common.values.none')
                ) : (
                  <MeasurementStatusBadge status={current.status} />
                )}
              </td>
              <td>
                {published === null ? (
                  t('financialKpi.kpis.nonePublished')
                ) : (
                  <span className="figure-stack">
                    <KpiFigure
                      state={figureOf(published, 'measuredValue', published.valueStatus)}
                      absent="financialKpi.value.MISSING"
                    />
                    <span className="details__aside" dir="ltr">
                      {t('financialKpi.kpiValue.period', {
                        start: published.periodStart,
                        end: published.periodEnd,
                      })}
                    </span>
                  </span>
                )}
              </td>
              {actions !== undefined && <td>{actions(entry)}</td>}
            </tr>
          );
        })}
      </tbody>
    </TableContainer>
  );
}
