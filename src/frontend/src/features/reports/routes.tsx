import { type RouteObject } from 'react-router';

import { type NavigationItem } from '@/features/identity-access/routes.tsx';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';

import { isInternal } from '@/features/projects/access.ts';
import { ExplorerPage } from './ExplorerPage.tsx';
import { ExportHistoryPage } from './ExportHistoryPage.tsx';
import { ReportFamilyPage } from './ReportFamilyPage.tsx';
import { ReportPage } from './ReportPage.tsx';
import { ReportsCenterPage } from './ReportsCenterPage.tsx';
import { SavedViewsPage } from './SavedViewsPage.tsx';

/**
 * FG-02's screens under /reports (Step 16 proposes /views/scr-13n/…; the application's routes are named for what
 * they show, as every earlier feature's are). MOD-060 to MOD-062 are dialogs of the report shell, the explorer and the
 * saved views list, with no route of their own (Step 16: "authorized host binding required").
 */
export const reportRoutes: RouteObject[] = [
  { index: true, element: <ReportsCenterPage /> }, // SCR-130
  { path: 'explorer', element: <ExplorerPage /> }, // SCR-138, MOD-060, MOD-061, MOD-062
  { path: 'saved-views', element: <SavedViewsPage /> }, // SCR-139, MOD-062
  { path: 'exports', element: <ExportHistoryPage /> }, // SCR-140
  { path: ':family', element: <ReportFamilyPage /> }, // SCR-131 to SCR-137
  { path: ':family/:reportCode', element: <ReportPage /> }, // SCR-131 to SCR-137, MOD-060, MOD-061, MOD-062
];

/**
 * The Reports group (navigation, not protection): the centre for AHDA's business roles — a report's rows are projects, so a
 * holder of PROJECT_VIEW — and none for an external user (ADR-019: no reports); the explorer and saved views for ADR-019's
 * composers; the exports for those who may export.
 */
export function reportNavigation(user: SessionUser): NavigationItem[] {
  return isInternal(user)
    ? [
        { to: '/reports', label: 'reports.nav.center', end: true, permissions: ['PROJECT_VIEW'] },
        { to: '/reports/explorer', label: 'reports.nav.explorer', permissions: ['REPORT_COMPOSE'] },
        {
          to: '/reports/saved-views',
          label: 'reports.nav.savedViews',
          permissions: ['REPORT_COMPOSE'],
        },
        { to: '/reports/exports', label: 'reports.nav.exports', permissions: ['REPORT_EXPORT'] },
      ]
    : [];
}
