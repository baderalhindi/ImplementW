import { type ReactElement, useEffect, useId, useRef, useState } from 'react';
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

function GlobeIcon(): ReactElement {
  return (
    <svg
      className="language-switch__icon"
      viewBox="0 0 24 24"
      width="18"
      height="18"
      aria-hidden="true"
      focusable="false"
    >
      <g fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round">
        <circle cx="12" cy="12" r="9" />
        <path d="M3 12h18M12 3c2.5 2.6 3.75 5.6 3.75 9S14.5 18.4 12 21c-2.5-2.6-3.75-5.6-3.75-9S9.5 5.6 12 3z" />
      </g>
    </svg>
  );
}

/** Each language in its own script, so a reader finds theirs whichever is showing. */
const languageChoices = [
  { code: 'en', name: 'English' },
  { code: 'ar', name: 'العربية' },
] as const;

/**
 * The globe opens a list of the languages; the current one is ticked. A choice, Escape or a click elsewhere closes it,
 * and Escape returns focus to the globe.
 */
function LanguageSwitch(): ReactElement {
  const { t, language, setLanguage } = useI18n();
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);
  const buttonRef = useRef<HTMLButtonElement>(null);
  const menuId = useId();

  useEffect(() => {
    if (!open) {
      return undefined;
    }
    const closeOnOutsideClick = (event: PointerEvent): void => {
      if (!containerRef.current?.contains(event.target as Node)) {
        setOpen(false);
      }
    };
    const closeOnEscape = (event: KeyboardEvent): void => {
      if (event.key === 'Escape') {
        setOpen(false);
        buttonRef.current?.focus();
      }
    };
    document.addEventListener('pointerdown', closeOnOutsideClick);
    document.addEventListener('keydown', closeOnEscape);
    return () => {
      document.removeEventListener('pointerdown', closeOnOutsideClick);
      document.removeEventListener('keydown', closeOnEscape);
    };
  }, [open]);

  return (
    <div className="language-switch" ref={containerRef}>
      <button
        ref={buttonRef}
        type="button"
        className="button button--quiet language-switch__button"
        aria-expanded={open}
        aria-controls={menuId}
        title={t('common.language')}
        onClick={() => {
          setOpen((value) => !value);
        }}
      >
        <GlobeIcon />
        <span className="visually-hidden">{t('common.language')}</span>
      </button>
      <ul className="language-switch__menu" id={menuId} hidden={!open}>
        {languageChoices.map((choice) => (
          <li key={choice.code}>
            <button
              type="button"
              className="language-switch__option"
              lang={choice.code}
              aria-pressed={language === choice.code}
              onClick={() => {
                setLanguage(choice.code);
                setOpen(false);
              }}
            >
              <span className="language-switch__check" aria-hidden="true">
                {language === choice.code ? '✓' : ''}
              </span>
              {choice.name}
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}

/** Whether the window has scrolled past <paramref name="offset"/> pixels; the header floats once it has. */
function useScrolledPast(offset: number): boolean {
  const [scrolled, setScrolled] = useState(() => window.scrollY > offset);
  useEffect(() => {
    const update = (): void => {
      setScrolled(window.scrollY > offset);
    };
    update();
    window.addEventListener('scroll', update, { passive: true });
    return () => {
      window.removeEventListener('scroll', update);
    };
  }, [offset]);
  return scrolled;
}

function Navigation({
  label,
  items,
}: {
  label: TranslationKey;
  items: NavigationItem[];
}): ReactElement {
  const { t } = useI18n();
  const titleId = useId();
  return (
    <nav className="shell__nav" aria-labelledby={titleId}>
      <p className="shell__nav-title" id={titleId}>
        {t(label)}
      </p>
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
  const floating = useScrolledPast(8);

  return (
    <div className="shell">
      <a className="skip-link" href="#main">
        {t('common.skipToContent')}
      </a>
      <header className={floating ? 'shell__header shell__header--floating' : 'shell__header'}>
        <Link className="shell__brand" to="/">
          <img
            className="shell__logo"
            src="/brand/ahda-logo.png"
            alt={t('common.organizationName')}
            width="596"
            height="232"
          />
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
