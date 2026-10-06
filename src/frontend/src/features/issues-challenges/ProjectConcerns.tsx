import { type ReactElement, useState } from 'react';
import { useNavigate } from 'react-router';

import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { type ProjectDetail } from '@/features/projects/api/types.ts';
import { isForbidden } from '@/features/risks/problems.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { canRaiseConcerns } from './access.ts';
import { type ConcernType } from './api/types.ts';
import { ConcernRegisterView } from './components/ConcernRegisterView.tsx';
import { ConcernFormDialog } from './dialogs/ConcernFormDialog.tsx';
import { concernPath } from './paths.ts';
import { concernProblemMessage } from './problems.ts';
import { useConcernLookups, useProjectConcerns } from './useConcernData.ts';

/** The words of each register: SCR-083 lists issues, SCR-085 challenges. */
const REGISTER_TEXT = {
  ISSUE: {
    title: 'issuesChallenges.issues.projectTitle',
    caption: 'issuesChallenges.issues.projectCaption',
    create: 'issuesChallenges.actions.createIssue',
    emptyTitle: 'issuesChallenges.issues.emptyTitle',
    emptyRaiser: 'issuesChallenges.issues.emptyRaiser',
    emptyReader: 'issuesChallenges.issues.emptyReader',
  },
  CHALLENGE: {
    title: 'issuesChallenges.challenges.projectTitle',
    caption: 'issuesChallenges.challenges.projectCaption',
    create: 'issuesChallenges.actions.createChallenge',
    emptyTitle: 'issuesChallenges.challenges.emptyTitle',
    emptyRaiser: 'issuesChallenges.challenges.emptyRaiser',
    emptyReader: 'issuesChallenges.challenges.emptyReader',
  },
} as const;

/**
 * SCR-083 Issue Register or SCR-085 Challenge Register for one project, inside its workspace: the summary cards, the
 * filters and the project's concerns of that type, most severe first; a title opens SCR-084 or SCR-086. The Project
 * Manager and the delivering entity raise them (MOD-036, MOD-038) while the project is APPROVED_PLANNED, ACTIVE or
 * SUSPENDED, and land on the new one.
 */
export function ProjectConcerns({
  project,
  user,
  concernType,
}: {
  project: ProjectDetail;
  user: SessionUser;
  concernType: ConcernType;
}): ReactElement {
  const { t } = useI18n();
  const navigate = useNavigate();
  const concerns = useProjectConcerns(project.id, concernType);
  const lookups = useConcernLookups();
  const [creating, setCreating] = useState(false);
  const text = REGISTER_TEXT[concernType];

  if (concerns.data === undefined) {
    if (concerns.loading) {
      return <LoadingState />;
    }
    return isForbidden(concerns.error) ? (
      <p className="state">{t('issuesChallenges.forbidden')}</p>
    ) : (
      <ErrorState message={concernProblemMessage(concerns.error, t)} onRetry={concerns.reload} />
    );
  }

  const raiser = canRaiseConcerns(user, project);
  const entries = concerns.data.map((concern) => ({ concern, project }));
  const headingId = `project-${concernType.toLowerCase()}s`;

  return (
    <section className="section" aria-labelledby={headingId}>
      <div className="section__header">
        <h2 id={headingId}>{t(text.title)}</h2>
        {raiser && (
          <span className="tasks__actions">
            <button
              type="button"
              className="button button--primary"
              onClick={() => {
                setCreating(true);
              }}
            >
              {t(text.create)}
            </button>
          </span>
        )}
      </div>
      {entries.length === 0 ? (
        <EmptyState title={t(text.emptyTitle)}>
          <p>{t(raiser ? text.emptyRaiser : text.emptyReader)}</p>
        </EmptyState>
      ) : (
        <ConcernRegisterView
          entries={entries}
          lookups={lookups}
          userId={user.id}
          showProject={false}
          caption={t(text.caption)}
        />
      )}
      {creating && (
        <ConcernFormDialog
          editing={null}
          concernType={concernType}
          project={project}
          lookups={lookups}
          onClose={() => {
            setCreating(false);
          }}
          onStale={() => {
            setCreating(false);
            concerns.reload();
          }}
          onDone={(concern) => {
            void navigate(concernPath(project.id, concern.id), {
              state: { notice: 'concernRaised' },
            });
          }}
        />
      )}
    </section>
  );
}
