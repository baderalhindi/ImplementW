import { type ReactElement, type ReactNode } from 'react';

import { useSession } from '@/features/identity-access/session/useSession.ts';
import { isInternal } from '@/features/projects/access.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';

/**
 * FG-02's screens for AHDA's people only: an external user runs no report and the API answers them 404 on every reports
 * operation (ADR-019, shipped matrix D-7), so they are shown "Access denied" and nothing is read (QA INT-05). Placed inside a
 * RequirePermission, which has already asked for the session. Navigation, not protection.
 */
export function RequireInternal({ children }: { children: ReactNode }): ReactElement | null {
  const { t } = useI18n();
  const { session } = useSession();
  if (session === null) {
    return null;
  }
  return isInternal(session.user) ? (
    <>{children}</>
  ) : (
    <PageHeader
      title={t('common.forbidden.title')}
      description={t('common.forbidden.description')}
    />
  );
}
