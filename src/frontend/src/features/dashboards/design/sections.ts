import { type PortfolioFilters } from '../api/dashboardsApi.ts';
import { type PortfolioInsights } from '../api/insightTypes.ts';
import { type DashboardView } from '../api/types.ts';
import { insightBlockShown } from '../insights/insightSections.ts';
import { visibleWidgets } from '../layout.ts';
import { hasNoData } from '../presentation.ts';
import { type Section } from './designApi.ts';

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

/** Whether a section has anything for the caller: what the caller may not see, or what has nothing in it, is not drawn, as before. */
export function sectionShown(section: Section, sources: SectionSources): boolean {
  switch (section) {
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
    default:
      return (
        sources.insights !== undefined &&
        insightBlockShown(section, sources.insights, sources.showsCompletion)
      );
  }
}
