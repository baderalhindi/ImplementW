import { type RouteObject } from 'react-router';

import { type NavigationItem } from '@/features/identity-access/routes.tsx';

import { TaskListPage } from './TaskListPage.tsx';

/**
 * WF-04's cross-project lists, mounted under /tasks for any signed-in person. No role is checked here: which tasks a
 * person sees is the API's answer (TASK_VIEW; TASK-048 D-11). SCR-047 is the workspace's Tasks tab (projects/routes).
 */
export const taskRoutes: RouteObject[] = [
  { index: true, element: <TaskListPage key="mine" list="mine" /> }, // SCR-063
  { path: 'team', element: <TaskListPage key="team" list="team" /> }, // SCR-064
  { path: 'overdue', element: <TaskListPage key="overdue" list="overdue" /> }, // SCR-065
  { path: 'updates', element: <TaskListPage key="updates" list="updates" /> }, // SCR-066
];

export const taskNavigation: NavigationItem[] = [
  { to: '/tasks', label: 'tasks.nav.mine', end: true },
  { to: '/tasks/team', label: 'tasks.nav.team' },
  { to: '/tasks/overdue', label: 'tasks.nav.overdue' },
  { to: '/tasks/updates', label: 'tasks.nav.updates' },
];
