import { Navigate, type RouteObject } from 'react-router';

import { type NavigationItem } from '@/features/identity-access/routes.tsx';

import { DelegatedApprovalsPage } from './delegations/DelegatedApprovalsPage.tsx';
import { ApprovalHistoryPage } from './history/ApprovalHistoryPage.tsx';
import { ApprovalInboxPage } from './inbox/ApprovalInboxPage.tsx';
import { MyRequestsPage } from './requests/MyRequestsPage.tsx';

/**
 * WF-11, mounted under /approvals for any signed-in person. No role is checked here: who may decide or see what is
 * the API's answer (APPROVAL_DECIDE, APPROVAL_VIEW and the run's scope, TASK-035 D-4, D-10), shown as it comes.
 */
export const approvalRoutes: RouteObject[] = [
  { index: true, element: <Navigate to="inbox" replace /> },
  { path: 'inbox', element: <ApprovalInboxPage /> }, // SCR-100, MOD-040, MOD-041, MOD-042
  { path: 'requests', element: <MyRequestsPage /> }, // SCR-101, MOD-044
  { path: 'delegations', element: <DelegatedApprovalsPage /> }, // SCR-114, MOD-043
  { path: 'instances/:instanceId', element: <ApprovalHistoryPage /> }, // SCR-115, MOD-044, MOD-045
];

export const approvalNavigation: NavigationItem[] = [
  { to: '/approvals/inbox', label: 'approvals.nav.inbox' },
  { to: '/approvals/requests', label: 'approvals.nav.requests' },
  { to: '/approvals/delegations', label: 'approvals.nav.delegations' },
];
