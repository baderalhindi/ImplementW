import { type ReactElement, useState } from 'react';
import { useNavigate } from 'react-router';

import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { type ProjectDetail } from '@/features/projects/api/types.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { canRegisterRisks } from './access.ts';
import { RiskRegisterView } from './components/RiskRegisterView.tsx';
import { RiskFormDialog } from './dialogs/RiskFormDialog.tsx';
import { isForbidden, riskProblemMessage } from './problems.ts';
import { matrixOf, useProjectRisks, useRiskLookups, useRiskMatrix } from './useRiskData.ts';

/**
 * SCR-080 Risk Register for one project, inside its workspace: the summary cards, the filters and the project's risks,
 * most severe first; a title opens SCR-082. The project's own Project Manager registers risks (MOD-030) while the
 * project is APPROVED_PLANNED, ACTIVE or SUSPENDED, and lands on the new risk to assess it.
 */
export function ProjectRisks({
  project,
  user,
}: {
  project: ProjectDetail;
  user: SessionUser;
}): ReactElement {
  const { t } = useI18n();
  const navigate = useNavigate();
  const risks = useProjectRisks(project.id);
  const matrixState = useRiskMatrix();
  const lookups = useRiskLookups();
  const [creating, setCreating] = useState(false);

  if (risks.data === undefined) {
    if (risks.loading) {
      return <LoadingState />;
    }
    return isForbidden(risks.error) ? (
      <p className="state">{t('risks.forbidden')}</p>
    ) : (
      <ErrorState message={riskProblemMessage(risks.error, t)} onRetry={risks.reload} />
    );
  }

  const registrar = canRegisterRisks(user, project);
  const entries = risks.data.map((risk) => ({ risk, project }));

  return (
    <div className="tasks">
      <section className="section" aria-labelledby="project-risks">
        <div className="section__header">
          <h2 id="project-risks">{t('risks.project.title')}</h2>
          {registrar && (
            <span className="tasks__actions">
              <button
                type="button"
                className="button button--primary"
                onClick={() => {
                  setCreating(true);
                }}
              >
                {t('risks.actions.create')}
              </button>
            </span>
          )}
        </div>
        {entries.length === 0 ? (
          <EmptyState title={t('risks.empty.project.title')}>
            <p>
              {registrar ? t('risks.empty.project.registrar') : t('risks.empty.project.reader')}
            </p>
          </EmptyState>
        ) : (
          <RiskRegisterView
            entries={entries}
            matrix={matrixOf(matrixState)}
            lookups={lookups}
            showProject={false}
            caption={t('risks.project.caption')}
          />
        )}
      </section>
      {creating && (
        <RiskFormDialog
          editing={null}
          project={project}
          user={user}
          lookups={lookups}
          knownUserIds={risks.data.map((risk) => risk.ownerUserId)}
          onClose={() => {
            setCreating(false);
          }}
          onStale={() => {
            setCreating(false);
            risks.reload();
          }}
          onDone={(risk) => {
            void navigate(`/projects/${project.id}/risks/${risk.id}`, {
              state: { notice: 'riskCreated' },
            });
          }}
        />
      )}
    </div>
  );
}
