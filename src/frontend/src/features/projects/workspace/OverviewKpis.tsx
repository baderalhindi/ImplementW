import { type ReactElement, type ReactNode } from 'react';

import { GaugeChart } from '@/features/dashboards/components/Charts.tsx';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { type StatusTone } from '@/shared/ui/StatusBadge.tsx';

import { type OnPlanStatus, type ProjectDetail } from '../api/types.ts';
import { GapValue, OnPlanBadge } from '../components/FollowUpBadges.tsx';
import { oneDecimal } from '../stagePlan/stageProgress.ts';

const ON_PLAN_TONES: Record<OnPlanStatus, StatusTone> = {
  ON_TRACK: 'positive',
  LATE: 'warning',
  STALLED: 'negative',
};

function Tile({
  label,
  tone,
  children,
}: {
  label: string;
  tone?: StatusTone | undefined;
  children: ReactNode;
}): ReactElement {
  return (
    <li className="home__tile overview-kpis__tile" data-tone={tone}>
      <span className="home__tile-label">{label}</span>
      {children}
    </li>
  );
}

/**
 * The Overview's headline tiles (the 15 Nov 2026 request: the Overview as a dashboard): the overall progress on a gauge with its planned figure, the
 * gap and the on-plan label, the current stage and the days to the planned end. Every figure is the API's: the overall figure by the project's
 * method (review round 8, point 02) and the register's calculated columns.
 */
export function OverviewKpis({ project }: { project: ProjectDetail }): ReactElement | null {
  const { t, language } = useI18n();
  const indicators = project.indicators;
  const progress = indicators?.progress ?? null;
  if (indicators === undefined) {
    return null;
  }
  const none = t('projects.stageProgress.notRecorded');
  const percent = (value: number | null | undefined) =>
    value === null || value === undefined ? none : `${oneDecimal(value)}%`;
  const stage =
    progress?.currentStageCode == null
      ? null
      : (progress.stages.find((entry) => entry.stageCode === progress.currentStageCode)?.name[
          language
        ] ?? progress.currentStageCode);
  const onPlan = indicators.onPlan;

  return (
    <ul className="home__tiles overview-kpis" aria-label={t('projects.overview.kpis')}>
      <Tile label={t('projects.overallProgress.title')}>
        <div className="overview-kpis__progress">
          <GaugeChart
            rings={[
              {
                key: 'actual',
                value: progress?.actualPercent ?? null,
                color: 'var(--series-actual)',
              },
              {
                key: 'planned',
                value: progress?.plannedPercent ?? null,
                color: 'var(--series-planned)',
              },
            ]}
            lead={progress?.actualPercent == null ? null : `${oneDecimal(progress.actualPercent)}%`}
          />
          <span className="overview-kpis__legend">
            <span>
              <span className="distribution__key overview-kpis__key--actual" aria-hidden="true" />
              {t('projects.overallProgress.actual')}:{' '}
              <bdi dir="ltr">{percent(progress?.actualPercent)}</bdi>
            </span>
            <span>
              <span className="distribution__key overview-kpis__key--planned" aria-hidden="true" />
              {t('projects.overallProgress.planned')}:{' '}
              <bdi dir="ltr">{percent(progress?.plannedPercent)}</bdi>
            </span>
          </span>
        </div>
      </Tile>
      <Tile
        label={t('projects.followUp.gap')}
        tone={onPlan === null ? undefined : ON_PLAN_TONES[onPlan]}
      >
        <span className="home__tile-value">
          <GapValue points={indicators.gapPoints} />
        </span>
        <span>
          <OnPlanBadge status={onPlan} />
        </span>
      </Tile>
      <Tile label={t('projects.stageProgress.current')}>
        <span className="home__tile-value overview-kpis__text">{stage ?? none}</span>
        <span className="insights__coverage">
          {progress?.method == null
            ? t('projects.progressMethod.none')
            : t(`projects.progressMethod.methods.${progress.method}`)}
        </span>
      </Tile>
      <Tile label={t('projects.followUp.daysRemaining')}>
        <span className="home__tile-value" dir="ltr">
          {indicators.daysRemaining ?? '—'}
        </span>
        <span className="insights__coverage">
          {project.plannedEndDate === null ? none : <bdi dir="ltr">{project.plannedEndDate}</bdi>}
        </span>
      </Tile>
    </ul>
  );
}
