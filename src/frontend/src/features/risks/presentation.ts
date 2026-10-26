import { type StatusTone } from '@/shared/ui/StatusBadge.tsx';

import {
  type RiskAcceptanceStatus,
  type RiskStatus,
  type RiskTreatmentActionStatus,
} from './api/types.ts';

// How risk values read on screen. Each state has its own word; the colour only repeats it (WCAG 1.4.1). A rating's
// colour is the heat-map's severity step (riskRules.ts severityStep), never one of these tones.

const STATUS_TONES: Record<RiskStatus, StatusTone> = {
  IDENTIFIED: 'neutral',
  ASSESSED: 'info',
  TREATMENT: 'warning',
  MONITORING: 'info',
  CLOSED: 'neutral',
};

export function riskStatusTone(status: RiskStatus): StatusTone {
  return STATUS_TONES[status];
}

const ACTION_TONES: Record<RiskTreatmentActionStatus, StatusTone> = {
  PLANNED: 'neutral',
  IN_PROGRESS: 'info',
  COMPLETED: 'positive',
  CANCELLED: 'neutral',
};

export function actionStatusTone(status: RiskTreatmentActionStatus): StatusTone {
  return ACTION_TONES[status];
}

const ACCEPTANCE_TONES: Record<RiskAcceptanceStatus, StatusTone> = {
  ACTIVE: 'info',
  EXPIRED: 'neutral',
  REVOKED: 'neutral',
};

export function acceptanceStatusTone(status: RiskAcceptanceStatus): StatusTone {
  return ACCEPTANCE_TONES[status];
}
