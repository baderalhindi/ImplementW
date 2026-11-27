import { Navigate, type RouteObject } from 'react-router';

import { type NavigationItem } from '@/features/identity-access/routes.tsx';
import { RequirePermission } from '@/features/identity-access/session/RequirePermission.tsx';

import { DelegatedApprovalsPage } from './delegations/DelegatedApprovalsPage.tsx';
import { ApprovalHistoryPage } from './history/ApprovalHistoryPage.tsx';
import { ApprovalInboxPage } from './inbox/ApprovalInboxPage.tsx';
import { MyRequestsPage } from './requests/MyRequestsPage.tsx';

/**
 * WF-11, mounted under /approvals. Each screen opens for the permission its read needs (QA INT-05): the inbox and the
 * delegations for APPROVAL_DECIDE, the requests and a run's history for APPROVAL_VIEW. Which runs and tasks a person sees is
 * the API's answer (the run's scope, TASK-035 D-4, D-10).
 */
export const approvalRoutes: RouteObject[] = [
  { index: true, element: <Navigate to="inbox" replace /> },
  {
    path: 'inbox', // SCR-100, MOD-040, MOD-041, MOD-042
    element: (
      <RequirePermission codes={['APPROVAL_DECIDE']}>
        <ApprovalInboxPage />
      </RequirePermission>
    ),
  },
  {
    path: 'requests', // SCR-101, MOD-044
    element: (
      <RequirePermission codes={['APPROVAL_VIEW']}>
        <MyRequestsPage />
      </RequirePermission>
    ),
  },
  {
    path: 'delegations', // SCR-114, MOD-043
    element: (
      <RequirePermission codes={['APPROVAL_DECIDE']}>
        <DelegatedApprovalsPage />
      </RequirePermission>
    ),
  },
  {
    path: 'instances/:instanceId', // SCR-115, MOD-044, MOD-045
    element: (
      <RequirePermission codes={['APPROVAL_VIEW']}>
        <ApprovalHistoryPage />
      </RequirePermission>
    ),
  },
];

export const approvalNavigation: NavigationItem[] = [
  { to: '/approvals/inbox', label: 'approvals.nav.inbox', permissions: ['APPROVAL_DECIDE'] },
  { to: '/approvals/requests', label: 'approvals.nav.requests', permissions: ['APPROVAL_VIEW'] },
  {
    to: '/approvals/delegations',
    label: 'approvals.nav.delegations',
    permissions: ['APPROVAL_DECIDE'],
  },
];
