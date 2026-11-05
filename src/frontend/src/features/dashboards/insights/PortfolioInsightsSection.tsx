import {
  type CSSProperties,
  type ReactElement,
  type ReactNode,
  useCallback,
  useState,
} from 'react';
import { Link } from 'react-router';

import { useCan } from '@/features/identity-access/permissions/permissions.ts';
import { type OnPlanStatus } from '@/features/projects/api/types.ts';
import { GapValue, OnPlanBadge } from '@/features/projects/components/FollowUpBadges.tsx';
import { formatSar, languageTag } from '@/features/projects/presentation.ts';
import { useProjectLookups } from '@/features/projects/useProjectLookups.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { TableContainer } from '@/shared/ui/Layout.tsx';
import { EmptyState } from '@/shared/ui/States.tsx';
import { type StatusTone } from '@/shared/ui/StatusBadge.tsx';

import { dashboardsApi, type PortfolioFilters } from '../api/dashboardsApi.ts';
import {
  type PortfolioBreakdown,
  type PortfolioBreakdownRow,
  type PortfolioFollowUpReasons,
  type PortfolioHeadline,
  type PortfolioInsights,
  type PortfolioOnPlan,
  type PortfolioStageCount,
  type PortfolioTopProject,
} from '../api/insightTypes.ts';
import { bucketColors, type ChartBucket } from '../charts.ts';
import { BarChart, DonutChart, GroupedColumnChart } from '../components/Charts.tsx';
import { portfolioCardPath } from '../paths.ts';

import { Coverage, Percent } from './figures.tsx';

type ItemName = (id: string | null) => string;
type CardDimension = 'portfolio' | 'initiative' | 'sector';

/** How many projects the figures cover, or null when the caller may see no section that counts them. */
function projectCountOf(insights: PortfolioInsights): number | null {
  return (
    insights.headline?.registeredProjects ??
    insights.byPortfolio?.total.projects ??
    insights.bySector?.total.projects ??
    insights.stages?.reduce((sum, stage) => sum + stage.projects, 0) ??
    null
  );
}

/**
 * RPMO's figures on the Portfolio Dashboard (the 5 Nov 2026 review §3), reacting to its filters. The API decides what the caller sees: a
 * section it answers null is not shown. No project means "no data", never a row of zeros; a value not measured reads "not available".
 */
export function PortfolioInsightsSection({
  filters,
  emptySaidElsewhere,
}: {
  filters: PortfolioFilters;
  /** The dashboard under this section already says there is no data. */
  emptySaidElsewhere: boolean;
}): ReactElement | null {
  const { t } = useI18n();
  const {
    departmentId,
    externalEntityId,
    classificationItemId,
    regionItemId,
    cityItemId,
    status,
    priorityItemId,
    strategicPortfolioItemId,
    strategicInitiativeItemId,
    period,
  } = filters;
  const insights = useApiResource(
    useCallback(
      (signal: AbortSignal) =>
        dashboardsApi.insights(
          {
            departmentId,
            externalEntityId,
            classificationItemId,
            regionItemId,
            cityItemId,
            status,
            priorityItemId,
            strategicPortfolioItemId,
            strategicInitiativeItemId,
            period,
          },
          signal,
        ),
      [
        departmentId,
        externalEntityId,
        classificationItemId,
        regionItemId,
        cityItemId,
        status,
        priorityItemId,
        strategicPortfolioItemId,
        strategicInitiativeItemId,
        period,
      ],
    ),
    { keepWhileReloading: true },
  );

  if (insights.data === undefined) {
    return insights.loading ? null : (
      <p className="state">{t('dashboards.insights.unavailable')}</p>
    );
  }
  const count = projectCountOf(insights.data);
  if (count === 0) {
    return emptySaidElsewhere ? null : <EmptyState title={t('dashboards.noData')} />;
  }
  const { followUpReasons, topFollowUp } = insights.data;
  const anything = count !== null || followUpReasons !== null || topFollowUp !== null;
  return anything ? <Sections insights={insights.data} /> : null;
}

function Sections({ insights }: { insights: PortfolioInsights }): ReactElement {
  const { t } = useI18n();
  const lookups = useProjectLookups();
  const itemName: ItemName = (id) => lookups.itemLabel(id) ?? t('dashboards.insights.unclassified');
  const {
    headline,
    byPortfolio,
    byInitiative,
    bySector,
    stages,
    onPlan,
    followUpReasons,
    topFollowUp,
  } = insights;

  return (
    <div className="insights">
      {headline !== null && <Headline headline={headline} />}
      <InsightCharts
        byPortfolio={byPortfolio}
        stages={stages}
        onPlan={onPlan}
        itemName={itemName}
      />
      {byPortfolio !== null && byPortfolio.rows.length > 0 && (
        <InsightSection id="portfolio" title={t('dashboards.insights.byPortfolio')}>
          <BreakdownTable
            caption={t('dashboards.insights.byPortfolio')}
            breakdown={byPortfolio}
            dimension="portfolio"
            nameHeader={t('dashboards.insights.portfolio')}
            itemName={itemName}
          />
        </InsightSection>
      )}
      {byInitiative !== null && byInitiative.rows.length > 0 && (
        <InsightSection id="initiative" title={t('dashboards.insights.byInitiative')}>
          <BreakdownTable
            caption={t('dashboards.insights.byInitiative')}
            breakdown={byInitiative}
            dimension="initiative"
            nameHeader={t('dashboards.insights.initiative')}
            itemName={itemName}
            grouped
          />
        </InsightSection>
      )}
      {bySector !== null && bySector.rows.length > 0 && (
        <InsightSection id="sector" title={t('dashboards.insights.bySector')}>
          <BreakdownTable
            caption={t('dashboards.insights.bySector')}
            breakdown={bySector}
            dimension="sector"
            nameHeader={t('dashboards.insights.sector')}
            itemName={itemName}
            sector
          />
        </InsightSection>
      )}
      {stages !== null && stages.length > 0 && <Stages stages={stages} />}
      {onPlan !== null && <OnPlanCounts onPlan={onPlan} />}
      {followUpReasons !== null && <Reasons reasons={followUpReasons} />}
      {topFollowUp !== null && topFollowUp.length > 0 && (
        <TopProjects projects={topFollowUp} itemName={itemName} />
      )}
    </div>
  );
}

function InsightSection({
  id,
  title,
  children,
}: {
  id: string;
  title: string;
  children: ReactNode;
}): ReactElement {
  return (
    <section className="insights__section" data-section={id}>
      <h2>{title}</h2>
      {children}
    </section>
  );
}

function Card({
  label,
  children,
  tone,
}: {
  label: string;
  children: ReactNode;
  tone?: StatusTone | undefined;
}): ReactElement {
  return (
    <li className="home__tile insights__card" data-tone={tone}>
      <span className="home__tile-label">{label}</span>
      {children}
    </li>
  );
}

const ON_PLAN_TONES: Record<OnPlanStatus, StatusTone> = {
  ON_TRACK: 'positive',
  LATE: 'warning',
  STALLED: 'negative',
};

function Headline({ headline }: { headline: PortfolioHeadline }): ReactElement {
  const { t } = useI18n();
  const total = headline.registeredProjects;
  const measured = <Coverage kind="progress" count={headline.measured} of={total} />;
  return (
    <section className="insights__section" aria-label={t('dashboards.insights.headlineLabel')}>
      <ul className="home__tiles" data-section="headline">
        <Card label={t('dashboards.insights.registered')}>
          <span className="home__tile-value" dir="ltr">
            {total}
          </span>
          <span className="insights__coverage">
            {t('dashboards.insights.inProgress', { count: headline.inProgressProjects })}
          </span>
        </Card>
        {headline.totalValueSar !== null && (
          <Card label={t('dashboards.insights.totalValue')}>
            {headline.valueEntered === 0 ? (
              <Percent value={null} />
            ) : (
              <span className="home__tile-value" dir="ltr">
                {t('projects.money.sar', { amount: formatSar(headline.totalValueSar) })}
              </span>
            )}
            <Coverage kind="value" count={headline.valueEntered} of={total} />
          </Card>
        )}
        <Card label={t('dashboards.insights.averagePlanned')}>
          <span className="home__tile-value">
            <Percent value={headline.averagePlannedPercent} />
          </span>
          <span className="insights__coverage">{t('dashboards.insights.simpleAverage')}</span>
          {measured}
        </Card>
        <Card label={t('dashboards.insights.averageActual')}>
          <span className="home__tile-value">
            <Percent value={headline.averageActualPercent} />
          </span>
          <span className="insights__coverage">{t('dashboards.insights.simpleAverage')}</span>
          {measured}
        </Card>
        <Card
          label={t('dashboards.insights.gap')}
          tone={headline.onPlan === null ? undefined : ON_PLAN_TONES[headline.onPlan]}
        >
          <span className="home__tile-value">
            <GapValue points={headline.gapPoints} />
          </span>
          <span>
            <OnPlanBadge status={headline.onPlan} />
          </span>
          {measured}
        </Card>
        <Card label={t('dashboards.insights.needingFollowUp')}>
          <span className="home__tile-value" dir="ltr">
            {headline.needingFollowUp}
          </span>
          <span className="insights__coverage">
            {t('dashboards.insights.overdueActions', { count: headline.overdueActions })}
          </span>
        </Card>
      </ul>
    </section>
  );
}

/** The cells of a breakdown row, the same for its total. */
function RowFigures({
  row,
  showValue,
  sector,
  itemName,
}: {
  row: PortfolioBreakdownRow;
  showValue: boolean;
  sector: boolean;
  itemName: ItemName;
}): ReactElement {
  const { t } = useI18n();
  return (
    <>
      <td dir="ltr" className="cell--ltr">
        {row.projects}
      </td>
      {showValue && (
        <td>
          {row.valueSar === null || row.valueEntered === 0 ? (
            <Percent value={null} />
          ) : (
            <span dir="ltr">{formatSar(row.valueSar)}</span>
          )}
        </td>
      )}
      <td dir="ltr" className="cell--ltr">
        {row.measured}
      </td>
      <td>
        <Percent value={row.averagePlannedPercent} />
      </td>
      <td>
        <Percent value={row.averageActualPercent} />
      </td>
      <td>
        <GapValue points={row.gapPoints} />
      </td>
      <td>
        <OnPlanBadge status={row.onPlan} />
      </td>
      <td dir="ltr" className="cell--ltr">
        {row.needingFollowUp}
      </td>
      {sector && (
        <>
          <td>
            <Percent value={row.averagePlannedSpendPercent} />
          </td>
          <td>
            <Percent value={row.averageActualSpendPercent} />
            <Coverage kind="spend" count={row.spendMeasured ?? 0} of={row.projects} />
          </td>
          <td>
            {row.itemId !== null && (
              <ul className="insights__split">
                {(row.portfolioSplit ?? []).map((split) => (
                  <li key={split.portfolioItemId ?? 'none'}>
                    {t('dashboards.insights.splitRow', {
                      portfolio: itemName(split.portfolioItemId),
                      count: split.projects,
                    })}
                  </li>
                ))}
              </ul>
            )}
          </td>
        </>
      )}
    </>
  );
}

function BreakdownTable({
  caption,
  breakdown,
  dimension,
  nameHeader,
  itemName,
  grouped = false,
  sector = false,
}: {
  caption: string;
  breakdown: PortfolioBreakdown;
  dimension: CardDimension;
  nameHeader: string;
  itemName: ItemName;
  /** Under each row's parent: an initiative under its portfolio. */
  grouped?: boolean;
  sector?: boolean;
}): ReactElement {
  const { t } = useI18n();
  const mayOpenCards = useCan('PORTFOLIO_BREAKDOWN_VIEW');
  const { rows, total } = breakdown;
  const showValue = total.valueSar !== null;
  const columns = 8 + (showValue ? 1 : 0) + (sector ? 3 : 0);
  const parents = grouped ? [...new Set(rows.map((row) => row.parentItemId))] : [null];

  const line = (row: PortfolioBreakdownRow) => (
    <tr key={row.itemId ?? 'none'}>
      <th scope="row">
        {row.itemId !== null && mayOpenCards ? (
          <Link to={portfolioCardPath(dimension, row.itemId)}>{itemName(row.itemId)}</Link>
        ) : (
          itemName(row.itemId)
        )}
      </th>
      <RowFigures row={row} showValue={showValue} sector={sector} itemName={itemName} />
    </tr>
  );

  return (
    <>
      <TableContainer caption={caption}>
        <thead>
          <tr>
            <th scope="col">{nameHeader}</th>
            <th scope="col">{t('dashboards.insights.projects')}</th>
            {showValue && <th scope="col">{t('dashboards.insights.value')}</th>}
            <th scope="col">{t('dashboards.insights.measured')}</th>
            <th scope="col">{t('dashboards.insights.averagePlanned')}</th>
            <th scope="col">{t('dashboards.insights.averageActual')}</th>
            <th scope="col">{t('dashboards.insights.gap')}</th>
            <th scope="col">{t('dashboards.insights.label')}</th>
            <th scope="col">{t('dashboards.insights.needingFollowUp')}</th>
            {sector && (
              <>
                <th scope="col">{t('dashboards.insights.plannedSpend')}</th>
                <th scope="col">{t('dashboards.insights.actualSpend')}</th>
                <th scope="col">{t('dashboards.insights.split')}</th>
              </>
            )}
          </tr>
        </thead>
        {parents.map((parent) => (
          <tbody key={parent ?? 'none'}>
            {grouped && (
              <tr className="insights__group">
                <th scope="rowgroup" colSpan={columns}>
                  {itemName(parent)}
                </th>
              </tr>
            )}
            {rows.filter((row) => !grouped || row.parentItemId === parent).map(line)}
          </tbody>
        ))}
        <tfoot>
          <tr data-row="total">
            <th scope="row">{t('dashboards.insights.total')}</th>
            <RowFigures row={total} showValue={showValue} sector={sector} itemName={itemName} />
          </tr>
        </tfoot>
      </TableContainer>
      <p className="insights__coverage">
        {showValue && <Coverage kind="value" count={total.valueEntered} of={total.projects} />}{' '}
        <Coverage kind="progress" count={total.measured} of={total.projects} />{' '}
        <span>{t('dashboards.insights.simpleAverage')}</span>
      </p>
    </>
  );
}

function Stages({ stages }: { stages: PortfolioStageCount[] }): ReactElement {
  const { t } = useI18n();
  return (
    <InsightSection id="stages" title={t('dashboards.insights.stages')}>
      <TableContainer caption={t('dashboards.insights.stages')}>
        <thead>
          <tr>
            <th scope="col">{t('projects.fields.status')}</th>
            <th scope="col">{t('dashboards.insights.projects')}</th>
            <th scope="col">{t('dashboards.insights.percent')}</th>
          </tr>
        </thead>
        <tbody>
          {stages.map((stage) => (
            <tr key={stage.status}>
              <th scope="row">{t(`projects.status.${stage.status}`)}</th>
              <td dir="ltr" className="cell--ltr">
                {stage.projects}
              </td>
              <td>
                <Percent value={stage.percent} />
              </td>
            </tr>
          ))}
        </tbody>
      </TableContainer>
    </InsightSection>
  );
}

function CountList({
  rows,
}: {
  rows: { key: string; label: string; count: number }[];
}): ReactElement {
  return (
    <dl className="details">
      {rows.map((row) => (
        <div key={row.key} data-row={row.key}>
          <dt>{row.label}</dt>
          <dd dir="ltr">{row.count}</dd>
        </div>
      ))}
    </dl>
  );
}

function OnPlanCounts({ onPlan }: { onPlan: PortfolioOnPlan }): ReactElement {
  const { t } = useI18n();
  return (
    <InsightSection id="onPlan" title={t('dashboards.insights.onPlan')}>
      <CountList
        rows={[
          { key: 'onTrack', label: t('projects.onPlan.ON_TRACK'), count: onPlan.onTrack },
          { key: 'late', label: t('projects.onPlan.LATE'), count: onPlan.late },
          { key: 'stalled', label: t('projects.onPlan.STALLED'), count: onPlan.stalled },
          {
            key: 'notMeasured',
            label: t('dashboards.insights.notMeasured'),
            count: onPlan.notMeasured,
          },
        ]}
      />
    </InsightSection>
  );
}

function Reasons({ reasons }: { reasons: PortfolioFollowUpReasons }): ReactElement {
  const { t } = useI18n();
  return (
    <InsightSection id="reasons" title={t('dashboards.insights.reasons')}>
      {reasons.total === 0 ? (
        <p className="form__note">{t('dashboards.insights.noFollowUp')}</p>
      ) : (
        <CountList
          rows={[
            { key: 'stalled', label: t('projects.followUpReason.STALLED'), count: reasons.stalled },
            {
              key: 'suspended',
              label: t('projects.followUpReason.SUSPENDED'),
              count: reasons.suspended,
            },
            { key: 'late', label: t('projects.followUpReason.LATE'), count: reasons.late },
            {
              key: 'overdueAction',
              label: t('projects.followUpReason.ACTION_OVERDUE'),
              count: reasons.overdueAction,
            },
            {
              key: 'openObstacle',
              label: t('projects.followUpReason.OPEN_OBSTACLE'),
              count: reasons.openObstacle,
            },
            { key: 'total', label: t('dashboards.insights.total'), count: reasons.total },
          ]}
        />
      )}
    </InsightSection>
  );
}

function TopProjects({
  projects,
  itemName,
}: {
  projects: PortfolioTopProject[];
  itemName: ItemName;
}): ReactElement {
  const { t } = useI18n();
  const notAvailable = t('dashboards.insights.notAvailable');
  return (
    <InsightSection id="top" title={t('dashboards.insights.top')}>
      <TableContainer caption={t('dashboards.insights.top')}>
        <thead>
          <tr>
            <th scope="col">{t('projects.fields.formalProjectId')}</th>
            <th scope="col">{t('projects.fields.title')}</th>
            <th scope="col">{t('dashboards.insights.portfolio')}</th>
            <th scope="col">{t('projects.followUpList.reason')}</th>
            <th scope="col">{t('projects.followUpList.obstacle')}</th>
            <th scope="col">{t('projects.followUpList.nextAction')}</th>
          </tr>
        </thead>
        <tbody>
          {projects.map((project) => (
            <tr key={project.projectId}>
              <td dir="ltr" className="cell--ltr">
                {project.formalProjectId ?? project.legacyReference ?? '—'}
              </td>
              <td>
                <Link
                  to={`/projects/${project.projectId}`}
                  lang={languageTag(project.title.language)}
                  dir="auto"
                >
                  {project.title.text}
                </Link>
              </td>
              <td>{itemName(project.portfolioItemId)}</td>
              <td>{t(`projects.followUpReason.${project.reason}`)}</td>
              <td dir="auto">{project.currentObstacle?.text ?? notAvailable}</td>
              <td>
                <span dir="auto">{project.nextAction?.text ?? notAvailable}</span>
                {project.nextActionOwner !== null && (
                  <span className="cell__aside" dir="auto">
                    {project.nextActionOwner}
                  </span>
                )}
                {project.nextActionDueDate !== null && (
                  <span className="cell__aside" dir="ltr">
                    {project.nextActionDueDate}
                  </span>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </TableContainer>
      <p>
        <Link to="/projects/follow-up">{t('dashboards.insights.fullFollowUp')}</Link>
      </p>
    </InsightSection>
  );
}

/** One chart with what it hovers on; the tables and lists of this page state the same values in words. */
function ChartCard({
  title,
  children,
}: {
  title: string;
  children: (active: string | null, onActive: (key: string | null) => void) => ReactNode;
}): ReactElement {
  const [active, setActive] = useState<string | null>(null);
  return (
    <div className="widget insights__chart">
      <h3 className="widget__title">{title}</h3>
      {children(active, setActive)}
    </div>
  );
}

function BucketLegend({ buckets }: { buckets: ChartBucket[] }): ReactElement {
  return (
    <ul className="grouped-columns__legend" aria-hidden="true">
      {buckets.map((bucket) => (
        <li key={bucket.key}>
          <span
            className="distribution__key"
            style={{ '--chart-color': bucket.color } as CSSProperties}
          />
          {`${bucket.text} (${String(bucket.count)})`}
        </li>
      ))}
    </ul>
  );
}

function InsightCharts({
  byPortfolio,
  stages,
  onPlan,
  itemName,
}: {
  byPortfolio: PortfolioBreakdown | null;
  stages: PortfolioStageCount[] | null;
  onPlan: PortfolioOnPlan | null;
  itemName: ItemName;
}): ReactElement | null {
  const { t } = useI18n();
  const portfolioRows = byPortfolio?.rows ?? [];
  const single = 'var(--chart-single)';
  const onPlanBuckets: ChartBucket[] | null =
    onPlan === null
      ? null
      : (() => {
          const colors = bucketColors(['positive', 'warning', 'negative', 'neutral'], true);
          return [
            { key: 'onTrack', count: onPlan.onTrack, text: t('projects.onPlan.ON_TRACK') },
            { key: 'late', count: onPlan.late, text: t('projects.onPlan.LATE') },
            { key: 'stalled', count: onPlan.stalled, text: t('projects.onPlan.STALLED') },
            {
              key: 'notMeasured',
              count: onPlan.notMeasured,
              text: t('dashboards.insights.notMeasured'),
            },
          ].map((bucket, index) => ({ ...bucket, color: colors[index] ?? single }));
        })();
  if (portfolioRows.length === 0 && (stages ?? []).length === 0 && onPlanBuckets === null) {
    return null;
  }
  return (
    <section className="insights__charts">
      <h2 className="visually-hidden">{t('dashboards.insights.chartsLabel')}</h2>
      {portfolioRows.length > 0 && (
        <>
          <ChartCard title={t('dashboards.insights.chartProjectsByPortfolio')}>
            {(active, onActive) => (
              <BarChart
                buckets={portfolioRows.map((row) => ({
                  key: row.itemId ?? 'none',
                  count: row.projects,
                  text: itemName(row.itemId),
                  color: single,
                }))}
                active={active}
                onActive={onActive}
              />
            )}
          </ChartCard>
          <ChartCard title={t('dashboards.insights.chartPlannedActual')}>
            {() => (
              <GroupedColumnChart
                groups={portfolioRows.map((row) => ({
                  key: row.itemId ?? 'none',
                  text: itemName(row.itemId),
                  values: [row.averagePlannedPercent, row.averageActualPercent],
                }))}
                series={[
                  {
                    key: 'planned',
                    text: t('dashboards.insights.averagePlanned'),
                    color: 'var(--chart-category-1)',
                  },
                  {
                    key: 'actual',
                    text: t('dashboards.insights.averageActual'),
                    color: 'var(--chart-category-2)',
                  },
                ]}
                missing={t('dashboards.insights.notAvailable')}
              />
            )}
          </ChartCard>
        </>
      )}
      {stages !== null && stages.length > 0 && (
        <ChartCard title={t('dashboards.insights.chartStatus')}>
          {(active, onActive) => (
            <BarChart
              buckets={stages.map((stage) => ({
                key: stage.status,
                count: stage.projects,
                text: t(`projects.status.${stage.status}`),
                color: single,
              }))}
              active={active}
              onActive={onActive}
            />
          )}
        </ChartCard>
      )}
      {onPlanBuckets !== null && (
        <ChartCard title={t('dashboards.insights.onPlan')}>
          {(active, onActive) => (
            <>
              <DonutChart
                buckets={onPlanBuckets}
                active={active}
                onActive={onActive}
                totalLabel={t('dashboards.insights.projects')}
              />
              <BucketLegend buckets={onPlanBuckets} />
            </>
          )}
        </ChartCard>
      )}
    </section>
  );
}
