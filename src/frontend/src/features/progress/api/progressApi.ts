import { type NarrativeTextRequest, type Page } from '@/features/projects/api/types.ts';
import { apiRequest, type ApiResponse } from '@/shared/api/httpClient.ts';

import {
  type ProgressSubmissionDetail,
  type ProgressSubmissionRequest,
  type ProjectHealthStatusDetail,
  type PublishedProgressSnapshotDetail,
  type ReportingCycleSummary,
} from './types.ts';

// WF-02 (TASK-044 progress-update.md §4). Every collection is one project's, `projectId` required; a project the
// caller may not see answers an empty page. Every POST and PUT is a sensitive write with an Idempotency-Key, and a
// command sends the ETag it was shown, so a change made meanwhile is 412, not overwritten.

/** R-29's largest page: a project's periods are read in one page to name each revision's period. */
export const CYCLE_PAGE_SIZE = 200;

async function data<T>(request: Promise<ApiResponse<T>>): Promise<T> {
  return (await request).data;
}

interface PageQuery {
  page?: number | undefined;
  pageSize?: number | undefined;
}

function collection<T>(path: string, projectId: string, query: PageQuery, signal?: AbortSignal) {
  return data(apiRequest<Page<T>>(path, { query: { projectId, ...query }, signal }));
}

function command(id: string, name: string, etag: string | null, body?: unknown) {
  return apiRequest<ProgressSubmissionDetail>(`/progress-submissions/${id}/${name}`, {
    method: 'POST',
    body: body ?? {},
    ifMatch: etag,
  });
}

export const progressApi = {
  /** The project's reporting periods, earliest first. Reading generates none. */
  cycles: (projectId: string, query: PageQuery, signal?: AbortSignal) =>
    collection<ReportingCycleSummary>('/reporting-cycles', projectId, query, signal),
  /** SCR-070: every revision of every period, newest first. */
  submissions: (projectId: string, query: PageQuery, signal?: AbortSignal) =>
    collection<ProgressSubmissionDetail>('/progress-submissions', projectId, query, signal),
  get: (id: string, signal?: AbortSignal) =>
    apiRequest<ProgressSubmissionDetail>(`/progress-submissions/${id}`, { signal }),
  /** A DRAFT for the earliest open period that has begun, with the derived figures, pre-filled (ADR-017). */
  start: (projectId: string) =>
    apiRequest<ProgressSubmissionDetail>('/progress-submissions', {
      method: 'POST',
      body: { projectId },
    }),
  /** MOD-021: the narrative and the override of a DRAFT, as a whole. If-Match is required. */
  update: (id: string, request: ProgressSubmissionRequest, etag: string | null) =>
    apiRequest<ProgressSubmissionDetail>(`/progress-submissions/${id}`, {
      method: 'PUT',
      body: request,
      ifMatch: etag,
    }),
  /** MOD-020: DRAFT → SUBMITTED; the figures are derived again and fixed. */
  submit: (id: string, etag: string | null) => command(id, 'submit', etag),
  /** MOD-022: SUBMITTED → UNDER_REVIEW, by AHDA, never the submitter. */
  startReview: (id: string, etag: string | null) => command(id, 'start-review', etag),
  /** MOD-022: UNDER_REVIEW → RETURNED; the period continues as the next revision, a DRAFT. */
  returnForRevision: (id: string, reason: NarrativeTextRequest, etag: string | null) =>
    command(id, 'return', etag, { reason }),
  /** MOD-022: UNDER_REVIEW → PUBLISHED; the period's immutable snapshot is written and the period closed. */
  publish: (id: string, etag: string | null) => command(id, 'publish', etag),
  /** PUBLISHED/OFFICIAL snapshots, latest first. */
  snapshots: (projectId: string, query: PageQuery, signal?: AbortSignal) =>
    collection<PublishedProgressSnapshotDetail>(
      '/published-progress-snapshots',
      projectId,
      query,
      signal,
    ),
  /** CURRENT/LIVE health: one item once computed, none before. */
  healthStatuses: (projectId: string, signal?: AbortSignal) =>
    collection<ProjectHealthStatusDetail>('/project-health-statuses', projectId, {}, signal),
};
