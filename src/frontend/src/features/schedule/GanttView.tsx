import { type ReactElement, useState } from 'react';

import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { type ProjectDetail } from '@/features/projects/api/types.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
import { EmptyState, ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';

import { canEditSchedule, isPlanFrozen } from './access.ts';
import { GanttChart } from './components/GanttChart.tsx';
import { GanttLegend } from './components/GanttLegend.tsx';
import { ScheduleNav } from './components/ScheduleNav.tsx';
import { ScheduleSummary } from './components/ScheduleSummary.tsx';
import { type DateTrack, dependencyConflicts } from './dependencyRules.ts';
import { isForbidden, isStale, scheduleProblemMessage } from './problems.ts';
import { activeBaselineOf, useScheduleView } from './useScheduleView.ts';

/**
 * SCR-045 Gantt View: the schedule against time, the Approved Baseline and the Current Forecast told apart by the legend
 * and by shape (acceptance criterion 1). The project's Project Manager moves an activity by dragging its bar: the plan
 * before a baseline is active, unless a candidate is under review; the forecast after (D-5). A move never breaks a
 * dependency rule (dependencyRules.ts).
 */
export function GanttView({
  project,
  user,
}: {
  project: ProjectDetail;
  user: SessionUser;
}): ReactElement {
  const { t } = useI18n();
  const view = useScheduleView(project.id);
  const [notice, setNotice] = useState<Notice | null>(null);
  const [failure, setFailure] = useState<string | null>(null);

  if (view.loading) {
    return <LoadingState />;
  }
  if (view.data === undefined) {
    return isForbidden(view.error) ? (
      <p className="state">{t('schedule.forbidden')}</p>
    ) : (
      <ErrorState message={scheduleProblemMessage(view.error, t)} onRetry={view.reload} />
    );
  }

  const data = view.data;
  const active = activeBaselineOf(data);
  const track: DateTrack = data.schedule?.activeBaselineId == null ? 'plan' : 'forecast';
  const editor = canEditSchedule(user, project);
  const frozen = track === 'plan' && isPlanFrozen(data.baselines);
  const conflicts =
    track === 'forecast'
      ? dependencyConflicts(data.activities, data.dependencies, 'forecast')
      : new Set<string>();

  return (
    <div className="schedule">
      <ScheduleNav projectId={project.id} />
      <PageNotice notice={notice} />
      <FormAlert message={failure} />
      {data.schedule === null ? (
        <EmptyState title={t('schedule.notInitialized.title')} />
      ) : (
        <>
          <ScheduleSummary view={data} projectId={project.id} />
          <section className="section" aria-labelledby="schedule-gantt">
            <h2 id="schedule-gantt">{t('schedule.gantt.title')}</h2>
            <GanttLegend active={active} conflicts={conflicts.size > 0} />
            {editor && frozen && <p className="form__note">{t('schedule.gantt.frozen')}</p>}
            <GanttChart
              activities={data.activities}
              dependencies={data.dependencies}
              track={track}
              draggable={editor && !frozen}
              conflicts={conflicts}
              onSaved={(activity, dates) => {
                setFailure(null);
                setNotice({
                  tone: 'success',
                  message: t(
                    track === 'plan' ? 'schedule.done.planMoved' : 'schedule.done.forecastMoved',
                    {
                      activity: activity.wbsCode,
                      start: dates.start,
                      finish: dates.finish,
                    },
                  ),
                });
                view.reload();
              }}
              onFailed={(error) => {
                if (isStale(error)) {
                  setFailure(null);
                  setNotice({ tone: 'warning', message: t('schedule.done.stale') });
                  view.reload();
                } else {
                  setNotice(null);
                  setFailure(scheduleProblemMessage(error, t));
                }
              }}
            />
          </section>
        </>
      )}
    </div>
  );
}
