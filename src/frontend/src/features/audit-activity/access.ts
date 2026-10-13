import { type NavigationItem } from '@/features/identity-access/routes.tsx';

import { type AuditAccessView, type AuditCategory } from './api/types.ts';

// What FG-06 offers a person, from GET /audit-access (AUD-API-014). The session carries roles, not permissions, and
// audit is authorised by permission — one per category — never by role (BR-AUD-023), so every screen here asks the
// API what the caller holds. This is navigation, not protection: the API decides every read again and answers 403 for
// a category the caller does not hold and 404 for an event out of reach.

/** Every category, in the order the API declares them. */
export const AUDIT_CATEGORIES: readonly AuditCategory[] = [
  'SECURITY',
  'ADMINISTRATION',
  'INTEGRATION',
  'FINANCIAL',
  'EXTERNAL',
  'BUSINESS',
];

/**
 * The categories SCR-058 Project Audit History reads: the formal audit of a project's business records (FG-06 §7.2).
 * Administration, security and integration audit are platform evidence, read in ADM-050 and ADM-053 (where a project
 * filter is still available). R01 ships with ADMINISTRATION only (TASK-073 D-11), so holding R01 never opens SCR-058.
 */
export const PROJECT_AUDIT_CATEGORIES: readonly AuditCategory[] = [
  'BUSINESS',
  'FINANCIAL',
  'EXTERNAL',
];

/** The categories the caller holds, in declaration order; none when access is unknown. */
export function heldCategories(access: AuditAccessView | null): AuditCategory[] {
  return AUDIT_CATEGORIES.filter((category) => access?.categories.includes(category) ?? false);
}

/** The project audit categories the caller holds: what SCR-058 asks for, and all it may ask for. */
export function projectAuditCategories(access: AuditAccessView | null): AuditCategory[] {
  return heldCategories(access).filter((category) => PROJECT_AUDIT_CATEGORIES.includes(category));
}

/** SCR-057: ACTIVITY_VIEW, whatever its scope; the API lists only the projects that scope reaches. */
export function canReadActivity(access: AuditAccessView | null): boolean {
  return access?.activityVisible ?? false;
}

/** SCR-058: an explicit project audit category permission, not R01 (acceptance criterion 2). */
export function canReadProjectAudit(access: AuditAccessView | null): boolean {
  return projectAuditCategories(access).length > 0;
}

/** ADM-050 and ADM-051: any audit category. */
export function canReadAuditTrail(access: AuditAccessView | null): boolean {
  return heldCategories(access).length > 0;
}

/** ADM-053: AUDIT_INTEGRATION_VIEW. */
export function canReadIntegrationAudit(access: AuditAccessView | null): boolean {
  return access?.categories.includes('INTEGRATION') ?? false;
}

/** ADM-052: AUDIT_OPERATIONS_MANAGE, which reveals counts and event ids, never content (TASK-073 D-13). */
export function canReadSystemEvents(access: AuditAccessView | null): boolean {
  return access?.operationsPermitted ?? false;
}

export const AUDIT_TRAIL_PATH = '/admin/audit-trail';
export const INTEGRATION_AUDIT_PATH = '/admin/integration-audit-logs';
export const SYSTEM_EVENT_LOGS_PATH = '/admin/system-event-logs';

/** ADM-051, reached from ADM-050 and ADM-053. */
export function auditEventPath(auditEventId: string): string {
  return `${AUDIT_TRAIL_PATH}/${auditEventId}`;
}

/** ADM-051 inside the project's workspace, reached from SCR-058 without leaving the project (FG-06 §7.2). */
export function projectAuditEventPath(projectId: string, auditEventId: string): string {
  return `/projects/${projectId}/audit-history/${auditEventId}`;
}

/** The sidebar's audit group: one entry per screen the caller's permissions open. */
export function auditNavigation(access: AuditAccessView | null): NavigationItem[] {
  return [
    ...(canReadAuditTrail(access)
      ? [{ to: AUDIT_TRAIL_PATH, label: 'auditActivity.nav.auditTrail' } as const]
      : []),
    ...(canReadIntegrationAudit(access)
      ? [{ to: INTEGRATION_AUDIT_PATH, label: 'auditActivity.nav.integrationAudit' } as const]
      : []),
    ...(canReadSystemEvents(access)
      ? [{ to: SYSTEM_EVENT_LOGS_PATH, label: 'auditActivity.nav.systemEvents' } as const]
      : []),
  ];
}
