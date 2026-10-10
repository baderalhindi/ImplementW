import { type NavigationItem } from '@/features/identity-access/routes.tsx';

/**
 * FG-01 has no route of its own. Its dashboards are hosted by the Home (`/`, HomeDashboardPage), which the
 * application routes mount for any signed-in person; DSH-009 is a section of SCR-040's Overview tab (Blueprint §21.1,
 * Step 16 "no separate dashboard route"). Which dashboards a person sees, and every widget's data, is the API's answer.
 */
export const dashboardNavigation: NavigationItem[] = [
  { to: '/', label: 'dashboards.nav.home', end: true },
];
