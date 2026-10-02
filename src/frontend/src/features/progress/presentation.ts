import { type StatusTone } from '@/shared/ui/StatusBadge.tsx';

import { type Decimal, type HealthStatus, type ProgressSubmissionStatus } from './api/types.ts';

// How WF-02 values read on screen. Each state has its own word; the colour only repeats it (WCAG 1.4.1).

const SUBMISSION_TONES: Record<ProgressSubmissionStatus, StatusTone> = {
  DRAFT: 'neutral',
  SUBMITTED: 'info',
  UNDER_REVIEW: 'info',
  RETURNED: 'warning',
  PUBLISHED: 'positive',
};

export function submissionTone(status: ProgressSubmissionStatus): StatusTone {
  return SUBMISSION_TONES[status];
}

/** UNKNOWN is grey and says so: a rating without its inputs is never shown as a colour (TASK-044 D-4). */
const HEALTH_TONES: Record<HealthStatus, StatusTone> = {
  GREEN: 'positive',
  AMBER: 'warning',
  RED: 'negative',
  UNKNOWN: 'neutral',
};

export function healthTone(health: HealthStatus): StatusTone {
  return HEALTH_TONES[health];
}

/** A revision still in the workflow; RETURNED and PUBLISHED are final. */
export function isInProgress(status: ProgressSubmissionStatus): boolean {
  return status === 'DRAFT' || status === 'SUBMITTED' || status === 'UNDER_REVIEW';
}

/** Four places is the API's precision; Latin digits in both languages (TASK-032 D-5). */
const PERCENT_FORMAT = new Intl.NumberFormat('en', { maximumFractionDigits: 4 });
const SCALE = 10_000;

export function toNumber(value: Decimal): number {
  return typeof value === 'number' ? value : Number(value);
}

/** `42.5%`, or null when there is no figure. */
export function formatPercent(value: Decimal | null): string | null {
  return value === null ? null : `${PERCENT_FORMAT.format(toNumber(value))}%`;
}

/**
 * Actual minus planned, in percentage points, to the API's four places: negative is behind plan. Null without a plan.
 * A difference of two figures the API published, shown beside them; it is not a health rating (M-12).
 */
export function variance(actual: Decimal | null, planned: Decimal | null): number | null {
  if (actual === null || planned === null) {
    return null;
  }
  return Math.round((toNumber(actual) - toNumber(planned)) * SCALE) / SCALE;
}

/** `+2.5 pp`, `−10 pp` or `0 pp`, with a true minus sign. */
export function formatVariance(points: number): string {
  const magnitude = PERCENT_FORMAT.format(Math.abs(points));
  const sign = points > 0 ? '+' : points < 0 ? '−' : '';
  return `${sign}${magnitude}`;
}

export function varianceTone(points: number): StatusTone {
  return points < 0 ? 'warning' : 'neutral';
}
