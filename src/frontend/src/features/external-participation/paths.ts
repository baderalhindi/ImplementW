// Where the WF-13 screens live. AHDA's are under /external-requests and /external-contributions; an entity user's two
// lists are /my-external-requests and /my-contributions; a project's requests are its workspace tab. A request's
// detail (SCR-162) is one address for both audiences: the API's projection decides what it shows.

/** SCR-160 External Update Requests. */
export const EXTERNAL_REQUESTS_PATH = '/external-requests';

/** SCR-166 Source Application Monitoring. */
export const APPLICATION_MONITOR_PATH = '/external-requests/applications';

/** SCR-167 External Participation Monitor. */
export const PARTICIPATION_MONITOR_PATH = '/external-requests/monitor';

/** SCR-163 My External Requests. */
export const MY_EXTERNAL_REQUESTS_PATH = '/my-external-requests';

/** SCR-164 My Contributions. */
export const MY_CONTRIBUTIONS_PATH = '/my-contributions';

/** SCR-162 External Update Request Detail. */
export function externalRequestPath(requestId: string): string {
  return `/external-requests/${requestId}`;
}

/** SCR-161 Create External Update Request, for one project. */
export function newExternalRequestPath(projectId: string): string {
  return `/external-requests/new?projectId=${encodeURIComponent(projectId)}`;
}

/** SCR-161 for a DRAFT. */
export function editExternalRequestPath(requestId: string): string {
  return `/external-requests/${requestId}/edit`;
}

/** SCR-165 Contribution Review, of one revision. */
export function contributionReviewPath(contributionId: string): string {
  return `/external-contributions/${contributionId}`;
}

/** SCR-160 for one project in its workspace. */
export function projectExternalRequestsPath(projectId: string): string {
  return `/projects/${projectId}/external-requests`;
}

/** SCR-160 narrowed to one project and entity (SCR-167's row). */
export function registerOfPath(projectId: string, externalEntityId: string): string {
  const query = new URLSearchParams({ projectId, externalEntityId });
  return `${EXTERNAL_REQUESTS_PATH}?${query.toString()}`;
}
