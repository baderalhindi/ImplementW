import { type ReactElement, useCallback } from 'react';
import { Link, useParams } from 'react-router';

import { useSession } from '@/features/identity-access/session/useSession.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { isEditable } from '../access.ts';
import { projectsApi } from '../api/projectsApi.ts';
import { projectProblemMessage } from '../problems.ts';

import { ProjectForm } from './ProjectForm.tsx';

/**
 * SCR-033 Create Project, for anyone signed in: whether they may register, and for which department or entity, is the
 * API's answer (PROJECT_REGISTER; TASK-041 D-10). Per ADR-013 as amended, this includes an external entity's users,
 * for their own entity; the project starts as a DRAFT with no Formal Project ID, and AHDA alone approves it.
 */
export function CreateProjectPage(): ReactElement | null {
  const { t } = useI18n();
  const { session } = useSession();
  if (session === null) {
    return null;
  }
  return (
    <>
      <PageHeader
        title={t('projects.create.title')}
        description={t('projects.create.description')}
      />
      <ProjectForm user={session.user} />
    </>
  );
}

/** SCR-034 Edit Project Draft: the whole registration of a DRAFT or RETURNED project (TASK-041 D-12). */
export function EditProjectPage(): ReactElement | null {
  const { t } = useI18n();
  const { session } = useSession();
  const { projectId = '' } = useParams();
  const load = useCallback(
    (signal: AbortSignal) => projectsApi.get(projectId, signal),
    [projectId],
  );
  const project = useApiResource(load);

  if (session === null) {
    return null;
  }
  if (project.loading) {
    return <LoadingState />;
  }
  if (project.data === undefined) {
    return (
      <ErrorState message={projectProblemMessage(project.error, t)} onRetry={project.reload} />
    );
  }
  const detail = project.data.data;
  return (
    <>
      <PageHeader title={t('projects.edit.title', { title: detail.title.text })} />
      {isEditable(detail.status) ? (
        // Keyed on the ETag: a reload after someone else's change starts the form from the new version.
        <ProjectForm
          key={project.data.etag ?? detail.id}
          user={session.user}
          existing={{ project: detail, etag: project.data.etag }}
        />
      ) : (
        <EmptyState
          title={t('projects.edit.notEditable', { status: t(`projects.status.${detail.status}`) })}
        >
          <Link to={`/projects/${detail.id}`}>{t('projects.edit.backToProject')}</Link>
        </EmptyState>
      )}
    </>
  );
}
