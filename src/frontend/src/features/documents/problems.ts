import { problemMessage, type Translate } from '@/features/identity-access/problems.ts';
import { ApiError } from '@/shared/api/httpClient.ts';
import { type TranslationKey } from '@/shared/i18n/i18n.ts';

// What a person reads for each WF-12 refusal (TASK-037 document-management.md §3, §4). Codes not listed here fall back
// to the platform messages.

const DOCUMENT_PROBLEMS: Record<string, TranslationKey> = {
  DOCUMENT_NOT_AVAILABLE: 'documents.problems.notAvailable',
  DOCUMENT_REFERENCE_INVALID: 'documents.problems.referenceInvalid',
  DOCUMENT_VERSION_CONFLICT: 'documents.problems.versionConflict',
  PAYLOAD_TOO_LARGE: 'documents.problems.payloadTooLarge',
  UNSUPPORTED_MEDIA_TYPE: 'documents.problems.unsupportedMediaType',
  UNAVAILABLE: 'documents.problems.storageUnavailable',
  TERMINAL_STATE: 'documents.problems.archived',
  INVALID_TRANSITION: 'documents.problems.notRescannable',
  PERMISSION_DENIED: 'documents.problems.permissionDenied',
};

/** Refusals of the file itself, shown on the file input as well as above the form. */
const FILE_PROBLEMS = new Set(['PAYLOAD_TOO_LARGE', 'UNSUPPORTED_MEDIA_TYPE']);

export function documentProblemMessage(error: unknown, t: Translate): string {
  const key = error instanceof ApiError ? DOCUMENT_PROBLEMS[error.code] : undefined;
  return key === undefined ? problemMessage(error, t) : t(key);
}

export function isFileProblem(error: unknown): boolean {
  return error instanceof ApiError && FILE_PROBLEMS.has(error.code);
}
