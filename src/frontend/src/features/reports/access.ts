import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';

import { SHARE_ROLES } from './presentation.ts';

/**
 * Whether to offer SCR-138 and SCR-139: ADR-019 gives REPORT_COMPOSE to R02, R03 and R07, and never to an external
 * entity's person. The session carries roles, not permissions, so this is navigation, not protection — the API
 * answers 403 to anyone without the permission (dashboards-ui.md F-6).
 */
export function canCompose(user: SessionUser): boolean {
  return (
    user.userType !== 'EXTERNAL' &&
    user.roleAssignments.some((assignment) =>
      (SHARE_ROLES as readonly string[]).includes(assignment.roleCode),
    )
  );
}
