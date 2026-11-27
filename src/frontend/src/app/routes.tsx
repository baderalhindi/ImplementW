import { Outlet, type RouteObject } from 'react-router';

import { approvalRoutes } from '@/features/approvals/routes.tsx';
import { auditActivityRoutes } from '@/features/audit-activity/routes.tsx';
import { changeRequestRoutes } from '@/features/change-requests/routes.tsx';
import { dataImportRoutes } from '@/features/data-import/routes.tsx';
import { documentRoutes } from '@/features/documents/routes.tsx';
import {
  externalContributionRoutes,
  externalRequestRoutes,
  myParticipationRoutes,
} from '@/features/external-participation/routes.tsx';
import { dashboardRoutes } from '@/features/dashboards/routes.tsx';
import { HomePage } from '@/features/home/HomePage.tsx';
import { kpiRoutes } from '@/features/financial-kpi/routes.tsx';
import { identityAccessRoutes } from '@/features/identity-access/routes.tsx';
import { integrationAdminRoutes } from '@/features/integration-admin/routes.tsx';
import { issuesChallengesRoutes } from '@/features/issues-challenges/routes.tsx';
import { milestoneRoutes } from '@/features/milestones/routes.tsx';
import { type PermissionCode } from '@/features/identity-access/permissions/permissionCodes.ts';
import { RequirePermission } from '@/features/identity-access/session/RequirePermission.tsx';
import { RequireSession } from '@/features/identity-access/session/RequireSession.tsx';
import { SignInPage } from '@/features/identity-access/session/SignInPage.tsx';
import { SsoCallbackPage } from '@/features/identity-access/session/SsoCallbackPage.tsx';
import { ChangePasswordPage } from '@/features/identity-access/session/ChangePasswordPage.tsx';
import { SignInMethodsPage } from '@/features/identity-access/session/SignInMethodsPage.tsx';
import {
  ForgotPasswordPage,
  PasswordUpdatedPage,
  ResetPasswordPage,
} from '@/features/identity-access/session/PasswordResetPages.tsx';
import { notificationRoutes } from '@/features/notifications/routes.tsx';
import { projectRoutes } from '@/features/projects/routes.tsx';
import { reportRoutes } from '@/features/reports/routes.tsx';
import { riskRoutes } from '@/features/risks/routes.tsx';
import { closeoutRoutes, suspensionRoutes } from '@/features/suspension-closure/routes.tsx';
import { taskRoutes } from '@/features/tasks/routes.tsx';
import { RouteErrorPage } from '@/shared/observability/RouteErrorPage.tsx';

import { AppLayout } from './AppLayout.tsx';
import { helpRoutes } from './help/routes.tsx';
import { administrationNavigation } from './menus.ts';
import { MenuOverview } from './MenuOverview.tsx';
import { AdministrationIndex, NotFoundPage } from './pages.tsx';

/** What opens /admin: any permission of a screen the Administration menu offers there. */
const INTEGRATION_PERMISSIONS: readonly PermissionCode[] = [
  'INTEGRATION_VIEW',
  'INTEGRATION_LOG_VIEW',
  'INTEGRATION_OPERATE',
  'INTEGRATION_ALERT_MANAGE',
  'INTEGRATION_CONFIGURE',
];

const ADMINISTRATION_PERMISSIONS: readonly PermissionCode[] = administrationNavigation.flatMap(
  (item) => item.permissions ?? [],
);

export const appRoutes: RouteObject[] = [
  {
    element: <AppLayout />,
    // TASK-090: a screen that throws is reported to the APM and replaced by an explanation.
    errorElement: <RouteErrorPage />,
    children: [
      { index: true, element: <HomePage menuOverview={<MenuOverview />} /> }, // the person's own work, or platform health for the administrator (review H2, M-1)
      {
        path: 'dashboards', // DSH-001–008, DSH-010–012
        element: (
          <RequireSession>
            <Outlet />
          </RequireSession>
        ),
        children: dashboardRoutes,
      },
      { path: 'sign-in', element: <SignInPage /> },
      { path: 'auth/callback', element: <SsoCallbackPage /> }, // SSO_OIDC_CALLBACK_URL
      { path: 'forgot-password', element: <ForgotPasswordPage /> },
      { path: 'reset-password', element: <ResetPasswordPage /> },
      { path: 'password-updated', element: <PasswordUpdatedPage /> },
      {
        path: 'account/sign-in-methods',
        element: (
          <RequireSession>
            <SignInMethodsPage />
          </RequireSession>
        ),
      },
      {
        path: 'account/password',
        element: (
          <RequireSession>
            <ChangePasswordPage />
          </RequireSession>
        ),
      },
      {
        path: 'admin',
        children: [
          {
            element: (
              <RequirePermission codes={ADMINISTRATION_PERMISSIONS}>
                <Outlet />
              </RequirePermission>
            ),
            children: [
              { index: true, element: <AdministrationIndex /> },
              ...identityAccessRoutes,
              {
                // FG-05 (TASK-076): another administration permission does not open these.
                element: (
                  <RequirePermission codes={INTEGRATION_PERMISSIONS}>
                    <Outlet />
                  </RequirePermission>
                ),
                children: integrationAdminRoutes,
              },
              ...dataImportRoutes, // TASK-102
            ],
          },
          // FG-06's screens open on audit permissions, not on this guard (TASK-074; each guards itself).
          ...auditActivityRoutes,
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
        path: 'tasks', // every list reads under TASK_VIEW (QA D1-04)
        element: (
          <RequirePermission codes={['TASK_VIEW']}>
            <Outlet />
          </RequirePermission>
        ),
        children: taskRoutes,
      },
      {
        path: 'milestones', // the register reads achievements under MILESTONE_VIEW (QA INT-05)
        element: (
          <RequirePermission codes={['MILESTONE_VIEW']}>
            <Outlet />
          </RequirePermission>
        ),
        children: milestoneRoutes,
      },
      {
        path: 'kpis', // the register reads under KPI_VIEW (QA INT-05)
        element: (
          <RequirePermission codes={['KPI_VIEW']}>
            <Outlet />
          </RequirePermission>
        ),
        children: kpiRoutes,
      },
      {
        path: 'risks', // both registers read under RISK_VIEW (QA D1-04)
        element: (
          <RequirePermission codes={['RISK_VIEW']}>
            <Outlet />
          </RequirePermission>
        ),
        children: riskRoutes,
      },
      {
        path: 'issues-challenges', // the registers and escalations read under CONCERN_VIEW (QA INT-05)
        element: (
          <RequirePermission codes={['CONCERN_VIEW']}>
            <Outlet />
          </RequirePermission>
        ),
        children: issuesChallengesRoutes,
      },
      {
        path: 'change-requests', // every WF-08 screen reads under CHANGE_REQUEST_VIEW (QA INT-05)
        element: (
          <RequirePermission codes={['CHANGE_REQUEST_VIEW']}>
            <Outlet />
          </RequirePermission>
        ),
        children: changeRequestRoutes,
      },
      {
        path: 'suspension-requests', // every WF-09 screen reads under SUSPENSION_VIEW (QA INT-05)
        element: (
          <RequirePermission codes={['SUSPENSION_VIEW']}>
            <Outlet />
          </RequirePermission>
        ),
        children: suspensionRoutes,
      },
      {
        path: 'closure-requests', // every WF-10 screen reads under CLOSEOUT_VIEW (QA INT-05)
        element: (
          <RequirePermission codes={['CLOSEOUT_VIEW']}>
            <Outlet />
          </RequirePermission>
        ),
        children: closeoutRoutes,
      },
      {
        path: 'external-requests',
        element: (
          <RequireSession>
            <Outlet />
          </RequireSession>
        ),
        children: externalRequestRoutes,
      },
      {
        path: 'external-contributions',
        element: (
          <RequireSession>
            <Outlet />
          </RequireSession>
        ),
        children: externalContributionRoutes,
      },
      {
        element: (
          <RequireSession>
            <Outlet />
          </RequireSession>
        ),
        children: myParticipationRoutes,
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
        path: 'documents', // every WF-12 screen reads under DOCUMENT_VIEW (QA D2-03)
        element: (
          <RequirePermission codes={['DOCUMENT_VIEW']}>
            <Outlet />
          </RequirePermission>
        ),
        children: documentRoutes,
      },
      {
        path: 'reports',
        element: (
          <RequireSession>
            <Outlet />
          </RequireSession>
        ),
        children: reportRoutes,
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
      {
        path: 'help',
        element: (
          <RequireSession>
            <Outlet />
          </RequireSession>
        ),
        children: helpRoutes,
      },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
];
