import { type ReactElement, useState } from 'react';

import { todayUtc } from '@/features/tasks/taskRules.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { TextField } from '@/shared/ui/FormFields.tsx';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import {
  type AggregateCoverage,
  type KpiPortfolioAggregate,
  type KpiRagCounts,
} from './api/types.ts';
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

/** The rating segments of an aggregate's bar, in reading order, each in its rating's colour. */
const RAG_SEGMENTS = [
  ['green', 'GREEN'],
  ['amber', 'AMBER'],
  ['red', 'RED'],
  ['unknown', 'UNKNOWN'],
  ['notApplicable', 'NOT_APPLICABLE'],
] as const satisfies readonly (readonly [keyof KpiRagCounts, string])[];

/** A search matches the project's title or the KPI's name, ignoring case. */
function matchesSearch(entry: KpiRegisterEntry, search: string, kpiName: string): boolean {
  const needle = search.trim().toLocaleLowerCase();
  return (
    needle === '' ||
    entry.project.title.text.toLocaleLowerCase().includes(needle) ||
    kpiName.toLocaleLowerCase().includes(needle)
  );
}

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
  const [search, setSearch] = useState('');

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
            error={portfolio.error}
            message={financialKpiProblemMessage(portfolio.error, t)}
            onRetry={portfolio.reload}
          />
        )}
      </>
    );
  }

  const { entries } = portfolio.data;
  const searched = entries.filter((entry) =>
    matchesSearch(entry, search, lookups.kpiName(entry.assignment.kpiDefinitionId)),
  );
  const shown = searched.filter((entry) => matches(entry, filter, today));
  return (
    <>
      {header}
      {entries.length === 0 ? (
        <EmptyState title={t('financialKpi.register.empty')}>
          <p>{t('financialKpi.register.emptyBody')}</p>
        </EmptyState>
      ) : (
        <div className="kpi-register">
          <PeriodSummary
            counts={Object.fromEntries(
              FILTERS.map((value) => [
                value,
                searched.filter((entry) => matches(entry, value, today)).length,
              ]),
            )}
            filter={filter}
            onFilter={setFilter}
          />
          <Aggregates portfolio={portfolio.data} lookups={lookups} />
          <section className="section" aria-labelledby="kpi-register-list">
            <div className="kpi-register__list-header">
              <h2 id="kpi-register-list">{t('financialKpi.register.listTitle')}</h2>
              <TextField
                label={t('financialKpi.register.search')}
                name="search"
                type="search"
                value={search}
                onChange={setSearch}
              />
            </div>
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

/**
 * Where the register's KPIs stand this period, one card per rating. Each card is also the list's filter: pressing it
 * shows only those KPIs, and the pressed card says so (aria-pressed).
 */
function PeriodSummary({
  counts,
  filter,
  onFilter,
}: {
  counts: Partial<Record<Filter, number>>;
  filter: Filter;
  onFilter: (filter: Filter) => void;
}): ReactElement {
  const { t } = useI18n();
  return (
    <section className="kpi-summary" aria-labelledby="kpi-summary-title">
      <h2 id="kpi-summary-title" className="kpi-summary__title">
        {t('financialKpi.register.summaryTitle')}
      </h2>
      <div
        className="kpi-summary__cards"
        role="group"
        aria-label={t('financialKpi.register.filter')}
      >
        {FILTERS.map((value) => (
          <button
            key={value}
            type="button"
            className={`kpi-summary__card kpi-summary__card--${value}`}
            aria-pressed={filter === value}
            onClick={() => {
              onFilter(value);
            }}
          >
            <span className="kpi-summary__label">
              {t(`financialKpi.register.filters.${value}`)}
            </span>
            <span className="kpi-summary__count">{counts[value] ?? 0}</span>
          </button>
        ))}
      </div>
    </section>
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
      <ul className="kpi-aggregates">
        {portfolio.aggregates.map(({ kpiDefinitionId, aggregate }) => (
          <li
            key={kpiDefinitionId}
            className="widget kpi-aggregate"
            data-aggregate={kpiDefinitionId}
          >
            <div className="widget__header">
              <h3 className="widget__title">{lookups.kpiName(kpiDefinitionId)}</h3>
              {aggregate !== null && <CoverageBadge coverage={aggregate.coverage} />}
            </div>
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
  const rated = RAG_SEGMENTS.reduce((sum, [key]) => sum + ragCounts[key], 0);
  return (
    <>
      {/* With nothing counted the API reports no unit as compatible (F-17): that is "no value", not "units differ". */}
      {aggregate.measuredCount === 0 ? (
        <p className="kpi-aggregate__note">{t('financialKpi.aggregate.noMean')}</p>
      ) : !aggregate.isUnitCompatible || aggregate.meanValue === null ? (
        <p className="kpi-aggregate__note">{t('financialKpi.aggregate.unitsDiffer')}</p>
      ) : (
        <div className="kpi-aggregate__mean">
          <span className="kpi-aggregate__label">{t('financialKpi.aggregate.average')}</span>
          <p className="kpi-aggregate__value">
            <span className="figure" dir="ltr">
              {formatKpiValue(aggregate.meanValue)}
            </span>
            {aggregate.unitItemId !== null && (
              <span className="kpi-aggregate__unit">{lookups.itemLabel(aggregate.unitItemId)}</span>
            )}
          </p>
          <span className="kpi-aggregate__label">
            {t('financialKpi.aggregate.over', { count: aggregate.measuredCount })}
          </span>
        </div>
      )}
      {rated > 0 && (
        // The bar draws the legend's counts; the legend says them, so the bar is not read out twice.
        <div className="kpi-aggregate__bar" aria-hidden="true">
          {RAG_SEGMENTS.filter(([key]) => ragCounts[key] > 0).map(([key]) => (
            <span
              key={key}
              className={`kpi-aggregate__segment kpi-aggregate__segment--${key}`}
              style={{ flexGrow: ragCounts[key] }}
            />
          ))}
        </div>
      )}
      <ul className="kpi-aggregate__legend">
        {RAG_SEGMENTS.map(([key, rag]) => (
          <li key={key} className={`kpi-aggregate__key kpi-aggregate__key--${key}`}>
            {t(`financialKpi.rag.${rag}`)}{' '}
            <span className="kpi-aggregate__count">{ragCounts[key]}</span>
          </li>
        ))}
      </ul>
      {aggregate.isPartial && aggregate.exclusions.length > 0 && (
        <details className="kpi-aggregate__excluded">
          <summary>
            {t('financialKpi.aggregate.partial', { count: aggregate.exclusions.length })}
          </summary>
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
        </details>
      )}
    </>
  );
}
