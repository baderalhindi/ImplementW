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

/** The portfolio filters, each narrowing the projects a dashboard counts (D-12; the 27 Oct 2026 review added the last three). */
export interface PortfolioFilters {
  departmentId?: string | undefined;
  externalEntityId?: string | undefined;
  regionItemId?: string | undefined;
  cityItemId?: string | undefined;
}

/** The context a dashboard is read in: PROJECT takes a project, the others optional portfolio filters. */
export interface DashboardContext extends PortfolioFilters {
  projectId?: string | undefined;
}

export const dashboardsApi = {
  /** The role-aware Home's catalogue: the dashboards the caller may open, the landing marked. At most three. */
  catalogue: (signal?: AbortSignal) =>
    readAllPages<DashboardCatalogueEntry>('/dashboards', {}, signal),
  /** The PUBLISHED composition and every widget's result, assembled now. */
  get: async (code: DashboardCode, context: DashboardContext, signal?: AbortSignal) =>
    (
      await apiRequest<DashboardView>(`/dashboards/${code}`, {
        query: {
          projectId: context.projectId,
          departmentId: context.departmentId,
          externalEntityId: context.externalEntityId,
          regionItemId: context.regionItemId,
          cityItemId: context.cityItemId,
        },
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
