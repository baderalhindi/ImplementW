import { type BilingualLabel } from '@/features/identity-access/api/types.ts';
import { type OnPlanStatus } from '@/features/projects/api/types.ts';
import { apiRequest } from '@/shared/api/httpClient.ts';

import { type PortfolioFilters } from '../api/dashboardsApi.ts';
import { type InsightSectionCode } from '../insights/insightSections.ts';

// The Portfolio dashboard's designer (17 Nov 2026): the Portfolio Manager places visuals for everyone, with no approval. A visual is a fixed type,
// measures and a grouping — never a formula (BR-DSH-030) — on a twelve-column grid. The API calculates every figure over the same projects as the
// insights; a visual whose grouping the caller may not see comes back with `groups` null and is left out.

export type VisualType =
  'CARD' | 'BAR' | 'COLUMN' | 'PIE' | 'DONUT' | 'TABLE' | 'GAUGE' | 'SECTION';

/** One of the dashboard's own sections a design places (18 Nov 2026): drawn as it always was, with its own figures and permission. */
export type Section =
  InsightSectionCode | 'PROJECT_LOCATIONS' | 'DASHBOARD_WIDGETS' | 'ENTITY_COMPLETION_TABLE';

export type Measure =
  | 'PROJECTS'
  | 'IN_PROGRESS'
  | 'VALUE_SAR'
  | 'AVERAGE_PLANNED'
  | 'AVERAGE_ACTUAL'
  | 'GAP_POINTS'
  | 'NEEDING_FOLLOW_UP'
  | 'OVERDUE_ACTIONS';

export type GroupBy =
  | 'NONE'
  | 'PORTFOLIO'
  | 'INITIATIVE'
  | 'SECTOR'
  | 'DEPARTMENT'
  | 'ENTITY'
  | 'STATUS'
  | 'STAGE'
  | 'ON_PLAN'
  | 'REGION'
  | 'CITY'
  | 'PRIORITY';

/** A visual as the designer keeps and sends it. Columns and rows count from 0, from the reading start. */
export interface Visual {
  title: BilingualLabel;
  visualType: VisualType;
  measures: Measure[];
  groupBy: GroupBy;
  layoutColumn: number;
  layoutRow: number;
  layoutWidth: number;
  layoutHeight: number;
  /** The section a SECTION places, with no measures and no grouping; absent for any other type. */
  section?: Section | null | undefined;
}

/** One group's figures, a value per measure in the visual's order; null when it cannot be calculated or may not be seen. */
export interface VisualGroup {
  key: string | null;
  label: BilingualLabel | null;
  projects: number;
  measured: number;
  valueEntered: number;
  values: (number | null)[];
  onPlan: OnPlanStatus | null;
}

export interface VisualResult extends Visual {
  groups: VisualGroup[] | null;
}

export interface DesignView {
  isStandard: boolean;
  updatedAt: string | null;
  visuals: VisualResult[];
}

export const designApi = {
  /** The saved design, or the standard one, with the caller's figures under the dashboard's filters. */
  get: async (filters: PortfolioFilters, signal?: AbortSignal) =>
    (await apiRequest<DesignView>('/portfolio-insights/design', { query: { ...filters }, signal }))
      .data,
  /** A design being edited, calculated but not saved. */
  preview: async (visuals: Visual[], signal?: AbortSignal) =>
    (
      await apiRequest<DesignView>('/portfolio-insights/design/preview', {
        method: 'POST',
        body: { visuals },
        signal,
      })
    ).data,
  /** Saves the design for everyone. */
  save: async (visuals: Visual[]) =>
    (
      await apiRequest<DesignView>('/portfolio-insights/design', {
        method: 'PUT',
        body: { visuals },
      })
    ).data,
  /** Back to the standard design. */
  reset: async () => {
    await apiRequest<undefined>('/portfolio-insights/design/reset', { method: 'POST', body: {} });
  },
};
