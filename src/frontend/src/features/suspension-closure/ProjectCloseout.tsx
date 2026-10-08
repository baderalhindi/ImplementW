import { type ReactElement, useState } from 'react';
import { Link } from 'react-router';

import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { type ProjectDetail } from '@/features/projects/api/types.ts';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { canRaiseCase, closeoutStagesOf, obligationCaseOf } from './closeoutRules.ts';
import { CloseoutRegisterView } from './components/CloseoutRegisterView.tsx';
import { CloseoutStages } from './components/CloseoutStages.tsx';
import { ObligationsPanel } from './components/ObligationsPanel.tsx';
import { newCloseoutCasePath } from './paths.ts';
import { isForbidden, suspensionClosureProblemMessage } from './problems.ts';
import { type CloseoutEntry, useProjectCloseout } from './useSuspensionClosureData.ts';

/**
 * SCR-111 for one project, inside its workspace: the closeout as two sequential stages, each with its own case and its
 * own action (acceptance criterion 1), every case of the project, and its post-project obligations.
 */
export function ProjectCloseout({
  project,
  user,
}: {
  project: ProjectDetail;
  user: SessionUser;
}): ReactElement {
  const { t } = useI18n();
  const closeout = useProjectCloseout(project.id);
  const [notice, setNotice] = useState<Notice | null>(null);

  if (closeout.data === undefined) {
    if (closeout.loading) {
      return <LoadingState />;
    }
    return isForbidden(closeout.error) ? (
      <p className="state">{t('suspensionClosure.closeout.forbidden')}</p>
    ) : (
      <ErrorState
        message={suspensionClosureProblemMessage(closeout.error, t)}
        onRetry={closeout.reload}
      />
    );
  }

  const { completions, closures, obligations } = closeout.data;
  const stages = closeoutStagesOf(project.status, completions, closures);
  const ref = { id: project.id, title: project.title, status: project.status };
  const entries: CloseoutEntry[] = [
    ...completions.map((closeoutCase) => ({
      stage: 'completion' as const,
      closeoutCase,
      project: ref,
    })),
    ...closures.map((closeoutCase) => ({ stage: 'closure' as const, closeoutCase, project: ref })),
  ].sort((a, b) => b.closeoutCase.updatedAt.localeCompare(a.closeoutCase.updatedAt));

  const changed = (message: TranslationKey, tone: Notice['tone'] = 'success') => {
    setNotice({ tone, message: t(message) });
    closeout.reload();
  };

  return (
    <div className="project-closeout">
      <PageNotice notice={notice} />
      <CloseoutStages
        stages={stages}
        actions={{
          completion: canRaiseCase(user, project, stages, 'completion') && (
            <Link
              className="button button--primary"
              to={newCloseoutCasePath(project.id, 'completion')}
            >
              {t('suspensionClosure.actions.raise.completion')}
            </Link>
          ),
          closure: canRaiseCase(user, project, stages, 'closure') && (
            <Link
              className="button button--primary"
              to={newCloseoutCasePath(project.id, 'closure')}
            >
              {t(
                stages.terminal
                  ? 'suspensionClosure.actions.raise.terminalClosure'
                  : 'suspensionClosure.actions.raise.closure',
              )}
            </Link>
          ),
        }}
      />

      <section className="section" aria-labelledby="project-closeout-cases">
        <h2 id="project-closeout-cases">{t('suspensionClosure.closeout.projectTitle')}</h2>
        {entries.length === 0 ? (
          <EmptyState title={t('suspensionClosure.closeout.emptyTitle')}>
            <p>{t('suspensionClosure.closeout.emptyProject')}</p>
          </EmptyState>
        ) : (
          <CloseoutRegisterView
            entries={entries}
            showProject={false}
            caption={t('suspensionClosure.closeout.projectCaption')}
          />
        )}
      </section>

      <ObligationsPanel
        obligations={obligations}
        user={user}
        project={project}
        target={obligationCaseOf(stages)}
        onChanged={changed}
      />
    </div>
  );
}
