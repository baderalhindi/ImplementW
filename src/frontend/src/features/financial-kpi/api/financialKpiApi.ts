import { type NarrativeTextRequest } from '@/features/projects/api/types.ts';
import { apiRequest } from '@/shared/api/httpClient.ts';
import { readAllPages } from '@/shared/api/paging.ts';

import {
  type FinancialCommitmentDetail,
  type FinancialPositionDetail,
  type FinancialProgressUpdateDetail,
  type FinancialProgressUpdateRequest,
  type FinancialSourceModeDetail,
  type KpiAssignmentDetail,
  type KpiDefinitionSummary,
  type KpiMeasurementCreateRequest,
  type KpiMeasurementDetail,
  type KpiMeasurementRequest,
  type KpiPortfolioAggregate,
  type KpiTargetVersionDetail,
  type PublishedFinancialSnapshotDetail,
} from './types.ts';

// WF-14 (TASK-052 financial-kpi.md §4). A collection of a project the caller may not see is an empty page. Every POST
// and PUT is a sensitive write with an Idempotency-Key, never retried here (TASK-052 F-17); the PUTs require If-Match
// and the commands honour it, so a change made meanwhile is 412, not overwritten.

function updateCommand(id: string, name: string, etag: string | null, body?: unknown) {
  return apiRequest<FinancialProgressUpdateDetail>(`/financial-progress-updates/${id}/${name}`, {
    method: 'POST',
    body: body ?? {},
    ifMatch: etag,
  });
}

function measurementCommand(id: string, name: string, etag: string | null) {
  return apiRequest<KpiMeasurementDetail>(`/kpi-measurements/${id}/${name}`, {
    method: 'POST',
    body: {},
    ifMatch: etag,
  });
}

export const financialKpiApi = {
  /** Every financial field's source mode, MANUAL where none is set (D-8). */
  sourceModes: (projectId: string, signal?: AbortSignal) =>
    readAllPages<FinancialSourceModeDetail>('/financial-source-modes', { projectId }, signal),
  /** Every Approved Budget version, newest first, each with its provenance. */
  commitments: (projectId: string, signal?: AbortSignal) =>
    readAllPages<FinancialCommitmentDetail>('/financial-commitments', { projectId }, signal),
  /** Every revision of every period, newest first. */
  updates: (projectId: string, signal?: AbortSignal) =>
    readAllPages<FinancialProgressUpdateDetail>(
      '/financial-progress-updates',
      { projectId },
      signal,
    ),
  /** One revision with the ETag its commands send back. */
  update: (id: string, signal?: AbortSignal) =>
    apiRequest<FinancialProgressUpdateDetail>(`/financial-progress-updates/${id}`, { signal }),
  /** MOD-023: the next period's DRAFT, every figure Unknown (MISSING, null): nothing is presumed. */
  startUpdate: (projectId: string) =>
    apiRequest<FinancialProgressUpdateDetail>('/financial-progress-updates', {
      method: 'POST',
      body: { projectId },
    }),
  /** MOD-023: a DRAFT's figures, as a whole. */
  saveUpdate: (id: string, request: FinancialProgressUpdateRequest, etag: string | null) =>
    apiRequest<FinancialProgressUpdateDetail>(`/financial-progress-updates/${id}`, {
      method: 'PUT',
      body: request,
      ifMatch: etag,
    }),
  submitUpdate: (id: string, etag: string | null) => updateCommand(id, 'submit', etag),
  /** AHDA's gate, never the submitter (D-15). */
  startReview: (id: string, etag: string | null) => updateCommand(id, 'start-review', etag),
  returnUpdate: (id: string, reason: NarrativeTextRequest, etag: string | null) =>
    updateCommand(id, 'return', etag, { reason }),
  /** Writes the period's immutable snapshot, rated under the thresholds in force (422 without them). */
  publishUpdate: (id: string, etag: string | null) => updateCommand(id, 'publish', etag),
  /** PUBLISHED/OFFICIAL, latest first. */
  snapshots: (projectId: string, signal?: AbortSignal) =>
    readAllPages<PublishedFinancialSnapshotDetail>(
      '/published-financial-snapshots',
      { projectId },
      signal,
    ),
  /** CURRENT/LIVE: one item, or none for a project the caller may not see. */
  positions: (projectId: string, signal?: AbortSignal) =>
    readAllPages<FinancialPositionDetail>('/financial-positions', { projectId }, signal),

  assignments: (projectId: string, signal?: AbortSignal) =>
    readAllPages<KpiAssignmentDetail>('/kpi-assignments', { projectId }, signal),
  assignment: (id: string, signal?: AbortSignal) =>
    apiRequest<KpiAssignmentDetail>(`/kpi-assignments/${id}`, { signal }),
  /** Every target version of an assignment, newest first. */
  targetVersions: (kpiAssignmentId: string, signal?: AbortSignal) =>
    readAllPages<KpiTargetVersionDetail>('/kpi-target-versions', { kpiAssignmentId }, signal),
  /** Every measurement of an assignment, latest period first, each with the target version it is pinned to. */
  measurements: (kpiAssignmentId: string, signal?: AbortSignal) =>
    readAllPages<KpiMeasurementDetail>('/kpi-measurements', { kpiAssignmentId }, signal),
  measurement: (id: string, signal?: AbortSignal) =>
    apiRequest<KpiMeasurementDetail>(`/kpi-measurements/${id}`, { signal }),
  /** MOD-024: a DRAFT pinned to the ACTIVE target version (422 KPI_TARGET_NOT_APPROVED without one). */
  recordMeasurement: (request: KpiMeasurementCreateRequest) =>
    apiRequest<KpiMeasurementDetail>('/kpi-measurements', { method: 'POST', body: request }),
  /** MOD-024: a DRAFT's value, re-rated against the version it is pinned to. */
  saveMeasurement: (id: string, request: KpiMeasurementRequest, etag: string | null) =>
    apiRequest<KpiMeasurementDetail>(`/kpi-measurements/${id}`, {
      method: 'PUT',
      body: request,
      ifMatch: etag,
    }),
  submitMeasurement: (id: string, etag: string | null) => measurementCommand(id, 'submit', etag),
  /** AHDA's gate, never the recorder (D-15). */
  publishMeasurement: (id: string, etag: string | null) => measurementCommand(id, 'publish', etag),
  /** The latest published value of one KPI across the projects named (1 to 200). */
  kpiAggregate: async (kpiDefinitionId: string, projectIds: string[], signal?: AbortSignal) =>
    (
      await apiRequest<KpiPortfolioAggregate>('/kpi-portfolio-aggregates', {
        query: { kpiDefinitionId: [kpiDefinitionId], projectId: projectIds },
        signal,
      })
    ).data,
  /** The KPI catalogue, to name a KPI and its direction (MASTER_DATA_VIEW, R01 only today). */
  definitions: (signal?: AbortSignal) =>
    readAllPages<KpiDefinitionSummary>('/kpi-definitions', {}, signal),
};
