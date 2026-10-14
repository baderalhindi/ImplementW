import { useCallback } from 'react';

import { type ApiResource, useApiResource } from '@/shared/api/useApiResource.ts';

import { integrationAdminApi } from './api/integrationAdminApi.ts';
import { type IntegrationCatalogueView } from './api/types.ts';

/** `GET /integrations`: the catalogue every FG-05 screen starts from, read again when the screen opens. */
export function useCatalogue(): ApiResource<IntegrationCatalogueView> {
  const load = useCallback((signal: AbortSignal) => integrationAdminApi.catalogue(signal), []);
  return useApiResource(load, { keepWhileReloading: true });
}
