import { type ReactElement, useCallback } from 'react';
import { Link } from 'react-router';

import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';

import { type PortfolioFilters } from '../api/dashboardsApi.ts';
import { designApi, type VisualResult } from './designApi.ts';
import { drillPath, gridPlace, placeKey } from './visuals.ts';
import { VisualBody } from './VisualBody.tsx';

/** One visual on the dashboard: its title over its figures. A card's or gauge's title opens the records its figure counts, as a headline card's did. */
function VisualFrame({
  visual,
  filters,
}: {
  visual: VisualResult;
  filters: PortfolioFilters;
}): ReactElement {
  const { t, language } = useI18n();
  const title = visual.title[language];
  const whole = visual.groupBy === 'NONE' ? visual.groups?.[0] : undefined;
  const href =
    whole === undefined || visual.visualType === 'TABLE'
      ? undefined
      : drillPath(visual.measures[0] ?? 'PROJECTS', 'NONE', null, filters);
  return (
    <section
      className={href === undefined ? 'widget visual' : 'widget visual home__tile--linked'}
      style={gridPlace(visual)}
      data-visual={visual.visualType}
    >
      <h3 className="widget__title">
        {href === undefined ? (
          title
        ) : (
          <Link
            className="home__tile-link"
            to={href}
            title={
              visual.measures[0] === 'NEEDING_FOLLOW_UP'
                ? t('dashboards.drill.followUp', { count: whole?.values[0] ?? 0 })
                : t('dashboards.drill.projects', { count: whole?.projects ?? 0 })
            }
          >
            {title}
          </Link>
        )}
      </h3>
      <div className="visual__body">
        <VisualBody visual={visual} filters={filters} />
      </div>
    </section>
  );
}

/** A figure the caller may not see at all, such as the value without the amounts permission: left out, as the value card always was. */
function withheld(visual: VisualResult): boolean {
  return (
    visual.measures.every((measure) => measure === 'VALUE_SAR') &&
    (visual.groups ?? []).every((group) => group.values.every((value) => value === null))
  );
}

/**
 * The Portfolio dashboard's designed visuals (17 Nov 2026), in place of its fixed headline cards and portfolio charts: the design the Portfolio
 * Manager saved, or the standard one, under the dashboard's filters. A visual the caller holds no permission for is left out.
 */
export function DesignedVisuals({ filters }: { filters: PortfolioFilters }): ReactElement | null {
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
  const design = useApiResource(
    useCallback(
      (signal: AbortSignal) =>
        designApi.get(
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
  if (design.data === undefined) {
    return design.loading ? null : <p className="state">{t('dashboards.insights.unavailable')}</p>;
  }
  const shown = design.data.visuals.filter((visual) => visual.groups !== null && !withheld(visual));
  return shown.length === 0 ? null : (
    <section className="insights__section" aria-labelledby="designed-visuals">
      <h2 id="designed-visuals" className="visually-hidden">
        {t('dashboards.insights.headlineLabel')}
      </h2>
      <div className="visual-grid" data-section="designed">
        {shown.map((visual) => (
          // No two visuals share a place, so the place names one.
          <VisualFrame key={placeKey(visual)} visual={visual} filters={filters} />
        ))}
      </div>
    </section>
  );
}
