import { problemMessage, type Translate } from '@/features/identity-access/problems.ts';
import { ApiError } from '@/shared/api/httpClient.ts';
import { type TranslationKey } from '@/shared/i18n/i18n.ts';

// What a person reads for each FG-06 refusal (TASK-073 audit-activity.md §5). An event out of reach is 404 like an
// unknown one (R-47), so "not found" never says whether it exists. Codes not listed fall back to the platform's.

const PROBLEMS: Record<string, TranslationKey> = {
  NOT_FOUND: 'auditActivity.problems.notFound',
  PERMISSION_DENIED: 'auditActivity.problems.permissionDenied',
};

export function auditProblemMessage(error: unknown, t: Translate): string {
  const key = error instanceof ApiError ? PROBLEMS[error.code] : undefined;
  return key === undefined ? problemMessage(error, t) : t(key);
}
