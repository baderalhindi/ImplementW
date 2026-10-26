import { type ReactElement, type ReactNode } from 'react';
import { Link } from 'react-router';

import { useSession } from '@/features/identity-access/session/useSession.ts';
import { isInternal } from '@/features/projects/access.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';

import { MY_EXTERNAL_REQUESTS_PATH } from '../paths.ts';

/**
 * AHDA's screens (SCR-160, SCR-161, SCR-165, SCR-166, SCR-167). An entity user who reaches one by address is sent to
 * their own requests, and the screen makes no read at all: least disclosure starts with not asking (WF-13 §8.2). The
 * API refuses or narrows every read anyway.
 */
export function InternalOnly({ children }: { children: ReactNode }): ReactElement | null {
  const { t } = useI18n();
  const { session } = useSession();
  if (session === null) {
    return null;
  }
  if (isInternal(session.user)) {
    return <>{children}</>;
  }
  return (
    <>
      <PageHeader title={t('externalParticipation.internalOnly.title')} />
      <p className="state">{t('externalParticipation.internalOnly.description')}</p>
      <p>
        <Link to={MY_EXTERNAL_REQUESTS_PATH}>{t('externalParticipation.nav.mine')}</Link>
      </p>
    </>
  );
}
