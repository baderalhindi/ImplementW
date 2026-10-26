import { cyclePath as graphCyclePath } from '@/shared/graph/dependencyGraph.ts';

import {
  type ScheduleActivityDetail,
  type ScheduleDependencyDetail,
  type ScheduleDependencyType,
} from './api/types.ts';

// The dependency rules the backend applies (TASK-046 D-3, D-4: `DependencyGraph`, `ScheduleCalculation`,
// `WorkingDays`), mirrored so a person learns of a broken rule before anything is sent. The API stays the authority
// and its refusals are shown the same way. Every day is a working day until a calendar exists (TASK-046 F-3), so a
// working day is a calendar day here too.

const MS_PER_DAY = 86_400_000;

/** The longest duration and lag the API accepts, in working days (ScheduleActivityRequest.MaxDurationDays). */
export const MAX_DAYS = 9999;

/** Days since 1970-01-01 of an ISO calendar date, read as a UTC date (TASK-046 F-13). */
export function dayNumber(date: string): number {
  const [year = 0, month = 1, day = 1] = date.split('-').map(Number);
  return Math.round(Date.UTC(year, month - 1, day) / MS_PER_DAY);
}

export function fromDayNumber(days: number): string {
  return new Date(days * MS_PER_DAY).toISOString().slice(0, 10);
}

/** The working day `days` after `date`; zero is the date itself. */
export function addDays(date: string, days: number): string {
  return fromDayNumber(dayNumber(date) + days);
}

/** The working days from `start` to `finish`, both included. */
export function daySpan(start: string, finish: string): number {
  return dayNumber(finish) - dayNumber(start) + 1;
}

/** A leaf that takes part in the plan: the only kind a dependency joins or a forecast is set on (D-4, D-5). */
export function isLiveLeaf(activity: ScheduleActivityDetail): boolean {
  return activity.activityKind === 'ACTIVITY' && activity.status !== 'CANCELLED';
}

type Edge = Pick<ScheduleDependencyDetail, 'predecessorActivityId' | 'successorActivityId'>;

/**
 * The chain a new predecessor → successor dependency would close into a cycle (VAL-SCH-008): the activities from the
 * successor along existing dependencies to the predecessor, in order. Null when it closes none. The same activity at
 * both ends is a cycle of one.
 */
export function cyclePath(edges: Edge[], predecessor: string, successor: string): string[] | null {
  return graphCyclePath(
    edges.map((edge) => ({ from: edge.predecessorActivityId, to: edge.successorActivityId })),
    predecessor,
    successor,
  );
}

export interface DependencyValues {
  predecessorActivityId: string;
  successorActivityId: string;
  dependencyType: ScheduleDependencyType | '';
  lagDays: string;
}

/** Field path → API code, or a code of this screen's own: SAME_ACTIVITY, DEPENDENCY_EXISTS, CIRCULAR. */
export type DependencyCodes = Record<keyof DependencyValues, string | null>;

export interface DependencyCheck {
  codes: DependencyCodes;
  /** The chain the dependency would close, successor first; null when it closes none. */
  cycle: string[] | null;
}

/** Whole working days, 0 to MAX_DAYS: REQUIRED when blank and required, MALFORMED, or OUT_OF_RANGE. */
export function checkDays(value: string, { min, required }: { min: number; required: boolean }) {
  const trimmed = value.trim();
  if (trimmed === '') {
    return required ? 'REQUIRED' : null;
  }
  if (!/^\d+$/.test(trimmed)) {
    return 'MALFORMED';
  }
  const days = Number(trimmed);
  return days < min || days > MAX_DAYS ? 'OUT_OF_RANGE' : null;
}

/**
 * MOD-015's checks, in the API's order (D-4): both ends chosen, two distinct activities, not already linked (the
 * pair is unique whatever the type), FS/SS/FF, a lag of 0 or more, and no cycle. A cycle is refused here, before the
 * request is made (acceptance criterion 2).
 */
export function checkDependency(
  values: DependencyValues,
  dependencies: ScheduleDependencyDetail[],
): DependencyCheck {
  const { predecessorActivityId: predecessor, successorActivityId: successor } = values;
  const chosen = predecessor !== '' && successor !== '';
  const linked =
    chosen &&
    dependencies.some(
      (dependency) =>
        dependency.predecessorActivityId === predecessor &&
        dependency.successorActivityId === successor,
    );
  const cycle =
    chosen && predecessor !== successor ? cyclePath(dependencies, predecessor, successor) : null;
  return {
    codes: {
      predecessorActivityId: predecessor === '' ? 'REQUIRED' : null,
      successorActivityId:
        successor === ''
          ? 'REQUIRED'
          : predecessor === successor
            ? 'SAME_ACTIVITY'
            : linked
              ? 'DEPENDENCY_EXISTS'
              : cycle !== null
                ? 'CIRCULAR'
                : null,
      dependencyType: values.dependencyType === '' ? 'REQUIRED' : null,
      lagDays: checkDays(values.lagDays, { min: 0, required: false }),
    },
    cycle,
  };
}

/** Which dates a drag or a reforecast moves: the working plan before an ACTIVE baseline, the forecast after (D-5). */
export type DateTrack = 'plan' | 'forecast';

export interface DateRange {
  start: string;
  finish: string;
}

export function datesOf(activity: ScheduleActivityDetail, track: DateTrack): DateRange {
  return track === 'plan'
    ? { start: activity.plannedStartDate, finish: activity.plannedFinishDate }
    : { start: activity.forecastStartDate, finish: activity.forecastFinishDate };
}

export interface StartConstraint {
  /** The earliest start the activity's predecessors allow on this track. */
  earliest: string;
  /** The dependency that sets it, to explain the limit. */
  dependency: ScheduleDependencyDetail;
}

/**
 * The earliest start the predecessors allow an activity of `durationDays` (ScheduleCalculation): FS the working day
 * after the predecessor's finish, SS the predecessor's start, FF no finish before the predecessor's finish, each
 * moved by the lag. Null when nothing constrains it. Predecessors are read on the same track as the activity.
 */
export function startConstraint(
  activityId: string,
  durationDays: number,
  activities: ScheduleActivityDetail[],
  dependencies: ScheduleDependencyDetail[],
  track: DateTrack,
): StartConstraint | null {
  const byId = new Map(activities.map((activity) => [activity.id, activity]));
  let binding: StartConstraint | null = null;
  for (const dependency of dependencies) {
    const predecessor = byId.get(dependency.predecessorActivityId);
    if (dependency.successorActivityId !== activityId || predecessor === undefined) {
      continue;
    }
    if (!isLiveLeaf(predecessor)) {
      continue;
    }
    const { start, finish } = datesOf(predecessor, track);
    const earliest =
      dependency.dependencyType === 'FS'
        ? addDays(finish, 1 + dependency.lagDays)
        : dependency.dependencyType === 'SS'
          ? addDays(start, dependency.lagDays)
          : addDays(finish, dependency.lagDays + 1 - durationDays);
    if (binding === null || dayNumber(earliest) > dayNumber(binding.earliest)) {
      binding = { earliest, dependency };
    }
  }
  return binding;
}

/**
 * The live leaves whose dates on `track` start before their predecessors allow. The backend keeps the plan free of
 * these; a forecast is the planner's (D-5), so after a predecessor's forecast slips its successors can be left in
 * conflict until they are reforecast too.
 */
export function dependencyConflicts(
  activities: ScheduleActivityDetail[],
  dependencies: ScheduleDependencyDetail[],
  track: DateTrack,
): Set<string> {
  const conflicts = new Set<string>();
  for (const activity of activities.filter(isLiveLeaf)) {
    const { start, finish } = datesOf(activity, track);
    const constraint = startConstraint(
      activity.id,
      daySpan(start, finish),
      activities,
      dependencies,
      track,
    );
    if (constraint !== null && dayNumber(start) < dayNumber(constraint.earliest)) {
      conflicts.add(activity.id);
    }
  }
  return conflicts;
}

export interface MovedDates extends DateRange {
  /** Set when the move was held back at the earliest start the predecessors allow. */
  heldBy: StartConstraint | null;
}

/**
 * An activity's dates on `track` moved by `days` (the drag of SCR-045), its duration kept. A start before the
 * earliest its predecessors allow is held at that earliest start, so a drag can never break a dependency rule.
 */
export function moveDates(
  activity: ScheduleActivityDetail,
  days: number,
  activities: ScheduleActivityDetail[],
  dependencies: ScheduleDependencyDetail[],
  track: DateTrack,
): MovedDates {
  const { start, finish } = datesOf(activity, track);
  const duration = daySpan(start, finish);
  const constraint = startConstraint(activity.id, duration, activities, dependencies, track);
  const wanted = addDays(start, days);
  const held = constraint !== null && dayNumber(wanted) < dayNumber(constraint.earliest);
  const moved = held ? constraint.earliest : wanted;
  return { start: moved, finish: addDays(moved, duration - 1), heldBy: held ? constraint : null };
}

/** The project's finish on `track`: the latest finish of a live leaf, as ScheduleCalculation defines it; null without one. */
export function latestFinish(
  activities: ScheduleActivityDetail[],
  track: DateTrack,
): string | null {
  const days = activities
    .filter(isLiveLeaf)
    .map((activity) => dayNumber(datesOf(activity, track).finish));
  return days.length === 0 ? null : fromDayNumber(Math.max(...days));
}
