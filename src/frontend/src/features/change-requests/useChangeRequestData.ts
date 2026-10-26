import { useCallback } from 'react';

import {
  type ApprovalInboxItem,
  type ApprovalInstanceSummary,
} from '@/features/approvals/api/types.ts';
import { findInboxTask, unlessForbidden } from '@/features/approvals/sourceReview.ts';
import { projectsApi } from '@/features/projects/api/projectsApi.ts';
import { type ProjectDetail, type ProjectSummary } from '@/features/projects/api/types.ts';
import { ApiError, type ApiResponse } from '@/shared/api/httpClient.ts';
import { readInBatches } from '@/shared/api/paging.ts';
import { type ApiResource, useApiResource } from '@/shared/api/useApiResource.ts';

import { changeRequestSubject } from './access.ts';
import { changeRequestsApi } from './api/changeRequestsApi.ts';
import { type ChangeRequestDetail } from './api/types.ts';

/** A change request with the project it belongs to, as a list across projects shows it. */
export interface ChangeRequestEntry {
  request: ChangeRequestDetail;
  project: Pick<ProjectSummary, 'id' | 'title'>;
}

/** A project's change requests. They stay on screen while read again, so a dialog over them is not unmounted. */
export function useProjectChangeRequests(projectId: string): ApiResource<ChangeRequestDetail[]> {
  const load = useCallback(
    (signal: AbortSignal) => changeRequestsApi.list(projectId, signal),
    [projectId],
  );
  return useApiResource(load, { keepWhileReloading: true });
}

/** The project states a change request can exist in: raised from APPROVED_PLANNED (TASK-060 D-12) and kept after. */
const CHANGE_REQUEST_PROJECT_STATUSES = 'APPROVED_PLANNED,ACTIVE,SUSPENDED,COMPLETED,CLOSED';

/**
 * The change requests across the projects the caller may see, most recently changed first. The API lists them one
 * project at a time (`projectId` is required, TASK-060 D-14), so the projects are read first, then each one's requests;
 * a project whose requests the caller may not see answers an empty page.
 */
async function readPortfolio(signal: AbortSignal): Promise<ChangeRequestEntry[]> {
  const projects = await projectsApi.listAll({ status: CHANGE_REQUEST_PROJECT_STATUSES }, signal);
  const entries = await readInBatches(projects, async (project: ProjectSummary) =>
    (await changeRequestsApi.list(project.id, signal)).map((request) => ({ request, project })),
  );
  return entries.sort((a, b) => b.request.updatedAt.localeCompare(a.request.updatedAt));
}

/** SCR-105 across projects. */
export function useChangeRequestPortfolio(): ApiResource<ChangeRequestEntry[]> {
  return useApiResource(readPortfolio, { keepWhileReloading: true });
}

/** One request with its ETag; kept while read again, so the dialog that changed it stays mounted until it closes. */
export function useChangeRequestRecord(
  changeRequestId: string,
): ApiResource<ApiResponse<ChangeRequestDetail>> {
  const load = useCallback(
    (signal: AbortSignal) => changeRequestsApi.get(changeRequestId, signal),
    [changeRequestId],
  );
  return useApiResource(load, { keepWhileReloading: true });
}

/** The project, or null when the person may not read it: refused (403) or not visible to them (404, R-47). */
async function readProjectIfVisible(
  projectId: string,
  signal: AbortSignal,
): Promise<ProjectDetail | null> {
  try {
    return (await projectsApi.get(projectId, signal)).data;
  } catch (error) {
    if (error instanceof ApiError && (error.status === 403 || error.status === 404)) {
      return null;
    }
    throw error;
  }
}

/**
 * The request's project, or null when the person may not read it. The Department Manager who reviews a request holds no
 * PROJECT_VIEW (TASK-058 F-1), and on the shipped grants a Project Manager cannot read their own project (TASK-061 F-1),
 * so SCR-106 and SCR-107 work without it: what they may do is the WF-08 API's answer.
 */
export function useOptionalProject(projectId: string | null): ApiResource<ProjectDetail | null> {
  const load = useCallback(
    async (signal: AbortSignal) =>
      projectId === null ? null : readProjectIfVisible(projectId, signal),
    [projectId],
  );
  return useApiResource(load);
}

/** The request's WF-11 review as far as the person may see it. */
export interface ChangeRequestReview {
  /** The inbox task deciding the revision under review, when the person may decide it now (APPROVAL_DECIDE). */
  task: ApprovalInboxItem | null;
  /** The runs that reviewed it, newest first; null when the person may not read runs (APPROVAL_VIEW, TASK-060 F-12). */
  runs: ApprovalInstanceSummary[] | null;
}

const NO_REVIEW: ChangeRequestReview = { task: null, runs: [] };

/** A run exists once a review has started: from UNDER_REVIEW on, or for any revision after the first. */
function hasRuns(request: Pick<ChangeRequestDetail, 'status' | 'revisionNo'>): boolean {
  return (request.status !== 'DRAFT' && request.status !== 'SUBMITTED') || request.revisionNo > 1;
}

/**
 * SCR-107's part in WF-11 (the reused MOD-040–042 and MOD-044): the person's inbox task for the revision under review,
 * read only while it is under review, and the runs, read only once there are any.
 */
export function useChangeRequestReview(
  request: ChangeRequestDetail | undefined,
): ApiResource<ChangeRequestReview> {
  const id = request?.id ?? null;
  const status = request?.status ?? 'DRAFT';
  const revisionNo = request?.revisionNo ?? 1;
  const load = useCallback(
    async (signal: AbortSignal): Promise<ChangeRequestReview> => {
      if (id === null || !hasRuns({ status, revisionNo })) {
        return NO_REVIEW;
      }
      const [task, runs] = await Promise.all([
        status === 'UNDER_REVIEW'
          ? unlessForbidden(findInboxTask(changeRequestSubject({ id, revisionNo }), signal))
          : Promise.resolve(null),
        unlessForbidden(changeRequestsApi.approvalRuns(id, signal)),
      ]);
      return { task, runs };
    },
    [id, status, revisionNo],
  );
  return useApiResource(load);
}
