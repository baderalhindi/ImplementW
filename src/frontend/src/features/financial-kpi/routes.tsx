import { type RouteObject } from 'react-router';

import { type NavigationItem } from '@/features/identity-access/routes.tsx';

import { KpiRegisterPage } from './KpiRegisterPage.tsx';

/**
 * WF-14's register, mounted under /kpis for any signed-in person. No role is checked here: which KPIs a person sees is
 * the API's answer (KPI_VIEW; TASK-052 D-10). SCR-049, SCR-071, SCR-050 and SCR-073 are the workspace's Financials
 * and KPIs tabs (projects/routes).
 */
export const kpiRoutes: RouteObject[] = [
  { index: true, element: <KpiRegisterPage /> }, // SCR-072
];

export const kpiNavigation: NavigationItem[] = [
  { to: '/kpis', label: 'financialKpi.nav.register', end: true },
];
