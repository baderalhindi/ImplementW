import { type ReactElement, type ReactNode } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';

import { RequireSession } from './RequireSession.tsx';
import { useHasRole } from './useSession.ts';

interface RequireRoleProps {
  roleCode: string;
  children: ReactNode;
}

/**
 * Shows a route only to a session holding the role; without a session, sends the person to sign in and back. This
 * is navigation, not protection: the API refuses every call the role's grants do not cover (TASK-030, TASK-031).
 */
export function RequireRole({ roleCode, children }: RequireRoleProps): ReactElement {
  const permitted = useHasRole(roleCode);
  const { t } = useI18n();
  return (
    <RequireSession>
      {permitted ? (
        children
      ) : (
        <PageHeader
          title={t('common.forbidden.title')}
          description={t('common.forbidden.description')}
        />
      )}
    </RequireSession>
  );
}
