import { problemMessage, type Translate } from '@/features/identity-access/problems.ts';
import { ApiError } from '@/shared/api/httpClient.ts';
import { type TranslationKey } from '@/shared/i18n/i18n.ts';

// What a person reads for each WF-15 refusal (TASK-039 notification-runtime.md §3, §4). Codes not listed here fall back
// to the platform messages.

const NOTIFICATION_PROBLEMS: Record<string, TranslationKey> = {
  CONFIGURATION_MISSING: 'notifications.problems.configurationMissing',
  NOTIFICATION_PREFERENCE_NOT_CONFIGURABLE: 'notifications.problems.preferenceNotConfigurable',
  NOTIFICATION_PREFERENCE_INVALID: 'notifications.problems.preferenceInvalid',
  NOT_FOUND: 'notifications.problems.notFound',
};

export function notificationProblemMessage(error: unknown, t: Translate): string {
  const key = error instanceof ApiError ? NOTIFICATION_PROBLEMS[error.code] : undefined;
  return key === undefined ? problemMessage(error, t) : t(key);
}

/** No NOTIFICATION_ROUTING is in force: there are no families to list or choose (TASK-039 F-4). */
export function isConfigurationMissing(error: unknown): boolean {
  return error instanceof ApiError && error.code === 'CONFIGURATION_MISSING';
}
