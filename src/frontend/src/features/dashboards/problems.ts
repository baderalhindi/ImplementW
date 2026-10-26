import { problemMessage, type Translate } from '@/features/identity-access/problems.ts';
import { ApiError } from '@/shared/api/httpClient.ts';
import { type TranslationKey } from '@/shared/i18n/i18n.ts';

// What a person reads for each FG-01 refusal (TASK-069 dashboards.md §6). Codes not listed fall back to the platform
// messages.

const PROBLEMS: Record<string, TranslationKey> = {
  NOT_FOUND: 'dashboards.problems.notFound',
  DASHBOARD_FILTER_VALUE_UNAUTHORIZED: 'dashboards.problems.filterValueUnauthorized',
  DASHBOARD_CONTEXT_INVALID: 'dashboards.problems.contextInvalid',
  DASHBOARD_PERSONALIZATION_INVALID: 'dashboards.problems.personalizationInvalid',
  PERMISSION_DENIED: 'dashboards.problems.permissionDenied',
};

export function dashboardProblemMessage(error: unknown, t: Translate): string {
  const key = error instanceof ApiError ? PROBLEMS[error.code] : undefined;
  return key === undefined ? problemMessage(error, t) : t(key);
}

/** A dashboard outside the caller's audience, or a Project Dashboard no widget reaches (R-47). */
export function isNotFound(error: unknown): boolean {
  return error instanceof ApiError && error.status === 404;
}
