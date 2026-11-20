import { translate } from '@/shared/i18n/i18n.ts';

import { type PortfolioFilters } from '../api/dashboardsApi.ts';
import { type PortfolioInsights } from '../api/insightTypes.ts';
import { type DashboardView, type DashboardWidgetResult } from '../api/types.ts';
import { insightBlockShown } from '../insights/insightSections.ts';
import { visibleWidgets } from '../layout.ts';
import { hasNoData } from '../presentation.ts';
import { type Nestable, pathWhere, type VisualPath } from './containers.ts';
import { type Visual } from './designApi.ts';
import { freePlace } from './visuals.ts';

/**
 * What the dashboard's own sections are drawn from: the insights and the dashboard's view (its map and governed widgets), both under the
 * dashboard's filters. On the dashboard `map` sets the region and city filters from the map; the designer draws the same without it.
 */
export interface SectionSources {
  insights: PortfolioInsights | undefined;
  view: DashboardView | undefined;
  filters: PortfolioFilters;
  showsCompletion: boolean;
  map?:
    | {
        selectedRegion: string | undefined;
        onSelectRegion: (region: string | undefined) => void;
        onFilterCity: (city: string | undefined) => void;
      }
    | undefined;
}

/** The widget a DASHBOARD_WIDGET visual draws: none when the caller may not see it, has hidden it, or the view no longer has it. */
export function widgetOf(
  widgetCode: string | null | undefined,
  view: DashboardView | undefined,
): DashboardWidgetResult | undefined {
  return view === undefined
    ? undefined
    : visibleWidgets(view.widgets).find((widget) => widget.code === widgetCode);
}

/** Whether a section has anything for the caller: what the caller may not see, or what has nothing in it, is not drawn, as before. */
export function sectionShown(
  { section, widgetCode }: Pick<Visual, 'section' | 'widgetCode'>,
  sources: SectionSources,
): boolean {
  switch (section) {
    case undefined:
    case null:
      return false;
    case 'ENTITY_COMPLETION_TABLE':
      return sources.showsCompletion;
    case 'PROJECT_LOCATIONS':
      return (sources.view?.locations.length ?? 0) > 0;
    case 'DASHBOARD_WIDGETS':
      return (
        sources.view !== undefined &&
        !hasNoData(sources.view) &&
        visibleWidgets(sources.view.widgets).length > 0
      );
    case 'DASHBOARD_WIDGET':
      return (
        sources.view !== undefined &&
        !hasNoData(sources.view) &&
        widgetOf(widgetCode, sources.view) !== undefined
      );
    default:
      return (
        sources.insights !== undefined &&
        insightBlockShown(section, sources.insights, sources.showsCompletion)
      );
  }
}

/** The map's size when placed from the gallery: the grid's width, eight rows. */
export const MAP_SIZE = { width: 12, height: 7 };

/** Where the map (Projects by location) is, top-level or in a container; null when the design has none. Sections are once per design. */
export function mapPath<T extends Nestable<T> & Pick<Visual, 'section'>>(
  visuals: readonly T[],
): VisualPath | null {
  return pathWhere(visuals, (visual) => visual.section === 'PROJECT_LOCATIONS');
}

/**
 * The gallery's Map (20 Nov 2026, docs/architecture/dashboard-designer-map-text.md): the Projects by location section at the first free place,
 * titled as the dashboard names it; null when the grid has no room.
 */
export function newMapVisual(visuals: Visual[]): Visual | null {
  const place = freePlace(visuals, MAP_SIZE.width, MAP_SIZE.height);
  return place === null
    ? null
    : {
        title: {
          ar: translate('ar', 'dashboards.locations.title'),
          en: translate('en', 'dashboards.locations.title'),
        },
        visualType: 'SECTION',
        measures: [],
        groupBy: 'NONE',
        section: 'PROJECT_LOCATIONS',
        layoutColumn: place.column,
        layoutRow: place.row,
        layoutWidth: MAP_SIZE.width,
        layoutHeight: MAP_SIZE.height,
      };
}
