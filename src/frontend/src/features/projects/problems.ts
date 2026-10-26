import { problemMessage, type Translate } from '@/features/identity-access/problems.ts';
import { ApiError } from '@/shared/api/httpClient.ts';
import { type TranslationKey } from '@/shared/i18n/i18n.ts';

// What a person reads for each WF-01 refusal (TASK-041 project-registration.md §5). Codes not listed here fall back to
// the platform messages.

const PROJECT_PROBLEMS: Record<string, TranslationKey> = {
  PROJECT_REFERENCE_INVALID: 'projects.problems.referenceInvalid',
  PROJECT_PARTICIPATION_INVALID: 'projects.problems.participationInvalid',
  PROJECT_INCOMPLETE: 'projects.problems.incomplete',
  PROJECT_MANAGER_INVALID: 'projects.problems.managerInvalid',
  PROJECT_NOT_EDITABLE: 'projects.problems.notEditable',
  PROJECT_IN_USE: 'projects.problems.inUse',
  INVALID_TRANSITION: 'projects.problems.invalidTransition',
  CONFIGURATION_MISSING: 'projects.problems.reviewRouteMissing',
  PERMISSION_DENIED: 'projects.problems.permissionDenied',
  NOT_FOUND: 'projects.problems.notFound',
};

export function projectProblemMessage(error: unknown, t: Translate): string {
  const key = error instanceof ApiError ? PROJECT_PROBLEMS[error.code] : undefined;
  return key === undefined ? problemMessage(error, t) : t(key);
}

/** A read refused for want of a permission: the screen says what it cannot show instead of failing. */
export function isForbidden(error: unknown): boolean {
  return error instanceof ApiError && error.status === 403;
}
