import { type ReactElement, useCallback, useState } from 'react';

import { useCan } from '@/features/identity-access/permissions/permissions.ts';
import { progressApi } from '@/features/progress/api/progressApi.ts';
import { type ProgressSubmissionDetail } from '@/features/progress/api/types.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { type ProjectDetail } from '../api/types.ts';
import { Detail } from '../components/Detail.tsx';

import { stagePlanApi } from './stagePlanApi.ts';
import { StagePlanDialog } from './StagePlanDialog.tsx';
import { oneDecimal, plannedPercent, todayIso, weightedOverall } from './stageProgress.ts';

/** The latest published periods hold the stage figures the Overview shows; the first page reaches the latest. */
const RECENT_PERIODS = 20;

interface StageView {
  code: string;
  name: string;
  weight: number;
  applies: boolean;
  planned: number | null;
  actual: number | null;
}

/**
 * The Overview's stage bar (review round 7, item 1): each stage's planned % today, from the stage plan, beside its % complete in the latest published
 * period, with the current stage marked; overall planned, actual and the gap between them, weighed as the API weighs them. The stage plan opens from
 * here, to read it, to set it once on a project that has none, or to change it under an approved schedule change.
 */
export function StageProgressPanel({
  project,
  onChanged,
}: {
  project: ProjectDetail;
  onChanged: () => void;
}): ReactElement | null {
  const { t, language } = useI18n();
  const canChange = useCan('PROJECT_REGISTER', 'PROGRESS_SUBMIT');
  const [open, setOpen] = useState(false);
  const load = useCallback(
    async (signal: AbortSignal) => {
      const [plan, periods] = await Promise.all([
        stagePlanApi.get(project.id, signal),
        progressApi
          .submissions(project.id, { page: 1, pageSize: RECENT_PERIODS }, signal)
          .catch(() => null),
      ]);
      return {
        plan: plan.data,
        published:
          periods?.items.find(
            (item) => item.status === 'PUBLISHED' && (item.stages?.length ?? 0) > 0,
          ) ?? null,
      };
    },
    [project.id],
  );
  const data = useApiResource(load, { keepWhileReloading: true });
  if (data.data === undefined || data.data.plan.stages.length === 0) {
    return null;
  }

  const { plan, published } = data.data;
  const today = todayIso();
  const stages: StageView[] = [...plan.stages]
    .sort((a, b) => a.sortOrder - b.sortOrder)
    .map((stage) => {
      const recorded = published?.stages?.find((entry) => entry.stageCode === stage.stageCode);
      return {
        code: stage.stageCode,
        name: stage.name[language],
        weight: Number(stage.weightPercent),
        applies: stage.applies,
        planned:
          stage.applies && stage.plannedStartDate !== null && stage.plannedEndDate !== null
            ? plannedPercent(stage.plannedStartDate, stage.plannedEndDate, today)
            : null,
        actual: recorded?.percentComplete == null ? null : Number(recorded.percentComplete),
      };
    });
  const applicable = stages.filter((stage) => stage.applies);
  const plannedOverall = weightedOverall(
    applicable.map((stage) => ({ weight: stage.weight, percent: stage.planned })),
  );
  const actualOverall = published === null ? null : Number(published.actualPercent);
  const current = published?.currentStageCode ?? null;

  return (
    <section className="stage-progress" aria-labelledby="stage-progress-title">
      <div className="stage-progress__header">
        <h2 id="stage-progress-title">{t('projects.stageProgress.title')}</h2>
        <button
          type="button"
          className="button"
          onClick={() => {
            setOpen(true);
          }}
        >
          {plan.revisionNo === 0 && canChange
            ? t('projects.stageProgress.setPlan')
            : t('projects.stageProgress.openPlan')}
        </button>
      </div>
      {plan.revisionNo === 0 ? (
        <p className="form__note">{t('projects.stageProgress.noPlan')}</p>
      ) : (
        <>
          <Overall planned={plannedOverall} actual={actualOverall} published={published} />
          <ol className="stage-progress__stages">
            {stages.map((stage) => (
              <li
                key={stage.code}
                className={`stage-progress__stage${stage.code === current ? ' is-current' : ''}${stage.applies ? '' : ' is-skipped'}`}
                aria-current={stage.code === current ? 'step' : undefined}
              >
                <p className="stage-progress__name">
                  <span>{stage.name}</span>
                  <span className="stage-progress__weight" dir="ltr">
                    {t('projects.stagePlan.weight', { weight: stage.weight })}
                  </span>
                  {stage.code === current && (
                    <StatusBadge label={t('projects.stageProgress.current')} tone="info" />
                  )}
                </p>
                {stage.applies ? (
                  <dl className="stage-progress__figures">
                    <Bar
                      label={t('projects.stageProgress.planned')}
                      value={stage.planned}
                      tone="planned"
                    />
                    <Bar
                      label={t('projects.stageProgress.actual')}
                      value={stage.actual}
                      tone="actual"
                    />
                  </dl>
                ) : (
                  <p className="form__note">{t('projects.stageProgress.notApplicable')}</p>
                )}
              </li>
            ))}
          </ol>
        </>
      )}
      <StagePlanDialog
        open={open}
        project={project}
        canChange={canChange}
        onClose={() => {
          setOpen(false);
        }}
        onSaved={() => {
          setOpen(false);
          data.reload();
          onChanged();
        }}
      />
    </section>
  );
}

function Overall({
  planned,
  actual,
  published,
}: {
  planned: number | null;
  actual: number | null;
  published: ProgressSubmissionDetail | null;
}): ReactElement {
  const { t } = useI18n();
  const none = t('projects.stageProgress.notRecorded');
  const gap = planned === null || actual === null ? null : actual - planned;
  return (
    <dl className="details stage-progress__overall">
      <Detail term={t('projects.stageProgress.plannedOverall')}>
        <span dir="ltr">{planned === null ? none : `${oneDecimal(planned)}%`}</span>
      </Detail>
      <Detail term={t('projects.stageProgress.actualOverall')}>
        <span dir="ltr">{actual === null ? none : `${oneDecimal(actual)}%`}</span>
        {published?.stageVersionNo != null && (
          <span className="details__aside">
            {t('projects.stageProgress.versionUsed', { version: published.stageVersionNo })}
          </span>
        )}
      </Detail>
      <Detail term={t('projects.stageProgress.gap')}>
        <span dir="ltr">
          {gap === null
            ? none
            : t('projects.stageProgress.points', {
                points: `${gap > 0 ? '+' : ''}${oneDecimal(gap)}`,
              })}
        </span>
      </Detail>
    </dl>
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
