import { type ReactElement, type ReactNode } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { LoadingState } from '@/shared/ui/States.tsx';

import { type PermissionCode } from '../permissions/permissionCodes.ts';
import { usePermissions } from '../permissions/permissions.ts';
import { RequireSession } from './RequireSession.tsx';

interface RequirePermissionProps {
  /** Any one of these opens the route. */
  codes: readonly PermissionCode[];
  children: ReactNode;
}

function Guarded({ codes, children }: RequirePermissionProps): ReactElement {
  const { loaded, can } = usePermissions();
  const { t } = useI18n();
  if (!loaded) {
    return <LoadingState />;
  }
  return can(...codes) ? (
    <>{children}</>
  ) : (
    <PageHeader
      title={t('common.forbidden.title')}
      description={t('common.forbidden.description')}
    />
  );
}

/**
 * Shows a route only to a holder of one of the permissions; without a session, sends the person to sign in and back. This is
 * navigation, not protection: the API refuses every call the person's grants do not cover (TASK-030, TASK-031).
 */
export function RequirePermission(props: RequirePermissionProps): ReactElement {
  return (
    <RequireSession>
      <Guarded {...props} />
    </RequireSession>
  );
}
