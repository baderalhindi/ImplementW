import { type ReactElement } from 'react';
import { Link, Navigate } from 'react-router';

import {
  SYSTEM_ADMINISTRATOR_ROLE,
  useHasRole,
  useSession,
} from '@/features/identity-access/session/useSession.ts';
import { isInternal } from '@/features/projects/access.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';

/**
 * The landing page: the screens the session can reach. Projects, tasks, milestones, KPIs, risks, issues and challenges, change requests, suspensions, closeouts, approvals, documents and
 * notifications are for everyone; WF-13's register for AHDA, or the entity user's own update requests; administration for R01.
 */
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
      <ul className="link-list">
        <li>
          <Link to="/projects">{t('common.home.projects')}</Link>
        </li>
        <li>
          <Link to="/tasks">{t('common.home.tasks')}</Link>
        </li>
        <li>
          <Link to="/milestones">{t('common.home.milestones')}</Link>
        </li>
        <li>
          <Link to="/kpis">{t('common.home.kpis')}</Link>
        </li>
        <li>
          <Link to="/risks">{t('common.home.risks')}</Link>
        </li>
        <li>
          <Link to="/issues-challenges">{t('common.home.issuesChallenges')}</Link>
        </li>
        <li>
          <Link to="/change-requests">{t('common.home.changeRequests')}</Link>
        </li>
        <li>
          <Link to="/suspension-requests">{t('common.home.suspensions')}</Link>
        </li>
        <li>
          <Link to="/closure-requests">{t('common.home.closeouts')}</Link>
        </li>
        {isInternal(session.user) ? (
          <li>
            <Link to="/external-requests">{t('common.home.externalRequests')}</Link>
          </li>
        ) : (
          <li>
            <Link to="/my-external-requests">{t('common.home.myExternalRequests')}</Link>
          </li>
        )}
        <li>
          <Link to="/approvals/inbox">{t('common.home.approvals')}</Link>
        </li>
        <li>
          <Link to="/documents">{t('common.home.documents')}</Link>
        </li>
        <li>
          <Link to="/notifications">{t('common.home.notifications')}</Link>
        </li>
        {administrator && (
          <li>
            <Link to="/admin/users">{t('common.home.administration')}</Link>
          </li>
        )}
      </ul>
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
