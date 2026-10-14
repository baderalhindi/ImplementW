import { type Language } from '@/shared/i18n/i18n.ts';
import { type StatusTone } from '@/shared/ui/StatusBadge.tsx';

import {
  type BilingualLabel,
  type IntegrationAvailability,
  type IntegrationDefinitionStatus,
  type IntegrationFreshness,
  type InvocationStatus,
  type OperationalAlertSeverity,
  type OperationalAlertStatus,
  type SyncRunStatus,
} from './api/types.ts';

// How FG-05's states read. UNKNOWN is never green (Blueprint: "Unknown ≠ Green"), and activation is a status of its
// own, never a health: a disabled integration is neutral, not a failure (FG-05 §5.2).

export function availabilityTone(availability: IntegrationAvailability): StatusTone {
  switch (availability) {
    case 'UP':
      return 'positive';
    case 'DOWN':
      return 'negative';
    case 'UNKNOWN':
      return 'neutral';
  }
}

export function freshnessTone(freshness: IntegrationFreshness): StatusTone {
  switch (freshness) {
    case 'FRESH':
      return 'positive';
    case 'STALE':
      return 'warning';
    case 'UNKNOWN':
      return 'neutral';
  }
}

export function activationTone(status: IntegrationDefinitionStatus): StatusTone {
  return status === 'ACTIVE' ? 'info' : 'neutral';
}

export function invocationTone(status: InvocationStatus): StatusTone {
  switch (status) {
    case 'SUCCEEDED':
      return 'positive';
    case 'IN_PROGRESS':
    case 'RETRY_SCHEDULED':
      return 'info';
    case 'FAILED':
      return 'warning';
    case 'DEAD_LETTER':
      return 'negative';
  }
}

export function syncRunTone(status: SyncRunStatus): StatusTone {
  switch (status) {
    case 'SUCCEEDED':
      return 'positive';
    case 'RUNNING':
      return 'info';
    case 'PARTIAL':
      return 'warning';
    case 'FAILED':
      return 'negative';
  }
}

export function severityTone(severity: OperationalAlertSeverity): StatusTone {
  switch (severity) {
    case 'CRITICAL':
      return 'negative';
    case 'WARNING':
      return 'warning';
    case 'INFO':
      return 'info';
  }
}

export function alertStatusTone(status: OperationalAlertStatus): StatusTone {
  switch (status) {
    case 'OPEN':
      return 'negative';
    case 'ACKNOWLEDGED':
      return 'warning';
    case 'RESOLVED':
      return 'positive';
    case 'DISMISSED':
      return 'neutral';
  }
}

export function labelIn(label: BilingualLabel, language: Language): string {
  return language === 'ar' ? label.ar : label.en;
}

const PERCENT_FORMAT = new Intl.NumberFormat('en', {
  style: 'percent',
  maximumFractionDigits: 1,
});
const MILLISECONDS_FORMAT = new Intl.NumberFormat('en', { maximumFractionDigits: 0 });

/** A rate the API computed (0 to 1), as a percentage; formatting only, nothing is computed here. */
export function formatRate(rate: number): string {
  return PERCENT_FORMAT.format(rate);
}

export function formatMilliseconds(milliseconds: number): string {
  return `${MILLISECONDS_FORMAT.format(milliseconds)} ms`;
}
