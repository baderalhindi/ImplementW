import { type Translate } from '@/features/identity-access/problems.ts';
import { shortId } from '@/features/projects/presentation.ts';
import { type StatusTone } from '@/shared/ui/StatusBadge.tsx';

import {
  type ProjectBaselineDetail,
  type ProjectBaselineStatus,
  type ScheduleActivityDetail,
  type ScheduleHealth,
} from './api/types.ts';
import { type StartConstraint } from './dependencyRules.ts';

// How WF-03 values read on screen. Each state has its own word; the colour only repeats it (WCAG 1.4.1).

const BASELINE_TONES: Record<ProjectBaselineStatus, StatusTone> = {
  DRAFT: 'neutral',
  SUBMITTED: 'info',
  UNDER_REVIEW: 'info',
  RETURNED: 'warning',
  ACTIVE: 'positive',
  SUPERSEDED: 'neutral',
  REJECTED: 'negative',
  WITHDRAWN: 'neutral',
};

export function baselineTone(status: ProjectBaselineStatus): StatusTone {
  return BASELINE_TONES[status];
}

/** UNKNOWN is grey and says so: a rating without a baseline or thresholds is never shown as a colour (TASK-046 D-9). */
const HEALTH_TONES: Record<ScheduleHealth, StatusTone> = {
  GREEN: 'positive',
  AMBER: 'warning',
  RED: 'negative',
  UNKNOWN: 'neutral',
};

export function scheduleHealthTone(health: ScheduleHealth): StatusTone {
  return HEALTH_TONES[health];
}

/** Late is amber, early or on the baseline neutral: a variance is a difference of dates, not a health rating. */
export function varianceTone(days: number): StatusTone {
  return days > 0 ? 'warning' : 'neutral';
}

/**
 * A signed variance in working days, positive late (BR-SCH-032): "3 days late", "2 days early", "On the baseline".
 * Null is no baseline entry, never 0 (TASK-046 D-9).
 */
export function varianceLabel(days: number | null, t: Translate): string {
  if (days === null) {
    return t('schedule.variance.none');
  }
  if (days === 0) {
    return t('schedule.variance.onBaseline');
  }
  const magnitude = Math.abs(days);
  if (days > 0) {
    return magnitude === 1
      ? t('schedule.variance.lateOne')
      : t('schedule.variance.late', { days: magnitude });
  }
  return magnitude === 1
    ? t('schedule.variance.earlyOne')
    : t('schedule.variance.early', { days: magnitude });
}

/** An activity as a person picks it: its WBS code and name. */
export function activityLabel(activity: ScheduleActivityDetail): string {
  return `${activity.wbsCode} · ${activity.name.text}`;
}

/** An activity referenced by id, by its WBS code when it is known, else its shortened id. */
export function activityCode(
  id: string,
  byId: ReadonlyMap<string, ScheduleActivityDetail>,
): string {
  return byId.get(id)?.wbsCode ?? shortId(id);
}

export function daysLabel(days: number, t: Translate): string {
  return days === 1 ? t('schedule.days.one') : t('schedule.days.many', { days });
}

/**
 * Why a start is held back (SCR-045's drag, the reforecast form): "Cannot start before 2026-11-11: 1.2 comes first
 * (finish to start, lag 2 days)."
 */
export function constraintMessage(
  constraint: StartConstraint,
  byId: ReadonlyMap<string, ScheduleActivityDetail>,
  t: Translate,
): string {
  return t('schedule.constraint.held', {
    date: constraint.earliest,
    predecessor: activityCode(constraint.dependency.predecessorActivityId, byId),
    type: t(`schedule.dependencyTypeShort.${constraint.dependency.dependencyType}`),
    lag: daysLabel(constraint.dependency.lagDays, t),
  });
}

/** "Version 2 · revision 1 · Approved baseline", or "… · Declared baseline" for ADR-014's. */
export function baselineName(baseline: ProjectBaselineDetail, t: Translate): string {
  return t('schedule.baseline.name', {
    version: baseline.versionNo,
    revision: baseline.revisionNo,
    type: t(`schedule.baselineType.${baseline.baselineType}`),
  });
}
