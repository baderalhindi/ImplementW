import { type StatusTone } from '@/shared/ui/StatusBadge.tsx';

import { type ConcernEscalationStatus, type ConcernStatus } from './api/types.ts';

// How WF-07 values read on screen. Each state has its own word; the colour only repeats it (WCAG 1.4.1). A severity's
// colour is its overall impact level's step (concernRules.ts severityStep), never one of these tones.

const STATUS_TONES: Record<ConcernStatus, StatusTone> = {
  OPEN: 'neutral',
  ASSIGNED: 'info',
  IN_PROGRESS: 'info',
  PENDING_VALIDATION: 'info',
  RESOLVED: 'positive',
  CLOSED: 'neutral',
};

export function concernStatusTone(status: ConcernStatus): StatusTone {
  return STATUS_TONES[status];
}

/** An open escalation asks for attention; an ended one does not. */
const ESCALATION_TONES: Record<ConcernEscalationStatus, StatusTone> = {
  OPEN: 'warning',
  RESOLVED: 'positive',
  WITHDRAWN: 'neutral',
};

export function escalationStatusTone(status: ConcernEscalationStatus): StatusTone {
  return ESCALATION_TONES[status];
}
