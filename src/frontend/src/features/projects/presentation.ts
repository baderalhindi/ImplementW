import { type StatusTone } from '@/shared/ui/StatusBadge.tsx';

import { type ProjectStatus } from './api/types.ts';

// How WF-01 states read on screen. Each state has its own word; the colour only repeats it (WCAG 1.4.1).

/** Every state, in lifecycle order: the Register's status filter offers them all. */
export const PROJECT_STATUSES: ProjectStatus[] = [
  'DRAFT',
  'SUBMITTED',
  'UNDER_REVIEW',
  'RETURNED',
  'APPROVED_PLANNED',
  'ACTIVE',
  'SUSPENDED',
  'COMPLETED',
  'CLOSED',
];

const STATUS_TONES: Record<ProjectStatus, StatusTone> = {
  DRAFT: 'neutral',
  SUBMITTED: 'info',
  UNDER_REVIEW: 'info',
  RETURNED: 'warning',
  APPROVED_PLANNED: 'positive',
  ACTIVE: 'positive',
  SUSPENDED: 'warning',
  COMPLETED: 'neutral',
  CLOSED: 'neutral',
};

export function statusTone(status: ProjectStatus): StatusTone {
  return STATUS_TONES[status];
}

export function isProjectStatus(value: string): value is ProjectStatus {
  return (PROJECT_STATUSES as string[]).includes(value);
}

const SHORT_ID_LENGTH = 8;

export function shortId(id: string): string {
  return id.slice(0, SHORT_ID_LENGTH);
}

/** The API writes a text's language as `EN`/`AR`; the `lang` attribute wants `en`/`ar`. */
export function languageTag(language: string): string {
  return language.toLowerCase();
}

/** A SAR amount with grouping, Latin digits in both languages (TASK-032 D-5); the API's string is exact. */
export function formatSar(amount: string): string {
  const [whole = '0', fraction = '00'] = amount.split('.');
  const negative = whole.startsWith('-');
  const digits = negative ? whole.slice(1) : whole;
  return `${negative ? '-' : ''}${digits.replace(/\B(?=(\d{3})+(?!\d))/g, ',')}.${fraction}`;
}
