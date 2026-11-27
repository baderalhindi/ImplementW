import { type RouteObject } from 'react-router';

import { type NavigationItem } from '@/features/identity-access/routes.tsx';
import { RequirePermission } from '@/features/identity-access/session/RequirePermission.tsx';

import { ChangeRequestDetailPage } from './ChangeRequestDetailPage.tsx';
import { CreateChangeRequestPage, EditChangeRequestPage } from './ChangeRequestFormPage.tsx';
import { ChangeRequestRegisterPage } from './ChangeRequestRegisterPage.tsx';

/**
 * WF-08, mounted under /change-requests for a holder of CHANGE_REQUEST_VIEW (QA INT-05); the form for CHANGE_REQUEST_RAISE. What a
 * person sees and may do is the API's answer (CHANGE_REQUEST_VIEW, _RAISE, _REVIEW, _IMPLEMENT; WF-11's APPROVAL_DECIDE). A project's list
 * (SCR-105) is also its workspace tab, /projects/:projectId/change-requests.
 */
export const changeRequestRoutes: RouteObject[] = [
  { index: true, element: <ChangeRequestRegisterPage /> }, // SCR-105 across projects
  {
    path: 'new', // SCR-106, ?projectId=
    element: (
      <RequirePermission codes={['CHANGE_REQUEST_RAISE']}>
        <CreateChangeRequestPage />
      </RequirePermission>
    ),
  },
  { path: ':changeRequestId', element: <ChangeRequestDetailPage /> }, // SCR-107, MOD-040–042, MOD-044
  {
    path: ':changeRequestId/edit', // SCR-106 for a DRAFT or RETURNED request
    element: (
      <RequirePermission codes={['CHANGE_REQUEST_RAISE']}>
        <EditChangeRequestPage />
      </RequirePermission>
    ),
  },
];

export const changeRequestNavigation: NavigationItem[] = [
  {
    to: '/change-requests',
    label: 'changeRequests.nav.register',
    end: true,
    permissions: ['CHANGE_REQUEST_VIEW'],
  },
];
