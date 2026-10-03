import { useCallback } from 'react';

import { type ApiResource, useApiResource } from '@/shared/api/useApiResource.ts';

import { scheduleApi } from './api/scheduleApi.ts';
import {
  type ProjectBaselineDetail,
  type ProjectScheduleDetail,
  type ScheduleActivityDetail,
  type ScheduleDependencyDetail,
  type ScheduleHealthStatusDetail,
} from './api/types.ts';

/** A project's schedule as the three screens read it: whole, so every rule and the Gantt see every row. */
export interface ScheduleView {
  /** Null until the schedule is initialized. */
  schedule: ProjectScheduleDetail | null;
  activities: ScheduleActivityDetail[];
  dependencies: ScheduleDependencyDetail[];
  /** Latest version first. */
  baselines: ProjectBaselineDetail[];
  /** CURRENT/LIVE Schedule Health; null until computed. */
  health: ScheduleHealthStatusDetail | null;
}

export function useScheduleView(projectId: string): ApiResource<ScheduleView> {
  const load = useCallback(
    async (signal: AbortSignal): Promise<ScheduleView> => {
      const [schedules, activities, dependencies, baselines, health] = await Promise.all([
        scheduleApi.schedules(projectId, signal),
        scheduleApi.activities(projectId, signal),
        scheduleApi.dependencies(projectId, signal),
        scheduleApi.baselines(projectId, signal),
        scheduleApi.healthStatuses(projectId, signal),
      ]);
      return {
        schedule: schedules.items[0] ?? null,
        activities,
        dependencies,
        baselines,
        health: health.items[0] ?? null,
      };
    },
    [projectId],
  );
  return useApiResource(load);
}

/** The ACTIVE baseline the schedule names, if any. */
export function activeBaselineOf(view: ScheduleView): ProjectBaselineDetail | null {
  const id = view.schedule?.activeBaselineId ?? null;
  return id === null ? null : (view.baselines.find((baseline) => baseline.id === id) ?? null);
}
