import { Navigate, Outlet, type RouteObject } from 'react-router';

import { approvalRoutes } from '@/features/approvals/routes.tsx';
import { identityAccessRoutes } from '@/features/identity-access/routes.tsx';
import { RequireRole } from '@/features/identity-access/session/RequireRole.tsx';
import { RequireSession } from '@/features/identity-access/session/RequireSession.tsx';
import { SignInPage } from '@/features/identity-access/session/SignInPage.tsx';
import { SYSTEM_ADMINISTRATOR_ROLE } from '@/features/identity-access/session/useSession.ts';

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
        path: 'approvals',
        element: (
          <RequireSession>
            <Outlet />
          </RequireSession>
        ),
        children: approvalRoutes,
      },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
];
