import { type ReactElement, useState } from 'react';

import { useCan } from '@/features/identity-access/permissions/permissions.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import {
  type DeclaredSource,
  type ProjectDetail,
  type ProjectOverallProgress,
} from '../api/types.ts';
import { ProgressMethodDialog } from '../progressMethod/ProgressMethodDialog.tsx';

import { StagePlanDialog } from './StagePlanDialog.tsx';
import { oneDecimal } from './stageProgress.ts';

/**
 * "الإنجاز الكلي" at the top of the Overview (review round 8, point 02): the project's one overall figure as the API calculates it from its method, the
 * same figure the register, the dashboards and the cards show. Actual, planned and the gap; where the figure comes from; the date it holds for; the
 * current stage; and, on the stage method, each stage's weight, planned % today and % complete. A project with no method, or on the stage method with
 * no plan, asks whoever may set it up to do so.
 */
export function OverallProgressPanel({
  project,
  etag,
  onChanged,
}: {
  project: ProjectDetail;
  etag: string | null;
  onChanged: () => void;
}): ReactElement | null {
  const { t } = useI18n();
  const mayWrite = useCan('PROJECT_REGISTER', 'PROGRESS_SUBMIT');
  const settable = mayWrite && !['SUBMITTED', 'UNDER_REVIEW', 'CLOSED'].includes(project.status);
  const [dialog, setDialog] = useState<'method' | 'plan' | null>(null);
  const progress = project.indicators?.progress ?? null;
  if (progress === null) {
    return null;
  }
  const close = () => {
    setDialog(null);
  };
  const done = () => {
    setDialog(null);
    onChanged();
  };

  return (
    <section className="stage-progress overall-progress" aria-labelledby="overall-progress-title">
      <div className="stage-progress__header">
        <h2 id="overall-progress-title">{t('projects.overallProgress.cardTitle')}</h2>
        <p className="overall-progress__actions">
          {settable && progress.method !== null && (
            <button
              type="button"
              className="button button--quiet"
              onClick={() => {
                setDialog('method');
              }}
            >
              {t('projects.progressMethod.change')}
            </button>
          )}
          {/* While the plan is missing, the notice below offers setting it; the header does not repeat it. */}
          {progress.method === 'STAGES' && !(progress.needsSetup && settable) && (
            <button
              type="button"
              className="button"
              onClick={() => {
                setDialog('plan');
              }}
            >
              {t('projects.stageProgress.openPlan')}
            </button>
          )}
        </p>
      </div>
      {progress.needsSetup && settable && (
        <div className="notice notice--warning" role="status">
          <p>
            <strong>
              {t(
                progress.method === null
                  ? 'projects.overallProgress.setUp'
                  : 'projects.overallProgress.setUpPlanTitle',
              )}
            </strong>{' '}
            {t(
              progress.method === null
                ? 'projects.overallProgress.setUpMethod'
                : 'projects.overallProgress.setUpPlan',
            )}
          </p>
          <button
            type="button"
            className="button button--primary"
            onClick={() => {
              setDialog(progress.method === null ? 'method' : 'plan');
            }}
          >
            {t(
              progress.method === null
                ? 'projects.overallProgress.chooseMethod'
                : 'projects.stageProgress.setPlan',
            )}
          </button>
        </div>
      )}
      <Source progress={progress} declaredSource={project.followUp?.declaredSource ?? null} />
      {progress.method === 'STAGES' && progress.stages.length > 0 && !progress.needsSetup && (
        <StageBars progress={progress} />
      )}
      <ProgressMethodDialog
        open={dialog === 'method'}
        project={project}
        etag={etag}
        onClose={close}
        onChanged={done}
      />
      <StagePlanDialog
        open={dialog === 'plan'}
        project={project}
        canChange={settable}
        onClose={close}
        onSaved={done}
      />
    </section>
  );
}

/** Where the figure comes from, the date it holds for and the method: the tiles above show the figures themselves. */
function Source({
  progress,
  declaredSource,
}: {
  progress: ProjectOverallProgress;
  /** Where a declared figure came from: the import, a person, an entity update. */
  declaredSource: DeclaredSource | null;
}): ReactElement {
  const { t } = useI18n();
  const declared = progress.method === null || progress.source === 'DECLARED';
  return (
    <>
      <p className="overall-progress__source">
        <StatusBadge
          label={
            progress.method === null
              ? t('projects.progressMethod.none')
              : t(`projects.progressMethod.methods.${progress.method}`)
          }
          tone={progress.method === null ? 'warning' : 'info'}
        />
        <span>
          {t(
            declared
              ? 'projects.overallProgress.source.DECLARED'
              : progress.source === 'SCHEDULE'
                ? 'projects.overallProgress.source.SCHEDULE'
                : 'projects.overallProgress.source.STAGES',
          )}
        </span>
        {declared && declaredSource !== null && (
          <span>
            {t('projects.overallProgress.declaredFrom', {
              source: t(`projects.followUp.source.${declaredSource}`),
            })}
          </span>
        )}
        {progress.asOf !== null && (
          <span>{t('projects.overallProgress.asOf', { date: progress.asOf })}</span>
        )}
      </p>
      {progress.method === 'SCHEDULE' && progress.actualPercent === null && (
        <p className="form__note">{t('projects.overallProgress.noBaseline')}</p>
      )}
    </>
  );
}

/** Each stage's weight, planned % today and % complete, the current one marked; a stage that does not apply says so. */
function StageBars({ progress }: { progress: ProjectOverallProgress }): ReactElement {
  const { t, language } = useI18n();
  const current = progress.currentStageCode;
  return (
    <ol className="stage-progress__stages">
      {progress.stages.map((stage) => (
        <li
          key={stage.stageCode}
          className={`stage-progress__stage${stage.stageCode === current ? ' is-current' : ''}${stage.applies ? '' : ' is-skipped'}`}
          aria-current={stage.stageCode === current ? 'step' : undefined}
        >
          <p className="stage-progress__name">
            <span>{stage.name[language]}</span>
            <span className="stage-progress__weight" dir="ltr">
              {t('projects.stagePlan.weight', { weight: stage.weightPercent })}
            </span>
            {stage.stageCode === current && (
              <StatusBadge label={t('projects.stageProgress.current')} tone="info" />
            )}
          </p>
          {stage.applies ? (
            <dl className="stage-progress__figures">
              <Bar
                label={t('projects.stageProgress.planned')}
                value={stage.plannedPercent}
                tone="planned"
              />
              <Bar
                label={t('projects.stageProgress.actual')}
                value={stage.percentComplete}
                tone="actual"
              />
            </dl>
          ) : (
            <p className="form__note">{t('projects.stageProgress.notApplicable')}</p>
          )}
        </li>
      ))}
    </ol>
  );
}

function Bar({
  label,
  value,
  tone,
}: {
  label: string;
  value: number | null;
  tone: 'planned' | 'actual';
}): ReactElement {
  const { t } = useI18n();
  return (
    <div className={`stage-progress__bar stage-progress__bar--${tone}`}>
      <dt>{label}</dt>
      <dd>
        <span dir="ltr">
          {value === null ? t('projects.stageProgress.notRecorded') : `${oneDecimal(value)}%`}
        </span>
        <span className="stage-progress__track" aria-hidden="true">
          <span className="stage-progress__fill" style={{ inlineSize: `${String(value ?? 0)}%` }} />
        </span>
      </dd>
    </div>
  );
}
