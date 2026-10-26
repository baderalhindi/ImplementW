import {
  type CSSProperties,
  type KeyboardEvent,
  type PointerEvent,
  type ReactElement,
  useId,
  useState,
} from 'react';

import { DATE_LOCALES, useI18n } from '@/shared/i18n/i18n.ts';

import { type ScheduleActivityDetail, type ScheduleDependencyDetail } from '../api/types.ts';
import {
  type DateTrack,
  datesOf,
  dayNumber,
  isLiveLeaf,
  type MovedDates,
  moveDates,
} from '../dependencyRules.ts';
import {
  activityTree,
  barPosition,
  daysForDistance,
  ticksOf,
  type Timeline,
  timelineOf,
} from '../gantt.ts';
import { saveMove } from '../moveActivity.ts';
import { activityLabel, constraintMessage, varianceLabel } from '../presentation.ts';

/** Days of room before the first and after the last date, so a bar can be dragged past the ends a little. */
const TIMELINE_MARGIN_DAYS = 7;
const WEEK = 7;

interface Move {
  activityId: string;
  days: number;
  /** Set while a pointer drags: where it started and how wide the track was, to turn pixels into days. */
  pointer: { startX: number; trackWidth: number } | null;
}

interface GanttChartProps {
  activities: ScheduleActivityDetail[];
  dependencies: ScheduleDependencyDetail[];
  /** What a drag moves: the plan before an ACTIVE baseline, the forecast after (D-5). */
  track: DateTrack;
  /** Whether bars may be dragged at all: the person plans this schedule, and the plan is not frozen. */
  draggable: boolean;
  /** Activities marked as starting before their predecessors allow. */
  conflicts: Set<string>;
  onSaved: (activity: ScheduleActivityDetail, dates: MovedDates) => void;
  onFailed: (error: unknown) => void;
}

function barStyle(timeline: Timeline, start: string, finish: string): CSSProperties {
  const { offset, width } = barPosition(timeline, start, finish);
  return { insetInlineStart: `${String(offset)}%`, inlineSize: `${String(width)}%` };
}

/**
 * SCR-045's chart: one row per activity in work-breakdown order, its Current Forecast as a solid bar and, where the
 * ACTIVE baseline has it, its Approved Baseline as a hatched bar beneath (acceptance criterion 1). Every position is a
 * share of the timeline, so the chart fits the width it is given and the page never scrolls sideways (criterion 3).
 * A live leaf's bar can be dragged, or moved with the arrow keys and saved with Enter; its start is held at the
 * earliest its predecessors allow, and the person is told why.
 */
export function GanttChart({
  activities,
  dependencies,
  track,
  draggable,
  conflicts,
  onSaved,
  onFailed,
}: GanttChartProps): ReactElement {
  const { t, language, direction } = useI18n();
  const instructionsId = useId();
  const [move, setMove] = useState<Move | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [saving, setSaving] = useState<string | null>(null);

  const live = activities.filter((activity) => activity.status !== 'CANCELLED');
  const rows = activityTree(live);
  const byId = new Map(activities.map((activity) => [activity.id, activity]));
  const ranges = live.flatMap((activity) => [
    { start: activity.forecastStartDate, finish: activity.forecastFinishDate },
    ...(activity.baselineStartDate !== null && activity.baselineFinishDate !== null
      ? [{ start: activity.baselineStartDate, finish: activity.baselineFinishDate }]
      : []),
  ]);
  const span = timelineOf(ranges);
  if (span === null) {
    return <p className="state">{t('schedule.gantt.empty')}</p>;
  }
  const timeline: Timeline = {
    first: span.first - TIMELINE_MARGIN_DAYS,
    days: span.days + 2 * TIMELINE_MARGIN_DAYS,
  };
  const tickFormat = new Intl.DateTimeFormat(DATE_LOCALES[language], {
    ...(timeline.days <= 70 ? { day: '2-digit' } : { year: 'numeric' }),
    month: 'short',
    timeZone: 'UTC',
  });
  const sign = direction === 'rtl' ? -1 : 1;

  const movedOf = (activity: ScheduleActivityDetail, days: number) =>
    moveDates(activity, days, activities, dependencies, track);

  const describe = (moved: MovedDates): string =>
    moved.heldBy === null
      ? t('schedule.gantt.moving', { start: moved.start, finish: moved.finish })
      : constraintMessage(moved.heldBy, byId, t);

  const update = (activity: ScheduleActivityDetail, next: Move) => {
    setMove(next);
    setMessage(describe(movedOf(activity, next.days)));
  };

  const commit = async (activity: ScheduleActivityDetail, days: number) => {
    const moved = movedOf(activity, days);
    setMove(null);
    if (moved.start === datesOf(activity, track).start) {
      setMessage(
        moved.heldBy === null
          ? null
          : `${constraintMessage(moved.heldBy, byId, t)} ${t('schedule.gantt.unchanged')}`,
      );
      return;
    }
    setSaving(activity.id);
    setMessage(t('common.states.saving'));
    try {
      await saveMove(activity, moved, track);
      setMessage(null);
      onSaved(activity, moved);
    } catch (error) {
      setMessage(null);
      onFailed(error);
    } finally {
      setSaving(null);
    }
  };

  const onPointerDown = (event: PointerEvent<HTMLElement>, activity: ScheduleActivityDetail) => {
    if (saving !== null || event.button !== 0) {
      return;
    }
    event.preventDefault();
    const trackWidth = event.currentTarget.parentElement?.getBoundingClientRect().width ?? 0;
    event.currentTarget.setPointerCapture(event.pointerId);
    event.currentTarget.focus();
    update(activity, {
      activityId: activity.id,
      days: 0,
      pointer: { startX: event.clientX, trackWidth },
    });
  };

  const onPointerMove = (event: PointerEvent<HTMLElement>, activity: ScheduleActivityDetail) => {
    if (move?.activityId !== activity.id || move.pointer === null) {
      return;
    }
    const days = daysForDistance(
      sign * (event.clientX - move.pointer.startX),
      move.pointer.trackWidth,
      timeline,
    );
    if (days !== move.days) {
      update(activity, { ...move, days });
    }
  };

  const onPointerUp = (activity: ScheduleActivityDetail) => {
    if (move?.activityId === activity.id && move.pointer !== null) {
      void commit(activity, move.days);
    }
  };

  const onKeyDown = (event: KeyboardEvent<HTMLElement>, activity: ScheduleActivityDetail) => {
    if (saving !== null) {
      return;
    }
    const current = move?.activityId === activity.id ? move.days : 0;
    // The timeline mirrors in Arabic: the right arrow moves a bar to the right, which is earlier there.
    const steps: Partial<Record<string, number>> = {
      ArrowRight: sign,
      ArrowLeft: -sign,
      PageDown: WEEK,
      PageUp: -WEEK,
    };
    const step = steps[event.key];
    if (step !== undefined) {
      event.preventDefault();
      update(activity, { activityId: activity.id, days: current + step, pointer: null });
    } else if (event.key === 'Enter' && move?.activityId === activity.id) {
      event.preventDefault();
      void commit(activity, current);
    } else if (event.key === 'Escape' && move?.activityId === activity.id) {
      event.preventDefault();
      setMove(null);
      setMessage(t('schedule.gantt.cancelled'));
    }
  };

  return (
    <div className="gantt">
      {draggable && (
        <p id={instructionsId} className="form__note">
          {t(
            track === 'plan'
              ? 'schedule.gantt.instructionsPlan'
              : 'schedule.gantt.instructionsForecast',
          )}
        </p>
      )}
      <p className="gantt__status" role="status">
        {message}
      </p>
      <div className="gantt__chart">
        <div className="gantt__header" aria-hidden="true">
          <span className="gantt__label">{t('schedule.gantt.activity')}</span>
          <span className="gantt__axis">
            {ticksOf(timeline).map((tick) => (
              <span
                key={tick.date}
                className="gantt__tick"
                style={{ insetInlineStart: `${String(tick.offset)}%` }}
              >
                {tickFormat.format(new Date(dayNumber(tick.date) * 86_400_000))}
              </span>
            ))}
          </span>
        </div>
        <ol className="gantt__rows">
          {rows.map(({ activity, depth }) => {
            const leaf = isLiveLeaf(activity);
            const moving = move?.activityId === activity.id;
            const shown = moving ? movedOf(activity, move.days) : datesOf(activity, 'forecast');
            const conflict = conflicts.has(activity.id);
            const label = activityLabel(activity);
            const hasBaseline =
              activity.baselineStartDate !== null && activity.baselineFinishDate !== null;
            const summary = [
              t('schedule.gantt.forecastRange', {
                start: activity.forecastStartDate,
                finish: activity.forecastFinishDate,
              }),
              activity.baselineStartDate !== null && activity.baselineFinishDate !== null
                ? t('schedule.gantt.baselineRange', {
                    start: activity.baselineStartDate,
                    finish: activity.baselineFinishDate,
                  })
                : t('schedule.activities.notBaselined'),
              varianceLabel(activity.finishVarianceDays, t),
              ...(conflict ? [t('schedule.legend.conflict')] : []),
            ].join('. ');
            const barClass = [
              'gantt__bar',
              leaf ? 'gantt__bar--forecast' : 'gantt__bar--summary',
              conflict ? 'gantt__bar--conflict' : '',
              moving ? 'gantt__bar--moving' : '',
            ]
              .filter((name) => name !== '')
              .join(' ');
            return (
              <li key={activity.id} className="gantt__row">
                <span
                  className="gantt__label"
                  style={{ paddingInlineStart: `${String(depth * 1.25)}rem` }}
                >
                  <span className="gantt__code" dir="ltr">
                    {activity.wbsCode}
                  </span>{' '}
                  <span
                    className={leaf ? 'gantt__name' : 'gantt__name gantt__name--summary'}
                    dir="auto"
                  >
                    {activity.name.text}
                  </span>
                  <span className="visually-hidden">{`. ${summary}.`}</span>
                </span>
                <span className="gantt__track">
                  {hasBaseline && (
                    <span
                      className="gantt__bar gantt__bar--baseline"
                      style={barStyle(
                        timeline,
                        activity.baselineStartDate ?? '',
                        activity.baselineFinishDate ?? '',
                      )}
                      aria-hidden="true"
                      data-testid={`baseline-bar-${activity.wbsCode}`}
                    />
                  )}
                  {leaf && draggable ? (
                    <span
                      className={barClass}
                      style={barStyle(timeline, shown.start, shown.finish)}
                      role="slider"
                      tabIndex={0}
                      aria-label={t('schedule.gantt.moveLabel', { activity: label })}
                      aria-describedby={instructionsId}
                      aria-valuemin={timeline.first}
                      aria-valuemax={timeline.first + timeline.days - 1}
                      aria-valuenow={dayNumber(shown.start)}
                      aria-valuetext={t('schedule.gantt.forecastRange', {
                        start: shown.start,
                        finish: shown.finish,
                      })}
                      aria-busy={saving === activity.id}
                      data-testid={`forecast-bar-${activity.wbsCode}`}
                      onPointerDown={(event) => {
                        onPointerDown(event, activity);
                      }}
                      onPointerMove={(event) => {
                        onPointerMove(event, activity);
                      }}
                      onPointerUp={() => {
                        onPointerUp(activity);
                      }}
                      onPointerCancel={() => {
                        setMove(null);
                      }}
                      onKeyDown={(event) => {
                        onKeyDown(event, activity);
                      }}
                      onBlur={() => {
                        if (moving && move.pointer === null) {
                          setMove(null);
                        }
                      }}
                    />
                  ) : (
                    <span
                      className={barClass}
                      style={barStyle(timeline, shown.start, shown.finish)}
                      aria-hidden="true"
                      data-testid={`forecast-bar-${activity.wbsCode}`}
                    />
                  )}
                </span>
              </li>
            );
          })}
        </ol>
      </div>
    </div>
  );
}
