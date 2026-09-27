import { type ReactElement } from 'react';
import { Link, Navigate } from 'react-router';

import {
  SYSTEM_ADMINISTRATOR_ROLE,
  useHasRole,
  useSession,
} from '@/features/identity-access/session/useSession.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';

/** The landing page: the screens the session's roles can reach. Administration is the only one built so far. */
export function HomePage(): ReactElement {
  const { t } = useI18n();
  const { session } = useSession();
  const administrator = useHasRole(SYSTEM_ADMINISTRATOR_ROLE);
  if (session === null) {
    return <Navigate to="/sign-in" replace />;
  }
  return (
    <>
      <PageHeader title={t('common.home.title', { name: session.user.displayName })} />
      {administrator ? (
        <p>
          <Link to="/admin/users">{t('common.home.administration')}</Link>
        </p>
      ) : (
        <p>{t('common.home.nothingYet')}</p>
      )}
    </>
  );
}

export function NotFoundPage(): ReactElement {
  const { t } = useI18n();
  return (
    <>
      <PageHeader title={t('common.notFound.title')} />
      <p>
        <Link to="/">{t('common.notFound.home')}</Link>
      </p>
    </>
  );
}
