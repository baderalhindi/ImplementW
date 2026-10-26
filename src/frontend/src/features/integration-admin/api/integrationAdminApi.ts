import { apiRequest } from '@/shared/api/httpClient.ts';

import {
  type IdentityIntegrationStatus,
  type IdentityIntegrationTestResult,
  type IntegrationCatalogueView,
  type IntegrationDetail,
  type InvocationDetail,
  type InvocationPage,
  type InvocationQuery,
  type NarrativeText,
  type OperationalAlertPage,
  type OperationalAlertQuery,
  type OperationalAlertView,
  type SyncRunPage,
} from './types.ts';

// FG-05 at runtime (TASK-075 integration-monitoring.md §5) and ADM-041's identity-integration endpoints (TASK-028).
// The API parses a set as one comma-joined value (`status=A,B`), not as the repeated parameter the shared client sends
// for an array, so sets are joined here. Every write is sensitive: the shared client sends its Idempotency-Key.

export interface PageRequest {
  page: number;
  pageSize: number;
}

export interface Versioned<T> {
  data: T;
  etag: string | null;
}

function joined(values: readonly string[] | undefined): string | undefined {
  return values === undefined || values.length === 0 ? undefined : values.join(',');
}

export const integrationAdminApi = {
  /** ADM-040: every integration with its instances' availability and freshness, and the environment. */
  catalogue: async (signal?: AbortSignal) =>
    (await apiRequest<IntegrationCatalogueView>('/integrations', { signal })).data,

  /** ADM-046: one integration, its retry policy, FG-05's operations for it and ADR-004's measures over a window. */
  integration: async (
    integrationId: string,
    window: { from?: string | undefined; to?: string | undefined },
    signal?: AbortSignal,
  ): Promise<Versioned<IntegrationDetail>> =>
    apiRequest<IntegrationDetail>(`/integrations/${integrationId}`, {
      query: { from: window.from, to: window.to },
      signal,
    }),

  /** MOD-101: ACTIVE → DISABLED or back; If-Match from the detail's ETag. */
  setActivation: async (integrationId: string, enable: boolean, ifMatch: string | null) =>
    (
      await apiRequest<IntegrationDetail>(
        `/integrations/${integrationId}/${enable ? 'enable' : 'disable'}`,
        { method: 'POST', ifMatch },
      )
    ).data,

  /** ADM-048: attempts, newest first. */
  invocations: async (query: InvocationQuery, paging: PageRequest, signal?: AbortSignal) =>
    (
      await apiRequest<InvocationPage>('/integration-invocations', {
        query: {
          integrationId: query.integrationId,
          status: joined(query.statuses),
          operationCode: query.operationCode,
          correlationId: query.correlationId,
          idempotencyKey: query.idempotencyKey,
          sourceReference: query.sourceReference,
          startedFrom: query.startedFrom,
          startedTo: query.startedTo,
          page: paging.page,
          pageSize: paging.pageSize,
        },
        signal,
      })
    ).data,

  /** ADM-048: one attempt with its lineage and whether FG-05 would retry it. */
  invocation: async (invocationId: string, signal?: AbortSignal) =>
    (await apiRequest<InvocationDetail>(`/integration-invocations/${invocationId}`, { signal }))
      .data,

  /** The lineage's next attempt, now, under its own correlation id and key. */
  retryInvocation: async (invocationId: string) =>
    (
      await apiRequest<InvocationDetail>(`/integration-invocations/${invocationId}/retry`, {
        method: 'POST',
      })
    ).data,

  /** Starts a reconciliation: it compares and reports, and repairs nothing (TASK-075 D-8). */
  reconcile: async (integrationId: string) =>
    (
      await apiRequest<InvocationDetail>('/integration-reconciliations', {
        method: 'POST',
        body: { integrationId },
      })
    ).data,

  /** ADM-046/047: synchronisation and reconciliation runs, newest first. */
  syncRuns: async (
    query: { integrationId?: string | undefined },
    paging: PageRequest,
    signal?: AbortSignal,
  ) =>
    (
      await apiRequest<SyncRunPage>('/sync-runs', {
        query: { integrationId: query.integrationId, page: paging.page, pageSize: paging.pageSize },
        signal,
      })
    ).data,

  /** ADM-047: operational alerts, newest first. */
  alerts: async (query: OperationalAlertQuery, paging: PageRequest, signal?: AbortSignal) =>
    (
      await apiRequest<OperationalAlertPage>('/operational-alerts', {
        query: {
          integrationId: query.integrationId,
          status: joined(query.statuses),
          page: paging.page,
          pageSize: paging.pageSize,
        },
        signal,
      })
    ).data,

  /** Records who acknowledged and when; changes no health (FG-05 §10.2). */
  acknowledgeAlert: async (alertId: string) =>
    (
      await apiRequest<OperationalAlertView>(`/operational-alerts/${alertId}/acknowledge`, {
        method: 'POST',
      })
    ).data,

  /** RESOLVED or DISMISSED, final, with the operator's note (ADR-012). */
  closeAlert: async (alertId: string, outcome: 'resolve' | 'dismiss', note: NarrativeText) =>
    (
      await apiRequest<OperationalAlertView>(`/operational-alerts/${alertId}/${outcome}`, {
        method: 'POST',
        body: { note },
      })
    ).data,

  /** ADM-041/043: the identity settings in effect, each secret only as whether it is set. */
  identityIntegration: async (signal?: AbortSignal) =>
    (await apiRequest<IdentityIntegrationStatus>('/identity-integration', { signal })).data,

  /** MOD-100: tests the directory, SSO and Nafath connections with the configured values; stores nothing. */
  testIdentityConnections: async () =>
    (
      await apiRequest<IdentityIntegrationTestResult>('/identity-integration/test', {
        method: 'POST',
      })
    ).data,
};
