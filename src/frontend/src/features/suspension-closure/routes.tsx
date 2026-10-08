import { type RouteObject } from 'react-router';

import { type NavigationItem } from '@/features/identity-access/routes.tsx';

import { CloseoutDetailPage } from './CloseoutDetailPage.tsx';
import { CreateCloseoutCasePage, EditCloseoutCasePage } from './CloseoutFormPage.tsx';
import { CloseoutRegisterPage } from './CloseoutRegisterPage.tsx';
import { SuspensionDetailPage } from './SuspensionDetailPage.tsx';
import { CreateSuspensionRequestPage, EditSuspensionRequestPage } from './SuspensionFormPage.tsx';
import { SuspensionRegisterPage } from './SuspensionRegisterPage.tsx';

/**
 * WF-09, mounted under /suspension-requests for any signed-in person. No role is checked here: what a person sees and
 * may do is the API's answer (SUSPENSION_VIEW, _RAISE, _REVIEW, _ACTIVATE; WF-11's APPROVAL_DECIDE). A project's
 * register (SCR-108) is also its workspace tab, /projects/:projectId/suspension.
 */
export const suspensionRoutes: RouteObject[] = [
  { index: true, element: <SuspensionRegisterPage /> }, // SCR-108 across projects
  { path: 'new', element: <CreateSuspensionRequestPage /> }, // SCR-109, ?projectId=&type=
  { path: ':suspensionRequestId', element: <SuspensionDetailPage /> }, // SCR-110, MOD-040–042, MOD-044
  { path: ':suspensionRequestId/edit', element: <EditSuspensionRequestPage /> }, // SCR-109 for DRAFT/RETURNED
];

/**
 * WF-10, mounted under /closure-requests: the canonical Closure screen family reused for both stages of the closeout
 * (WF-10 §14), each case addressed by its stage. A project's register (SCR-111) is also its workspace tab,
 * /projects/:projectId/closeout.
 */
export const closeoutRoutes: RouteObject[] = [
  { index: true, element: <CloseoutRegisterPage /> }, // SCR-111 across projects
  { path: 'new', element: <CreateCloseoutCasePage /> }, // SCR-112, ?projectId=&stage=
  { path: ':stage/:caseId', element: <CloseoutDetailPage /> }, // SCR-113, MOD-040–042, MOD-044
  { path: ':stage/:caseId/edit', element: <EditCloseoutCasePage /> }, // SCR-112 for DRAFT/RETURNED
];

export const suspensionClosureNavigation: NavigationItem[] = [
  { to: '/suspension-requests', label: 'suspensionClosure.nav.suspension', end: true },
  { to: '/closure-requests', label: 'suspensionClosure.nav.closeout', end: true },
];
