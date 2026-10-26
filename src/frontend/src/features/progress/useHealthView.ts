import { useCallback } from 'react';

import { type ApiResource, useApiResource } from '@/shared/api/useApiResource.ts';

import { progressApi } from './api/progressApi.ts';
import {
  type ProjectHealthStatusDetail,
  type PublishedProgressSnapshotDetail,
} from './api/types.ts';

/** Overall Project Health in its two semantic states, never merged (M-12, TASK-044 D-13). Either is null until it exists. */
export interface HealthView {
  official: PublishedProgressSnapshotDetail | null;
  live: ProjectHealthStatusDetail | null;
}

/** The latest PUBLISHED/OFFICIAL snapshot and the CURRENT/LIVE value, read side by side. */
export function useHealthView(projectId: string): ApiResource<HealthView> {
  const load = useCallback(
    async (signal: AbortSignal): Promise<HealthView> => {
      const [snapshots, live] = await Promise.all([
        progressApi.snapshots(projectId, { pageSize: 1 }, signal),
        progressApi.healthStatuses(projectId, signal),
      ]);
      return { official: snapshots.items[0] ?? null, live: live.items[0] ?? null };
    },
    [projectId],
  );
  return useApiResource(load);
}
