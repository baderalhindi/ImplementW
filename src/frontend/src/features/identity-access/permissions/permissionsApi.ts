import { apiRequest } from '@/shared/api/httpClient.ts';

// GET /sessions/current/permissions (the 24 Oct 2026 review, C3): what the caller holds now, read from the API's engine, so a
// grant changed since sign-in shows on the next read. Navigation, not protection: the API decides every operation again.

export type DataScope = 'ALL' | 'DEPT' | 'OWN' | 'ASSIGNED' | 'ENTITY' | 'READ_ONLY';

export interface EffectivePermission {
  code: string;
  scopes: DataScope[];
}

export interface EffectivePermissions {
  permissions: EffectivePermission[];
}

export const permissionsApi = {
  current: async (signal?: AbortSignal) =>
    (await apiRequest<EffectivePermissions>('/sessions/current/permissions', { signal })).data,
};
