import { type RouteObject } from 'react-router';

import { type NavigationItem } from '@/features/identity-access/routes.tsx';

import { NotificationHistoryPage } from './history/NotificationHistoryPage.tsx';
import {
  AlertsPage,
  AllNotificationsPage,
  EscalationsPage,
} from './inbox/NotificationInboxPages.tsx';
import { NotificationPreferencesPage } from './preferences/NotificationPreferencesPage.tsx';

/**
 * WF-15, mounted under /notifications for any signed-in person. Every screen is the caller's own: the API filters by
 * the caller and needs no permission (TASK-039 D-12, D-14). MOD-070 and MOD-071 are dialogs on the inbox screens.
 */
export const notificationRoutes: RouteObject[] = [
  { index: true, element: <AllNotificationsPage /> }, // SCR-150, MOD-070, MOD-071
  { path: 'alerts', element: <AlertsPage /> }, // SCR-151, MOD-070
  { path: 'escalations', element: <EscalationsPage /> }, // SCR-152, MOD-070
  { path: 'history', element: <NotificationHistoryPage /> }, // SCR-153
  { path: 'preferences', element: <NotificationPreferencesPage /> }, // SCR-154
];

export const notificationNavigation: NavigationItem[] = [
  { to: '/notifications', label: 'notifications.nav.all', end: true },
  { to: '/notifications/alerts', label: 'notifications.nav.alerts' },
  { to: '/notifications/escalations', label: 'notifications.nav.escalations' },
  { to: '/notifications/history', label: 'notifications.nav.history' },
  { to: '/notifications/preferences', label: 'notifications.nav.preferences' },
];
