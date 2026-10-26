import { type StatusTone } from '@/shared/ui/StatusBadge.tsx';

import { type ChangeAuthorizationStatus, type ChangeRequestStatus } from './api/types.ts';
import { type ApprovalState, type ImplementationState } from './changeRequestRules.ts';

// How WF-08 values read on screen. Each state has its own words; the colour and shape only repeat them (WCAG 1.4.1).
// Approved and Implemented are never drawn alike (acceptance criterion 2): Approved is an outlined badge with a tick,
// Implemented a solid one with a double tick, and nothing else uses either style.

/** A badge's style: a StatusBadge tone, or one of the two styles kept for approval and implementation. */
export type ChangeBadgeStyle = StatusTone | 'approved' | 'implemented';

const STATUS_STYLES: Record<ChangeRequestStatus, ChangeBadgeStyle> = {
  DRAFT: 'neutral',
  SUBMITTED: 'info',
  UNDER_REVIEW: 'info',
  RETURNED: 'warning',
  APPROVED: 'approved',
  REJECTED: 'negative',
  IMPLEMENTATION: 'info',
  IMPLEMENTED: 'implemented',
  CLOSED: 'neutral',
  WITHDRAWN: 'neutral',
};

export function statusStyle(status: ChangeRequestStatus): ChangeBadgeStyle {
  return STATUS_STYLES[status];
}

const APPROVAL_STYLES: Record<ApprovalState, ChangeBadgeStyle> = {
  NOT_SUBMITTED: 'neutral',
  AWAITING_REVIEW: 'info',
  IN_REVIEW: 'info',
  RETURNED: 'warning',
  APPROVED: 'approved',
  REJECTED: 'negative',
  WITHDRAWN: 'neutral',
};

export function approvalStyle(state: ApprovalState): ChangeBadgeStyle {
  return APPROVAL_STYLES[state];
}

const IMPLEMENTATION_STYLES: Record<ImplementationState, ChangeBadgeStyle> = {
  NOT_APPLICABLE: 'neutral',
  NOT_STARTED: 'warning',
  IN_PROGRESS: 'info',
  IMPLEMENTED: 'implemented',
};

export function implementationStyle(state: ImplementationState): ChangeBadgeStyle {
  return IMPLEMENTATION_STYLES[state];
}

/** An issued authorisation waits for its module; an applied one is done; an ended one no longer applies. */
const AUTHORIZATION_TONES: Record<ChangeAuthorizationStatus, StatusTone> = {
  ISSUED: 'warning',
  APPLIED: 'positive',
  EXPIRED: 'neutral',
  REVOKED: 'neutral',
};

export function authorizationTone(status: ChangeAuthorizationStatus): StatusTone {
  return AUTHORIZATION_TONES[status];
}

/** A band selects a route, it is not a verdict: the lowest reads neutral, the elevated band 3 asks for attention. */
export function bandTone(bandNo: number): StatusTone {
  return bandNo >= 3 ? 'warning' : bandNo === 2 ? 'info' : 'neutral';
}
