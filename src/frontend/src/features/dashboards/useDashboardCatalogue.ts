import { useCallback } from 'react';

import { useSession } from '@/features/identity-access/session/useSession.ts';
import { type ApiResource, useApiResource } from '@/shared/api/useApiResource.ts';

import { dashboardsApi } from './api/dashboardsApi.ts';
import { type DashboardCatalogueEntry } from './api/types.ts';

/** The dashboards the API offers the signed-in person: what the shell and the Home follow, whatever the person's roles are called. */
export function useDashboardCatalogue(): ApiResource<DashboardCatalogueEntry[]> {
  const { session } = useSession();
  const userId = session?.user.id ?? null;
  const load = useCallback(
    (signal: AbortSignal) =>
      userId === null ? Promise.resolve([]) : dashboardsApi.catalogue(signal),
    [userId],
  );
  return useApiResource(load);
}
