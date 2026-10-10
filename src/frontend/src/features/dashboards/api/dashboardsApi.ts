import { apiRequest } from '@/shared/api/httpClient.ts';
import { readAllPages } from '@/shared/api/paging.ts';

import {
  type DashboardCatalogueEntry,
  type DashboardCode,
  type DashboardPersonalizationDetail,
  type DashboardView,
  type WidgetPersonalization,
} from './types.ts';

// FG-01 at runtime (TASK-069 dashboards.md §5). A dashboard outside the caller's audience, or a Project Dashboard no
// widget reaches, is 404 (R-47). The personalisation commands are sensitive writes with an Idempotency-Key, never
// retried here; they are offered for the Portfolio Dashboard only (ADR-019).

/** The context a dashboard is read in: PROJECT takes a project, the others an optional department (D-12). */
export interface DashboardContext {
  projectId?: string | undefined;
  departmentId?: string | undefined;
}

export const dashboardsApi = {
  /** The role-aware Home's catalogue: the dashboards the caller may open, the landing marked. At most three. */
  catalogue: (signal?: AbortSignal) =>
    readAllPages<DashboardCatalogueEntry>('/dashboards', {}, signal),
  /** The PUBLISHED composition and every widget's result, assembled now. */
  get: async (code: DashboardCode, context: DashboardContext, signal?: AbortSignal) =>
    (
      await apiRequest<DashboardView>(`/dashboards/${code}`, {
        query: { projectId: context.projectId, departmentId: context.departmentId },
        signal,
      })
    ).data,
  /** ADR-019: the caller's choices for the optional widgets, as a whole. */
  personalize: async (code: DashboardCode, widgets: WidgetPersonalization[]) =>
    (
      await apiRequest<DashboardPersonalizationDetail>(`/dashboards/${code}/personalize`, {
        method: 'POST',
        body: { widgets },
      })
    ).data,
  /** Back to the governed layout. */
  resetPersonalization: async (code: DashboardCode) =>
    (
      await apiRequest<DashboardPersonalizationDetail>(
        `/dashboards/${code}/reset-personalization`,
        { method: 'POST', body: {} },
      )
    ).data,
};
