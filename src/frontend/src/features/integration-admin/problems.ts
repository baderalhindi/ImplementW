import { problemMessage, type Translate } from '@/features/identity-access/problems.ts';
import { ApiError } from '@/shared/api/httpClient.ts';
import { type TranslationKey } from '@/shared/i18n/i18n.ts';

// What an operator reads for each FG-05 refusal (TASK-075 integration-monitoring.md §5). Codes not listed fall back to
// the platform's. A refusal's own text is never shown: only its code is read, and it is translated.

const PROBLEMS: Record<string, TranslationKey> = {
  PERMISSION_DENIED: 'integrationAdmin.problems.permissionDenied',
  INVOCATION_ALREADY_SUCCEEDED: 'integrationAdmin.problems.alreadySucceeded',
  INVOCATION_NOT_RETRYABLE: 'integrationAdmin.problems.notRetryable',
  RETRY_NOT_PERMITTED: 'integrationAdmin.problems.retryNotPermitted',
  INTEGRATION_DISABLED: 'integrationAdmin.problems.integrationDisabled',
  RECONCILIATION_NOT_AVAILABLE: 'integrationAdmin.problems.reconciliationNotAvailable',
};

export function integrationProblemMessage(error: unknown, t: Translate): string {
  const key = error instanceof ApiError ? PROBLEMS[error.code] : undefined;
  return key === undefined ? problemMessage(error, t) : t(key);
}
