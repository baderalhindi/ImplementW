import { useCallback } from 'react';

import {
  type ApprovalInboxItem,
  type ApprovalInstanceSummary,
  type ApprovalSubject,
} from '@/features/approvals/api/types.ts';
import { findInboxTask, unlessForbidden } from '@/features/approvals/sourceReview.ts';
import { projectsApi } from '@/features/projects/api/projectsApi.ts';
import { type ProjectSummary } from '@/features/projects/api/types.ts';
import { type ApiResponse } from '@/shared/api/httpClient.ts';
import { readAllPages, readInBatches } from '@/shared/api/paging.ts';
import { type ApiResource, useApiResource } from '@/shared/api/useApiResource.ts';

import {
  closeoutApi,
  type CloseoutCaseOf,
  type CloseoutStage,
  obligationsApi,
} from './api/closeoutApi.ts';
import { suspensionApi } from './api/suspensionApi.ts';
import {
  type ActiveSuspensionDetail,
  type ClosureCaseDetail,
  type CompletionCaseDetail,
  type PostProjectObligationDetail,
  type SuspensionRequestDetail,
} from './api/types.ts';
import { type GovernedStatus, hasReviewRuns } from './governedRequest.ts';

/** The project states WF-09 and WF-10 records exist in: raised from ACTIVE and kept after. */
const GOVERNED_PROJECT_STATUSES = 'ACTIVE,SUSPENDED,COMPLETED,CLOSED';

type ProjectRef = Pick<ProjectSummary, 'id' | 'title' | 'status'>;

/**
 * Every record of the projects the caller may see. The APIs list one project at a time (`projectId` is required), so
 * the projects are read first, then each one's records, six at a time; a project whose records the caller may not see
 * answers an empty page.
 */
async function readAcrossProjects<R>(
  read: (project: ProjectRef) => Promise<R[]>,
  signal: AbortSignal,
): Promise<R[]> {
  const projects = await projectsApi.listAll({ status: GOVERNED_PROJECT_STATUSES }, signal);
  return readInBatches(projects, read);
}

// ---------------------------------------------------------------------------------------------------------------------
// WF-09

export interface ProjectSuspension {
  requests: SuspensionRequestDetail[];
  periods: ActiveSuspensionDetail[];
}

/** A project's requests and suspension periods; kept while read again, so a dialog over them stays mounted. */
export function useProjectSuspension(projectId: string): ApiResource<ProjectSuspension> {
  const load = useCallback(
    async (signal: AbortSignal) => {
      const [requests, periods] = await Promise.all([
        suspensionApi.list(projectId, signal),
        suspensionApi.periods(projectId, signal),
      ]);
      return { requests, periods };
    },
    [projectId],
  );
  return useApiResource(load, { keepWhileReloading: true });
}

export interface SuspensionEntry {
  request: SuspensionRequestDetail;
  project: ProjectRef;
}

async function readSuspensionPortfolio(signal: AbortSignal): Promise<SuspensionEntry[]> {
  const entries = await readAcrossProjects(
    async (project) =>
      (await suspensionApi.list(project.id, signal)).map((request) => ({ request, project })),
    signal,
  );
  return entries.sort((a, b) => b.request.updatedAt.localeCompare(a.request.updatedAt));
}

/** SCR-108 across projects, most recently changed first. */
export function useSuspensionPortfolio(): ApiResource<SuspensionEntry[]> {
  return useApiResource(readSuspensionPortfolio, { keepWhileReloading: true });
}

export function useSuspensionRecord(id: string): ApiResource<ApiResponse<SuspensionRequestDetail>> {
  const load = useCallback((signal: AbortSignal) => suspensionApi.get(id, signal), [id]);
  return useApiResource(load, { keepWhileReloading: true });
}

/** The project's other requests, to link a suspension to its resumption; empty when they cannot be read. */
export function useSiblingRequests(
  projectId: string | null,
): ApiResource<SuspensionRequestDetail[]> {
  const load = useCallback(
    async (signal: AbortSignal) =>
      projectId === null
        ? []
        : ((await unlessForbidden(suspensionApi.list(projectId, signal))) ?? []),
    [projectId],
  );
  return useApiResource(load, { keepWhileReloading: true });
}

// ---------------------------------------------------------------------------------------------------------------------
// WF-10

export interface ProjectCloseout {
  completions: CompletionCaseDetail[];
  closures: ClosureCaseDetail[];
  obligations: PostProjectObligationDetail[];
}

async function readProjectCloseout(
  projectId: string,
  signal: AbortSignal,
): Promise<ProjectCloseout> {
  const [completions, closures, obligations] = await Promise.all([
    closeoutApi.list('completion', projectId, signal),
    closeoutApi.list('closure', projectId, signal),
    obligationsApi.list(projectId, signal),
  ]);
  return { completions, closures, obligations };
}

/** A project's cases of both stages and its obligations. */
export function useProjectCloseout(projectId: string | null): ApiResource<ProjectCloseout> {
  const load = useCallback(
    (signal: AbortSignal) =>
      projectId === null
        ? Promise.resolve({ completions: [], closures: [], obligations: [] })
        : readProjectCloseout(projectId, signal),
    [projectId],
  );
  return useApiResource(load, { keepWhileReloading: true });
}

export type CloseoutEntry =
  | { stage: 'completion'; closeoutCase: CompletionCaseDetail; project: ProjectRef }
  | { stage: 'closure'; closeoutCase: ClosureCaseDetail; project: ProjectRef };

async function readCloseoutPortfolio(signal: AbortSignal): Promise<CloseoutEntry[]> {
  const entries = await readAcrossProjects(async (project): Promise<CloseoutEntry[]> => {
    const [completions, closures] = await Promise.all([
      closeoutApi.list('completion', project.id, signal),
      closeoutApi.list('closure', project.id, signal),
    ]);
    return [
      ...completions.map((closeoutCase) => ({
        stage: 'completion' as const,
        closeoutCase,
        project,
      })),
      ...closures.map((closeoutCase) => ({ stage: 'closure' as const, closeoutCase, project })),
    ];
  }, signal);
  return entries.sort((a, b) => b.closeoutCase.updatedAt.localeCompare(a.closeoutCase.updatedAt));
}

/** SCR-111 across projects: both stages' cases, most recently changed first. */
export function useCloseoutPortfolio(): ApiResource<CloseoutEntry[]> {
  return useApiResource(readCloseoutPortfolio, { keepWhileReloading: true });
}

export function useCloseoutRecord<S extends CloseoutStage>(
  stage: S,
  id: string,
): ApiResource<ApiResponse<CloseoutCaseOf[S]>> {
  const load = useCallback(
    (signal: AbortSignal) => closeoutApi.get(stage, id, signal),
    [stage, id],
  );
  return useApiResource(load, { keepWhileReloading: true });
}

// ---------------------------------------------------------------------------------------------------------------------
// WF-11

/** A record's WF-11 review as far as the person may see it. */
export interface Review {
  /** The inbox task deciding the revision under review, when the person may decide it now (APPROVAL_DECIDE). */
  task: ApprovalInboxItem | null;
  /** The runs that reviewed it, newest first; null when the person may not read runs (APPROVAL_VIEW). */
  runs: ApprovalInstanceSummary[] | null;
}

const NO_REVIEW: Review = { task: null, runs: [] };

/**
 * The record's part in WF-11 (the reused MOD-040–042 and MOD-044): the person's inbox task for the revision under
 * review, read only while it is under review, and the runs, read only once there are any.
 */
export function useReview(
  subject: Pick<ApprovalSubject, 'module' | 'type'>,
  record: { id: string; status: GovernedStatus; revisionNo: number } | undefined,
): ApiResource<Review> {
  const { module, type } = subject;
  const id = record?.id ?? null;
  const status = record?.status ?? 'DRAFT';
  const revisionNo = record?.revisionNo ?? 1;
  const load = useCallback(
    async (signal: AbortSignal): Promise<Review> => {
      if (id === null || !hasReviewRuns({ status, revisionNo })) {
        return NO_REVIEW;
      }
      const [task, runs] = await Promise.all([
        status === 'UNDER_REVIEW'
          ? unlessForbidden(findInboxTask({ module, type, id, revisionNo }, signal))
          : Promise.resolve(null),
        unlessForbidden(
          readAllPages<ApprovalInstanceSummary>(
            '/approval-instances',
            { subjectModule: module, subjectType: type, subjectId: id },
            signal,
          ),
        ),
      ]);
      return { task, runs };
    },
    [module, type, id, status, revisionNo],
  );
  return useApiResource(load);
}
