import { type ReactElement } from 'react';
import { Link, NavLink, Outlet } from 'react-router';

import { approvalNavigation } from '@/features/approvals/routes.tsx';
import { auditNavigation } from '@/features/audit-activity/access.ts';
import { useAuditAccess } from '@/features/audit-activity/useAuditAccess.ts';
import { changeRequestNavigation } from '@/features/change-requests/routes.tsx';
import { dashboardNavigation } from '@/features/dashboards/routes.tsx';
import { documentNavigation } from '@/features/documents/routes.tsx';
import { externalParticipationNavigation } from '@/features/external-participation/routes.tsx';
import { kpiNavigation } from '@/features/financial-kpi/routes.tsx';
import {
  identityAccessNavigation,
  type NavigationItem,
} from '@/features/identity-access/routes.tsx';
import { integrationAdminNavigation } from '@/features/integration-admin/catalogue.ts';
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
import { reportNavigation } from '@/features/reports/routes.tsx';
import { riskNavigation } from '@/features/risks/routes.tsx';
import { suspensionClosureNavigation } from '@/features/suspension-closure/routes.tsx';
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
 * (the role-aware Home with its dashboards, FG-02's reports, projects, tasks, milestones, KPIs, risks, issues and challenges, change requests, suspensions and closeouts, approvals, documents and notifications for
 * everyone signed in; WF-13's register and monitors for AHDA, or an entity user's requests and contributions; FG-06's
 * audit screens for the audit permissions the API reports; administration and FG-05's integrations for R01),
 * and the page.
 */
export function AppLayout(): ReactElement {
  const { t } = useI18n();
  const { session } = useSession();
  const administrator = useHasRole(SYSTEM_ADMINISTRATOR_ROLE);
  const auditItems = auditNavigation(useAuditAccess().data ?? null);

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
            <Navigation label="dashboards.nav.label" items={dashboardNavigation} />
            <Navigation label="reports.nav.label" items={reportNavigation(session.user)} />
            <Navigation label="projects.nav.label" items={projectNavigation} />
            <Navigation label="tasks.nav.label" items={taskNavigation} />
            <Navigation label="milestones.nav.label" items={milestoneNavigation} />
            <Navigation label="financialKpi.nav.label" items={kpiNavigation} />
            <Navigation label="risks.nav.label" items={riskNavigation} />
            <Navigation label="issuesChallenges.nav.label" items={issuesChallengesNavigation} />
            <Navigation label="changeRequests.nav.label" items={changeRequestNavigation} />
            <Navigation label="suspensionClosure.nav.label" items={suspensionClosureNavigation} />
            <Navigation
              label="externalParticipation.nav.label"
              items={externalParticipationNavigation(session.user)}
            />
            <Navigation label="approvals.nav.label" items={approvalNavigation} />
            <Navigation label="documents.nav.label" items={documentNavigation} />
            <Navigation label="notifications.nav.label" items={notificationNavigation} />
            {auditItems.length > 0 && (
              <Navigation label="auditActivity.nav.label" items={auditItems} />
            )}
            {administrator && (
              <Navigation label="identityAccess.nav.label" items={identityAccessNavigation} />
            )}
            {administrator && (
              <Navigation label="integrationAdmin.nav.label" items={integrationAdminNavigation} />
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
