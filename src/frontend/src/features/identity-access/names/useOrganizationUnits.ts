import { useScopedResource } from '@/shared/api/resourceScope.ts';

import { namesApi, type OrganizationUnits } from './namesApi.ts';

const NONE: OrganizationUnits = { departments: [], externalEntities: [] };

/**
 * The departments and entities the signed-in person may be told of, for naming them and offering them in a form. Until the API answers,
 * or if it fails, there are none: a screen shows the id's short form rather than a name it does not have.
 */
export function useOrganizationUnits(): OrganizationUnits {
  return useScopedResource(namesApi.units).data ?? NONE;
}
