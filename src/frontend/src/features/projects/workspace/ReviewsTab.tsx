import { type ReactElement, useCallback } from 'react';
import { Link } from 'react-router';

import { instanceTone } from '@/features/approvals/presentation.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { TableContainer } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { projectsApi } from '../api/projectsApi.ts';
import { isForbidden, projectProblemMessage } from '../problems.ts';

import { useWorkspace } from './workspaceContext.ts';

/**
 * SCR-042 Review History: each WF-11 run that reviewed a revision of the registration (TASK-041 D-3, D-6), newest
 * first, each opening its full history (SCR-115). Reading a run needs APPROVAL_VIEW over it (TASK-035 D-10).
 */
export function ReviewsTab(): ReactElement {
  const { t, formatDateTime } = useI18n();
  const { project } = useWorkspace();
  const load = useCallback(
    (signal: AbortSignal) => projectsApi.reviews(project.id, signal),
    [project.id],
  );
  const reviews = useApiResource(load);

  if (reviews.loading) {
    return <LoadingState />;
  }
  if (reviews.error !== null) {
    return isForbidden(reviews.error) ? (
      <p className="state">{t('projects.reviews.forbidden')}</p>
    ) : (
      <ErrorState message={projectProblemMessage(reviews.error, t)} onRetry={reviews.reload} />
    );
  }
  const runs = reviews.data?.items ?? [];
  if (runs.length === 0) {
    return <EmptyState title={t('projects.reviews.empty')} />;
  }
  return (
    <TableContainer caption={t('projects.reviews.caption')}>
      <thead>
        <tr>
          <th scope="col">{t('projects.fields.revision')}</th>
          <th scope="col">{t('projects.reviews.requestedAt')}</th>
          <th scope="col">{t('projects.reviews.outcome')}</th>
          <th scope="col">{t('projects.reviews.completedAt')}</th>
        </tr>
      </thead>
      <tbody>
        {runs.map((run) => (
          <tr key={run.id}>
            <td>
              <Link to={`/approvals/instances/${run.id}`}>
                {t('projects.reviews.revision', { revision: run.subject.revisionNo })}
              </Link>
            </td>
            <td>{formatDateTime(run.requestedAt)}</td>
            <td>
              <StatusBadge
                label={t(`approvals.instanceStatus.${run.status}`)}
                tone={instanceTone(run.status)}
              />
            </td>
            <td>
              {run.completedAt === null ? t('common.values.none') : formatDateTime(run.completedAt)}
            </td>
          </tr>
        ))}
      </tbody>
    </TableContainer>
  );
}
