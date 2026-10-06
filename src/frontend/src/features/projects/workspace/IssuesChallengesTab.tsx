import { type ReactElement } from 'react';
import { NavLink, useParams } from 'react-router';

import { ConcernDetail } from '@/features/issues-challenges/ConcernDetail.tsx';
import { ProjectConcerns } from '@/features/issues-challenges/ProjectConcerns.tsx';
import { useI18n } from '@/shared/i18n/i18n.ts';

import { useWorkspace } from './workspaceContext.ts';

/** SCR-083 and SCR-085 side by side under the Issues & challenges tab. */
function ConcernNav({ projectId }: { projectId: string }): ReactElement {
  const { t } = useI18n();
  return (
    <nav className="schedule-nav" aria-label={t('issuesChallenges.nav.registers')}>
      <ul>
        <li>
          <NavLink to={`/projects/${projectId}/issues-challenges`} end>
            {t('issuesChallenges.nav.issues')}
          </NavLink>
        </li>
        <li>
          <NavLink to={`/projects/${projectId}/issues-challenges/challenges`} end>
            {t('issuesChallenges.nav.challenges')}
          </NavLink>
        </li>
      </ul>
    </nav>
  );
}

/** SCR-083 Issue Register for the project, as WF-07 shows it (TASK-058), inside the workspace. */
export function IssuesTab(): ReactElement {
  const { project, user } = useWorkspace();
  return (
    <div className="tasks">
      <ConcernNav projectId={project.id} />
      <ProjectConcerns key={project.id} project={project} user={user} concernType="ISSUE" />
    </div>
  );
}

/** SCR-085 Challenge Register for the project. */
export function ChallengesTab(): ReactElement {
  const { project, user } = useWorkspace();
  return (
    <div className="tasks">
      <ConcernNav projectId={project.id} />
      <ProjectConcerns key={project.id} project={project} user={user} concernType="CHALLENGE" />
    </div>
  );
}

/** SCR-084 Issue Detail or SCR-086 Challenge Detail, at the address the escalation notification links to. */
export function ConcernDetailTab(): ReactElement {
  const { project, user } = useWorkspace();
  const { concernId = '' } = useParams();
  return <ConcernDetail key={concernId} project={project} user={user} concernId={concernId} />;
}
