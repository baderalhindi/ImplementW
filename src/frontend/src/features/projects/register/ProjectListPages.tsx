import { type ReactElement } from 'react';
import { Link, useLocation } from 'react-router';

import { useSession } from '@/features/identity-access/session/useSession.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { PageNotice } from '@/shared/ui/PageNotice.tsx';

import { ProjectList } from './ProjectList.tsx';

function CreateProjectLink(): ReactElement {
  const { t } = useI18n();
  return (
    <Link className="button button--primary" to="/projects/new">
      {t('projects.create.action')}
    </Link>
  );
}

/** SCR-025 Project Register (All Projects). MOD-001 returns here after deleting a draft. */
export function ProjectRegisterPage(): ReactElement | null {
  const { t } = useI18n();
  const { session } = useSession();
  const location = useLocation();
  const deleted = (location.state as { notice?: string } | null)?.notice === 'deleted';
  if (session === null) {
    return null;
  }
  return (
    <>
      <PageHeader
        title={t('projects.register.title')}
        description={t('projects.register.description')}
        actions={<CreateProjectLink />}
      />
      <PageNotice
        notice={deleted ? { tone: 'success', message: t('projects.done.deleted') } : null}
      />
      <ProjectList user={session.user} mine={false} basePath="/projects" />
    </>
  );
}

/** SCR-026 My Projects: the projects the signed-in person is the Project Manager of. */
export function MyProjectsPage(): ReactElement | null {
  const { t } = useI18n();
  const { session } = useSession();
  if (session === null) {
    return null;
  }
  return (
    <>
      <PageHeader
        title={t('projects.mine.title')}
        description={t('projects.mine.description')}
        actions={<CreateProjectLink />}
      />
      <ProjectList user={session.user} mine basePath="/projects/mine" />
    </>
  );
}
