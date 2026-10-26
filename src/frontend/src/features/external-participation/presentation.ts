import { type StatusTone } from '@/shared/ui/StatusBadge.tsx';

import {
  type ExternalContributionStatus,
  type ExternalUpdateRequestStatus,
  type ResponseDueCondition,
  type SourceApplicationStatus,
} from './api/types.ts';
import { type ApplicationState, type ExternalOutcome } from './rules.ts';

// How WF-13 values read on screen. Each state has its own words; the tone only repeats them (WCAG 1.4.1).

const REQUEST_TONES: Record<ExternalUpdateRequestStatus, StatusTone> = {
  DRAFT: 'neutral',
  ISSUED: 'info',
  IN_PROGRESS: 'info',
  RESPONDED: 'warning',
  CLOSED: 'positive',
  CANCELLED: 'neutral',
};

export function requestTone(status: ExternalUpdateRequestStatus): StatusTone {
  return REQUEST_TONES[status];
}

const CONTRIBUTION_TONES: Record<ExternalContributionStatus, StatusTone> = {
  DRAFT: 'neutral',
  SUBMITTED: 'info',
  UNDER_REVIEW: 'info',
  RETURNED: 'warning',
  REJECTED: 'negative',
  ACCEPTED_PENDING_APPLICATION: 'warning',
  APPLIED: 'positive',
  APPLICATION_FAILED: 'negative',
};

export function contributionTone(status: ExternalContributionStatus): StatusTone {
  return CONTRIBUTION_TONES[status];
}

const OUTCOME_TONES: Record<ExternalOutcome, StatusTone> = {
  DRAFT: 'neutral',
  SUBMITTED: 'info',
  UNDER_REVIEW: 'info',
  RETURNED: 'warning',
  REJECTED: 'negative',
  ACCEPTED: 'positive',
};

export function outcomeTone(outcome: ExternalOutcome): StatusTone {
  return OUTCOME_TONES[outcome];
}

const DUE_TONES: Record<ResponseDueCondition, StatusTone> = {
  NOT_APPLICABLE: 'neutral',
  NOT_DUE: 'neutral',
  DUE: 'warning',
  OVERDUE: 'negative',
};

export function dueTone(condition: ResponseDueCondition): StatusTone {
  return DUE_TONES[condition];
}

const ATTEMPT_TONES: Record<SourceApplicationStatus, StatusTone> = {
  APPLIED: 'positive',
  CONFLICT: 'warning',
  FAILED: 'negative',
};

export function attemptTone(status: SourceApplicationStatus): StatusTone {
  return ATTEMPT_TONES[status];
}

/**
 * SCR-166's states. A conflict and a retry are drawn in their own styles with their own marks (acceptance criterion
 * 2): a conflict is never the red of a failure, and a retryable refusal never reads as a final one.
 */
export type ApplicationStyle = 'pending' | 'conflict' | 'retry' | 'applied' | 'failed';

const APPLICATION_STYLES: Record<ApplicationState, ApplicationStyle> = {
  PENDING: 'pending',
  CONFLICT: 'conflict',
  REVALIDATED: 'retry',
  RETRYABLE: 'retry',
  APPLIED: 'applied',
  FAILED: 'failed',
};

export function applicationStyle(state: ApplicationState): ApplicationStyle {
  return APPLICATION_STYLES[state];
}
