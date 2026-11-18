import { type ReactElement } from 'react';

import { EntityCompletionPanel } from '@/features/external-participation/EntityCompletion.tsx';

import { DashboardCanvas } from '../components/DashboardCanvas.tsx';
import { ProjectLocations } from '../components/ProjectLocations.tsx';
import { InsightBlock } from '../insights/PortfolioInsightsSection.tsx';
import { type Section } from './designApi.ts';
import { type SectionSources } from './sections.ts';

/** One of the dashboard's own sections, as the dashboard always drew it, inside the frame its design gives it. */
export function SectionBody({
  section,
  sources,
}: {
  section: Section;
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
    default:
      return insights === undefined ? null : (
        <InsightBlock section={section} insights={insights} filters={filters} />
      );
  }
}
