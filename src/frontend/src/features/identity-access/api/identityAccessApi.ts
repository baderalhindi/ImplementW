import { apiRequest, type ApiResponse, type QueryValue } from '@/shared/api/httpClient.ts';

import {
  type AccessRelationshipCreateRequest,
  type AccessRelationshipDetail,
  type AccessRelationshipQuery,
  type AccessRelationshipSummary,
  type BilingualLabel,
  type DepartmentCreateRequest,
  type DepartmentDetail,
  type DepartmentQuery,
  type DepartmentSummary,
  type DepartmentUpdateRequest,
  type ExternalEntityCreateRequest,
  type ExternalEntityDetail,
  type ExternalEntityQuery,
  type ExternalEntitySummary,
  type ExternalEntityUpdateRequest,
  type Page,
  type PermissionProfileDetail,
  type PermissionProfileSummary,
  type PermissionSummary,
  type RoleDetail,
  type RoleSummary,
  type UserCreateRequest,
  type UserDetail,
  type UserQuery,
  type UserSummary,
  type UserUpdateRequest,
} from './types.ts';

/** R-29's largest page: what a lookup or a tree reads per request. */
export const MAX_PAGE_SIZE = 200;

async function data<T>(request: Promise<ApiResponse<T>>): Promise<T> {
  return (await request).data;
}

/** Every page of a collection, for lookups and the organization tree, which need the whole set. */
async function readAll<T>(
  path: string,
  query: Record<string, QueryValue>,
  signal?: AbortSignal,
): Promise<T[]> {
  const items: T[] = [];
  for (let page = 1; ; page += 1) {
    const result = await data(
      apiRequest<Page<T>>(path, { query: { ...query, page, pageSize: MAX_PAGE_SIZE }, signal }),
    );
    items.push(...result.items);
    if (items.length >= result.totalCount || result.items.length === 0) {
      return items;
    }
  }
}

function command<T>(path: string, etag?: string | null): Promise<ApiResponse<T>> {
  return apiRequest<T>(path, { method: 'POST', ifMatch: etag ?? null });
}

// ADM-002 to ADM-005, MOD-080
export const usersApi = {
  list: (query: UserQuery, signal?: AbortSignal) =>
    data(apiRequest<Page<UserSummary>>('/users', { query: { ...query }, signal })),
  get: (id: string, signal?: AbortSignal) => apiRequest<UserDetail>(`/users/${id}`, { signal }),
  create: (request: UserCreateRequest) =>
    apiRequest<UserDetail>('/users', { method: 'POST', body: request }),
  update: (id: string, request: UserUpdateRequest, etag: string | null) =>
    apiRequest<UserDetail>(`/users/${id}`, { method: 'PUT', body: request, ifMatch: etag }),
  activate: (id: string, etag: string | null) => command<UserDetail>(`/users/${id}/activate`, etag),
  disable: (id: string, etag: string | null) => command<UserDetail>(`/users/${id}/disable`, etag),
};

// ADM-010, MOD-081, MOD-082
export const accessRelationshipsApi = {
  list: (query: AccessRelationshipQuery, signal?: AbortSignal) =>
    data(
      apiRequest<Page<AccessRelationshipSummary>>('/access-relationships', {
        query: { ...query },
        signal,
      }),
    ),
  create: (request: AccessRelationshipCreateRequest) =>
    data(
      apiRequest<AccessRelationshipDetail>('/access-relationships', {
        method: 'POST',
        body: request,
      }),
    ),
  end: (id: string) => data(command<AccessRelationshipDetail>(`/access-relationships/${id}/end`)),
};

// ADM-006 to ADM-009
export const rolesApi = {
  list: (signal?: AbortSignal) => data(apiRequest<RoleSummary[]>('/roles', { signal })),
  get: (id: string, signal?: AbortSignal) => apiRequest<RoleDetail>(`/roles/${id}`, { signal }),
  update: (id: string, name: BilingualLabel, etag: string | null) =>
    apiRequest<RoleDetail>(`/roles/${id}`, { method: 'PUT', body: { name }, ifMatch: etag }),
  listPermissions: (signal?: AbortSignal) =>
    data(apiRequest<PermissionSummary[]>('/permissions', { signal })),
  listProfiles: (signal?: AbortSignal) =>
    readAll<PermissionProfileSummary>('/permission-profiles', {}, signal),
  getProfile: (id: string, signal?: AbortSignal) =>
    data(apiRequest<PermissionProfileDetail>(`/permission-profiles/${id}`, { signal })),
};

// ADM-011, ADM-012
export const departmentsApi = {
  list: (query: DepartmentQuery, signal?: AbortSignal) =>
    data(apiRequest<Page<DepartmentSummary>>('/departments', { query: { ...query }, signal })),
  listAll: (signal?: AbortSignal) => readAll<DepartmentSummary>('/departments', {}, signal),
  get: (id: string, signal?: AbortSignal) =>
    apiRequest<DepartmentDetail>(`/departments/${id}`, { signal }),
  create: (request: DepartmentCreateRequest) =>
    apiRequest<DepartmentDetail>('/departments', { method: 'POST', body: request }),
  update: (id: string, request: DepartmentUpdateRequest, etag: string | null) =>
    apiRequest<DepartmentDetail>(`/departments/${id}`, {
      method: 'PUT',
      body: request,
      ifMatch: etag,
    }),
  activate: (id: string) => command<DepartmentDetail>(`/departments/${id}/activate`),
  deactivate: (id: string) => command<DepartmentDetail>(`/departments/${id}/deactivate`),
};

export type ExternalEntityTransition = 'suspend' | 'activate' | 'retire';

// ADM-013
export const externalEntitiesApi = {
  list: (query: ExternalEntityQuery, signal?: AbortSignal) =>
    data(
      apiRequest<Page<ExternalEntitySummary>>('/external-entities', {
        query: { ...query },
        signal,
      }),
    ),
  listAll: (signal?: AbortSignal) =>
    readAll<ExternalEntitySummary>('/external-entities', {}, signal),
  get: (id: string, signal?: AbortSignal) =>
    apiRequest<ExternalEntityDetail>(`/external-entities/${id}`, { signal }),
  create: (request: ExternalEntityCreateRequest) =>
    apiRequest<ExternalEntityDetail>('/external-entities', { method: 'POST', body: request }),
  update: (id: string, request: ExternalEntityUpdateRequest, etag: string | null) =>
    apiRequest<ExternalEntityDetail>(`/external-entities/${id}`, {
      method: 'PUT',
      body: request,
      ifMatch: etag,
    }),
  transition: (id: string, transition: ExternalEntityTransition) =>
    command<ExternalEntityDetail>(`/external-entities/${id}/${transition}`),
};
