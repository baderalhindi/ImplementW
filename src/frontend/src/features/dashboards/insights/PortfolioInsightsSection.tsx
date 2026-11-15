import {
  type CSSProperties,
  type ReactElement,
  type ReactNode,
  useCallback,
  useState,
} from 'react';
import { Link, useLocation } from 'react-router';

import { useCan } from '@/features/identity-access/permissions/permissions.ts';
import { type FollowUpReason, type OnPlanStatus } from '@/features/projects/api/types.ts';
import { GapValue, OnPlanBadge } from '@/features/projects/components/FollowUpBadges.tsx';
import { formatSar, languageTag } from '@/features/projects/presentation.ts';
import { NO_STAGE, NOT_MEASURED } from '@/features/projects/register/projectFilterValues.ts';
import { useProjectLookups } from '@/features/projects/useProjectLookups.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { TableContainer } from '@/shared/ui/Layout.tsx';
import { SelectAllHeader, SelectRowCell } from '@/shared/ui/rowSelection.tsx';
import {
  COMPLETION_BAND_PARAM,
  type CompletionBandKey,
  ENTITY_COMPLETION_ANCHOR,
  percent,
  periodLabel,
  useCompletionPeriod,
  useEntityCompletionBands,
} from '@/features/external-participation/entityCompletion.ts';
import {
  NEW_UPDATE_BATCH_PATH,
  PARTICIPATION_MONITOR_PATH,
} from '@/features/external-participation/paths.ts';
import { useSession } from '@/features/identity-access/session/useSession.ts';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';
import { type StatusTone } from '@/shared/ui/StatusBadge.tsx';
import { useRowSelection } from '@/shared/ui/useRowSelection.ts';

import { dashboardsApi, type PortfolioFilters } from '../api/dashboardsApi.ts';
import {
  type PortfolioBreakdown,
  type PortfolioBreakdownRow,
  type PortfolioFollowUpReasons,
  type PortfolioHeadline,
  type PortfolioInsights,
  type PortfolioOnPlan,
  type PortfolioCurrentStage,
  type PortfolioStageCount,
  type PortfolioTopProject,
} from '../api/insightTypes.ts';
import { bucketColors, type ChartBucket } from '../charts.ts';
import { BarChart, DonutChart, GroupedColumnChart } from '../components/Charts.tsx';
import { followUpPath, registerPath } from '../drillLinks.ts';
import { portfolioCardPath } from '../paths.ts';
import { emptyPortfolioReason } from '../presentation.ts';

import { Coverage, Percent } from './figures.tsx';

type ItemName = (id: string | null) => string;
type CardDimension = 'portfolio' | 'initiative' | 'sector';

/** The register's filter for each breakdown's rows. */
const DIMENSION_FILTERS: Record<CardDimension, keyof PortfolioFilters> = {
  portfolio: 'strategicPortfolioItemId',
  initiative: 'strategicInitiativeItemId',
  sector: 'classificationItemId',
};

/** A count that opens the records it counts (review round 8, point 01); a count with nowhere to go stays text. */
function CountLink({
  count,
  to,
  hint,
}: {
  count: number;
  to: string | null;
  hint: string;
}): ReactElement {
  return to === null ? (
    <>{count}</>
  ) : (
    <Link className="drill-link" to={to} title={hint} dir="ltr">
      {count}
    </Link>
  );
}

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
  onClearFilters,
}: {
  filters: PortfolioFilters;
  /** Clears the dashboard's filters: offered when they are why nothing shows (review round 8, point 09). */
  onClearFilters: () => void;
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
    return emptySaidElsewhere ? null : (
      <EmptyState
        title={t('dashboards.noData')}
        reason={emptyPortfolioReason(filters)}
        action={
          emptyPortfolioReason(filters) === 'scope'
            ? undefined
            : { label: t('common.filters.clear'), onClick: onClearFilters }
        }
      />
    );
  }
  const { followUpReasons, topFollowUp } = insights.data;
  const anything = count !== null || followUpReasons !== null || topFollowUp !== null;
  return anything ? <Sections insights={insights.data} filters={filters} /> : null;
}

function Sections({
  insights,
  filters,
}: {
  insights: PortfolioInsights;
  filters: PortfolioFilters;
}): ReactElement {
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
  const byCurrentStage = insights.byCurrentStage ?? null;

  return (
    <div className="insights">
      {headline !== null && <Headline headline={headline} filters={filters} />}
      <InsightCharts
        filters={filters}
        byPortfolio={byPortfolio}
        stages={stages}
        byCurrentStage={byCurrentStage}
        onPlan={onPlan}
        itemName={itemName}
      />
      {byPortfolio !== null && byPortfolio.rows.length > 0 && (
        <InsightSection id="portfolio" title={t('dashboards.insights.byPortfolio')}>
          <BreakdownTable
            caption={t('dashboards.insights.byPortfolio')}
            breakdown={byPortfolio}
            dimension="portfolio"
            filters={filters}
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
            filters={filters}
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
            filters={filters}
            nameHeader={t('dashboards.insights.sector')}
            itemName={itemName}
          />
          <SectorSpend breakdown={bySector} itemName={itemName} filters={filters} />
        </InsightSection>
      )}
      {stages !== null && stages.length > 0 && <Stages stages={stages} filters={filters} />}
      {byCurrentStage !== null && byCurrentStage.some((stage) => stage.projects > 0) && (
        <CurrentStages stages={byCurrentStage} filters={filters} />
      )}
      {onPlan !== null && <OnPlanCounts onPlan={onPlan} filters={filters} />}
      {followUpReasons !== null && <Reasons reasons={followUpReasons} filters={filters} />}
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

/** A headline card; with `to`, its whole surface opens the records it counts. */
function Card({
  label,
  children,
  tone,
  to,
  hint,
}: {
  label: string;
  children: ReactNode;
  tone?: StatusTone | undefined;
  to?: string | undefined;
  hint?: string | undefined;
}): ReactElement {
  return (
    <li
      className={`home__tile insights__card${to === undefined ? '' : ' home__tile--linked'}`}
      data-tone={tone}
      title={hint}
    >
      {to === undefined ? (
        <span className="home__tile-label">{label}</span>
      ) : (
        <Link className="home__tile-label home__tile-link" to={to}>
          {label}
        </Link>
      )}
      {children}
    </li>
  );
}

const ON_PLAN_TONES: Record<OnPlanStatus, StatusTone> = {
  ON_TRACK: 'positive',
  LATE: 'warning',
  STALLED: 'negative',
};

function Headline({
  headline,
  filters,
}: {
  headline: PortfolioHeadline;
  filters: PortfolioFilters;
}): ReactElement {
  const { t } = useI18n();
  const total = headline.registeredProjects;
  const projectsHint = (count: number) => t('dashboards.drill.projects', { count });
  const measuredPath = registerPath(filters, { measured: 'true' });
  const measured = <Coverage kind="progress" count={headline.measured} of={total} />;
  return (
    <section className="insights__section" aria-label={t('dashboards.insights.headlineLabel')}>
      <ul className="home__tiles insights__headline" data-section="headline">
        <Card
          label={t('dashboards.insights.registered')}
          to={registerPath(filters)}
          hint={projectsHint(total)}
        >
          <span className="home__tile-value" dir="ltr">
            {total}
          </span>
          <Link
            className="insights__coverage drill-link"
            to={registerPath(filters, { status: 'ACTIVE' })}
            title={projectsHint(headline.inProgressProjects)}
          >
            {t('dashboards.insights.inProgress', { count: headline.inProgressProjects })}
          </Link>
        </Card>
        {headline.totalValueSar !== null && (
          <Card
            label={t('dashboards.insights.totalValue')}
            to={registerPath(filters, { sort: 'value:desc' })}
            hint={projectsHint(total)}
          >
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
        <Card
          label={t('dashboards.insights.averagePlanned')}
          to={measuredPath}
          hint={projectsHint(headline.measured)}
        >
          <span className="home__tile-value">
            <Percent value={headline.averagePlannedPercent} />
          </span>
          <span className="insights__notes">
            <span className="insights__coverage">{t('dashboards.insights.simpleAverage')}</span>
            {measured}
          </span>
        </Card>
        <Card
          label={t('dashboards.insights.averageActual')}
          to={measuredPath}
          hint={projectsHint(headline.measured)}
        >
          <span className="home__tile-value">
            <Percent value={headline.averageActualPercent} />
          </span>
          <span className="insights__notes">
            <span className="insights__coverage">{t('dashboards.insights.simpleAverage')}</span>
            {measured}
          </span>
        </Card>
        <Card
          label={t('dashboards.insights.gap')}
          tone={headline.onPlan === null ? undefined : ON_PLAN_TONES[headline.onPlan]}
          to={measuredPath}
          hint={projectsHint(headline.measured)}
        >
          <span className="home__tile-value">
            <GapValue points={headline.gapPoints} />
          </span>
          <span>
            <OnPlanBadge status={headline.onPlan} />
          </span>
          {measured}
        </Card>
        <Card
          label={t('dashboards.insights.needingFollowUp')}
          to={followUpPath(filters)}
          hint={t('dashboards.drill.followUp', { count: headline.needingFollowUp })}
        >
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

/** The cells of a breakdown row, the same for its total; `rowFilters` is null for a row with nowhere to open ("unclassified"). */
function RowFigures({
  row,
  showValue,
  filters,
  rowFilters,
}: {
  row: PortfolioBreakdownRow;
  showValue: boolean;
  filters: PortfolioFilters;
  rowFilters: Record<string, string> | null;
}): ReactElement {
  const { t } = useI18n();
  const projects = rowFilters === null ? null : registerPath(filters, rowFilters);
  const projectsHint = (count: number) => t('dashboards.drill.projects', { count });
  return (
    <>
      <td dir="ltr" className="cell--ltr">
        <CountLink count={row.projects} to={projects} hint={projectsHint(row.projects)} />
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
        <CountLink
          count={row.measured}
          to={
            rowFilters === null ? null : registerPath(filters, { ...rowFilters, measured: 'true' })
          }
          hint={projectsHint(row.measured)}
        />
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
        <CountLink
          count={row.needingFollowUp}
          to={rowFilters === null ? null : followUpPath(filters, rowFilters)}
          hint={t('dashboards.drill.followUp', { count: row.needingFollowUp })}
        />
      </td>
    </>
  );
}

function BreakdownTable({
  caption,
  breakdown,
  dimension,
  nameHeader,
  itemName,
  filters,
  grouped = false,
}: {
  caption: string;
  breakdown: PortfolioBreakdown;
  dimension: CardDimension;
  filters: PortfolioFilters;
  nameHeader: string;
  itemName: ItemName;
  /** Under each row's parent: an initiative under its portfolio. */
  grouped?: boolean;
}): ReactElement {
  const { t } = useI18n();
  const mayOpenCards = useCan('PORTFOLIO_BREAKDOWN_VIEW');
  const selection = useRowSelection();
  const { rows, total } = breakdown;
  const showValue = total.valueSar !== null;
  const columns = 9 + (showValue ? 1 : 0);
  const rowId = (row: PortfolioBreakdownRow) => row.itemId ?? 'none';
  const parents = grouped ? [...new Set(rows.map((row) => row.parentItemId))] : [null];

  const rowFilters = (row: PortfolioBreakdownRow) =>
    row.itemId === null ? null : { [DIMENSION_FILTERS[dimension]]: row.itemId };
  // Review round 8, point 01: the name opens the projects under it; the report card stays, as a second link.
  const line = (row: PortfolioBreakdownRow) => (
    <tr key={rowId(row)}>
      <SelectRowCell id={rowId(row)} name={itemName(row.itemId)} selection={selection} />
      <th scope="row">
        {row.itemId === null ? (
          itemName(row.itemId)
        ) : (
          <Link
            to={registerPath(filters, { [DIMENSION_FILTERS[dimension]]: row.itemId })}
            title={t('dashboards.drill.projects', { count: row.projects })}
          >
            {itemName(row.itemId)}
          </Link>
        )}
        {row.itemId !== null && mayOpenCards && (
          <Link className="cell__aside" to={portfolioCardPath(dimension, row.itemId)}>
            {t('dashboards.drill.reportCard')}
          </Link>
        )}
      </th>
      <RowFigures row={row} showValue={showValue} filters={filters} rowFilters={rowFilters(row)} />
    </tr>
  );

  return (
    <>
      <TableContainer
        caption={caption}
        selection={{ selection, exportName: `projects-by-${dimension}` }}
      >
        <thead>
          <tr>
            <SelectAllHeader ids={rows.map(rowId)} selection={selection} />
            <th scope="col">{nameHeader}</th>
            <th scope="col">{t('dashboards.insights.projects')}</th>
            {showValue && <th scope="col">{t('dashboards.insights.value')}</th>}
            <th scope="col">{t('dashboards.insights.measured')}</th>
            <th scope="col">{t('dashboards.insights.averagePlanned')}</th>
            <th scope="col">{t('dashboards.insights.averageActual')}</th>
            <th scope="col">{t('dashboards.insights.gap')}</th>
            <th scope="col">{t('dashboards.insights.label')}</th>
            <th scope="col">{t('dashboards.insights.needingFollowUp')}</th>
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
            <td className="cell--select" />
            <th scope="row">{t('dashboards.insights.total')}</th>
            <RowFigures row={total} showValue={showValue} filters={filters} rowFilters={{}} />
          </tr>
        </tfoot>
      </TableContainer>
      <p className="insights__notes">
        {showValue && <Coverage kind="value" count={total.valueEntered} of={total.projects} />}
        <Coverage kind="progress" count={total.measured} of={total.projects} />
        <span className="insights__coverage">{t('dashboards.insights.simpleAverage')}</span>
      </p>
    </>
  );
}

/** Each sector's spend and its split by portfolio, apart: beside the figures above they would run the table off the page. */
function SectorSpend({
  breakdown,
  itemName,
  filters,
}: {
  breakdown: PortfolioBreakdown;
  itemName: ItemName;
  filters: PortfolioFilters;
}): ReactElement {
  const { t } = useI18n();
  const selection = useRowSelection();
  const { rows, total } = breakdown;
  return (
    <>
      <TableContainer
        caption={t('dashboards.insights.sectorSpend')}
        selection={{ selection, exportName: 'sector-spend' }}
      >
        <thead>
          <tr>
            <SelectAllHeader ids={rows.map((row) => row.itemId ?? 'none')} selection={selection} />
            <th scope="col">{t('dashboards.insights.sector')}</th>
            <th scope="col">{t('dashboards.insights.plannedSpend')}</th>
            <th scope="col">{t('dashboards.insights.actualSpend')}</th>
            <th scope="col">{t('dashboards.insights.split')}</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row.itemId ?? 'none'}>
              <SelectRowCell
                id={row.itemId ?? 'none'}
                name={itemName(row.itemId)}
                selection={selection}
              />
              <th scope="row">
                {row.itemId === null ? (
                  itemName(row.itemId)
                ) : (
                  <Link
                    to={registerPath(filters, { classificationItemId: row.itemId })}
                    title={t('dashboards.drill.projects', { count: row.projects })}
                  >
                    {itemName(row.itemId)}
                  </Link>
                )}
              </th>
              <td>
                <Percent value={row.averagePlannedSpendPercent} />
              </td>
              <td>
                <Percent value={row.averageActualSpendPercent} />
              </td>
              <td>
                <ul className="insights__split">
                  {(row.portfolioSplit ?? []).map((split) => {
                    const text = t('dashboards.insights.splitRow', {
                      portfolio: itemName(split.portfolioItemId),
                      count: split.projects,
                    });
                    return (
                      <li key={split.portfolioItemId ?? 'none'}>
                        {row.itemId === null || split.portfolioItemId === null ? (
                          text
                        ) : (
                          <Link
                            className="drill-link"
                            to={registerPath(filters, {
                              classificationItemId: row.itemId,
                              strategicPortfolioItemId: split.portfolioItemId,
                            })}
                            title={t('dashboards.drill.projects', { count: split.projects })}
                          >
                            {text}
                          </Link>
                        )}
                      </li>
                    );
                  })}
                </ul>
              </td>
            </tr>
          ))}
        </tbody>
      </TableContainer>
      <p className="insights__coverage">
        <Coverage kind="spend" count={total.spendMeasured ?? 0} of={total.projects} />
      </p>
    </>
  );
}

function Stages({
  stages,
  filters,
}: {
  stages: PortfolioStageCount[];
  filters: PortfolioFilters;
}): ReactElement {
  const { t } = useI18n();
  const selection = useRowSelection();
  return (
    <InsightSection id="stages" title={t('dashboards.insights.byStatus')}>
      <TableContainer
        caption={t('dashboards.insights.byStatus')}
        selection={{ selection, exportName: 'projects-by-stage' }}
      >
        <thead>
          <tr>
            <SelectAllHeader ids={stages.map((stage) => stage.status)} selection={selection} />
            <th scope="col">{t('projects.fields.status')}</th>
            <th scope="col">{t('dashboards.insights.projects')}</th>
            <th scope="col">{t('dashboards.insights.percent')}</th>
          </tr>
        </thead>
        <tbody>
          {stages.map((stage) => (
            <tr key={stage.status}>
              <SelectRowCell
                id={stage.status}
                name={t(`projects.status.${stage.status}`)}
                selection={selection}
              />
              <th scope="row">
                <Link
                  to={registerPath(filters, { status: stage.status })}
                  title={t('dashboards.drill.projects', { count: stage.projects })}
                >
                  {t(`projects.status.${stage.status}`)}
                </Link>
              </th>
              <td dir="ltr" className="cell--ltr">
                <CountLink
                  count={stage.projects}
                  to={registerPath(filters, { status: stage.status })}
                  hint={t('dashboards.drill.projects', { count: stage.projects })}
                />
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

/** Review round 7, item 1: the projects by their current stage, in stage order, with their value; then those with no stage reported yet. */
function CurrentStages({
  stages,
  filters,
}: {
  stages: PortfolioCurrentStage[];
  filters: PortfolioFilters;
}): ReactElement {
  const { t, language } = useI18n();
  const pathOf = (stage: PortfolioCurrentStage) =>
    registerPath(filters, { stage: stage.stageCode ?? NO_STAGE });
  const selection = useRowSelection();
  const showValue = stages.some((stage) => stage.valueSar !== null);
  const nameOf = (stage: PortfolioCurrentStage) =>
    stage.name?.[language] ?? t('dashboards.insights.noCurrentStage');
  return (
    <InsightSection id="current-stage" title={t('dashboards.insights.byCurrentStage')}>
      <TableContainer
        caption={t('dashboards.insights.byCurrentStage')}
        selection={{ selection, exportName: 'projects-by-current-stage' }}
      >
        <thead>
          <tr>
            <SelectAllHeader
              ids={stages.map((stage) => stage.stageCode ?? 'none')}
              selection={selection}
            />
            <th scope="col">{t('dashboards.insights.currentStage')}</th>
            <th scope="col">{t('dashboards.insights.projects')}</th>
            {showValue && <th scope="col">{t('dashboards.insights.value')}</th>}
          </tr>
        </thead>
        <tbody>
          {stages.map((stage) => (
            <tr key={stage.stageCode ?? 'none'}>
              <SelectRowCell
                id={stage.stageCode ?? 'none'}
                name={nameOf(stage)}
                selection={selection}
              />
              <th scope="row">
                <Link
                  to={pathOf(stage)}
                  title={t('dashboards.drill.projects', { count: stage.projects })}
                >
                  {nameOf(stage)}
                </Link>
              </th>
              <td dir="ltr" className="cell--ltr">
                <CountLink
                  count={stage.projects}
                  to={pathOf(stage)}
                  hint={t('dashboards.drill.projects', { count: stage.projects })}
                />
              </td>
              {showValue && (
                <td>
                  {stage.valueSar === null || stage.valueEntered === 0 ? (
                    <Percent value={null} />
                  ) : (
                    <span dir="ltr">{formatSar(stage.valueSar)}</span>
                  )}
                </td>
              )}
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
  rows: { key: string; label: string; count: number; to: string; hint: string }[];
}): ReactElement {
  return (
    <dl className="insights__counts">
      {rows.map((row) => (
        <div key={row.key} data-row={row.key}>
          <dt>{row.label}</dt>
          <dd>
            <CountLink count={row.count} to={row.to} hint={row.hint} />
          </dd>
        </div>
      ))}
    </dl>
  );
}

/** The on-plan counts in the dashboard's words, each with the register filter that lists its projects. */
function onPlanRows(onPlan: PortfolioOnPlan, t: ReturnType<typeof useI18n>['t']) {
  return [
    {
      key: 'onTrack',
      filter: 'ON_TRACK',
      label: t('projects.onPlan.ON_TRACK'),
      count: onPlan.onTrack,
    },
    { key: 'late', filter: 'LATE', label: t('projects.onPlan.LATE'), count: onPlan.late },
    {
      key: 'stalled',
      filter: 'STALLED',
      label: t('projects.onPlan.STALLED'),
      count: onPlan.stalled,
    },
    {
      key: 'notMeasured',
      filter: NOT_MEASURED,
      label: t('dashboards.insights.notMeasured'),
      count: onPlan.notMeasured,
    },
  ];
}

function OnPlanCounts({
  onPlan,
  filters,
}: {
  onPlan: PortfolioOnPlan;
  filters: PortfolioFilters;
}): ReactElement {
  const { t } = useI18n();
  return (
    <InsightSection id="onPlan" title={t('dashboards.insights.onPlan')}>
      <CountList
        rows={onPlanRows(onPlan, t).map((row) => ({
          ...row,
          to: registerPath(filters, { onPlan: row.filter }),
          hint: t('dashboards.drill.projects', { count: row.count }),
        }))}
      />
    </InsightSection>
  );
}

/** Each count's reason: the dashboard counts a project once, under its first reason, so the list filters by the main reason. */
const REASON_FILTERS: Record<string, FollowUpReason> = {
  stalled: 'STALLED',
  suspended: 'SUSPENDED',
  late: 'LATE',
  overdueAction: 'ACTION_OVERDUE',
  openObstacle: 'OPEN_OBSTACLE',
};

function Reasons({
  reasons,
  filters,
}: {
  reasons: PortfolioFollowUpReasons;
  filters: PortfolioFilters;
}): ReactElement {
  const { t } = useI18n();
  const linked = (row: { key: string; label: string; count: number }) => ({
    ...row,
    to: followUpPath(filters, { mainReason: REASON_FILTERS[row.key] }),
    hint: t('dashboards.drill.followUp', { count: row.count }),
  });
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
          ].map(linked)}
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
  const selection = useRowSelection();
  return (
    <InsightSection id="top" title={t('dashboards.insights.top')}>
      <TableContainer
        caption={t('dashboards.insights.top')}
        selection={{ selection, exportName: 'projects-needing-follow-up' }}
      >
        <thead>
          <tr>
            <SelectAllHeader
              ids={projects.map((project) => project.projectId)}
              selection={selection}
            />
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
              <SelectRowCell
                id={project.projectId}
                name={project.title.text}
                selection={selection}
              />
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

/** A donut's legend; a value that links is a link here, so the keyboard reaches every segment the mouse can open. */
function BucketLegend({ buckets }: { buckets: ChartBucket[] }): ReactElement {
  return (
    <ul className="grouped-columns__legend">
      {buckets.map((bucket) => {
        const words = (
          <>
            <span
              className="distribution__key"
              style={{ '--chart-color': bucket.color } as CSSProperties}
            />
            {`${bucket.text} (${String(bucket.count)})`}
          </>
        );
        return (
          <li key={bucket.key}>
            {bucket.href === undefined ? (
              words
            ) : (
              <Link className="drill-link" to={bucket.href} title={bucket.hint}>
                {words}
              </Link>
            )}
          </li>
        );
      })}
    </ul>
  );
}

/** Each band's fill, highest band first. Entities with no request in the period are not in the ring (review round 8, point 03). */
const COMPLETION_BAND_TONES: StatusTone[] = ['positive', 'info', 'warning', 'negative'];

/** How many entities the list beside the donut shows. */
const COMPLETION_LIST_SIZE = 5;

/**
 * The update completion of the entities, at a glance (round 7, item 5; review round 8, point 03). The centre is the simple average of the entities that
 * had requests in the period, each counted once; the ring splits only those entities into the four bands, and those with no request are counted on a
 * line under it. Beside it, five entities with their rate, lowest first by default, each opening its request monitor. A band and "see more" open the
 * entity table below, filtered to the band when one was clicked (point 01). The card title names the period the table shows.
 */
function EntityCompletionChart(): ReactElement {
  const { t, language } = useI18n();
  const location = useLocation();
  const { period } = useCompletionPeriod();
  const completion = useEntityCompletionBands();
  const [order, setOrder] = useState<'lowest' | 'highest'>('lowest');
  // Review round 8, point 09: whoever sends the monthly update requests is offered to, where entities had none in the period.
  const sendsRequests = useCan('UPDATE_REQUEST_SEND');
  const tableLink = (band: CompletionBandKey | null) => {
    const query = new URLSearchParams(location.search);
    if (band === null) {
      query.delete(COMPLETION_BAND_PARAM);
    } else {
      query.set(COMPLETION_BAND_PARAM, band);
    }
    return `${location.pathname}?${query.toString()}#${ENTITY_COMPLETION_ANCHOR}`;
  };
  return (
    <ChartCard title={`${t('dashboards.insights.completion')} · ${periodLabel(period, language)}`}>
      {(active, onActive) => {
        if (completion.data === undefined) {
          return completion.loading ? (
            <LoadingState />
          ) : (
            <ErrorState
              error={completion.error}
              message={t('dashboards.insights.completionUnavailable')}
              onRetry={completion.reload}
            />
          );
        }
        const { averagePercent, entities, bands, noRequests } = completion.data;
        const colors = bucketColors(COMPLETION_BAND_TONES, true);
        const buckets: ChartBucket[] = bands.map((band, index) => ({
          key: band.key,
          count: band.entities.length,
          text: t(`dashboards.insights.completionBands.${band.key}`),
          color: colors[index] ?? 'var(--chart-single)',
          href: tableLink(band.key),
          hint: t('dashboards.drill.entities', { count: band.entities.length }),
        }));
        const listed = (order === 'lowest' ? entities : [...entities].reverse()).slice(
          0,
          COMPLETION_LIST_SIZE,
        );
        return (
          <>
            <div className="completion-donut">
              <div className="completion-donut__chart">
                <div
                  className="distribution distribution--donut"
                  title={t('dashboards.insights.completionAverageHint')}
                >
                  <DonutChart
                    buckets={buckets}
                    active={active}
                    onActive={onActive}
                    totalLabel={t('dashboards.insights.completionEntities')}
                    summary={{
                      value: averagePercent === null ? '—' : percent(averagePercent),
                      label: t('dashboards.insights.completionAverage'),
                    }}
                  />
                  <BucketLegend buckets={buckets} />
                </div>
                <p className="insights__coverage">
                  {t('dashboards.insights.completionWithRequests', { count: entities.length })}
                </p>
                <p className="insights__coverage">
                  {t('dashboards.insights.completionNoRequests', { count: noRequests })}
                </p>
                {noRequests > 0 && (
                  <div className="insights__empty-hint" role="note">
                    <p>
                      {t('dashboards.insights.completionNoRequestsHint', {
                        period: periodLabel(period, language),
                      })}
                    </p>
                    {sendsRequests && (
                      <Link className="button" to={NEW_UPDATE_BATCH_PATH}>
                        {t('dashboards.insights.sendUpdateRequests')}
                      </Link>
                    )}
                  </div>
                )}
              </div>
              <div className="completion-donut__list">
                <div
                  className="completion-donut__order"
                  role="group"
                  aria-label={t('dashboards.insights.completionOrder')}
                >
                  {(['lowest', 'highest'] as const).map((value) => (
                    <button
                      key={value}
                      type="button"
                      className="button button--quiet"
                      aria-pressed={order === value}
                      onClick={() => {
                        setOrder(value);
                      }}
                    >
                      {t(`dashboards.insights.completionOrders.${value}`)}
                    </button>
                  ))}
                </div>
                {listed.length === 0 ? (
                  <p className="form__note">
                    {t('dashboards.insights.completionNone')} {t('common.empty.period')}
                  </p>
                ) : (
                  <ol className="completion-donut__entities">
                    {listed.map((entity) => (
                      <li key={entity.id}>
                        <Link
                          to={`${PARTICIPATION_MONITOR_PATH}?externalEntityId=${entity.id}`}
                          dir="auto"
                        >
                          {entity.name}
                        </Link>
                        <span className="completion-donut__track" aria-hidden="true">
                          <span
                            className="completion-donut__fill"
                            style={{ inlineSize: `${String(Math.min(100, entity.percent))}%` }}
                          />
                        </span>
                        <span className="completion-donut__figures">
                          <span dir="ltr">{percent(entity.percent)}</span>
                          <span className="cell__aside">
                            {t('dashboards.insights.completionCounts', {
                              accepted: entity.accepted,
                              issued: entity.issued,
                            })}
                          </span>
                        </span>
                      </li>
                    ))}
                  </ol>
                )}
                <Link className="completion-donut__more" to={tableLink(null)}>
                  {t('dashboards.insights.completionSeeMore')}
                </Link>
              </div>
            </div>
          </>
        );
      }}
    </ChartCard>
  );
}

function InsightCharts({
  filters,
  byPortfolio,
  stages,
  byCurrentStage,
  onPlan,
  itemName,
}: {
  filters: PortfolioFilters;
  byPortfolio: PortfolioBreakdown | null;
  stages: PortfolioStageCount[] | null;
  byCurrentStage: PortfolioCurrentStage[] | null;
  onPlan: PortfolioOnPlan | null;
  itemName: ItemName;
}): ReactElement | null {
  const { t, language } = useI18n();
  const { session } = useSession();
  // Beside the on-plan donut, for those who see the entity table at the bottom (AHDA's people with EXTERNAL_REQUEST_VIEW).
  const showsCompletion = useCan('EXTERNAL_REQUEST_VIEW') && session?.user.userType !== 'EXTERNAL';
  // Review round 8, point 09: whoever sets a project's stage plan is offered to do so from the stage chart.
  const setsStagePlans = useCan('PROJECT_REGISTER', 'PROGRESS_SUBMIT');
  const withoutStage = byCurrentStage?.find((stage) => stage.stageCode === null)?.projects ?? 0;
  const withStage = (byCurrentStage ?? []).reduce(
    (sum, stage) => sum + (stage.stageCode === null ? 0 : stage.projects),
    0,
  );
  const portfolioRows = byPortfolio?.rows ?? [];
  const single = 'var(--chart-single)';
  const projectsHint = (count: number) => t('dashboards.drill.projects', { count });
  const onPlanBuckets: ChartBucket[] | null =
    onPlan === null
      ? null
      : (() => {
          const colors = bucketColors(['positive', 'warning', 'negative', 'neutral'], true);
          return onPlanRows(onPlan, t).map((row, index) => ({
            key: row.key,
            count: row.count,
            text: row.label,
            color: colors[index] ?? single,
            href: registerPath(filters, { onPlan: row.filter }),
            hint: projectsHint(row.count),
          }));
        })();
  if (
    portfolioRows.length === 0 &&
    (stages ?? []).length === 0 &&
    onPlanBuckets === null &&
    !showsCompletion
  ) {
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
                  href:
                    row.itemId === null
                      ? undefined
                      : registerPath(filters, { strategicPortfolioItemId: row.itemId }),
                  hint: projectsHint(row.projects),
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
                  href:
                    row.itemId === null
                      ? undefined
                      : registerPath(filters, {
                          strategicPortfolioItemId: row.itemId,
                          measured: 'true',
                        }),
                  hint: projectsHint(row.measured),
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
                href: registerPath(filters, { status: stage.status }),
                hint: projectsHint(stage.projects),
              }))}
              active={active}
              onActive={onActive}
            />
          )}
        </ChartCard>
      )}
      {byCurrentStage !== null && byCurrentStage.some((stage) => stage.projects > 0) && (
        <ChartCard title={t('dashboards.insights.byCurrentStage')}>
          {(active, onActive) => (
            <>
              <BarChart
                buckets={byCurrentStage.map((stage) => ({
                  key: stage.stageCode ?? 'none',
                  count: stage.projects,
                  text: stage.name?.[language] ?? t('dashboards.insights.noCurrentStage'),
                  color: single,
                  href: registerPath(filters, { stage: stage.stageCode ?? NO_STAGE }),
                  hint: projectsHint(stage.projects),
                }))}
                active={active}
                onActive={onActive}
              />
              {withoutStage > 0 && (
                // Review round 8, point 09: projects with no stage yet have no plan set up; the chart says where to set it.
                <div className="insights__empty-hint" role="note">
                  <p>
                    {withStage === 0
                      ? t('dashboards.insights.stagePlanNone')
                      : t('dashboards.insights.stagePlanSome', { count: withoutStage })}
                  </p>
                  <Link className="button" to={registerPath(filters, { stage: NO_STAGE })}>
                    {t(
                      setsStagePlans
                        ? 'dashboards.insights.setUpStagePlans'
                        : 'dashboards.insights.viewWithoutStagePlan',
                    )}
                  </Link>
                </div>
              )}
            </>
          )}
        </ChartCard>
      )}
      {onPlanBuckets !== null && (
        <ChartCard title={t('dashboards.insights.onPlan')}>
          {(active, onActive) => (
            <div className="distribution distribution--donut">
              <DonutChart
                buckets={onPlanBuckets}
                active={active}
                onActive={onActive}
                totalLabel={t('dashboards.insights.projects')}
              />
              <BucketLegend buckets={onPlanBuckets} />
            </div>
          )}
        </ChartCard>
      )}
      {showsCompletion && <EntityCompletionChart />}
    </section>
  );
}
