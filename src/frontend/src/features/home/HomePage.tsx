import { type ReactElement } from 'react';
import { Navigate } from 'react-router';

import { usePermissions } from '@/features/identity-access/permissions/permissions.ts';
import { useSession } from '@/features/identity-access/session/useSession.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { LoadingState } from '@/shared/ui/States.tsx';

import { MyWork } from './MyWork.tsx';
import { PlatformHealth } from './PlatformHealth.tsx';

/**
 * The Home (Blueprint §20.1 "Home"), given its own job by the 24 Oct 2026 review (H2): the person's own work — what waits for them,
 * what they submitted, what is due — while the analysis is the Portfolio menu's (/dashboards). Someone who administers the platform and
 * holds no business data, R01, sees the platform's health instead (M-1).
 */
export function HomePage(): ReactElement {
  const { t } = useI18n();
  const { session } = useSession();
  const permissions = usePermissions();

  if (session === null) {
    return <Navigate to="/sign-in" replace />;
  }
  if (!permissions.loaded) {
    return (
      <>
        <PageHeader title={t('home.title')} />
        <LoadingState />
      </>
    );
  }

  const administers = permissions.can('USER_VIEW', 'INTEGRATION_VIEW') && !permissions.can('PROJECT_VIEW');
  return administers ? (
    <>
      <PageHeader title={t('home.health.title')} description={t('home.health.description')} />
      <PlatformHealth />
    </>
  ) : (
    <>
      <PageHeader
        title={t('home.title')}
        description={t('home.description', { name: session.user.displayName })}
      />
      <MyWork user={session.user} />
    </>
  );
}
