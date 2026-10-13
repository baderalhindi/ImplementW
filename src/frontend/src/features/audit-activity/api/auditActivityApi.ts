import { apiRequest } from '@/shared/api/httpClient.ts';

import {
  type ActivityEntryPage,
  type ActivityProjectionReport,
  type ActivityQuery,
  type AuditAccessView,
  type AuditEventDetail,
  type AuditEventPage,
  type AuditEventQuery,
  type AuditIngestionStatus,
  type AuditIntegrityReport,
} from './types.ts';

// FG-06 at runtime (TASK-073 audit-activity.md §5). The API parses a set as one comma-joined value (`category=A,B`),
// not as the repeated parameter the shared client sends for an array, so sets are joined here. An event the caller may
// not read is 404 like an unknown id (R-47); naming a category they do not hold is 403. No call writes an event: there
// is no such operation (D-7).

export interface PageRequest {
  page: number;
  pageSize: number;
}

function joined(values: readonly string[] | undefined): string | undefined {
  return values === undefined || values.length === 0 ? undefined : values.join(',');
}

export const auditActivityApi = {
  /** The categories the caller reads, and whether they see request context, Activity and the store's operations. */
  access: async (signal?: AbortSignal) =>
    (await apiRequest<AuditAccessView>('/audit-access', { signal })).data,

  /** ADM-050, ADM-053 and SCR-058: the events the caller reaches, newest first. */
  events: async (query: AuditEventQuery, paging: PageRequest, signal?: AbortSignal) =>
    (
      await apiRequest<AuditEventPage>('/audit-events', {
        query: {
          category: joined(query.categories),
          eventClass: joined(query.eventClasses),
          outcome: joined(query.outcomes),
          occurredFrom: query.occurredFrom,
          occurredTo: query.occurredTo,
          actorUserId: query.actorUserId,
          eventType: query.eventType,
          projectId: query.projectId,
          subjectType: query.subjectType,
          subjectId: query.subjectId,
          correlationId: query.correlationId,
          page: paging.page,
          pageSize: paging.pageSize,
        },
        signal,
      })
    ).data,

  /** ADM-051: one immutable event with its attributes, integrity metadata and SIEM forwarding. */
  event: async (auditEventId: string, signal?: AbortSignal) =>
    (await apiRequest<AuditEventDetail>(`/audit-events/${auditEventId}`, { signal })).data,

  /** SCR-057: the project's business Activity lines, newest first. */
  activity: async (query: ActivityQuery, paging: PageRequest, signal?: AbortSignal) =>
    (
      await apiRequest<ActivityEntryPage>('/activity-entries', {
        query: {
          projectId: query.projectId,
          activityKind: joined(query.activityKinds),
          occurredFrom: query.occurredFrom,
          occurredTo: query.occurredTo,
          page: paging.page,
          pageSize: paging.pageSize,
        },
        signal,
      })
    ).data,

  /** ADM-052: the audit pipeline's own state (AUDIT_OPERATIONS_MANAGE). */
  ingestionStatus: async (signal?: AbortSignal) =>
    (await apiRequest<AuditIngestionStatus>('/audit-ingestion-status', { signal })).data,

  /** ADM-052: verifies the whole hash chain and every attribute digest. A sensitive write: it is audited. */
  verifyIntegrity: async () =>
    (await apiRequest<AuditIntegrityReport>('/audit-integrity-verifications', { method: 'POST' }))
      .data,

  /** ADM-052: compares the Activity projection with what the store derives, without changing it. Audited. */
  checkProjection: async () =>
    (await apiRequest<ActivityProjectionReport>('/activity-projection-checks', { method: 'POST' }))
      .data,
};
