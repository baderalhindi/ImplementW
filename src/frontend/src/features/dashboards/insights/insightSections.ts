import { useCallback } from 'react';

import { useCan } from '@/features/identity-access/permissions/permissions.ts';
import { useSession } from '@/features/identity-access/session/useSession.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';

import { dashboardsApi, type PortfolioFilters } from '../api/dashboardsApi.ts';
import { type PortfolioInsights } from '../api/insightTypes.ts';

// What the Portfolio Dashboard reads of the insights, and which of their sections there is anything to draw (18 Nov 2026: the whole dashboard
// is designed, so the design's grid and the Executive's dashboard share these).

/** How many projects the insights count, or null when no section the caller may see counts them (the empty-state check). */
export function projectCountOf(insights: PortfolioInsights): number | null {
  return (
    insights.headline?.registeredProjects ??
    insights.byPortfolio?.total.projects ??
    insights.bySector?.total.projects ??
    insights.stages?.reduce((sum, stage) => sum + stage.projects, 0) ??
    null
  );
}

/** The insights' own sections a design places (18 Nov 2026: the whole dashboard is designed). */
export type InsightSectionCode =
  | 'PORTFOLIO_TABLE'
  | 'INITIATIVE_TABLE'
  | 'SECTOR_TABLE'
  | 'STATUS_TABLE'
  | 'CURRENT_STAGE_TABLE'
  | 'ON_PLAN_COUNTS'
  | 'FOLLOW_UP_REASONS'
  | 'TOP_FOLLOW_UP'
  | 'ENTITY_COMPLETION';

/** Whether a section has anything for the caller: the API answers a section it may not see null, and an empty one is not drawn, as before. */
export function insightBlockShown(
  section: InsightSectionCode,
  insights: PortfolioInsights,
  showsCompletion: boolean,
): boolean {
  switch (section) {
    case 'PORTFOLIO_TABLE':
      return (insights.byPortfolio?.rows.length ?? 0) > 0;
    case 'INITIATIVE_TABLE':
      return (insights.byInitiative?.rows.length ?? 0) > 0;
    case 'SECTOR_TABLE':
      return (insights.bySector?.rows.length ?? 0) > 0;
    case 'STATUS_TABLE':
      return (insights.stages?.length ?? 0) > 0;
    case 'CURRENT_STAGE_TABLE':
      return (insights.byCurrentStage ?? []).some((stage) => stage.projects > 0);
    case 'ON_PLAN_COUNTS':
      return insights.onPlan !== null;
    case 'FOLLOW_UP_REASONS':
      return insights.followUpReasons !== null;
    case 'TOP_FOLLOW_UP':
      return (insights.topFollowUp?.length ?? 0) > 0;
    case 'ENTITY_COMPLETION':
      return showsCompletion;
  }
}

/** Whether the caller sees the entity completion donut: AHDA's people holding EXTERNAL_REQUEST_VIEW. */
export function useShowsCompletion(): boolean {
  const { session } = useSession();
  return useCan('EXTERNAL_REQUEST_VIEW') && session?.user.userType !== 'EXTERNAL';
}

/** The insights the dashboard reads under its filters, once for every section and card it draws. */
export function usePortfolioInsights(filters: PortfolioFilters) {
  const {
    departmentId,
    externalEntityId,
    classificationItemId,
    regionItemId,
    cityItemId,
    status,
    priorityItemId,
    strategicPortfolioItemId,
    strategicInitiativeItemId,
    period,
  } = filters;
  return useApiResource(
    useCallback(
      (signal: AbortSignal) =>
        dashboardsApi.insights(
          {
            departmentId,
            externalEntityId,
            classificationItemId,
            regionItemId,
            cityItemId,
            status,
            priorityItemId,
            strategicPortfolioItemId,
            strategicInitiativeItemId,
            period,
          },
          signal,
        ),
      [
        departmentId,
        externalEntityId,
        classificationItemId,
        regionItemId,
        cityItemId,
        status,
        priorityItemId,
        strategicPortfolioItemId,
        strategicInitiativeItemId,
        period,
      ],
    ),
    { keepWhileReloading: true },
  );
}
