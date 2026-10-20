// TASK-090: the SPA's unhandled errors reach the APM through the API (POST /client-error-reports), which redacts them and
// forwards them with the server's own events. The browser never talks to the APM: APM_DSN is a secret, and the CSP allows
// only this origin. Reporting is on in a build that names its environment (VITE_APM_ENVIRONMENT_TAG), off otherwise.

import { ApiError, apiRequest } from '@/shared/api/httpClient.ts';

export type ClientErrorSource = 'WINDOW_ERROR' | 'UNHANDLED_REJECTION' | 'RENDER_ERROR';

/** The API's limits (ClientErrorReportCreateRequest); a longer value is cut, never refused. */
const LIMITS = { errorType: 200, message: 2_000, stack: 8_000 };

/** A page that keeps failing reports its first errors, not a flood: the API rate-limits reports per client address too. */
export const MAX_REPORTS_PER_PAGE = 10;

const reported = new Set<string>();

export function isErrorReportingEnabled(): boolean {
  return (import.meta.env.VITE_APM_ENVIRONMENT_TAG ?? '') !== '';
}

function cut(value: string, limit: number): string {
  return value.length > limit ? value.slice(0, limit) : value;
}

/**
 * Sends one error to the API. It never throws and never reports its own failure. The same error is sent once per page load. A
 * failed API call (ApiError) names its response's correlation id, so the report lands on the request that failed.
 */
export function reportError(error: unknown, source: ClientErrorSource): void {
  if (!isErrorReportingEnabled() || reported.size >= MAX_REPORTS_PER_PAGE) {
    return;
  }
  const errorType = cut(error instanceof Error ? error.name : typeof error, LIMITS.errorType);
  const message = cut(
    error instanceof Error ? error.message : String(error),
    LIMITS.message,
  ).trim();
  const key = `${source}|${errorType}|${message}`;
  if (reported.has(key)) {
    return;
  }
  reported.add(key);
  const stack = error instanceof Error && error.stack !== undefined ? error.stack : undefined;
  void apiRequest('/client-error-reports', {
    method: 'POST',
    anonymous: true,
    body: {
      source,
      errorType: errorType === '' ? 'Error' : errorType,
      message: message === '' ? '(no message)' : message,
      stack: stack === undefined ? undefined : cut(stack, LIMITS.stack),
      // The path only: a query string or fragment can hold what someone typed.
      path: window.location.pathname,
      correlationId: error instanceof ApiError ? (error.correlationId ?? undefined) : undefined,
    },
  }).catch(() => {
    // Reporting is best effort: a report that cannot be sent is dropped.
  });
}

/** Reports what no code caught: script errors and rejected promises. Returns the function that removes the listeners. */
export function installErrorReporting(target: Window = window): () => void {
  const onError = (event: ErrorEvent) => {
    reportError(event.error ?? new Error(event.message), 'WINDOW_ERROR');
  };
  const onRejection = (event: PromiseRejectionEvent) => {
    reportError(event.reason, 'UNHANDLED_REJECTION');
  };
  target.addEventListener('error', onError);
  target.addEventListener('unhandledrejection', onRejection);
  return () => {
    target.removeEventListener('error', onError);
    target.removeEventListener('unhandledrejection', onRejection);
  };
}

/** For tests: forget what was reported, so each starts from an empty page. */
export function resetErrorReporting(): void {
  reported.clear();
}
