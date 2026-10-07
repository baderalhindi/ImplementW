// Where the WF-08 screens live. A request's detail and form are under /change-requests, readable without the project
// (its Department Manager reviews it but holds no PROJECT_VIEW, TASK-058 F-1); a project's list is its workspace tab.

/** SCR-107 Change Request Detail. */
export function changeRequestPath(changeRequestId: string): string {
  return `/change-requests/${changeRequestId}`;
}

/** SCR-106 Create Change Request, for one project. */
export function newChangeRequestPath(projectId: string): string {
  return `/change-requests/new?projectId=${encodeURIComponent(projectId)}`;
}

/** SCR-106 for a DRAFT or RETURNED request: corrected, classified again, submitted. */
export function editChangeRequestPath(changeRequestId: string): string {
  return `/change-requests/${changeRequestId}/edit`;
}

/** SCR-105 for one project, in its workspace. */
export function projectChangeRequestsPath(projectId: string): string {
  return `/projects/${projectId}/change-requests`;
}
