import { type ReactElement, type ReactNode, useRef } from 'react';
import { Navigate } from 'react-router';

import { usePermissions } from '@/features/identity-access/permissions/permissions.ts';
import { useSession } from '@/features/identity-access/session/useSession.ts';
import { DATE_LOCALES, useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { LoadingState } from '@/shared/ui/States.tsx';

import { BannerBackdrop } from './banner/BannerBackdrop.tsx';
import { BannerCustomizer } from './banner/BannerCustomizer.tsx';
import { useBanner } from './banner/useBanner.ts';
import { EntityProjects, ManagedProjects } from './MyProjects.tsx';
import { MyWork, StartOrOpen } from './MyWork.tsx';
import { AdministrationActions, PlatformHealth } from './PlatformHealth.tsx';
import { PortfolioSnapshot } from './PortfolioSnapshot.tsx';

/**
 * The Home (Blueprint §20.1 "Home"). The 24 Oct 2026 review (H2) gave it the person's own work — what waits for them, what they
 * submitted, what is due; the 27 Oct 2026 review asked for more meaning: a welcome banner with the ways in, and for the Portfolio
 * Dashboard's audience a snapshot of the portfolio — its headline figures and where its projects are — above their work. The full
 * analysis stays the Portfolio menu's (/dashboards). Someone who administers the platform and holds no business data, R01, sees the
 * platform's health instead (M-1), under the same welcome banner with the administration's ways in. `menuOverview` is the shell's map
 * of the screens this person can open, directly under the banner; since the 13 Nov 2026 review only the administrator sees it, and
 * everyone else reaches the screens from the side menu.
 */
export function HomePage({ menuOverview }: { menuOverview: ReactNode }): ReactElement {
  const { t, language } = useI18n();
  const { session } = useSession();
  const today = new Intl.DateTimeFormat(DATE_LOCALES[language], { dateStyle: 'full' });
  const permissions = usePermissions();
  // Review round 8, point 07: this person's banner; the turns wait while the pointer is on it.
  const banner = useBanner(session !== null);
  const hero = useRef<HTMLElement>(null);

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
  return (
    <div className="home">
      <header
        className={
          banner.preference.style === 'DEFAULT' ? 'home__hero' : 'home__hero home__hero--image'
        }
        ref={hero}
      >
        {banner.preference.style !== 'DEFAULT' && (
          <BannerBackdrop
            images={banner.preference.imageIds
              .map((id) => banner.images.find((image) => image.id === id))
              .filter((image) => image !== undefined)}
            host={hero}
          />
        )}
        <p className="home__date">{today.format(new Date())}</p>
        <h1 className="home__title">{t('home.title', { name: session.user.displayName })}</h1>
        <p className="home__intro">
          {t(administers ? 'home.health.description' : 'home.description')}
        </p>
        {administers ? <AdministrationActions /> : <StartOrOpen user={session.user} />}
        <BannerCustomizer
          images={banner.images}
          preference={banner.preference}
          onChoose={banner.choose}
          failed={banner.failed}
        />
      </header>
      {administers && menuOverview}
      {administers ? (
        <PlatformHealth />
      ) : (
        <>
          <PortfolioSnapshot />
          {session.user.userType === 'EXTERNAL' && <EntityProjects />}
          {permissions.can('PROJECT_VIEW') && <ManagedProjects userId={session.user.id} />}
          <MyWork user={session.user} />
        </>
      )}
    </div>
  );
}
