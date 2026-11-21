import { type ReactElement } from 'react';
import { Link, useNavigate } from 'react-router';

import { dashboardsApi } from '@/features/dashboards/api/dashboardsApi.ts';
import {
  type DashboardView,
  type DashboardWidgetResult,
  type WidgetFigure,
} from '@/features/dashboards/api/types.ts';
import { ProjectLocations } from '@/features/dashboards/components/ProjectLocations.tsx';
import { lifecycleRegisterPath } from '@/features/dashboards/drillLinks.ts';
import { PORTFOLIO_DASHBOARD_PATH } from '@/features/dashboards/paths.ts';
import { formatFigure, hasNoData, labelOfValue } from '@/features/dashboards/presentation.ts';
import { useCan } from '@/features/identity-access/permissions/permissions.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { EmptyState } from '@/shared/ui/States.tsx';
import { type StatusTone } from '@/shared/ui/StatusBadge.tsx';

import { ReachedProjectLocations } from './ReachedProjectLocations.tsx';
import { sarExactLabel } from '@/shared/format/money.ts';

/** The Portfolio Dashboard, when the person's roles open it; null otherwise. */
async function readPortfolio(signal: AbortSignal): Promise<DashboardView | null> {
  const catalogue = await dashboardsApi.catalogue(signal);
  return catalogue.some((entry) => entry.code === 'PORTFOLIO')
    ? dashboardsApi.get('PORTFOLIO', {}, signal)
    : null;
}

function widgetOf(view: DashboardView, code: string): DashboardWidgetResult | undefined {
  return view.widgets.find((widget) => widget.code === code && widget.data !== null);
}

function figureOf(
  widget: DashboardWidgetResult | undefined,
  measure: string,
): WidgetFigure | undefined {
  return widget?.data?.figures.find((figure) => figure.measure === measure);
}

interface Tile {
  key: string;
  label: string;
  value: string;
  tone?: StatusTone;
  /** The records it counts (review round 8, point 01); a figure no list filters by has none. */
  to?: string;
  hint?: string;
  /** A compact amount's full figure, shown on hover (review round 8, point 11). */
  exact?: string;
}

function TileRow({
  tiles,
  kind,
}: {
  tiles: Tile[];
  kind: 'counts' | 'money';
}): ReactElement | null {
  if (tiles.length === 0) {
    return null;
  }
  return (
    <ul className={`home__tiles home__tiles--${kind}`}>
      {tiles.map((tile) => (
        <li
          key={tile.key}
          className={tile.to === undefined ? 'home__tile' : 'home__tile home__tile--linked'}
          data-tone={tile.tone}
          title={tile.hint}
        >
          {/* A compact amount reads in the page's direction ("64.6 مليار ريال"); a count stays left to right. */}
          <span
            className="home__tile-value"
            dir={kind === 'money' ? undefined : 'ltr'}
            title={tile.exact}
          >
            {tile.value}
          </span>
          {tile.to === undefined ? (
            <span className="home__tile-label">{tile.label}</span>
          ) : (
            <Link className="home__tile-label home__tile-link" to={tile.to}>
              {tile.label}
            </Link>
          )}
        </li>
      ))}
    </ul>
  );
}

/**
 * The Home's portfolio snapshot (the 27 Oct 2026 review): the headline figures of the Portfolio Dashboard — projects, active
 * projects, health by state, open risks and the money — and where the projects are delivered, each read from the dashboard's own
 * widgets, so the Home counts exactly what the dashboard counts. A widget the person may not see gives no tile. Someone who views
 * projects outside the dashboard's audience still sees where their own projects are.
 */
export function PortfolioSnapshot(): ReactElement | null {
  const { t, language } = useI18n();
  const navigate = useNavigate();
  const portfolio = useApiResource(readPortfolio);
  const viewsProjects = useCan('PROJECT_VIEW');
  const view = portfolio.data;
  if (view === undefined) {
    return null;
  }
  if (view === null) {
    return viewsProjects ? <ReachedProjectLocations /> : null;
  }

  if (hasNoData(view)) {
    return (
      <section className="home__portfolio" aria-labelledby="home-portfolio">
        <div className="section__header">
          <h2 id="home-portfolio">{t('home.portfolio.title')}</h2>
        </div>
        <EmptyState title={t('dashboards.noData')} reason="scope" />
      </section>
    );
  }

  const counts: Tile[] = [];
  const money: Tile[] = [];
  const lifecycle = widgetOf(view, 'PROJECTS_BY_LIFECYCLE');
  if (lifecycle?.data != null) {
    const buckets = lifecycle.data.distribution;
    const projects = buckets.reduce((sum, bucket) => sum + bucket.count, 0);
    const active = buckets.find((bucket) => bucket.key === 'ACTIVE')?.count ?? 0;
    // The lifecycle widget counts every state, so the register opens unfiltered (all) or on ACTIVE.
    counts.push({
      key: 'projects',
      label: t('home.portfolio.projects'),
      value: String(projects),
      to: '/projects',
      hint: t('dashboards.drill.projects', { count: projects }),
    });
    counts.push({
      key: 'active',
      label: t('home.portfolio.active'),
      value: String(active),
      to: lifecycleRegisterPath({}, 'ACTIVE'),
      hint: t('dashboards.drill.projects', { count: active }),
    });
  }
  const health = widgetOf(view, 'PUBLISHED_HEALTH') ?? widgetOf(view, 'CURRENT_HEALTH');
  for (const bucket of health?.data?.distribution ?? []) {
    if (bucket.key === 'GREEN' || bucket.key === 'AMBER' || bucket.key === 'RED') {
      const word = labelOfValue(
        health?.projectionCode ?? '',
        bucket.key,
        bucket.label,
        language,
        t,
      );
      counts.push({
        key: bucket.key,
        label: word.text,
        value: String(bucket.count),
        tone: word.tone,
      });
    }
  }
  const openRisks = figureOf(widgetOf(view, 'RISK_EXPOSURE'), 'OPEN_RISKS');
  const openRiskValue = openRisks === undefined ? null : formatFigure(openRisks, t);
  if (openRiskValue !== null) {
    // The risk register opens on the open risks.
    counts.push({
      key: 'risks',
      label: t('home.portfolio.openRisks'),
      value: openRiskValue,
      to: '/risks',
      hint: t('dashboards.drill.risks', { count: openRiskValue }),
    });
  }
  const financials = widgetOf(view, 'PUBLISHED_FINANCIALS') ?? widgetOf(view, 'CURRENT_FINANCIALS');
  for (const measure of ['APPROVED_BUDGET', 'ACTUAL_EXPENDITURE_TO_DATE'] as const) {
    const figure = figureOf(financials, measure);
    const value = figure === undefined ? null : formatFigure(figure, t);
    if (value !== null) {
      money.push({
        key: measure,
        label: t(`dashboards.measures.${measure}`),
        value,
        ...(figure?.value == null ? {} : { exact: sarExactLabel(figure.value, t) }),
      });
    }
  }

  return (
    <section className="home__portfolio" aria-labelledby="home-portfolio">
      <div className="section__header">
        <h2 id="home-portfolio">{t('home.portfolio.title')}</h2>
        <Link to={PORTFOLIO_DASHBOARD_PATH}>{t('home.portfolio.open')}</Link>
      </div>
      <TileRow tiles={counts} kind="counts" />
      <TileRow tiles={money} kind="money" />
      <ProjectLocations
        locations={view.locations}
        places={view.places}
        linkTo={(region) => `${PORTFOLIO_DASHBOARD_PATH}&region=${encodeURIComponent(region)}`}
        map={{
          filters: {},
          onFilterCity: (city) => {
            void navigate(
              city === undefined
                ? PORTFOLIO_DASHBOARD_PATH
                : `${PORTFOLIO_DASHBOARD_PATH}&city=${encodeURIComponent(city)}`,
            );
          },
        }}
      />
    </section>
  );
}
