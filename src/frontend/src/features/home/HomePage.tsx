import { type ReactElement } from 'react';
import { Navigate } from 'react-router';

import { usePermissions } from '@/features/identity-access/permissions/permissions.ts';
import { useSession } from '@/features/identity-access/session/useSession.ts';
import { DATE_LOCALES, useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { LoadingState } from '@/shared/ui/States.tsx';

import { MyWork, StartOrOpen } from './MyWork.tsx';
import { PlatformHealth } from './PlatformHealth.tsx';
import { PortfolioSnapshot } from './PortfolioSnapshot.tsx';

/**
 * The Home (Blueprint §20.1 "Home"). The 24 Oct 2026 review (H2) gave it the person's own work — what waits for them, what they
 * submitted, what is due; the 27 Oct 2026 review asked for more meaning: a welcome banner with the ways in, and for the Portfolio
 * Dashboard's audience a snapshot of the portfolio — its headline figures and where its projects are — above their work. The full
 * analysis stays the Portfolio menu's (/dashboards). Someone who administers the platform and holds no business data, R01, sees the
 * platform's health instead (M-1).
 */
export function HomePage(): ReactElement {
  const { t, language } = useI18n();
  const { session } = useSession();
  const today = new Intl.DateTimeFormat(DATE_LOCALES[language], { dateStyle: 'full' });
  const permissions = usePermissions();

  if (session === null) {
    return <Navigate to="/sign-in" replace />;
  }
  if (!permissions.loaded) {
    return (
      <>
        <PageHeader title={t('home.title', { name: session.user.displayName })} />
        <LoadingState />
      </>
    );
  }

  const administers =
    permissions.can('USER_VIEW', 'INTEGRATION_VIEW') && !permissions.can('PROJECT_VIEW');
  return administers ? (
    <>
      <PageHeader title={t('home.health.title')} description={t('home.health.description')} />
      <PlatformHealth />
    </>
  ) : (
    <div className="home">
      <header className="home__hero">
        <p className="home__date">{today.format(new Date())}</p>
        <h1 className="home__title">{t('home.title', { name: session.user.displayName })}</h1>
        <p className="home__intro">{t('home.description')}</p>
        <StartOrOpen user={session.user} />
      </header>
      <PortfolioSnapshot />
      <MyWork user={session.user} />
    </div>
  );
}
