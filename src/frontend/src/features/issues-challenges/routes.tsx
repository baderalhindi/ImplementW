import { type RouteObject } from 'react-router';

import { type NavigationItem } from '@/features/identity-access/routes.tsx';

import { ConcernRegisterPage } from './ConcernRegisterPage.tsx';
import { EscalationDetail } from './EscalationDetail.tsx';
import { EscalationsPage } from './EscalationsPage.tsx';

/**
 * WF-07's registers and escalations across projects, mounted under /issues-challenges for any signed-in person. No
 * role is checked here: what a person sees is the API's answer (CONCERN_VIEW). A project's registers (SCR-083,
 * SCR-085) and a concern's detail (SCR-084, SCR-086) are in the project workspace, /projects/:projectId/issues-challenges.
 */
export const issuesChallengesRoutes: RouteObject[] = [
  { index: true, element: <ConcernRegisterPage key="ISSUE" concernType="ISSUE" /> }, // SCR-083
  {
    path: 'challenges',
    element: <ConcernRegisterPage key="CHALLENGE" concernType="CHALLENGE" />, // SCR-085
  },
  { path: 'escalations', element: <EscalationsPage /> }, // SCR-087
  { path: 'escalations/:escalationId', element: <EscalationDetail /> }, // SCR-088
];

export const issuesChallengesNavigation: NavigationItem[] = [
  { to: '/issues-challenges', label: 'issuesChallenges.nav.issues', end: true },
  { to: '/issues-challenges/challenges', label: 'issuesChallenges.nav.challenges' },
  { to: '/issues-challenges/escalations', label: 'issuesChallenges.nav.escalations' },
];
