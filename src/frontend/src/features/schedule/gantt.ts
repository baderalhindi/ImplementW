import { type ScheduleActivityDetail } from './api/types.ts';
import { dayNumber, fromDayNumber } from './dependencyRules.ts';

// The Gantt's geometry (SCR-045). Positions are shares of the timeline in per cent, never pixels, so the chart always
// fits the width it is given: on a 1366 px laptop the page never scrolls sideways (acceptance criterion 3).

export interface ActivityRow {
  activity: ScheduleActivityDetail;
  /** 0 for a top-level activity; one more per parent above it. */
  depth: number;
}

/**
 * The work breakdown depth first: each activity followed by its children, siblings in the order the API lists them
 * (sort order, then WBS code). An activity whose parent is not in the list is shown at the top level.
 */
export function activityTree(activities: ScheduleActivityDetail[]): ActivityRow[] {
  const ids = new Set(activities.map((activity) => activity.id));
  const children = new Map<string | null, ScheduleActivityDetail[]>();
  for (const activity of activities) {
    const parent =
      activity.parentActivityId !== null && ids.has(activity.parentActivityId)
        ? activity.parentActivityId
        : null;
    children.set(parent, [...(children.get(parent) ?? []), activity]);
  }
  const rows: ActivityRow[] = [];
  const visit = (parent: string | null, depth: number) => {
    for (const activity of children.get(parent) ?? []) {
      rows.push({ activity, depth });
      visit(activity.id, depth + 1);
    }
  };
  visit(null, 0);
  return rows;
}

/** The activity and every activity under it: none of them may become its parent (VAL-SCH-003). */
export function descendantsOf(
  activityId: string,
  activities: ScheduleActivityDetail[],
): Set<string> {
  const found = new Set([activityId]);
  let grew = true;
  while (grew) {
    grew = false;
    for (const activity of activities) {
      if (
        activity.parentActivityId !== null &&
        found.has(activity.parentActivityId) &&
        !found.has(activity.id)
      ) {
        found.add(activity.id);
        grew = true;
      }
    }
  }
  return found;
}

export interface Timeline {
  /** Day number of the first day shown. */
  first: number;
  /** Days shown, the first and last included. */
  days: number;
}

/** The days from the earliest start to the latest finish of the given ranges; null when there are none. */
export function timelineOf(ranges: { start: string; finish: string }[]): Timeline | null {
  if (ranges.length === 0) {
    return null;
  }
  const first = Math.min(...ranges.map((range) => dayNumber(range.start)));
  const last = Math.max(...ranges.map((range) => dayNumber(range.finish)));
  return { first, days: last - first + 1 };
}

export interface BarPosition {
  /** Per cent of the timeline before the bar, from its inline start. */
  offset: number;
  /** Per cent of the timeline the bar covers, both ends included. */
  width: number;
}

export function barPosition(timeline: Timeline, start: string, finish: string): BarPosition {
  const offset = dayNumber(start) - timeline.first;
  const length = dayNumber(finish) - dayNumber(start) + 1;
  return { offset: (offset / timeline.days) * 100, width: (length / timeline.days) * 100 };
}

/** Days moved by a pointer travelling `distance` pixels across a track `trackWidth` pixels wide. */
export function daysForDistance(distance: number, trackWidth: number, timeline: Timeline): number {
  return trackWidth <= 0 ? 0 : Math.round((distance / trackWidth) * timeline.days);
}

export interface Tick {
  date: string;
  offset: number;
}

/** At most this many labelled ticks, so their labels never collide on a laptop-width chart. */
export const MAX_TICKS = 12;
const WEEKLY_UNTIL_DAYS = 70;
/** A label starting past this share of the timeline would be cut off at its end, so none is placed there. */
const LAST_TICK_OFFSET = 92;

/**
 * Where the axis is labelled: every seventh day on a short timeline, otherwise the first of each month, thinned to at
 * most MAX_TICKS evenly spaced ones, none so near the end that its label would be cut off.
 */
export function ticksOf(timeline: Timeline): Tick[] {
  const dates: number[] = [];
  const last = timeline.first + timeline.days - 1;
  if (timeline.days <= WEEKLY_UNTIL_DAYS) {
    for (let day = timeline.first; day <= last; day += 7) {
      dates.push(day);
    }
  } else {
    const start = new Date(timeline.first * 86_400_000);
    for (let month = 0; ; month += 1) {
      const day = Math.round(
        Date.UTC(start.getUTCFullYear(), start.getUTCMonth() + month, 1) / 86_400_000,
      );
      if (day > last) {
        break;
      }
      if (day >= timeline.first) {
        dates.push(day);
      }
    }
  }
  const step = Math.ceil(dates.length / MAX_TICKS);
  return dates
    .filter((_, index) => index % step === 0)
    .map((day) => ({
      date: fromDayNumber(day),
      offset: ((day - timeline.first) / timeline.days) * 100,
    }))
    .filter((tick) => tick.offset <= LAST_TICK_OFFSET);
}
