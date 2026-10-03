import { type ApprovalInstanceSummary } from '@/features/approvals/api/types.ts';
import { apiRequest, type ApiResponse } from '@/shared/api/httpClient.ts';
import { readAllPages } from '@/shared/api/paging.ts';

import {
  type GovernanceProfileResolution,
  type Page,
  type ProjectDetail,
  type ProjectQuery,
  type ProjectRequest,
  type ProjectSummary,
} from './types.ts';

// WF-01 (TASK-041 project-registration.md §4). Every POST and PUT is a sensitive write: the client sends an
// Idempotency-Key. Commands send the ETag they were shown, so a change made meanwhile is 412, not overwritten.

async function data<T>(request: Promise<ApiResponse<T>>): Promise<T> {
  return (await request).data;
}

function command(id: string, name: string, etag: string | null, body?: unknown) {
  return apiRequest<ProjectDetail>(`/projects/${id}/${name}`, {
    method: 'POST',
    body: body ?? {},
    ifMatch: etag,
  });
}

export const projectsApi = {
  /** SCR-025 and SCR-026: only projects the caller may see, most recently changed first (P-2). */
  list: (query: ProjectQuery, signal?: AbortSignal) =>
    data(apiRequest<Page<ProjectSummary>>('/projects', { query: { ...query }, signal })),
  /** Every project the caller may see that matches, page after page (the cross-project task lists, TASK-049 D-2). */
  listAll: (query: Omit<ProjectQuery, 'page' | 'pageSize'>, signal?: AbortSignal) =>
    readAllPages<ProjectSummary>('/projects', { ...query }, signal),
  get: (id: string, signal?: AbortSignal) =>
    apiRequest<ProjectDetail>(`/projects/${id}`, { signal }),
  /** SCR-033: a DRAFT, revision 1. */
  create: (request: ProjectRequest) =>
    apiRequest<ProjectDetail>('/projects', { method: 'POST', body: request }),
  /** SCR-034 and SCR-035: the whole registration of a DRAFT or RETURNED project. If-Match is required. */
  update: (id: string, request: ProjectRequest, etag: string | null) =>
    apiRequest<ProjectDetail>(`/projects/${id}`, { method: 'PUT', body: request, ifMatch: etag }),
  /** MOD-001: a DRAFT, by the person who created it. 204 also when it is already gone (R-40). */
  remove: (id: string, etag: string | null) =>
    apiRequest<undefined>(`/projects/${id}`, { method: 'DELETE', ifMatch: etag }),
  /** MOD-002: DRAFT or RETURNED → SUBMITTED, naming the Project Manager. */
  submit: (id: string, projectManagerUserId: string, etag: string | null) =>
    command(id, 'submit', etag, { projectManagerUserId }),
  /** MOD-003: SUBMITTED → DRAFT. */
  withdraw: (id: string, etag: string | null) => command(id, 'withdraw', etag),
  /** MOD-003: SUBMITTED → UNDER_REVIEW, by AHDA only. */
  startReview: (id: string, etag: string | null) => command(id, 'start-review', etag),
  /** MOD-003: APPROVED_PLANNED → ACTIVE, by AHDA only; the one way a project becomes ACTIVE. */
  activate: (id: string, etag: string | null) => command(id, 'activate', etag),
  /** SCR-042: the project's review runs (TASK-035), newest first. */
  reviews: (projectId: string, signal?: AbortSignal) =>
    data(
      apiRequest<Page<ApprovalInstanceSummary>>('/approval-instances', {
        query: { subjectModule: 'Project', subjectType: 'Project', subjectId: projectId },
        signal,
      }),
    ),
};

export const governanceProfileApi = {
  /** The GOVERNANCE_PROFILE version in force (ADR-015). Needs CONFIGURATION_VIEW; 422 when none is published. */
  resolve: (signal?: AbortSignal) =>
    data(
      apiRequest<GovernanceProfileResolution>('/configuration-resolutions/GOVERNANCE_PROFILE', {
        signal,
      }),
    ),
};
