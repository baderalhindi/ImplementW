import { type CloseoutStage } from './api/closeoutApi.ts';
import { type SuspensionRequestType } from './api/types.ts';

// Where the WF-09 and WF-10 screens live. A request's or case's detail and form are under /suspension-requests and
// /closure-requests, readable without the project (its Department Manager reviews it but holds no PROJECT_VIEW,
// TASK-058 F-1); a project's register is its workspace tab.

/** SCR-110 Suspension Request Detail. */
export function suspensionRequestPath(id: string): string {
  return `/suspension-requests/${id}`;
}

/** SCR-109 for one project: a suspension of an ACTIVE project, a resumption of a SUSPENDED one. */
export function newSuspensionRequestPath(projectId: string, type: SuspensionRequestType): string {
  return `/suspension-requests/new?projectId=${encodeURIComponent(projectId)}&type=${type}`;
}

/** SCR-109 for a DRAFT or RETURNED request. */
export function editSuspensionRequestPath(id: string): string {
  return `/suspension-requests/${id}/edit`;
}

/** SCR-108 for one project, in its workspace. */
export function projectSuspensionPath(projectId: string): string {
  return `/projects/${projectId}/suspension`;
}

/** SCR-113 Closure Request Detail, for a case of either stage. */
export function closeoutCasePath(stage: CloseoutStage, id: string): string {
  return `/closure-requests/${stage}/${id}`;
}

/** SCR-112 for one project and one stage. */
export function newCloseoutCasePath(projectId: string, stage: CloseoutStage): string {
  return `/closure-requests/new?projectId=${encodeURIComponent(projectId)}&stage=${stage}`;
}

/** SCR-112 for a DRAFT or RETURNED case. */
export function editCloseoutCasePath(stage: CloseoutStage, id: string): string {
  return `/closure-requests/${stage}/${id}/edit`;
}

/** SCR-111 for one project, in its workspace: the two stages and the obligations. */
export function projectCloseoutPath(projectId: string): string {
  return `/projects/${projectId}/closeout`;
}
