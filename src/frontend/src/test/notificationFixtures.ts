import {
  type NotificationFamilyPreference,
  type NotificationHistoryItem,
  type NotificationPreferenceSet,
  type NotificationSummary,
} from '@/features/notifications/api/types.ts';
import { type Session } from '@/features/identity-access/session/sessionApi.ts';

import { sessionFor } from './identityAccessFixtures.ts';
import { type MockApi, page } from './mockApi.ts';

// Test data only: invented subjects and fixed ids. Each id starts differently, so a shortened id is recognisable.

export const NOTIFICATION_ID = '1a1a1a1a-0000-4000-8000-000000000101';
export const READ_NOTIFICATION_ID = '2b2b2b2b-0000-4000-8000-000000000102';

/** A Project Manager (R04): a person's own inbox and preferences need no permission (TASK-039 D-14). */
export function notificationSession(): Session {
  return sessionFor(['R04']);
}

export function notification(overrides: Partial<NotificationSummary> = {}): NotificationSummary {
  return {
    id: NOTIFICATION_ID,
    eventFamilyCode: 'APPROVAL_TASK',
    eventType: 'Approval.ApprovalTaskAssigned',
    subject: 'Approval task assigned',
    body: 'A change request is waiting for your decision.\nIt is due on 10 October.',
    language: 'en',
    deepLink: '/approvals/inbox',
    status: 'SENT',
    occurredAt: '2026-10-01T08:00:00Z',
    sentAt: '2026-10-01T08:00:15Z',
    readAt: null,
    ...overrides,
  };
}

export function readNotification(
  overrides: Partial<NotificationSummary> = {},
): NotificationSummary {
  return notification({
    id: READ_NOTIFICATION_ID,
    subject: 'Report ready',
    eventFamilyCode: 'REPORT_READY',
    status: 'READ',
    readAt: '2026-10-01T09:00:00Z',
    ...overrides,
  });
}

export function historyItem(
  overrides: Partial<NotificationHistoryItem> = {},
): NotificationHistoryItem {
  return {
    id: '3c3c3c3c-0000-4000-8000-000000000103',
    channel: 'EMAIL',
    eventFamilyCode: 'APPROVAL_TASK',
    eventType: 'Approval.ApprovalTaskAssigned',
    subject: 'Approval task assigned',
    status: 'SENT',
    suppressionReason: null,
    createdAt: '2026-10-01T08:00:00Z',
    sentAt: '2026-10-01T08:01:00Z',
    deliveredAt: null,
    readAt: null,
    ...overrides,
  };
}

function family(
  eventFamilyCode: string,
  en: string,
  ar: string,
  isMandatory: boolean,
  channels: [NotificationFamilyPreference['channels'][number]['channel'], boolean][],
): NotificationFamilyPreference {
  return {
    eventFamilyCode,
    label: { en, ar },
    isMandatory,
    channels: channels.map(([channel, isEnabled]) => ({
      channel,
      isEnabled,
      isConfigurable: channel !== 'IN_APP' && !isMandatory,
    })),
  };
}

/** Four families as a NOTIFICATION_ROUTING version could hold them: two alerts, one escalation, one other. */
export function preferenceSet(): NotificationPreferenceSet {
  return {
    families: [
      family('APPROVAL_TASK', 'Approval tasks', 'مهام الموافقة', false, [
        ['IN_APP', true],
        ['EMAIL', true],
        ['SMS', true],
      ]),
      family('SECURITY_ALERT', 'Security alerts', 'تنبيهات أمنية', true, [
        ['IN_APP', true],
        ['SMS', true],
      ]),
      family('BUDGET_ALERT', 'Budget alerts', 'تنبيهات الميزانية', false, [
        ['IN_APP', true],
        ['EMAIL', false],
      ]),
      family('CONCERN_ESCALATION', 'Concern escalations', 'تصعيد المخاوف', true, [
        ['IN_APP', true],
        ['EMAIL', true],
        ['SMS', true],
      ]),
    ],
  };
}

interface InboxOptions {
  items?: NotificationSummary[];
  unreadCount?: number;
  preferences?: NotificationPreferenceSet;
}

/** The inbox reads every notification screen makes: the list (filtered as asked), the badge and the families. */
export function withInbox(
  api: MockApi,
  {
    items = [notification(), readNotification()],
    unreadCount = 1,
    preferences = preferenceSet(),
  }: InboxOptions = {},
): MockApi {
  return api
    .on('GET', /^\/notifications$/, (request) => {
      const unread = request.query.get('unread');
      const familyCode = request.query.get('eventFamilyCode');
      return {
        body: page(
          items.filter(
            (item) =>
              (unread === null || (item.status === 'SENT') === (unread === 'true')) &&
              (familyCode === null || item.eventFamilyCode === familyCode),
          ),
        ),
      };
    })
    .on('GET', /^\/notifications\/unread-count$/, { body: { unreadCount } })
    .on('GET', /^\/notification-preferences$/, { body: preferences });
}
