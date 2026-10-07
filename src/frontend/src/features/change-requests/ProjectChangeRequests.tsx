import { type ReactElement } from 'react';
import { Link } from 'react-router';

import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { type ProjectDetail } from '@/features/projects/api/types.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { canRaiseChangeRequests } from './access.ts';
import { ChangeRequestRegisterView } from './components/ChangeRequestRegisterView.tsx';
import { newChangeRequestPath } from './paths.ts';
import { changeRequestProblemMessage, isForbidden } from './problems.ts';
import { useProjectChangeRequests } from './useChangeRequestData.ts';

/**
 * SCR-105 Change Requests for one project, inside its workspace: the project's requests, most recently changed first,
 * each opening SCR-107. The project's Project Manager raises one (SCR-106) while the project is APPROVED_PLANNED, ACTIVE
 * or SUSPENDED.
 */
export function ProjectChangeRequests({
  project,
  user,
}: {
  project: ProjectDetail;
  user: SessionUser;
}): ReactElement {
  const { t } = useI18n();
  const requests = useProjectChangeRequests(project.id);

  if (requests.data === undefined) {
    if (requests.loading) {
      return <LoadingState />;
    }
    return isForbidden(requests.error) ? (
      <p className="state">{t('changeRequests.forbidden')}</p>
    ) : (
      <ErrorState
        message={changeRequestProblemMessage(requests.error, t)}
        onRetry={requests.reload}
      />
    );
  }

  const raiser = canRaiseChangeRequests(user, project);
  const entries = requests.data.map((request) => ({ request, project }));

  return (
    <section className="section" aria-labelledby="project-change-requests">
      <div className="section__header">
        <h2 id="project-change-requests">{t('changeRequests.list.projectTitle')}</h2>
        {raiser && (
          <span className="tasks__actions">
            <Link className="button button--primary" to={newChangeRequestPath(project.id)}>
              {t('changeRequests.actions.raise')}
            </Link>
          </span>
        )}
      </div>
      {entries.length === 0 ? (
        <EmptyState title={t('changeRequests.list.emptyTitle')}>
          <p>{t(raiser ? 'changeRequests.list.emptyRaiser' : 'changeRequests.list.emptyReader')}</p>
        </EmptyState>
      ) : (
        <ChangeRequestRegisterView
          entries={entries}
          showProject={false}
          caption={t('changeRequests.list.projectCaption')}
        />
      )}
    </section>
  );
}
