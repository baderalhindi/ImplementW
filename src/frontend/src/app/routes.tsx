import { Navigate, Outlet, type RouteObject } from 'react-router';

import { approvalRoutes } from '@/features/approvals/routes.tsx';
import { documentRoutes } from '@/features/documents/routes.tsx';
import { kpiRoutes } from '@/features/financial-kpi/routes.tsx';
import { identityAccessRoutes } from '@/features/identity-access/routes.tsx';
import { milestoneRoutes } from '@/features/milestones/routes.tsx';
import { RequireRole } from '@/features/identity-access/session/RequireRole.tsx';
import { RequireSession } from '@/features/identity-access/session/RequireSession.tsx';
import { SignInPage } from '@/features/identity-access/session/SignInPage.tsx';
import { SYSTEM_ADMINISTRATOR_ROLE } from '@/features/identity-access/session/useSession.ts';
import { notificationRoutes } from '@/features/notifications/routes.tsx';
import { projectRoutes } from '@/features/projects/routes.tsx';
import { riskRoutes } from '@/features/risks/routes.tsx';
import { taskRoutes } from '@/features/tasks/routes.tsx';

import { AppLayout } from './AppLayout.tsx';
import { HomePage, NotFoundPage } from './pages.tsx';

export const appRoutes: RouteObject[] = [
  {
    element: <AppLayout />,
    children: [
      { index: true, element: <HomePage /> },
      { path: 'sign-in', element: <SignInPage /> },
      {
        path: 'admin',
        element: (
          <RequireRole roleCode={SYSTEM_ADMINISTRATOR_ROLE}>
            <Outlet />
          </RequireRole>
        ),
        children: [
          { index: true, element: <Navigate to="users" replace /> },
          ...identityAccessRoutes,
        ],
      },
      {
        path: 'projects',
        element: (
          <RequireSession>
            <Outlet />
          </RequireSession>
        ),
        children: projectRoutes,
      },
      {
        path: 'tasks',
        element: (
          <RequireSession>
            <Outlet />
          </RequireSession>
        ),
        children: taskRoutes,
      },
      {
        path: 'milestones',
        element: (
          <RequireSession>
            <Outlet />
          </RequireSession>
        ),
        children: milestoneRoutes,
      },
      {
        path: 'kpis',
        element: (
          <RequireSession>
            <Outlet />
          </RequireSession>
        ),
        children: kpiRoutes,
      },
      {
        path: 'risks',
        element: (
          <RequireSession>
            <Outlet />
          </RequireSession>
        ),
        children: riskRoutes,
      },
      {
        path: 'approvals',
        element: (
          <RequireSession>
            <Outlet />
          </RequireSession>
        ),
        children: approvalRoutes,
      },
      {
        path: 'documents',
        element: (
          <RequireSession>
            <Outlet />
          </RequireSession>
        ),
        children: documentRoutes,
      },
      {
        path: 'notifications',
        element: (
          <RequireSession>
            <Outlet />
          </RequireSession>
        ),
        children: notificationRoutes,
      },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
];
