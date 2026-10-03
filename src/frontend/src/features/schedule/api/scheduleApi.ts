import { type ApprovalInstanceSummary } from '@/features/approvals/api/types.ts';
import { type Page } from '@/features/projects/api/types.ts';
import { apiRequest, type ApiResponse } from '@/shared/api/httpClient.ts';

import {
  type BaselineActivityDetail,
  type BaselineDependencyDetail,
  type ProjectBaselineDetail,
  type ProjectScheduleDetail,
  type ScheduleActivityCreateRequest,
  type ScheduleActivityDetail,
  type ScheduleActivityRequest,
  type ScheduleDependencyDetail,
  type ScheduleDependencyRequest,
  type ScheduleForecastRequest,
  type ScheduleHealthStatusDetail,
} from './types.ts';

// WF-03 (TASK-046 schedule-baseline.md §4). Every collection is one project's, `projectId` required; a project the
// caller may not see answers an empty page. Every POST and PUT is a sensitive write with an Idempotency-Key. The PUT
// of an activity requires If-Match (428 without); the commands honour it when sent.

/** R-29's largest page. A schedule is read whole: the Gantt, the cycle check and the drag rules need every row. */
export const SCHEDULE_PAGE_SIZE = 200;

async function data<T>(request: Promise<ApiResponse<T>>): Promise<T> {
  return (await request).data;
}

/** Every item of a collection, reading page after page of SCHEDULE_PAGE_SIZE until the total is reached. */
async function allItems<T>(
  path: string,
  query: Record<string, string>,
  signal?: AbortSignal,
): Promise<T[]> {
  const items: T[] = [];
  for (let page = 1; ; page += 1) {
    const result = await data(
      apiRequest<Page<T>>(path, {
        query: { ...query, page, pageSize: SCHEDULE_PAGE_SIZE },
        signal,
      }),
    );
    items.push(...result.items);
    if (result.items.length === 0 || items.length >= result.totalCount) {
      return items;
    }
  }
}

export const scheduleApi = {
  /** One item once the schedule is initialized, none before. */
  schedules: (projectId: string, signal?: AbortSignal) =>
    data(
      apiRequest<Page<ProjectScheduleDetail>>('/project-schedules', {
        query: { projectId },
        signal,
      }),
    ),
  initialize: (projectId: string) =>
    apiRequest<ProjectScheduleDetail>('/project-schedules', {
      method: 'POST',
      body: { projectId },
    }),
  /** Sort order then WBS code, cancelled activities included, each with its baseline dates and variance. */
  activities: (projectId: string, signal?: AbortSignal) =>
    allItems<ScheduleActivityDetail>('/schedule-activities', { projectId }, signal),
  /** One activity with the ETag its PUT sends back. */
  activity: (id: string, signal?: AbortSignal) =>
    apiRequest<ScheduleActivityDetail>(`/schedule-activities/${id}`, { signal }),
  createActivity: (request: ScheduleActivityCreateRequest) =>
    apiRequest<ScheduleActivityDetail>('/schedule-activities', { method: 'POST', body: request }),
  updateActivity: (id: string, request: ScheduleActivityRequest, etag: string | null) =>
    apiRequest<ScheduleActivityDetail>(`/schedule-activities/${id}`, {
      method: 'PUT',
      body: request,
      ifMatch: etag,
    }),
  cancelActivity: (id: string, etag: string | null) =>
    apiRequest<ScheduleActivityDetail>(`/schedule-activities/${id}/cancel`, {
      method: 'POST',
      body: {},
      ifMatch: etag,
    }),
  /** A live leaf's Current Forecast, once the project has an ACTIVE baseline (TASK-046 D-5). */
  reforecast: (id: string, request: ScheduleForecastRequest, etag: string | null) =>
    apiRequest<ScheduleActivityDetail>(`/schedule-activities/${id}/reforecast`, {
      method: 'POST',
      body: request,
      ifMatch: etag,
    }),
  dependencies: (projectId: string, signal?: AbortSignal) =>
    allItems<ScheduleDependencyDetail>('/schedule-dependencies', { projectId }, signal),
  createDependency: (request: ScheduleDependencyRequest) =>
    apiRequest<ScheduleDependencyDetail>('/schedule-dependencies', {
      method: 'POST',
      body: request,
    }),
  /** 204, also when it is already gone (R-40). A dependency is never edited, only removed and added again. */
  deleteDependency: (id: string) =>
    apiRequest<undefined>(`/schedule-dependencies/${id}`, { method: 'DELETE' }),
  /** Latest version first. */
  baselines: (projectId: string, signal?: AbortSignal) =>
    allItems<ProjectBaselineDetail>('/project-baselines', { projectId }, signal),
  /** A DRAFT candidate from the working schedule, the project's next version. */
  createBaseline: (projectId: string) =>
    apiRequest<ProjectBaselineDetail>('/project-baselines', {
      method: 'POST',
      body: { projectId },
    }),
  /**
   * DRAFT or RETURNED → SUBMITTED (a WF-11 run), or → ACTIVE at once when the governance profile requires no approval
   * (ADR-015). A rebaseline names the WF-08 change authorisation it implements (BR-SCH-034).
   */
  submitBaseline: (id: string, changeAuthorizationId: string | null) =>
    apiRequest<ProjectBaselineDetail>(`/project-baselines/${id}/submit`, {
      method: 'POST',
      body: { changeAuthorizationId },
    }),
  /** Only a DRAFT is deleted; 204, also when it is already gone (R-40). */
  deleteBaseline: (id: string) =>
    apiRequest<undefined>(`/project-baselines/${id}`, { method: 'DELETE' }),
  baselineActivities: (id: string, signal?: AbortSignal) =>
    allItems<BaselineActivityDetail>(`/project-baselines/${id}/baseline-activities`, {}, signal),
  baselineDependencies: (id: string, signal?: AbortSignal) =>
    allItems<BaselineDependencyDetail>(
      `/project-baselines/${id}/baseline-dependencies`,
      {},
      signal,
    ),
  /** CURRENT/LIVE Schedule Health: one item once computed, none before. */
  healthStatuses: (projectId: string, signal?: AbortSignal) =>
    data(
      apiRequest<Page<ScheduleHealthStatusDetail>>('/schedule-health-statuses', {
        query: { projectId },
        signal,
      }),
    ),
  /**
   * The WF-11 runs that reviewed a baseline, as many as the caller may see: their requester, an APPROVAL_VIEW holder,
   * or whoever holds authority over a current task (TASK-035 D-10). Newest first.
   */
  approvalRuns: (baselineId: string, signal?: AbortSignal) =>
    data(
      apiRequest<Page<ApprovalInstanceSummary>>('/approval-instances', {
        query: { subjectModule: 'Schedule', subjectType: 'ProjectBaseline', subjectId: baselineId },
        signal,
      }),
    ),
};
