import { type ReactElement } from 'react';

import { EntityCompletionPanel } from '@/features/external-participation/EntityCompletion.tsx';

import { DashboardCanvas, WidgetCard } from '../components/DashboardCanvas.tsx';
import { ProjectLocations } from '../components/ProjectLocations.tsx';
import { InsightBlock } from '../insights/PortfolioInsightsSection.tsx';
import { type Section } from './designApi.ts';
import { type SectionSources, widgetOf } from './sections.ts';

/** One of the dashboard's own sections, as the dashboard always drew it, inside the frame its design gives it. */
export function SectionBody({
  section,
  widgetCode,
  sources,
}: {
  section: Section;
  /** The widget a DASHBOARD_WIDGET section draws. */
  widgetCode?: string | null | undefined;
  sources: SectionSources;
}): ReactElement | null {
  const { insights, view, filters, map } = sources;
  switch (section) {
    case 'PROJECT_LOCATIONS':
      return view === undefined ? null : (
        <ProjectLocations
          locations={view.locations}
          places={view.places}
          {...(map === undefined
            ? { map: { filters } }
            : {
                selectedRegion: map.selectedRegion,
                onSelectRegion: map.onSelectRegion,
                map: { filters, onFilterCity: map.onFilterCity },
              })}
        />
      );
    case 'ENTITY_COMPLETION_TABLE':
      return <EntityCompletionPanel />;
    case 'DASHBOARD_WIDGETS':
      return view === undefined ? null : (
        <DashboardCanvas view={view} headingLevel={3} filters={filters} />
      );
    case 'DASHBOARD_WIDGET': {
      // One governed widget filling its visual, whose title it takes.
      const widget = widgetOf(widgetCode, view);
      return view === undefined || widget === undefined ? null : (
        <WidgetCard
          widget={widget}
          projectId={view.projectId}
          filters={filters}
          placement="visual"
        />
      );
    }
    default:
      return insights === undefined ? null : (
        <InsightBlock section={section} insights={insights} filters={filters} />
      );
  }
}
