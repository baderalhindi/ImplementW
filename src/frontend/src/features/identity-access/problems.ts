import { ApiError, NETWORK_ERROR_CODE } from '@/shared/api/httpClient.ts';
import { type TranslationKey, type TranslationParams } from '@/shared/i18n/i18n.ts';

// What the administrator reads for each refusal (identity-access-administration.md §4). The field paths are the
// request's, so a server error lands on the input it names.

export type Translate = (key: TranslationKey, params?: TranslationParams) => string;

/** What the person reads for a refusal; a feature with codes of its own passes its describer to useSaveAction. */
export type ProblemDescriber = (error: unknown, t: Translate) => string;

const PROBLEM_MESSAGES: Record<string, TranslationKey> = {
  [NETWORK_ERROR_CODE]: 'common.problems.network',
  AUTHENTICATION_REQUIRED: 'common.problems.authenticationRequired',
  PERMISSION_DENIED: 'common.problems.permissionDenied',
  STEP_UP_REQUIRED: 'common.problems.stepUpRequired',
  NOT_FOUND: 'common.problems.notFound',
  VALIDATION_FAILED: 'common.problems.validationFailed',
  PRECONDITION_FAILED: 'common.problems.preconditionFailed',
  PRECONDITION_REQUIRED: 'common.problems.preconditionFailed',
  INVALID_TRANSITION: 'common.problems.invalidTransition',
  TERMINAL_STATE: 'common.problems.terminalState',
  UNAVAILABLE: 'common.problems.unavailable',
  IDENTITY_ACCESS_DUPLICATE_KEY: 'identityAccess.problems.duplicateKey',
  IDENTITY_ACCESS_ALREADY_ASSIGNED: 'identityAccess.problems.alreadyAssigned',
  IDENTITY_ACCESS_REFERENCE_INVALID: 'identityAccess.problems.referenceInvalid',
  IDENTITY_ACCESS_EXTERNAL_GRANT_INVALID: 'identityAccess.problems.externalGrantInvalid',
  IDENTITY_ACCESS_PROFILE_VERSION_NOT_PUBLISHED:
    'identityAccess.problems.profileVersionNotPublished',
  IDENTITY_ACCESS_PROJECT_CLOSED: 'identityAccess.problems.projectClosed',
  IDENTITY_ACCESS_SELF_ADMINISTRATION: 'identityAccess.problems.selfAdministration',
  IDENTITY_ACCESS_SERVICE_PRINCIPAL: 'identityAccess.problems.servicePrincipal',
  IDENTITY_ACCESS_DIRECTORY_AUTHORITATIVE: 'identityAccess.problems.directoryAuthoritative',
  IDENTITY_ACCESS_INVALID_PERIOD: 'identityAccess.problems.invalidPeriod',
  IDENTITY_ACCESS_DEPARTMENT_CYCLE: 'identityAccess.problems.departmentCycle',
};

const FIELD_MESSAGES: Record<string, TranslationKey> = {
  REQUIRED: 'common.fieldErrors.required',
  MAX_LENGTH: 'common.fieldErrors.maxLength',
  MALFORMED: 'common.fieldErrors.malformed',
  ENUM_VALUE: 'common.fieldErrors.invalidChoice',
  OUT_OF_RANGE: 'common.fieldErrors.invalidChoice',
  NOT_ALLOWED: 'common.fieldErrors.notAllowed',
  NOT_FOUND: 'identityAccess.fieldErrors.notFound',
  INACTIVE: 'identityAccess.fieldErrors.inactive',
  DUPLICATE: 'identityAccess.fieldErrors.duplicate',
  OUTSIDE_ENTITY: 'identityAccess.fieldErrors.outsideEntity',
  NOT_EXTERNAL_ELIGIBLE: 'identityAccess.fieldErrors.notExternalEligible',
  DATE_BEFORE_START: 'identityAccess.fieldErrors.dateBeforeStart',
};

export function problemMessage(error: unknown, t: Translate): string {
  const key = error instanceof ApiError ? PROBLEM_MESSAGES[error.code] : undefined;
  if (key !== undefined) {
    return t(key);
  }
  const correlationId = error instanceof ApiError ? error.correlationId : null;
  return correlationId === null
    ? t('common.problems.unexpected')
    : t('common.problems.unexpectedWithReference', { reference: correlationId });
}

export function fieldMessage(code: string, t: Translate): string {
  return t(FIELD_MESSAGES[code] ?? 'common.fieldErrors.invalid');
}

export type FieldErrors = Partial<Record<string, string>>;

/**
 * The field messages of a refusal, keyed by the request's field path. A duplicate key (409) and a rule broken on a
 * field (422) name their field the same way a 400 does.
 */
export function fieldErrorsOf(error: unknown, t: Translate): FieldErrors {
  const errors: FieldErrors = {};
  if (error instanceof ApiError) {
    for (const issue of error.fieldIssues) {
      errors[issue.field] ??= fieldMessage(issue.code, t);
    }
  }
  return errors;
}

export function isStaleVersion(error: unknown): boolean {
  return error instanceof ApiError && error.code === 'PRECONDITION_FAILED';
}
