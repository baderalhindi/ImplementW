import { type StatusTone } from '@/shared/ui/StatusBadge.tsx';

import { type DocumentStatus, type ScanState } from './api/types.ts';

// How WF-12 states read on screen. Each scan state has its own tone and its own word; the word carries the meaning and
// the colour only repeats it (WCAG 1.4.1).

export const DOCUMENT_STATUSES: DocumentStatus[] = ['ACTIVE', 'ARCHIVED'];

const SCAN_TONES: Record<ScanState, StatusTone> = {
  SCAN_PENDING: 'neutral',
  CLEAN: 'positive',
  QUARANTINED: 'negative',
  SCAN_FAILED: 'warning',
};

export function scanTone(state: ScanState): StatusTone {
  return SCAN_TONES[state];
}

export function documentTone(status: DocumentStatus): StatusTone {
  return status === 'ACTIVE' ? 'positive' : 'neutral';
}

/**
 * Whether a version's content may be used: downloaded, or chosen as an attachment or as evidence. Only CLEAN content
 * leaves the store (TASK-037 D-4, D-12); the API refuses the rest with 409 DOCUMENT_NOT_AVAILABLE, and the screens
 * disable those actions before anyone tries.
 */
export function isUsable(state: ScanState | null): boolean {
  return state === 'CLEAN';
}

/** The API writes a text's language as `EN`/`AR`; the `lang` attribute wants `en`/`ar`. */
export function languageTag(language: string): string {
  return language.toLowerCase();
}

/** NarrativeTextRequest.TextLength on the API: a title or description. */
export const TEXT_LENGTH = 2000;

const SHORT_ID_LENGTH = 8;

/** A record another module owns, by the start of its id, while no module names it (TASK-038 F-2). */
export function shortId(id: string): string {
  return id.slice(0, SHORT_ID_LENGTH);
}

/** The checksum shown, shortened; the full value is in the element's title for comparison. */
export function shortChecksum(checksum: string): string {
  return checksum.slice(0, 12);
}

const BYTE_UNITS = ['B', 'KB', 'MB', 'GB'] as const;

/** Sizes in binary units with one decimal above bytes, e.g. "2.4 MB"; Latin digits in both languages (TASK-032 D-5). */
export function formatFileSize(bytes: number): string {
  let value = bytes;
  let unit = 0;
  while (value >= 1024 && unit < BYTE_UNITS.length - 1) {
    value /= 1024;
    unit += 1;
  }
  return unit === 0 ? `${String(value)} B` : `${value.toFixed(1)} ${BYTE_UNITS[unit] ?? ''}`;
}
