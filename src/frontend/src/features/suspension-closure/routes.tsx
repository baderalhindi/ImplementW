import { type RouteObject } from 'react-router';

import { type NavigationItem } from '@/features/identity-access/routes.tsx';
import { RequirePermission } from '@/features/identity-access/session/RequirePermission.tsx';

import { CloseoutDetailPage } from './CloseoutDetailPage.tsx';
import { CreateCloseoutCasePage, EditCloseoutCasePage } from './CloseoutFormPage.tsx';
import { CloseoutRegisterPage } from './CloseoutRegisterPage.tsx';
import { SuspensionDetailPage } from './SuspensionDetailPage.tsx';
import { CreateSuspensionRequestPage, EditSuspensionRequestPage } from './SuspensionFormPage.tsx';
import { SuspensionRegisterPage } from './SuspensionRegisterPage.tsx';

/**
 * WF-09, mounted under /suspension-requests for a holder of SUSPENSION_VIEW (QA INT-05); the form for SUSPENSION_RAISE. What a
 * person sees and may do is the API's answer (SUSPENSION_VIEW, _RAISE, _REVIEW, _ACTIVATE; WF-11's APPROVAL_DECIDE). A project's
 * register (SCR-108) is also its workspace tab, /projects/:projectId/suspension.
 */
export const suspensionRoutes: RouteObject[] = [
  { index: true, element: <SuspensionRegisterPage /> }, // SCR-108 across projects
  {
    path: 'new', // SCR-109, ?projectId=&type=
    element: (
      <RequirePermission codes={['SUSPENSION_RAISE']}>
        <CreateSuspensionRequestPage />
      </RequirePermission>
    ),
  },
  { path: ':suspensionRequestId', element: <SuspensionDetailPage /> }, // SCR-110, MOD-040–042, MOD-044
  {
    path: ':suspensionRequestId/edit', // SCR-109 for DRAFT/RETURNED
    element: (
      <RequirePermission codes={['SUSPENSION_RAISE']}>
        <EditSuspensionRequestPage />
      </RequirePermission>
    ),
  },
];

/**
 * WF-10, mounted under /closure-requests: the canonical Closure screen family reused for both stages of the closeout
 * (WF-10 §14), each case addressed by its stage. A project's register (SCR-111) is also its workspace tab,
 * /projects/:projectId/closeout.
 */
export const closeoutRoutes: RouteObject[] = [
  { index: true, element: <CloseoutRegisterPage /> }, // SCR-111 across projects
  {
    path: 'new', // SCR-112, ?projectId=&stage=
    element: (
      <RequirePermission codes={['CLOSEOUT_RAISE']}>
        <CreateCloseoutCasePage />
      </RequirePermission>
    ),
  },
  { path: ':stage/:caseId', element: <CloseoutDetailPage /> }, // SCR-113, MOD-040–042, MOD-044
  {
    path: ':stage/:caseId/edit', // SCR-112 for DRAFT/RETURNED
    element: (
      <RequirePermission codes={['CLOSEOUT_RAISE']}>
        <EditCloseoutCasePage />
      </RequirePermission>
    ),
  },
];

export const suspensionClosureNavigation: NavigationItem[] = [
  {
    to: '/suspension-requests',
    label: 'suspensionClosure.nav.suspension',
    end: true,
    permissions: ['SUSPENSION_VIEW'],
  },
  {
    to: '/closure-requests',
    label: 'suspensionClosure.nav.closeout',
    end: true,
    permissions: ['CLOSEOUT_VIEW'],
  },
];
