import { type RouteObject } from 'react-router';

import { type NavigationItem } from '@/features/identity-access/routes.tsx';

import { ChangeRequestDetailPage } from './ChangeRequestDetailPage.tsx';
import { CreateChangeRequestPage, EditChangeRequestPage } from './ChangeRequestFormPage.tsx';
import { ChangeRequestRegisterPage } from './ChangeRequestRegisterPage.tsx';

/**
 * WF-08, mounted under /change-requests for any signed-in person. No role is checked here: what a person sees and may do
 * is the API's answer (CHANGE_REQUEST_VIEW, _RAISE, _REVIEW, _IMPLEMENT; WF-11's APPROVAL_DECIDE). A project's list
 * (SCR-105) is also its workspace tab, /projects/:projectId/change-requests.
 */
export const changeRequestRoutes: RouteObject[] = [
  { index: true, element: <ChangeRequestRegisterPage /> }, // SCR-105 across projects
  { path: 'new', element: <CreateChangeRequestPage /> }, // SCR-106, ?projectId=
  { path: ':changeRequestId', element: <ChangeRequestDetailPage /> }, // SCR-107, MOD-040–042, MOD-044
  { path: ':changeRequestId/edit', element: <EditChangeRequestPage /> }, // SCR-106 for a DRAFT or RETURNED request
];

export const changeRequestNavigation: NavigationItem[] = [
  { to: '/change-requests', label: 'changeRequests.nav.register', end: true },
];
