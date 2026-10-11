import { apiDownload, apiRequest } from '@/shared/api/httpClient.ts';
import { MAX_PAGE_SIZE, readAllPages } from '@/shared/api/paging.ts';

import {
  type ExplorerRunRequest,
  type ExportOptions,
  type Paged,
  type ReportAllowlist,
  type ReportAllowlistEntry,
  type ReportCatalogueEntry,
  type ReportCode,
  type ReportJobDetail,
  type ReportJobSummary,
  type ReportResultPage,
  type ReportRunRequest,
  type ReportView,
  type SavedViewDetail,
  type SavedViewRequest,
  type SavedViewSummary,
} from './types.ts';

// FG-02 at runtime (TASK-071 reports.md §5). A report outside the caller's audience, another person's job or output,
// and a saved view neither owned nor shared with the caller are all 404 (R-47). `run` is a POST that stores nothing
// (R-35). An export answers 202 with its job, which is polled until it ends and downloaded through `content`, where it
// is authorised again (D-7). The SPA never builds a query: it sends field codes the API offered and nothing else.

export interface ResultPaging {
  page: number;
  pageSize: number;
}

export const reportsApi = {
  /** SCR-130's catalogue: the PUBLISHED reports the caller's roles may run. At most ten (ADR-006). */
  catalogue: (signal?: AbortSignal) => readAllPages<ReportCatalogueEntry>('/reports', {}, signal),
  /** The report as the caller may run it: columns, parameters with their authorised options, formats, may export/save. */
  get: async (code: ReportCode, signal?: AbortSignal) =>
    (await apiRequest<ReportView>(`/reports/${code}`, { signal })).data,
  run: async (
    code: ReportCode,
    request: ReportRunRequest,
    paging: ResultPaging,
    signal?: AbortSignal,
  ) =>
    (
      await apiRequest<ReportResultPage>(`/reports/${code}/run`, {
        method: 'POST',
        query: { page: paging.page, pageSize: paging.pageSize },
        body: request,
        signal,
      })
    ).data,
  /** MOD-060: one job per request; the Idempotency-Key the client sends makes a retried click the same job. */
  export: async (code: ReportCode, request: ReportRunRequest, options: ExportOptions) =>
    (
      await apiRequest<ReportJobDetail>(`/reports/${code}/export`, {
        method: 'POST',
        body: { ...request, ...options },
      })
    ).data,

  /** SCR-138: the PUBLISHED REPORT_RULES allowlist, whole. 422 CONFIGURATION_MISSING while none is published. */
  allowlist: async (signal?: AbortSignal): Promise<ReportAllowlist> => {
    const items: ReportAllowlistEntry[] = [];
    for (let page = 1; ; page += 1) {
      const { data } = await apiRequest<Paged<ReportAllowlistEntry> & ReportAllowlist>(
        '/report-allowlist-entries',
        { query: { page, pageSize: MAX_PAGE_SIZE }, signal },
      );
      items.push(...data.items);
      if (data.items.length === 0 || items.length >= data.totalCount) {
        return { items, configurationVersionNo: data.configurationVersionNo };
      }
    }
  },
  runExplorer: async (request: ExplorerRunRequest, paging: ResultPaging, signal?: AbortSignal) =>
    (
      await apiRequest<ReportResultPage>('/report-explorer/run', {
        method: 'POST',
        query: { page: paging.page, pageSize: paging.pageSize },
        body: request,
        signal,
      })
    ).data,
  exportExplorer: async (request: ExplorerRunRequest, options: ExportOptions) =>
    (
      await apiRequest<ReportJobDetail>('/report-explorer/export', {
        method: 'POST',
        body: { ...request, ...options },
      })
    ).data,

  /** SCR-140: the caller's own jobs, newest first. */
  jobs: async (page: number, pageSize: number, signal?: AbortSignal) =>
    (
      await apiRequest<Paged<ReportJobSummary>>('/report-jobs', {
        query: { page, pageSize },
        signal,
      })
    ).data,
  job: async (jobId: string, signal?: AbortSignal) =>
    (await apiRequest<ReportJobDetail>(`/report-jobs/${jobId}`, { signal })).data,
  cancelJob: async (jobId: string) =>
    (
      await apiRequest<ReportJobDetail>(`/report-jobs/${jobId}/cancel`, {
        method: 'POST',
        body: {},
      })
    ).data,
  /** The file, authorised again now: 403 once the requester may no longer see what it holds. */
  download: (jobId: string) => apiDownload(`/report-jobs/${jobId}/content`),

  /** SCR-139: the caller's own views and those shared with their roles, most recently changed first. */
  savedViews: async (page: number, pageSize: number, signal?: AbortSignal) =>
    (
      await apiRequest<Paged<SavedViewSummary>>('/saved-views', {
        query: { page, pageSize },
        signal,
      })
    ).data,
  savedView: (savedViewId: string, signal?: AbortSignal) =>
    apiRequest<SavedViewDetail>(`/saved-views/${savedViewId}`, { signal }),
  createSavedView: (request: SavedViewRequest) =>
    apiRequest<SavedViewDetail>('/saved-views', { method: 'POST', body: request }),
  /** The view, whole, its sharing with it: its owner's alone, under its ETag (R-21). */
  updateSavedView: (savedViewId: string, request: SavedViewRequest, etag: string | null) =>
    apiRequest<SavedViewDetail>(`/saved-views/${savedViewId}`, {
      method: 'PUT',
      body: request,
      ifMatch: etag,
    }),
  deleteSavedView: async (savedViewId: string) => {
    await apiRequest<undefined>(`/saved-views/${savedViewId}`, { method: 'DELETE' });
  },
};
