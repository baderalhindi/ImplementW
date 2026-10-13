import {
  type ActivityEntryView,
  type ActivityProjectionReport,
  type AuditAccessView,
  type AuditCategory,
  type AuditEventDetail,
  type AuditEventSummary,
  type AuditIngestionStatus,
  type AuditIntegrityReport,
} from '@/features/audit-activity/api/types.ts';

import { type MockApi, page, problem } from './mockApi.ts';
import { PROJECT_ID, REVIEWER_ID } from './projectFixtures.ts';

// FG-06 as TASK-073 answers it, for the audit and Activity UI tests. The mock enforces what the API enforces: naming a
// category the caller does not hold is 403, an event outside the caller's categories is 404 (R-47), and Activity
// needs ACTIVITY_VIEW. The access presets follow the shipped grants (audit-activity.md §4, D-11) plus a reviewer
// granted through a permission profile.

export const RISK_ID = '9a000000-0000-4000-8000-000000000401';
export const CORRELATION_ID = '9b000000-0000-4000-8000-000000000402';
export const BUSINESS_EVENT_ID = '9c000000-0000-4000-8000-000000000501';
export const FINANCIAL_EVENT_ID = '9c000000-0000-4000-8000-000000000502';
export const ADMINISTRATION_EVENT_ID = '9c000000-0000-4000-8000-000000000503';
export const INTEGRATION_EVENT_ID = '9c000000-0000-4000-8000-000000000504';

function accessOf(overrides: Partial<AuditAccessView>): AuditAccessView {
  return {
    categories: [],
    requestContextVisible: false,
    activityVisible: false,
    operationsPermitted: false,
    ...overrides,
  };
}

/** R01 as shipped: administration audit and the store's operations, nothing else (TASK-073 D-11). */
export const SYSTEM_ADMINISTRATOR_ACCESS = accessOf({
  categories: ['ADMINISTRATION'],
  operationsPermitted: true,
});

/** An Audit Reviewer granted business and financial audit through a permission profile — not R01. */
export const AUDIT_REVIEWER_ACCESS = accessOf({
  categories: ['BUSINESS', 'FINANCIAL'],
  activityVisible: true,
});

/** An integration operator granted integration audit only. */
export const INTEGRATION_REVIEWER_ACCESS = accessOf({ categories: ['INTEGRATION'] });

/** R04 as shipped: the project's Activity at OWN, no audit category. */
export const PROJECT_MANAGER_ACCESS = accessOf({ activityVisible: true });

/** Nothing of FG-06. */
export const NO_AUDIT_ACCESS = accessOf({});

export function auditEvent(overrides: Partial<AuditEventSummary> = {}): AuditEventSummary {
  return {
    id: BUSINESS_EVENT_ID,
    occurredAt: '2026-10-12T09:15:00Z',
    recordedAt: '2026-10-12T09:15:01Z',
    category: 'BUSINESS',
    eventClass: 'LIFECYCLE_TRANSITION',
    eventType: 'Risk.RiskClosed',
    outcome: 'SUCCESS',
    actorType: 'USER',
    actorUserId: REVIEWER_ID,
    actorDisplayName: 'Faisal Reviewer',
    subjectModule: 'Risk',
    subjectType: 'Risk',
    subjectId: RISK_ID,
    scopeProjectId: PROJECT_ID,
    scopeExternalEntityId: null,
    correlationId: CORRELATION_ID,
    dataClassificationItemId: null,
    ...overrides,
  };
}

export const EVENTS: AuditEventSummary[] = [
  auditEvent(),
  auditEvent({
    id: FINANCIAL_EVENT_ID,
    category: 'FINANCIAL',
    eventClass: 'APPROVAL_DECISION',
    eventType: 'FinancialKpi.BudgetApproved',
    subjectModule: 'FinancialKpi',
    subjectType: 'Budget',
  }),
  auditEvent({
    id: ADMINISTRATION_EVENT_ID,
    category: 'ADMINISTRATION',
    eventClass: 'PERMISSION_CHANGE',
    eventType: 'IdentityAccess.RoleAssigned',
    outcome: 'SUCCESS',
    subjectModule: 'IdentityAccess',
    subjectType: 'RoleAssignment',
    scopeProjectId: null,
  }),
  auditEvent({
    id: INTEGRATION_EVENT_ID,
    category: 'INTEGRATION',
    eventClass: 'INTEGRATION',
    eventType: 'IntegrationMonitoring.MessageReprocessed',
    actorType: 'INTEGRATION',
    actorUserId: null,
    actorDisplayName: null,
    subjectModule: 'IntegrationMonitoring',
    subjectType: 'Message',
    scopeProjectId: null,
  }),
];

export function eventDetail(event: AuditEventSummary = auditEvent()): AuditEventDetail {
  return {
    event,
    attributes: [
      { name: 'status', oldValue: 'OPEN', newValue: 'CLOSED' },
      { name: 'authentication_method', oldValue: null, newValue: 'PASSWORD' },
    ],
    maskedAttributes: ['client_address'],
    integrity: {
      eventHash: 'a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f90',
      previousEventHash: 'ffeeddccbbaa99887766554433221100ffeeddccbbaa99887766554433221100',
      attributeDigest: '0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef',
    },
    forwarding: { status: 'FORWARDED', forwardedAt: '2026-10-12T09:16:00Z' },
  };
}

export function activityEntry(overrides: Partial<ActivityEntryView> = {}): ActivityEntryView {
  return {
    id: '9d000000-0000-4000-8000-000000000601',
    auditEventId: BUSINESS_EVENT_ID,
    projectId: PROJECT_ID,
    occurredAt: '2026-10-12T09:15:00Z',
    activityKind: 'RISK',
    eventType: 'Risk.RiskClosed',
    summaryAr: 'تم إغلاق الخطر',
    summaryEn: 'Risk closed',
    actorUserId: REVIEWER_ID,
    actorDisplayName: 'Faisal Reviewer',
    subjectModule: 'Risk',
    subjectType: 'Risk',
    subjectId: RISK_ID,
    ...overrides,
  };
}

export const ACTIVITY: ActivityEntryView[] = [
  activityEntry(),
  activityEntry({
    id: '9d000000-0000-4000-8000-000000000602',
    auditEventId: '9c000000-0000-4000-8000-000000000505',
    occurredAt: '2026-10-11T08:00:00Z',
    activityKind: 'PROJECT',
    eventType: 'Project.ProjectActivated',
    summaryAr: 'تم تفعيل المشروع',
    summaryEn: 'Project activated',
    // An internal actor shown to an external viewer is withheld (D-12).
    actorUserId: null,
    actorDisplayName: null,
    subjectModule: 'Project',
    subjectType: 'Project',
    subjectId: PROJECT_ID,
  }),
];

export const INGESTION_STATUS: AuditIngestionStatus = {
  pendingMessages: 2,
  failingMessages: 1,
  oldestPendingOccurredAt: '2026-10-12T09:00:00Z',
  lastError: 'AUDIT_EVENT_INVALID',
  eventsStored: 1340,
  storeHeadRecordedAt: '2026-10-12T09:15:01Z',
  projectionLag: 3,
  projectionCheckpointRecordedAt: '2026-10-12T09:14:00Z',
};

export const INTACT_REPORT: AuditIntegrityReport = {
  eventsVerified: 1340,
  brokenLinks: 0,
  attributeDigestMismatches: 0,
  eventsWithoutDigest: 12,
  findings: [],
  intact: true,
};

export const DIFFERING_PROJECTION: ActivityProjectionReport = {
  rebuilt: false,
  eventsCompared: 1337,
  entriesExpected: 410,
  missing: 1,
  unexpected: 0,
  changed: 0,
  pendingEvents: 3,
  checkpointRecordedAt: '2026-10-12T09:14:00Z',
  differences: [{ auditEventId: BUSINESS_EVENT_ID, kind: 'MISSING' }],
  matches: false,
};

export interface AuditState {
  access?: AuditAccessView;
  events?: AuditEventSummary[];
  activity?: ActivityEntryView[];
}

/** FG-06's read side over the fixtures, enforcing categories as the API does. */
export function withAudit(api: MockApi, state: AuditState = {}): MockApi {
  const access = state.access ?? NO_AUDIT_ACCESS;
  const events = state.events ?? EVENTS;
  const activity = state.activity ?? ACTIVITY;
  const held = (category: AuditCategory) => access.categories.includes(category);
  return api
    .on('GET', /^\/audit-access$/, { body: access })
    .on('GET', /^\/audit-events$/, (request) => {
      const named = (request.query.get('category') ?? '').split(',').filter((c) => c !== '');
      if (named.some((category) => !held(category as AuditCategory))) {
        return problem(403, 'PERMISSION_DENIED');
      }
      const projectId = request.query.get('projectId');
      const items = events.filter(
        (event) =>
          held(event.category) &&
          (named.length === 0 || named.includes(event.category)) &&
          (projectId === null || event.scopeProjectId === projectId),
      );
      return { body: page(items) };
    })
    .on('GET', /^\/audit-events\/[^/]+$/, (request) => {
      const id = request.path.split('/').at(-1);
      const event = events.find((candidate) => candidate.id === id);
      return event === undefined || !held(event.category)
        ? problem(404, 'NOT_FOUND')
        : { body: eventDetail(event) };
    })
    .on('GET', /^\/activity-entries$/, (request) => {
      if (!access.activityVisible) {
        return problem(403, 'PERMISSION_DENIED');
      }
      const kinds = (request.query.get('activityKind') ?? '').split(',').filter((k) => k !== '');
      return {
        body: page(
          activity.filter(
            (entry) =>
              entry.projectId === request.query.get('projectId') &&
              (kinds.length === 0 || kinds.includes(entry.activityKind)),
          ),
        ),
      };
    })
    .on('GET', /^\/audit-ingestion-status$/, () =>
      access.operationsPermitted ? { body: INGESTION_STATUS } : problem(403, 'PERMISSION_DENIED'),
    )
    .on('POST', /^\/audit-integrity-verifications$/, { body: INTACT_REPORT })
    .on('POST', /^\/activity-projection-checks$/, { body: DIFFERING_PROJECTION });
}
