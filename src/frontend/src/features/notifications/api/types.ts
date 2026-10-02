// The WF-15 inbox and preference shapes (TASK-039 notification-runtime.md §3), as the API serialises them: enums in
// UPPER_SNAKE_CASE, dates as ISO 8601 strings.

export type NotificationChannel = 'IN_APP' | 'EMAIL' | 'SMS';

export type NotificationDeliveryStatus =
  'PENDING' | 'SENT' | 'DELIVERED' | 'FAILED' | 'DEAD_LETTER' | 'SUPPRESSED' | 'READ';

export interface Page<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

/** An in-app notification (SCR-150–152, MOD-070). `language` is the one it was rendered in, `ar` or `en`. */
export interface NotificationSummary {
  id: string;
  eventFamilyCode: string;
  eventType: string;
  subject: string | null;
  body: string;
  language: string;
  /** An SPA route, never a URL with a host (TASK-039 D-3). */
  deepLink: string | null;
  status: NotificationDeliveryStatus;
  occurredAt: string;
  sentAt: string | null;
  readAt: string | null;
}

export interface NotificationQuery {
  unread?: boolean | undefined;
  eventFamilyCode?: string | undefined;
  page?: number;
  pageSize?: number;
}

/** One delivery to the caller on any channel (SCR-153). */
export interface NotificationHistoryItem {
  id: string;
  channel: NotificationChannel;
  eventFamilyCode: string;
  eventType: string;
  subject: string | null;
  status: NotificationDeliveryStatus;
  suppressionReason: string | null;
  createdAt: string;
  sentAt: string | null;
  deliveredAt: string | null;
  readAt: string | null;
}

export interface NotificationHistoryQuery {
  channel?: NotificationChannel | undefined;
  status?: NotificationDeliveryStatus | undefined;
  page?: number;
  pageSize?: number;
}

export interface UnreadNotificationCount {
  unreadCount: number;
}

export interface NotificationsMarkedRead {
  markedCount: number;
}

export interface BilingualLabel {
  ar: string;
  en: string;
}

/** `isConfigurable` is false for in-app and for every channel of a mandatory family (TASK-039 D-11). */
export interface NotificationChannelPreference {
  channel: NotificationChannel;
  isEnabled: boolean;
  isConfigurable: boolean;
}

export interface NotificationFamilyPreference {
  eventFamilyCode: string;
  label: BilingualLabel;
  isMandatory: boolean;
  channels: NotificationChannelPreference[];
}

/** The caller's channels for every family of the routing configuration in force (SCR-154). */
export interface NotificationPreferenceSet {
  families: NotificationFamilyPreference[];
}

export interface NotificationPreferenceChange {
  eventFamilyCode: string;
  channel: NotificationChannel;
  isEnabled: boolean;
}
