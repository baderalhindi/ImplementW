import { type Language } from '@/shared/i18n/i18n.ts';
import { type StatusTone } from '@/shared/ui/StatusBadge.tsx';

import {
  type NotificationChannel,
  type NotificationChannelPreference,
  type NotificationDeliveryStatus,
  type NotificationFamilyPreference,
  type NotificationSummary,
} from './api/types.ts';

// How WF-15 values read on screen. The word carries the meaning; the colour only repeats it (WCAG 1.4.1).

export const CHANNELS: NotificationChannel[] = ['IN_APP', 'EMAIL', 'SMS'];

export const DELIVERY_STATUSES: NotificationDeliveryStatus[] = [
  'PENDING',
  'SENT',
  'DELIVERED',
  'READ',
  'FAILED',
  'DEAD_LETTER',
  'SUPPRESSED',
];

const STATUS_TONES: Record<NotificationDeliveryStatus, StatusTone> = {
  PENDING: 'info',
  SENT: 'positive',
  DELIVERED: 'positive',
  READ: 'positive',
  FAILED: 'warning',
  DEAD_LETTER: 'negative',
  SUPPRESSED: 'neutral',
};

export function deliveryTone(status: NotificationDeliveryStatus): StatusTone {
  return STATUS_TONES[status];
}

/** An in-app notification is unread until its recipient reads it: SENT, not READ. */
export function isUnread(notification: Pick<NotificationSummary, 'status'>): boolean {
  return notification.status === 'SENT';
}

/** The suppression reasons TASK-039 records on a delivery (`NotificationSuppressionReasons`), each with its own words. */
export const SUPPRESSION_REASONS = [
  'RECIPIENT_OPTED_OUT',
  'CHANNEL_OFF_BY_DEFAULT',
  'CHANNEL_NOT_CONFIGURED',
  'MOBILE_UNVERIFIED',
  'SMS_SEGMENT_BUDGET_EXCEEDED',
  'RECIPIENT_INELIGIBLE',
] as const;

export type SuppressionReason = (typeof SUPPRESSION_REASONS)[number];

export function isKnownSuppressionReason(reason: string): reason is SuppressionReason {
  return (SUPPRESSION_REASONS as readonly string[]).includes(reason);
}

/**
 * SCR-151 Alerts and SCR-152 Escalations by the family code's last word. FG-04 families carry only a code, a label and a
 * mandatory flag (TASK-039 F-14), so the code's suffix is the one field to tell them by: `SECURITY_ALERT`,
 * `CONCERN_ESCALATION`. The convention is this task's (TASK-040 F-1).
 */
export type FamilyKind = 'alert' | 'escalation';

const FAMILY_KIND_PATTERNS: Record<FamilyKind, RegExp> = {
  alert: /(^|_)ALERT$/,
  escalation: /(^|_)ESCALATION$/,
};

export function isFamilyOfKind(eventFamilyCode: string, kind: FamilyKind): boolean {
  return FAMILY_KIND_PATTERNS[kind].test(eventFamilyCode);
}

/**
 * Whether the recipient may turn this channel of this family on or off. In-app is always on and a mandatory family's
 * channels are fixed (ADR-004 as amended); the API says the same through `isConfigurable` and refuses the rest with 422.
 */
export function canChangePreference(
  family: NotificationFamilyPreference,
  preference: NotificationChannelPreference,
): boolean {
  return preference.channel !== 'IN_APP' && !family.isMandatory && preference.isConfigurable;
}

/** A family's label in the interface language, or its code when the routing in force no longer has it. */
export function familyLabel(
  families: NotificationFamilyPreference[] | undefined,
  eventFamilyCode: string,
  language: Language,
): string {
  const family = families?.find((candidate) => candidate.eventFamilyCode === eventFamilyCode);
  return family === undefined ? eventFamilyCode : family.label[language];
}

const HEADLINE_LENGTH = 120;

/** The line a list shows for a notification: its subject, or the start of its body when it has none. */
export function headline(notification: Pick<NotificationSummary, 'subject' | 'body'>): string {
  if (notification.subject !== null && notification.subject.trim() !== '') {
    return notification.subject;
  }
  const firstLine = notification.body.split('\n', 1)[0] ?? '';
  return firstLine.length > HEADLINE_LENGTH ? `${firstLine.slice(0, HEADLINE_LENGTH)}…` : firstLine;
}

/**
 * A deep link the SPA may follow: a route on this origin. The API already refuses anything else at intake (TASK-039
 * D-3); the screen checks again rather than render a link it cannot vouch for.
 */
export function isAppRoute(deepLink: string | null): deepLink is string {
  return deepLink !== null && deepLink.startsWith('/') && !deepLink.startsWith('//');
}
