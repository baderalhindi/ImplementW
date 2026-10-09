import { type ReactElement } from 'react';
import { Link } from 'react-router';

import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { isInternal } from '@/features/projects/access.ts';
import { type ProjectDetail } from '@/features/projects/api/types.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { canCreateRequest } from './access.ts';
import { RequestRegisterView } from './components/RequestRegisterView.tsx';
import { newExternalRequestPath } from './paths.ts';
import { isForbidden, participationProblemMessage } from './problems.ts';
import { useRequestList } from './useExternalParticipationData.ts';

/**
 * The project's update requests as a workspace tab (ADR-013 core rewrite: the external UI is a working project workspace
 * for an entity's Project Manager, not only an inbox). AHDA sees every request on the project and creates one (SCR-161)
 * while the project takes them; an entity user sees its own entity's, answered from SCR-162, and is offered no
 * origination (gate decision).
 */
export function ProjectExternalRequests({
  project,
  user,
}: {
  project: ProjectDetail;
  user: SessionUser;
}): ReactElement {
  const { t } = useI18n();
  const requests = useRequestList({ projectId: project.id });
  const internal = isInternal(user);
  const creator = canCreateRequest(user, project);

  if (requests.data === undefined) {
    if (requests.loading) {
      return <LoadingState />;
    }
    return isForbidden(requests.error) ? (
      <p className="state">{t('externalParticipation.forbidden')}</p>
    ) : (
      <ErrorState
        message={participationProblemMessage(requests.error, t)}
        onRetry={requests.reload}
      />
    );
  }

  return (
    <section className="section" aria-labelledby="project-external-requests">
      <div className="section__header">
        <h2 id="project-external-requests">
          {t(
            internal
              ? 'externalParticipation.project.internalTitle'
              : 'externalParticipation.project.externalTitle',
          )}
        </h2>
        {creator && (
          <span className="tasks__actions">
            <Link className="button button--primary" to={newExternalRequestPath(project.id)}>
              {t('externalParticipation.actions.create')}
            </Link>
          </span>
        )}
      </div>
      {requests.data.length === 0 ? (
        <EmptyState title={t('externalParticipation.project.emptyTitle')}>
          <p>
            {t(
              creator
                ? 'externalParticipation.project.emptyCreator'
                : internal
                  ? 'externalParticipation.project.emptyReader'
                  : 'externalParticipation.project.emptyEntity',
            )}
          </p>
        </EmptyState>
      ) : (
        <RequestRegisterView
          requests={requests.data}
          audience={internal ? 'internal' : 'external'}
          userId={user.id}
          showProject={false}
          caption={t('externalParticipation.project.caption')}
        />
      )}
    </section>
  );
}
