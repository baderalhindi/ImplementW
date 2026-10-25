import { type RouteObject } from 'react-router';

import { type NavigationItem } from '@/features/identity-access/routes.tsx';

import { DashboardsPage } from './DashboardsPage.tsx';
import { DASHBOARDS_PATH } from './paths.ts';

/**
 * FG-01's dashboards at /dashboards, the Home being the person's own work since the 24 Oct 2026 review (H2). DSH-009 is a
 * section of SCR-040's Overview tab (Blueprint §21.1, Step 16 "no separate dashboard route"). Which dashboards a person sees, and
 * every widget's data, is the API's answer.
 */
export const dashboardRoutes: RouteObject[] = [{ index: true, element: <DashboardsPage /> }];

/** A dashboard's widgets read business projections, so the dashboards are offered to a holder of PROJECT_VIEW. */
export const dashboardNavigation: NavigationItem[] = [
  {
    to: DASHBOARDS_PATH,
    label: 'common.menu.dashboards',
    end: true,
    permissions: ['PROJECT_VIEW'],
  },
];
