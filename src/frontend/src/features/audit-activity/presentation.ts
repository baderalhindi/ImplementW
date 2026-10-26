import { closeoutCasePath, suspensionRequestPath } from '@/features/suspension-closure/paths.ts';
import { externalRequestPath } from '@/features/external-participation/paths.ts';
import { type Language } from '@/shared/i18n/i18n.ts';
import { type StatusTone } from '@/shared/ui/StatusBadge.tsx';

import {
  type ActivityEntryView,
  type AuditForwardingStatus,
  type AuditOutcome,
} from './api/types.ts';

export function outcomeTone(outcome: AuditOutcome): StatusTone {
  switch (outcome) {
    case 'SUCCESS':
      return 'positive';
    case 'DENIED':
      return 'warning';
    case 'FAILED':
      return 'negative';
  }
}

export function forwardingTone(status: AuditForwardingStatus): StatusTone {
  switch (status) {
    case 'FORWARDED':
      return 'positive';
    case 'PENDING':
      return 'info';
    case 'FAILED':
      return 'negative';
  }
}

/**
 * The source domains an Activity line can name: each module the projection templates, in upper snake case
 * (TASK-073 `ActivityProjection.KindOf`, `ActivityTemplateCatalogue`). A line of a domain added later is still shown,
 * under its code; it is only absent from the filter until it is listed here.
 */
export const ACTIVITY_KINDS = [
  'PROJECT',
  'PROGRESS',
  'SCHEDULE',
  'PROJECT_TASK',
  'MILESTONE',
  'FINANCIAL_KPI',
  'RISK',
  'MANAGEMENT_CONCERN',
  'CHANGE_REQUEST',
  'SUSPENSION',
  'CLOSURE',
  'EXTERNAL_PARTICIPATION',
  'DOCUMENT_MANAGEMENT',
  'APPROVAL',
] as const;

export type ActivityKind = (typeof ACTIVITY_KINDS)[number];

export function isActivityKind(value: string): value is ActivityKind {
  return (ACTIVITY_KINDS as readonly string[]).includes(value);
}

/** The line in the interface language; the API stores both (FG-06 §7.3). */
export function activitySummary(entry: ActivityEntryView, language: Language): string {
  return language === 'ar' ? entry.summaryAr : entry.summaryEn;
}

/**
 * Where the record an Activity line or audit event is about is opened (FG-06 §7.1 "optional source link"). The link
 * carries the id only: the source screen reads the record under its own permission, so a person who may no longer see
 * it is refused there (AUD-CC-17). Subjects with no screen of their own (a progress cycle, a KPI value) have no link.
 */
export function sourceRecordPath(
  subject: { subjectModule: string | null; subjectType: string | null; subjectId: string | null },
  projectId: string | null,
): string | null {
  const { subjectModule, subjectType, subjectId } = subject;
  if (subjectId === null) {
    return null;
  }
  switch (`${subjectModule ?? ''}.${subjectType ?? ''}`) {
    case 'Project.Project':
      return `/projects/${subjectId}`;
    case 'Risk.Risk':
      return projectId === null ? null : `/projects/${projectId}/risks/${subjectId}`;
    case 'ManagementConcern.ManagementConcern':
      return projectId === null ? null : `/projects/${projectId}/issues-challenges/${subjectId}`;
    case 'ChangeRequest.ChangeRequest':
      return `/change-requests/${subjectId}`;
    case 'Suspension.SuspensionRequest':
      return suspensionRequestPath(subjectId);
    case 'Closure.CompletionCase':
      return closeoutCasePath('completion', subjectId);
    case 'Closure.ClosureCase':
      return closeoutCasePath('closure', subjectId);
    case 'ExternalParticipation.ExternalUpdateRequest':
      return externalRequestPath(subjectId);
    case 'DocumentManagement.Document':
      return `/documents/${subjectId}`;
    case 'Approval.ApprovalInstance':
      return `/approvals/instances/${subjectId}`;
    default:
      return null;
  }
}
