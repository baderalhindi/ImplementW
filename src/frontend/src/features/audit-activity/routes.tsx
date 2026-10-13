import { type RouteObject } from 'react-router';

import { AuditEventDetailPage, AuditTrailPage, IntegrationAuditPage } from './AuditTrailPage.tsx';
import { SystemEventLogsPage } from './SystemEventLogsPage.tsx';

/**
 * FG-06's administration screens under /admin, at Step 16's proposed addresses (ADM-051 takes the event's id). They
 * are not behind the R01 guard: each opens on the audit permission it needs, which an Audit Reviewer may hold without
 * R01 and R01 holds only for administration audit (TASK-073 D-11). SCR-057/058 are workspace tabs (projects/routes).
 */
export const auditActivityRoutes: RouteObject[] = [
  { path: 'audit-trail', element: <AuditTrailPage /> }, // ADM-050
  { path: 'audit-trail/:auditEventId', element: <AuditEventDetailPage /> }, // ADM-051
  { path: 'system-event-logs', element: <SystemEventLogsPage /> }, // ADM-052
  { path: 'integration-audit-logs', element: <IntegrationAuditPage /> }, // ADM-053
];
