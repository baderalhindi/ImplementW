import { type RouteObject } from 'react-router';

import { type NavigationItem } from '@/features/identity-access/routes.tsx';

import { MilestoneRegisterPage } from './MilestoneRegisterPage.tsx';

/**
 * WF-05's register, mounted under /milestones for any signed-in person. No role is checked here: which milestones and
 * claims a person sees is the API's answer (SCHEDULE_VIEW, MILESTONE_VIEW; TASK-050 D-10). SCR-046 is the workspace's
 * Milestones tab (projects/routes).
 */
export const milestoneRoutes: RouteObject[] = [
  { index: true, element: <MilestoneRegisterPage /> }, // SCR-062
];

export const milestoneNavigation: NavigationItem[] = [
  { to: '/milestones', label: 'milestones.nav.register', end: true },
];
