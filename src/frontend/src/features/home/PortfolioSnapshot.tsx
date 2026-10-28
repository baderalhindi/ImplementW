import { type ReactElement } from 'react';
import { Link } from 'react-router';

import { dashboardsApi } from '@/features/dashboards/api/dashboardsApi.ts';
import {
  type DashboardView,
  type DashboardWidgetResult,
  type WidgetFigure,
} from '@/features/dashboards/api/types.ts';
import { ProjectLocations } from '@/features/dashboards/components/ProjectLocations.tsx';
import { PORTFOLIO_DASHBOARD_PATH } from '@/features/dashboards/paths.ts';
import { formatFigure, labelOfValue } from '@/features/dashboards/presentation.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { type StatusTone } from '@/shared/ui/StatusBadge.tsx';

/** The Portfolio Dashboard, when the person's roles open it; null otherwise (the snapshot is then not shown). */
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
  /** A money figure: long, so its tile takes two columns. */
  wide?: boolean;
}

/**
 * The Home's portfolio snapshot (the 27 Oct 2026 review): the headline figures of the Portfolio Dashboard — projects, active
 * projects, health by state, open risks and the money — and where the projects are delivered, each read from the dashboard's own
 * widgets, so the Home counts exactly what the dashboard counts. A widget the person may not see gives no tile.
 */
export function PortfolioSnapshot(): ReactElement | null {
  const { t, language } = useI18n();
  const portfolio = useApiResource(readPortfolio);
  const view = portfolio.data;
  if (view === undefined || view === null) {
    return null;
  }

  const tiles: Tile[] = [];
  const lifecycle = widgetOf(view, 'PROJECTS_BY_LIFECYCLE');
  if (lifecycle?.data != null) {
    const buckets = lifecycle.data.distribution;
    tiles.push({
      key: 'projects',
      label: t('home.portfolio.projects'),
      value: String(buckets.reduce((sum, bucket) => sum + bucket.count, 0)),
    });
    tiles.push({
      key: 'active',
      label: t('home.portfolio.active'),
      value: String(buckets.find((bucket) => bucket.key === 'ACTIVE')?.count ?? 0),
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
      tiles.push({
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
    tiles.push({ key: 'risks', label: t('home.portfolio.openRisks'), value: openRiskValue });
  }
  const financials = widgetOf(view, 'PUBLISHED_FINANCIALS') ?? widgetOf(view, 'CURRENT_FINANCIALS');
  for (const measure of ['APPROVED_BUDGET', 'ACTUAL_EXPENDITURE_TO_DATE'] as const) {
    const figure = figureOf(financials, measure);
    const value = figure === undefined ? null : formatFigure(figure, t);
    if (value !== null) {
      tiles.push({ key: measure, label: t(`dashboards.measures.${measure}`), value, wide: true });
    }
  }

  return (
    <section className="home__portfolio" aria-labelledby="home-portfolio">
      <div className="section__header">
        <h2 id="home-portfolio">{t('home.portfolio.title')}</h2>
        <Link to={PORTFOLIO_DASHBOARD_PATH}>{t('home.portfolio.open')}</Link>
      </div>
      {tiles.length > 0 && (
        <ul className="home__tiles">
          {tiles.map((tile) => (
            <li
              key={tile.key}
              className={tile.wide === true ? 'home__tile home__tile--wide' : 'home__tile'}
              data-tone={tile.tone}
            >
              <span className="home__tile-value" dir="ltr">
                {tile.value}
              </span>
              <span className="home__tile-label">{tile.label}</span>
            </li>
          ))}
        </ul>
      )}
      <ProjectLocations
        locations={view.locations}
        places={view.places}
        linkTo={(region) => `${PORTFOLIO_DASHBOARD_PATH}&region=${encodeURIComponent(region)}`}
      />
    </section>
  );
}
