import { type ReactElement } from 'react';
import { Link, NavLink, Outlet } from 'react-router';

import { identityAccessNavigation } from '@/features/identity-access/routes.tsx';
import { sessionStore } from '@/features/identity-access/session/sessionStore.ts';
import { StepUpDialog } from '@/features/identity-access/session/StepUpDialog.tsx';
import {
  SYSTEM_ADMINISTRATOR_ROLE,
  useHasRole,
  useSession,
} from '@/features/identity-access/session/useSession.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';

function LanguageSwitch(): ReactElement {
  const { language, setLanguage } = useI18n();
  const other = language === 'ar' ? 'en' : 'ar';
  return (
    <button
      type="button"
      className="button button--quiet"
      lang={other}
      onClick={() => {
        setLanguage(other);
      }}
    >
      {other === 'ar' ? 'العربية' : 'English'}
    </button>
  );
}

/** The application shell: skip link, header, the navigation the session's roles allow, and the page. */
export function AppLayout(): ReactElement {
  const { t } = useI18n();
  const { session } = useSession();
  const administrator = useHasRole(SYSTEM_ADMINISTRATOR_ROLE);

  return (
    <div className="shell">
      <a className="skip-link" href="#main">
        {t('common.skipToContent')}
      </a>
      <header className="shell__header">
        <Link className="shell__brand" to="/">
          {t('common.appName')}
        </Link>
        <div className="shell__session">
          <LanguageSwitch />
          {session !== null && (
            <>
              <span className="shell__user">{session.user.displayName}</span>
              <button
                type="button"
                className="button button--quiet"
                onClick={() => {
                  sessionStore.signOut();
                }}
              >
                {t('common.signOut')}
              </button>
            </>
          )}
        </div>
      </header>
      <div className="shell__body">
        {administrator && (
          <nav className="shell__nav" aria-label={t('identityAccess.nav.label')}>
            <ul>
              {identityAccessNavigation.map((item) => (
                <li key={item.to}>
                  <NavLink to={item.to} end={item.end ?? false}>
                    {t(item.label)}
                  </NavLink>
                </li>
              ))}
            </ul>
          </nav>
        )}
        <main id="main" className="shell__main" tabIndex={-1}>
          <Outlet />
        </main>
      </div>
      {session !== null && <StepUpDialog />}
    </div>
  );
}
