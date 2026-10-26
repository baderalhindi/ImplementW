import { useCallback } from 'react';

import { useApiResource } from '@/shared/api/useApiResource.ts';

import { CYCLE_PAGE_SIZE, progressApi } from './api/progressApi.ts';
import { type ReportingCycleSummary } from './api/types.ts';

/**
 * The project's reporting periods by id, so each revision can name its period. A revision carries only the period's
 * id; the periods are read once, in one page (F-3 of progress-ui.md for a project with more). Null when unreadable.
 */
export function usePeriods(projectId: string): (id: string) => ReportingCycleSummary | null {
  const load = useCallback(
    (signal: AbortSignal) => progressApi.cycles(projectId, { pageSize: CYCLE_PAGE_SIZE }, signal),
    [projectId],
  );
  const cycles = useApiResource(load).data?.items ?? [];
  return (id) => cycles.find((cycle) => cycle.id === id) ?? null;
}
