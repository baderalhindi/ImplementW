import { type ReactElement } from 'react';
import { Link } from 'react-router';

import { Detail } from '@/features/projects/components/Detail.tsx';
import { useI18n } from '@/shared/i18n/i18n.ts';

import { candidateOf, isPlanFrozen } from '../access.ts';
import { latestFinish } from '../dependencyRules.ts';
import { baselineName } from '../presentation.ts';
import { activeBaselineOf, type ScheduleView } from '../useScheduleView.ts';

import { BaselineStatusBadge, ScheduleHealthBadge, Variance } from './ScheduleBadges.tsx';

/**
 * Where the schedule stands: the live Schedule Health as WF-03 stored it (never recalculated here, TASK-046 D-17), the
 * Approved Baseline against the Current Forecast finish, and the candidate on its way, with the freeze it brings.
 */
export function ScheduleSummary({
  view,
  projectId,
}: {
  view: ScheduleView;
  projectId: string;
}): ReactElement {
  const { t, formatDateTime } = useI18n();
  const active = activeBaselineOf(view);
  const candidate = candidateOf(view.baselines);
  const finish = latestFinish(view.activities, 'forecast');

  return (
    <section className="section schedule-summary" aria-labelledby="schedule-summary">
      <h2 id="schedule-summary" className="visually-hidden">
        {t('schedule.summary.title')}
      </h2>
      <dl className="details">
        <Detail term={t('schedule.summary.health')}>
          {view.health === null ? (
            t('schedule.summary.healthNone')
          ) : (
            <span className="figure-group">
              <ScheduleHealthBadge health={view.health.scheduleHealth} />
              <Variance days={view.health.finishVarianceDays} />
              <span className="details__aside">
                {t('schedule.summary.computedAt', { at: formatDateTime(view.health.computedAt) })}
              </span>
            </span>
          )}
        </Detail>
        <Detail term={t('schedule.legend.baseline')}>
          {active === null ? (
            t('schedule.summary.noActiveBaseline')
          ) : (
            <>
              {t('schedule.summary.baselineFinish', {
                name: baselineName(active, t),
                date: active.baselineFinishDate,
              })}
              {active.activatedAt !== null && (
                <span className="details__aside">
                  {t('schedule.summary.activatedAt', { at: formatDateTime(active.activatedAt) })}
                </span>
              )}
            </>
          )}
        </Detail>
        <Detail term={t('schedule.legend.forecast')}>
          {finish === null
            ? t('schedule.summary.forecastNone')
            : t('schedule.summary.forecastFinish', { date: finish })}
        </Detail>
        {candidate !== null && (
          <Detail term={t('schedule.summary.candidate')}>
            <span className="figure-group">
              <BaselineStatusBadge baseline={candidate} />
              <Link to={`/projects/${projectId}/schedule/baselines?baselineId=${candidate.id}`}>
                {baselineName(candidate, t)}
              </Link>
            </span>
          </Detail>
        )}
      </dl>
      {active === null && <p className="form__note">{t('schedule.summary.forecastFollowsPlan')}</p>}
      {isPlanFrozen(view.baselines) && (
        <p className="notice notice--warning" role="status">
          {t('schedule.summary.frozen')}
        </p>
      )}
    </section>
  );
}
