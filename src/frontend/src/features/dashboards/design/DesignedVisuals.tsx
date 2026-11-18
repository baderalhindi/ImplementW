import { type ReactElement, useCallback } from 'react';
import { Link } from 'react-router';

import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { EmptyState, LoadingState } from '@/shared/ui/States.tsx';

import { type PortfolioFilters } from '../api/dashboardsApi.ts';
import { type DashboardView } from '../api/types.ts';
import {
  projectCountOf,
  usePortfolioInsights,
  useShowsCompletion,
} from '../insights/insightSections.ts';
import { emptyPortfolioReason, hasNoData } from '../presentation.ts';
import { designApi, type VisualResult } from './designApi.ts';
import { SectionBody } from './SectionBody.tsx';
import { sectionShown, type SectionSources } from './sections.ts';
import { compacted, drillPath, gridPlace, placeKey } from './visuals.ts';
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

/** A section of the dashboard in its design's place, titled as the design names it. */
function SectionFrame({
  visual,
  sources,
}: {
  visual: VisualResult;
  sources: SectionSources;
}): ReactElement | null {
  const { language } = useI18n();
  return visual.section === undefined || visual.section === null ? null : (
    <section
      className="widget visual visual--section"
      style={gridPlace(visual)}
      data-visual="SECTION"
      data-section-code={visual.section}
    >
      <h3 className="widget__title">{visual.title[language]}</h3>
      <div className="visual__body visual__body--section">
        <SectionBody section={visual.section} sources={sources} />
      </div>
    </section>
  );
}

/**
 * The Portfolio dashboard as the Portfolio Manager designed it (17 and 18 Nov 2026): every card, chart and section in its design's place, under
 * the dashboard's filters, the saved design or the standard one. What the caller may not see, or what has nothing, is left out and the rest rises
 * into its room. No project in reach reads "no data", never a grid of zeros.
 */
export function DesignedVisuals({
  filters,
  view,
  map,
  onClearFilters,
}: {
  filters: PortfolioFilters;
  view: DashboardView;
  map: SectionSources['map'];
  onClearFilters: () => void;
}): ReactElement | null {
  const { t } = useI18n();
  const insights = usePortfolioInsights(filters);
  const showsCompletion = useShowsCompletion();
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
  if (design.data === undefined || insights.data === undefined) {
    return design.loading || insights.loading ? (
      <LoadingState />
    ) : (
      <p className="state">{t('dashboards.insights.unavailable')}</p>
    );
  }
  // No project in reach: the insights count none, or count nothing the caller may see and the dashboard's own view has no data either.
  const projects = projectCountOf(insights.data);
  if (projects === 0 || (projects === null && hasNoData(view))) {
    const reason = emptyPortfolioReason(filters);
    return (
      <EmptyState
        title={t('dashboards.noData')}
        reason={reason}
        action={
          reason === 'scope'
            ? undefined
            : { label: t('common.filters.clear'), onClick: onClearFilters }
        }
      />
    );
  }
  const sources: SectionSources = { insights: insights.data, view, filters, showsCompletion, map };
  const shown = compacted(
    design.data.visuals.filter((visual) =>
      visual.section === undefined || visual.section === null
        ? visual.groups !== null && !withheld(visual)
        : sectionShown(visual.section, sources),
    ),
  );
  return shown.length === 0 ? null : (
    <section className="insights" aria-labelledby="designed-visuals">
      <h2 id="designed-visuals" className="visually-hidden">
        {t('dashboards.insights.headlineLabel')}
      </h2>
      <div className="visual-grid" data-section="designed">
        {shown.map((visual) =>
          // No two visuals share a place, so the place names one.
          visual.visualType === 'SECTION' ? (
            <SectionFrame key={placeKey(visual)} visual={visual} sources={sources} />
          ) : (
            <VisualFrame key={placeKey(visual)} visual={visual} filters={filters} />
          ),
        )}
      </div>
    </section>
  );
}
