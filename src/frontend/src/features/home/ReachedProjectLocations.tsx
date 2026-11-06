import { type ReactElement } from 'react';

import { dashboardsApi } from '@/features/dashboards/api/dashboardsApi.ts';
import { ProjectLocations } from '@/features/dashboards/components/ProjectLocations.tsx';
import { useApiResource } from '@/shared/api/useApiResource.ts';

/**
 * The map of a person's projects for whoever views projects without the Portfolio Dashboard's audience. A region links to the register
 * filtered by it, the one place such a person can narrow to it. Someone with no projects sees nothing.
 */
export function ReachedProjectLocations(): ReactElement | null {
  const reached = useApiResource(dashboardsApi.projectLocations);
  return reached.data === undefined ? null : (
    <ProjectLocations
      locations={reached.data.locations}
      places={reached.data.places}
      linkTo={(region) => `/projects?regionItemId=${encodeURIComponent(region)}`}
      map={{ filters: {} }}
    />
  );
}
