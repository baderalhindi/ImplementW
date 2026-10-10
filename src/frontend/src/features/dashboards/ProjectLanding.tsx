import { type ReactElement, useCallback } from 'react';
import { Link, useSearchParams } from 'react-router';

import { projectsApi } from '@/features/projects/api/projectsApi.ts';
import { ProjectStatusBadge } from '@/features/projects/components/ProjectStatusBadge.tsx';
import { languageTag } from '@/features/projects/presentation.ts';
import { pageFrom, PAGE_SIZE } from '@/shared/api/paging.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Pagination, TableContainer } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { dashboardProblemMessage } from './problems.ts';

/**
 * A landing on the Project Dashboard — DSH-008 for an external entity's person (Blueprint §20.2) — is opened for one
 * project at a time, and only inside SCR-040 Project Overview (DSH-009's one place). The Home lists the projects the
 * person may see, each opening its overview, where the dashboard renders in their scope. The list is the API's: an
 * entity's people see their own entity's projects and nothing of another's (ADR-013).
 */
export function ProjectLanding(): ReactElement {
  const { t } = useI18n();
  const [params, setParams] = useSearchParams();
  const page = pageFrom(params);
  const load = useCallback(
    (signal: AbortSignal) => projectsApi.list({ page, pageSize: PAGE_SIZE }, signal),
    [page],
  );
  const projects = useApiResource(load);

  if (projects.data === undefined) {
    return projects.loading ? (
      <LoadingState />
    ) : (
      <ErrorState message={dashboardProblemMessage(projects.error, t)} onRetry={projects.reload} />
    );
  }
  if (projects.data.totalCount === 0) {
    return <EmptyState title={t('dashboards.projectLanding.empty')} />;
  }
  return (
    <>
      <p>{t('dashboards.projectLanding.intro')}</p>
      <TableContainer caption={t('dashboards.projectLanding.caption')}>
        <thead>
          <tr>
            <th scope="col">{t('dashboards.projectLanding.project')}</th>
            <th scope="col">{t('dashboards.projectLanding.formalId')}</th>
            <th scope="col">{t('dashboards.projectLanding.status')}</th>
          </tr>
        </thead>
        <tbody>
          {projects.data.items.map((project) => (
            <tr key={project.id}>
              <td>
                <Link to={`/projects/${project.id}`} lang={languageTag(project.title.language)}>
                  {project.title.text}
                </Link>
              </td>
              <td className="cell--ltr">{project.formalProjectId ?? t('common.values.none')}</td>
              <td>
                <ProjectStatusBadge
                  status={project.status}
                  legacyIntakeDate={project.legacyIntakeDate}
                />
              </td>
            </tr>
          ))}
        </tbody>
      </TableContainer>
      <Pagination
        page={page}
        pageSize={PAGE_SIZE}
        totalCount={projects.data.totalCount}
        onPageChange={(next) => {
          const updated = new URLSearchParams(params);
          updated.set('page', String(next));
          setParams(updated);
        }}
      />
    </>
  );
}
