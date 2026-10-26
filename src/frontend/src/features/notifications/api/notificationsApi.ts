import { apiRequest, type ApiResponse } from '@/shared/api/httpClient.ts';

import {
  type NotificationHistoryItem,
  type NotificationHistoryQuery,
  type NotificationPreferenceChange,
  type NotificationPreferenceSet,
  type NotificationQuery,
  type NotificationsMarkedRead,
  type NotificationSummary,
  type Page,
  type UnreadNotificationCount,
} from './types.ts';

// WF-15, the caller's own notifications and preferences (TASK-039 notification-runtime.md §3). Every operation acts on
// the caller's deliveries only. Reading and preferences are R-35 non-sensitive writes: the API requires no
// Idempotency-Key and ignores the one the client sends on every write.

async function data<T>(request: Promise<ApiResponse<T>>): Promise<T> {
  return (await request).data;
}

export const notificationsApi = {
  /** SCR-150–152: in-app notifications, newest first. */
  list: (query: NotificationQuery, signal?: AbortSignal) =>
    data(apiRequest<Page<NotificationSummary>>('/notifications', { query: { ...query }, signal })),
  /** The header badge. */
  countUnread: (signal?: AbortSignal) =>
    data(apiRequest<UnreadNotificationCount>('/notifications/unread-count', { signal })),
  /** SCR-153: every delivery to the caller on every channel, newest first. */
  listHistory: (query: NotificationHistoryQuery, signal?: AbortSignal) =>
    data(
      apiRequest<Page<NotificationHistoryItem>>('/notifications/history', {
        query: { ...query },
        signal,
      }),
    ),
  /** MOD-070. */
  get: (id: string, signal?: AbortSignal) =>
    data(apiRequest<NotificationSummary>(`/notifications/${id}`, { signal })),
  /** SENT → READ; reading a read notification changes nothing. */
  markRead: (id: string) =>
    data(apiRequest<NotificationSummary>(`/notifications/${id}/read`, { method: 'POST' })),
  /** MOD-071: every notification sent until now; later ones arrive unread. */
  markAllRead: () =>
    data(apiRequest<NotificationsMarkedRead>('/notifications/read-all', { method: 'POST' })),
  /** SCR-154. 422 CONFIGURATION_MISSING while no NOTIFICATION_ROUTING is in force. */
  getPreferences: (signal?: AbortSignal) =>
    data(apiRequest<NotificationPreferenceSet>('/notification-preferences', { signal })),
  /** All changes or none; a family and channel not listed keeps its choice. */
  updatePreferences: (preferences: NotificationPreferenceChange[]) =>
    data(
      apiRequest<NotificationPreferenceSet>('/notification-preferences', {
        method: 'PUT',
        body: { preferences },
      }),
    ),
};
