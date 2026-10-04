import { type ReactElement, useState } from 'react';

import { todayUtc } from '@/features/tasks/taskRules.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { SelectField } from '@/shared/ui/FormFields.tsx';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { type AggregateCoverage, type KpiPortfolioAggregate } from './api/types.ts';
import { KpiTable } from './components/KpiTable.tsx';
import { currentMeasurement } from './financialKpiRules.ts';
import { formatKpiValue } from './presentation.ts';
import { financialKpiProblemMessage, isForbidden } from './problems.ts';
import {
  type KpiLookups,
  type KpiPortfolio,
  type KpiRegisterEntry,
  useKpiLookups,
  useKpiPortfolio,
} from './useKpiData.ts';

const FILTERS = ['all', 'noData', 'red', 'amber', 'green'] as const;
type Filter = (typeof FILTERS)[number];

function matches(entry: KpiRegisterEntry, filter: Filter, today: string): boolean {
  const current = currentMeasurement(entry.measurements, today);
  switch (filter) {
    case 'all':
      return true;
    case 'noData':
      return current?.valueStatus !== 'MEASURED';
    case 'red':
      return current?.ragStatus === 'RED';
    case 'amber':
      return current?.ragStatus === 'AMBER';
    case 'green':
      return current?.ragStatus === 'GREEN';
  }
}

const COVERAGE_TONES = { COMPLETE: 'info', PARTIAL: 'warning', NONE: 'neutral' } as const;

/**
 * SCR-072 KPI Register: every KPI of the projects the person can see, with where each stands for the period today
 * falls in (acceptance criterion 1), and, for each KPI, the API's aggregate of the latest published values across
 * those projects: a mean only within one unit, otherwise said; a partial aggregate names what it left out and why
 * (TASK-052 D-11).
 */
export function KpiRegisterPage(): ReactElement {
  const { t } = useI18n();
  const today = todayUtc();
  const portfolio = useKpiPortfolio();
  const lookups = useKpiLookups();
  const [filter, setFilter] = useState<Filter>('all');

  const header = (
    <PageHeader
      title={t('financialKpi.register.title')}
      description={t('financialKpi.register.description')}
    />
  );
  if (portfolio.data === undefined) {
    return (
      <>
        {header}
        {portfolio.loading ? (
          <LoadingState />
        ) : isForbidden(portfolio.error) ? (
          <p className="state">{t('financialKpi.kpis.forbidden')}</p>
        ) : (
          <ErrorState
            message={financialKpiProblemMessage(portfolio.error, t)}
            onRetry={portfolio.reload}
          />
        )}
      </>
    );
  }

  const { entries } = portfolio.data;
  const shown = entries.filter((entry) => matches(entry, filter, today));
  return (
    <>
      {header}
      {entries.length === 0 ? (
        <EmptyState title={t('financialKpi.register.empty')}>
          <p>{t('financialKpi.register.emptyBody')}</p>
        </EmptyState>
      ) : (
        <div className="progress">
          <Aggregates portfolio={portfolio.data} lookups={lookups} />
          <section className="section" aria-labelledby="kpi-register-list">
            <h2 id="kpi-register-list">{t('financialKpi.register.listTitle')}</h2>
            <SelectField
              label={t('financialKpi.register.filter')}
              name="filter"
              value={filter}
              options={FILTERS.map((value) => ({
                value,
                label: t(`financialKpi.register.filters.${value}`),
              }))}
              onChange={(value) => {
                setFilter(FILTERS.find((candidate) => candidate === value) ?? 'all');
              }}
            />
            {shown.length === 0 ? (
              <EmptyState title={t('financialKpi.register.filteredEmpty')}>
                <p>{t('financialKpi.register.filteredEmptyBody')}</p>
              </EmptyState>
            ) : (
              <KpiTable
                caption={t('financialKpi.register.caption')}
                rows={shown.map((entry) => ({ entry, project: entry.project }))}
                today={today}
                lookups={lookups}
                showTarget={false}
              />
            )}
          </section>
        </div>
      )}
    </>
  );
}

function CoverageBadge({ coverage }: { coverage: AggregateCoverage }): ReactElement {
  const { t } = useI18n();
  return (
    <StatusBadge
      label={t(`financialKpi.aggregate.coverage.${coverage}`)}
      tone={COVERAGE_TONES[coverage]}
    />
  );
}

/** Each KPI across the projects that carry it, as the API aggregated it; never combined across units. */
function Aggregates({
  portfolio,
  lookups,
}: {
  portfolio: KpiPortfolio;
  lookups: KpiLookups;
}): ReactElement {
  const { t } = useI18n();
  const projectName = (id: string) =>
    portfolio.projects.find((project) => project.id === id)?.title.text ?? id.slice(0, 8);
  return (
    <section className="section" aria-labelledby="kpi-aggregates">
      <h2 id="kpi-aggregates">{t('financialKpi.aggregate.title')}</h2>
      <p className="form__note">{t('financialKpi.aggregate.explanation')}</p>
      <ul className="revision-list">
        {portfolio.aggregates.map(({ kpiDefinitionId, aggregate }) => (
          <li key={kpiDefinitionId} data-aggregate={kpiDefinitionId}>
            <p className="figure-group">
              <span className="revision-list__number">{lookups.kpiName(kpiDefinitionId)}</span>
              {aggregate !== null && <CoverageBadge coverage={aggregate.coverage} />}
            </p>
            {aggregate === null ? (
              <p className="form__note">{t('financialKpi.aggregate.unavailable')}</p>
            ) : (
              <AggregateBody aggregate={aggregate} lookups={lookups} projectName={projectName} />
            )}
          </li>
        ))}
      </ul>
    </section>
  );
}

function AggregateBody({
  aggregate,
  lookups,
  projectName,
}: {
  aggregate: KpiPortfolioAggregate;
  lookups: KpiLookups;
  projectName: (id: string) => string;
}): ReactElement {
  const { t } = useI18n();
  const { ragCounts } = aggregate;
  return (
    <>
      <p>
        {/* With nothing counted the API reports no unit as compatible (F-17): that is "no value", not "units differ". */}
        {aggregate.measuredCount === 0
          ? t('financialKpi.aggregate.noMean')
          : !aggregate.isUnitCompatible || aggregate.meanValue === null
            ? t('financialKpi.aggregate.unitsDiffer')
            : t('financialKpi.aggregate.mean', {
                value: formatKpiValue(aggregate.meanValue),
                unit: aggregate.unitItemId === null ? '' : lookups.itemLabel(aggregate.unitItemId),
                count: aggregate.measuredCount,
              })}
      </p>
      <p className="details__aside">
        {t('financialKpi.aggregate.ragCounts', {
          green: ragCounts.green,
          amber: ragCounts.amber,
          red: ragCounts.red,
          unknown: ragCounts.unknown,
          notApplicable: ragCounts.notApplicable,
        })}
      </p>
      {aggregate.isPartial && aggregate.exclusions.length > 0 && (
        <>
          <p>{t('financialKpi.aggregate.partial')}</p>
          <ul className="reason-list">
            {aggregate.exclusions.map((exclusion) => (
              <li key={`${exclusion.projectId}-${exclusion.reason}`}>
                {t('financialKpi.aggregate.exclusion', {
                  project: projectName(exclusion.projectId),
                  reason: t(`financialKpi.aggregate.reason.${exclusion.reason}`),
                })}
              </li>
            ))}
          </ul>
        </>
      )}
    </>
  );
}
