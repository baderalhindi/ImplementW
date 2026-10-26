import { apiRequest } from '@/shared/api/httpClient.ts';

import { type BilingualLabel } from '../api/types.ts';

// Names only, for every signed-in person (the 24 Oct 2026 review, H1): the people a screen names by id, and the departments and
// entities screens name and forms offer. The API tells an external user its own entity's names alone.

export interface PersonName {
  id: string;
  displayName: string;
}

export interface OrganizationUnit {
  id: string;
  code: string;
  name: BilingualLabel;
  isActive: boolean;
}

export interface OrganizationUnits {
  departments: OrganizationUnit[];
  externalEntities: OrganizationUnit[];
}

/** The API's limit on ids per call. */
export const MAX_NAMES_PER_CALL = 100;

export const namesApi = {
  people: async (userIds: readonly string[], signal?: AbortSignal) =>
    (
      await apiRequest<{ users: PersonName[] }>('/display-names', {
        query: { userId: userIds },
        signal,
      })
    ).data.users,
  units: async (signal?: AbortSignal) =>
    (await apiRequest<OrganizationUnits>('/organization-units', { signal })).data,
};
