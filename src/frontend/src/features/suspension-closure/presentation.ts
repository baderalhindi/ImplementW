import { type StatusTone } from '@/shared/ui/StatusBadge.tsx';

import {
  type PostProjectObligationStatus,
  type ReadinessResult,
  type ReadinessStatus,
} from './api/types.ts';
import { type StageState } from './closeoutRules.ts';
import {
  type ActivationState,
  type ApprovalState,
  type GovernedStatus,
} from './governedRequest.ts';

// How WF-09 and WF-10 values read on screen. Each state has its own words; the colour only repeats them (WCAG 1.4.1).

const STATUS_TONES: Record<GovernedStatus, StatusTone> = {
  DRAFT: 'neutral',
  SUBMITTED: 'info',
  UNDER_REVIEW: 'info',
  RETURNED: 'warning',
  APPROVED: 'info',
  EFFECTED: 'positive',
  REJECTED: 'negative',
  WITHDRAWN: 'neutral',
};

export function statusTone(status: GovernedStatus): StatusTone {
  return STATUS_TONES[status];
}

const APPROVAL_TONES: Record<ApprovalState, StatusTone> = {
  NOT_SUBMITTED: 'neutral',
  AWAITING_REVIEW: 'info',
  IN_REVIEW: 'info',
  RETURNED: 'warning',
  APPROVED: 'positive',
  REJECTED: 'negative',
  WITHDRAWN: 'neutral',
};

export function approvalTone(state: ApprovalState): StatusTone {
  return APPROVAL_TONES[state];
}

/** Approved but not yet in effect asks for attention: the project has not moved. */
const ACTIVATION_TONES: Record<ActivationState, StatusTone> = {
  NOT_APPLICABLE: 'neutral',
  PENDING: 'warning',
  EFFECTED: 'positive',
};

export function activationTone(state: ActivationState): StatusTone {
  return ACTIVATION_TONES[state];
}

const READINESS_TONES: Record<ReadinessStatus, StatusTone> = {
  READY: 'positive',
  READY_WITH_CONDITIONS: 'info',
  NOT_READY: 'negative',
  INCOMPLETE: 'neutral',
};

export function readinessTone(status: ReadinessStatus): StatusTone {
  return READINESS_TONES[status];
}

const RESULT_TONES: Record<ReadinessResult, StatusTone> = {
  PASS: 'positive',
  FAIL: 'negative',
  WAIVED: 'info',
};

export function resultTone(result: ReadinessResult): StatusTone {
  return RESULT_TONES[result];
}

const OBLIGATION_TONES: Record<PostProjectObligationStatus, StatusTone> = {
  OPEN: 'warning',
  IN_PROGRESS: 'info',
  SATISFIED: 'positive',
  WAIVED: 'neutral',
  CANCELLED: 'neutral',
};

export function obligationTone(status: PostProjectObligationStatus): StatusTone {
  return OBLIGATION_TONES[status];
}

const STAGE_TONES: Record<StageState, StatusTone> = {
  NOT_YET: 'neutral',
  LOCKED: 'neutral',
  READY: 'info',
  IN_PROGRESS: 'info',
  DONE: 'positive',
  ON_HOLD: 'warning',
  SKIPPED: 'neutral',
};

export function stageTone(state: StageState): StatusTone {
  return STAGE_TONES[state];
}
