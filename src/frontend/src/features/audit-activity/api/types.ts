// FG-06 as TASK-073 serves it (audit-activity.md §5; docs/api/openapi.v1.json, tag AuditActivity).

/** The category an event is authorised by; each has its own read permission (D-10). */
export type AuditCategory =
  'SECURITY' | 'ADMINISTRATION' | 'INTEGRATION' | 'FINANCIAL' | 'EXTERNAL' | 'BUSINESS';

export type AuditEventClass =
  | 'AUTHENTICATION'
  | 'AUTHORIZATION_DENIAL'
  | 'PRIVILEGED_ACTION'
  | 'PERMISSION_CHANGE'
  | 'LIFECYCLE_TRANSITION'
  | 'DATA_CHANGE'
  | 'APPROVAL_DECISION'
  | 'INTEGRATION'
  | 'CONFIGURATION_CHANGE';

export type AuditOutcome = 'SUCCESS' | 'DENIED' | 'FAILED';

export type AuditActorType = 'USER' | 'SERVICE' | 'INTEGRATION';

export type AuditForwardingStatus = 'PENDING' | 'FORWARDED' | 'FAILED';

/** What the caller may do with audit and Activity (AUD-API-014). The API enforces it again on every read. */
export interface AuditAccessView {
  categories: AuditCategory[];
  requestContextVisible: boolean;
  activityVisible: boolean;
  operationsPermitted: boolean;
}

export interface AuditEventSummary {
  id: string;
  occurredAt: string;
  recordedAt: string;
  category: AuditCategory;
  eventClass: AuditEventClass;
  eventType: string;
  outcome: AuditOutcome;
  actorType: AuditActorType;
  actorUserId: string | null;
  actorDisplayName: string | null;
  subjectModule: string | null;
  subjectType: string | null;
  subjectId: string | null;
  scopeProjectId: string | null;
  scopeExternalEntityId: string | null;
  correlationId: string;
  dataClassificationItemId: string | null;
}

export interface AuditEventPage {
  items: AuditEventSummary[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface AuditAttributeView {
  name: string;
  oldValue: string | null;
  newValue: string | null;
}

export interface AuditEventDetail {
  event: AuditEventSummary;
  attributes: AuditAttributeView[];
  /** Request-context attributes the caller may not see: named, never shown (D-10). */
  maskedAttributes: string[];
  integrity: {
    eventHash: string;
    previousEventHash: string | null;
    attributeDigest: string | null;
  };
  /** Null for a class that is not forwarded to the SIEM. */
  forwarding: { status: AuditForwardingStatus; forwardedAt: string | null } | null;
}

/** ADM-050's server-side filters. Sets are sent comma-joined, as the API parses them. */
export interface AuditEventQuery {
  categories?: readonly AuditCategory[] | undefined;
  eventClasses?: readonly AuditEventClass[] | undefined;
  outcomes?: readonly AuditOutcome[] | undefined;
  occurredFrom?: string | undefined;
  occurredTo?: string | undefined;
  actorUserId?: string | undefined;
  eventType?: string | undefined;
  projectId?: string | undefined;
  subjectType?: string | undefined;
  subjectId?: string | undefined;
  correlationId?: string | undefined;
}

/** One line of a project's Activity timeline (SCR-057): what happened, when, in which domain, by whom when shown. */
export interface ActivityEntryView {
  id: string;
  auditEventId: string;
  projectId: string | null;
  occurredAt: string;
  activityKind: string;
  eventType: string;
  summaryAr: string;
  summaryEn: string;
  actorUserId: string | null;
  actorDisplayName: string | null;
  subjectModule: string | null;
  subjectType: string | null;
  subjectId: string | null;
}

export interface ActivityEntryPage {
  items: ActivityEntryView[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface ActivityQuery {
  projectId?: string | undefined;
  activityKinds?: readonly string[] | undefined;
  occurredFrom?: string | undefined;
  occurredTo?: string | undefined;
}

/** The audit pipeline's health (AUD-API-039/040): counts and times, never an event's content. */
export interface AuditIngestionStatus {
  pendingMessages: number;
  failingMessages: number;
  oldestPendingOccurredAt: string | null;
  lastError: string | null;
  eventsStored: number;
  storeHeadRecordedAt: string | null;
  projectionLag: number;
  projectionCheckpointRecordedAt: string | null;
}

export interface AuditIntegrityReport {
  eventsVerified: number;
  brokenLinks: number;
  attributeDigestMismatches: number;
  eventsWithoutDigest: number;
  findings: { auditEventId: string; kind: 'BROKEN_LINK' | 'ATTRIBUTE_DIGEST_MISMATCH' }[];
  intact: boolean;
}

export interface ActivityProjectionReport {
  rebuilt: boolean;
  eventsCompared: number;
  entriesExpected: number;
  missing: number;
  unexpected: number;
  changed: number;
  pendingEvents: number;
  checkpointRecordedAt: string | null;
  differences: { auditEventId: string; kind: 'MISSING' | 'UNEXPECTED' | 'CHANGED' }[];
  matches: boolean;
}
