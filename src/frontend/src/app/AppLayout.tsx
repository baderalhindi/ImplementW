import { type ReactElement } from 'react';
import { Link, NavLink, Outlet } from 'react-router';

import { approvalNavigation } from '@/features/approvals/routes.tsx';
import { documentNavigation } from '@/features/documents/routes.tsx';
import { kpiNavigation } from '@/features/financial-kpi/routes.tsx';
import {
  identityAccessNavigation,
  type NavigationItem,
} from '@/features/identity-access/routes.tsx';
import { issuesChallengesNavigation } from '@/features/issues-challenges/routes.tsx';
import { sessionStore } from '@/features/identity-access/session/sessionStore.ts';
import { StepUpDialog } from '@/features/identity-access/session/StepUpDialog.tsx';
import {
  SYSTEM_ADMINISTRATOR_ROLE,
  useHasRole,
  useSession,
} from '@/features/identity-access/session/useSession.ts';
import { milestoneNavigation } from '@/features/milestones/routes.tsx';
import { NotificationBell } from '@/features/notifications/components/NotificationBell.tsx';
import { notificationNavigation } from '@/features/notifications/routes.tsx';
import { projectNavigation } from '@/features/projects/routes.tsx';
import { riskNavigation } from '@/features/risks/routes.tsx';
import { taskNavigation } from '@/features/tasks/routes.tsx';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';

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

function Navigation({
  label,
  items,
}: {
  label: TranslationKey;
  items: NavigationItem[];
}): ReactElement {
  const { t } = useI18n();
  return (
    <nav className="shell__nav" aria-label={t(label)}>
      <ul>
        {items.map((item) => (
          <li key={item.to}>
            <NavLink to={item.to} end={item.end ?? false}>
              {t(item.label)}
            </NavLink>
          </li>
        ))}
      </ul>
    </nav>
  );
}

/**
 * The application shell: skip link, header with the unread notification badge, the navigation the session allows
 * (projects, tasks, milestones, KPIs, risks, issues and challenges, approvals, documents and notifications for everyone signed in, administration for R01),
 * and the page.
 */
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
              <NotificationBell />
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
        {session !== null && (
          <div className="shell__sidebar">
            <Navigation label="projects.nav.label" items={projectNavigation} />
            <Navigation label="tasks.nav.label" items={taskNavigation} />
            <Navigation label="milestones.nav.label" items={milestoneNavigation} />
            <Navigation label="financialKpi.nav.label" items={kpiNavigation} />
            <Navigation label="risks.nav.label" items={riskNavigation} />
            <Navigation label="issuesChallenges.nav.label" items={issuesChallengesNavigation} />
            <Navigation label="approvals.nav.label" items={approvalNavigation} />
            <Navigation label="documents.nav.label" items={documentNavigation} />
            <Navigation label="notifications.nav.label" items={notificationNavigation} />
            {administrator && (
              <Navigation label="identityAccess.nav.label" items={identityAccessNavigation} />
            )}
          </div>
        )}
        <main id="main" className="shell__main" tabIndex={-1}>
          <Outlet />
        </main>
      </div>
      {session !== null && <StepUpDialog />}
    </div>
  );
}
