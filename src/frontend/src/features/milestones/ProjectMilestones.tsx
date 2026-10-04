import { type ReactElement, useState } from 'react';

import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { type ProjectDetail } from '@/features/projects/api/types.ts';
import { todayUtc } from '@/features/tasks/taskRules.ts';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { canPlanMilestones } from './access.ts';
import { MilestoneTable } from './components/MilestoneTable.tsx';
import { type MilestoneDialog } from './dialogs/milestoneDialog.ts';
import { MilestoneDialogs } from './dialogs/MilestoneDialogs.tsx';
import { revisionsOf } from './milestoneRules.ts';
import { isForbidden, milestoneProblemMessage } from './problems.ts';
import { useMilestoneLookups, useProjectMilestones } from './useMilestoneData.ts';

/**
 * SCR-046 Project Milestones: the project's milestones, earliest forecast first, each with its category, its forecast
 * against the ACTIVE baseline, WF-03's status and where its claim stands — a returned claim's reason on its row. A title
 * opens MOD-019. The project's own Project Manager adds milestones (MOD-016) while the project is APPROVED_PLANNED or
 * ACTIVE.
 */
export function ProjectMilestones({
  project,
  user,
}: {
  project: ProjectDetail;
  user: SessionUser;
}): ReactElement {
  const { t } = useI18n();
  const data = useProjectMilestones(project.id);
  const lookups = useMilestoneLookups();
  const [notice, setNotice] = useState<Notice | null>(null);
  const [dialog, setDialog] = useState<MilestoneDialog | null>(null);

  if (data.data === undefined) {
    if (data.loading) {
      return <LoadingState />;
    }
    return isForbidden(data.error) ? (
      <p className="state">{t('milestones.forbidden')}</p>
    ) : (
      <ErrorState message={milestoneProblemMessage(data.error, t)} onRetry={data.reload} />
    );
  }

  const { milestones, achievements } = data.data;
  const planner = canPlanMilestones(user, project);
  const entries = milestones.map((milestone) => ({
    milestone,
    project,
    revisions: achievements === null ? null : revisionsOf(milestone.id, achievements),
  }));
  const changed = (message: TranslationKey | null, tone: Notice['tone'] = 'success') => {
    if (message !== null) {
      setNotice({ tone, message: t(message) });
    }
    data.reload();
  };

  return (
    <div className="tasks">
      <PageNotice notice={notice} />
      <section className="section" aria-labelledby="project-milestones">
        <div className="section__header">
          <h2 id="project-milestones">{t('milestones.project.title')}</h2>
          {planner && (
            <span className="tasks__actions">
              <button
                type="button"
                className="button button--primary"
                onClick={() => {
                  setDialog({ kind: 'create' });
                }}
              >
                {t('milestones.actions.create')}
              </button>
            </span>
          )}
        </div>
        {achievements === null && milestones.length > 0 && (
          <p className="form__note">{t('milestones.claimsForbidden')}</p>
        )}
        {milestones.length === 0 ? (
          <EmptyState title={t('milestones.empty.project.title')}>
            <p>
              {planner
                ? t('milestones.empty.project.planner')
                : project.status === 'ACTIVE' || project.status === 'APPROVED_PLANNED'
                  ? t('milestones.empty.project.waiting')
                  : t('milestones.empty.project.notPlanned')}
            </p>
          </EmptyState>
        ) : (
          <MilestoneTable
            caption={t('milestones.project.caption')}
            entries={entries}
            today={todayUtc()}
            showProject={false}
            categoryLabel={lookups.itemLabel}
            onOpen={({ milestone }) => {
              setDialog({ kind: 'achievement', milestoneId: milestone.id });
            }}
          />
        )}
      </section>

      <MilestoneDialogs
        dialog={dialog}
        onDialogChange={setDialog}
        project={project}
        user={user}
        lookups={lookups}
        onChanged={changed}
      />
    </div>
  );
}
