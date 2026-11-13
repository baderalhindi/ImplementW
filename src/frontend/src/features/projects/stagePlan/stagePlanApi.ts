import { type BilingualLabel } from '@/features/identity-access/api/types.ts';
import { apiRequest } from '@/shared/api/httpClient.ts';

import { type NarrativeTextRequest, type Page } from '../api/types.ts';

// A project's stage plan (review round 7, item 1): the stages of the PROJECT_STAGE version in force, their weights, and the project's plan for each.
// Weights and percentages are decimals the API may write as numbers or strings (R-16).

export type Decimal = number | string;

export interface StagePlanStage {
  stageCode: string;
  name: BilingualLabel;
  sortOrder: number;
  weightPercent: Decimal;
  applies: boolean;
  plannedStartDate: string | null;
  plannedEndDate: string | null;
  startingPercent: Decimal | null;
}

/**
 * `revisionNo` is 0 until the plan is first saved. `requiresChangeAuthorization`: the registration is approved and the plan set, so a change claims an
 * approved schedule change's STAGE_PLAN_CHANGE authorisation. `acceptsStartingPercent`: the project was under way when it entered the platform.
 */
export interface ProjectStagePlan {
  projectId: string;
  revisionNo: number;
  requiresChangeAuthorization: boolean;
  acceptsStartingPercent: boolean;
  stageVersionId: string | null;
  stageVersionNo: number | null;
  stages: StagePlanStage[];
}

export interface StagePlanEntryRequest {
  stageCode: string;
  applies: boolean;
  plannedStartDate: string | null;
  plannedEndDate: string | null;
  startingPercent: number | null;
}

export interface StagePlanRequest {
  stages: StagePlanEntryRequest[];
  changeAuthorizationId: string | null;
  reason: NarrativeTextRequest | null;
}

/** Every stage of a version fits one page: a version has a handful. */
const STAGE_PAGE_SIZE = 100;

export const stagePlanApi = {
  /** The stages in force, as a new registration's plan starts; none when AHDA has published none. */
  stages: async (signal?: AbortSignal) =>
    (
      await apiRequest<Page<StagePlanStage>>('/project-stages', {
        query: { pageSize: STAGE_PAGE_SIZE },
        signal,
      })
    ).data.items,
  /** The plan, with the project's ETag. */
  get: (projectId: string, signal?: AbortSignal) =>
    apiRequest<ProjectStagePlan>(`/project-stage-plans/${projectId}`, { signal }),
  /** The whole plan, under the project's ETag. */
  update: (projectId: string, request: StagePlanRequest, etag: string | null) =>
    apiRequest<ProjectStagePlan>(`/project-stage-plans/${projectId}`, {
      method: 'PUT',
      body: request,
      ifMatch: etag,
    }),
};
