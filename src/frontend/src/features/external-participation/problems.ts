import {
  fieldMessage,
  problemMessage,
  type Translate,
} from '@/features/identity-access/problems.ts';
import { ApiError } from '@/shared/api/httpClient.ts';
import { type TranslationKey } from '@/shared/i18n/i18n.ts';

// What a person reads for each refusal of the WF-13 API (TASK-066 external-participation.md §5). Codes not listed
// fall back to the platform messages. An external user reads business-safe words only (WF-13 §12.5): no code here names
// an internal record or person.

const PROBLEMS: Record<string, TranslationKey> = {
  PROJECT_STATE_NOT_PERMITTED: 'externalParticipation.problems.projectState',
  PROJECT_CLOSED: 'externalParticipation.problems.projectClosed',
  EXTERNAL_ENTITY_INACTIVE: 'externalParticipation.problems.entityInactive',
  EXTERNAL_REQUEST_SCHEMA_INVALID: 'externalParticipation.problems.schemaInvalid',
  EXTERNAL_CONTRIBUTION_TYPE_NOT_ENABLED: 'externalParticipation.problems.typeNotEnabled',
  EXTERNAL_REQUEST_SOURCE_INVALID: 'externalParticipation.problems.sourceInvalid',
  EXTERNAL_RESPONSIBLE_USER_INELIGIBLE: 'externalParticipation.problems.responderIneligible',
  REVIEWER_INELIGIBLE: 'externalParticipation.problems.reviewerIneligible',
  EXTERNAL_REQUEST_INCOMPLETE: 'externalParticipation.problems.incomplete',
  EXTERNAL_REQUEST_DUE_DATE_INVALID: 'externalParticipation.problems.dueDateInvalid',
  EXTERNAL_REQUEST_NOT_EDITABLE: 'externalParticipation.problems.requestNotEditable',
  EXTERNAL_REQUEST_NOT_OPEN: 'externalParticipation.problems.requestNotOpen',
  EXTERNAL_REQUEST_RESPONDED: 'externalParticipation.problems.requestResponded',
  CONTRIBUTION_NOT_EDITABLE: 'externalParticipation.problems.contributionNotEditable',
  CONTRIBUTION_ALREADY_SUBMITTED: 'externalParticipation.problems.alreadySubmitted',
  CONTRIBUTION_ALREADY_DECIDED: 'externalParticipation.problems.alreadyDecided',
  EXTERNAL_FIELD_ACCESS_DENIED: 'externalParticipation.problems.fieldNotAllowed',
  CONTRIBUTION_VALIDATION_FAILED: 'externalParticipation.problems.valueInvalid',
  CONTRIBUTION_REQUIRED_ITEM_MISSING: 'externalParticipation.problems.requiredMissing',
  SOURCE_APPLICATION_NOT_PERMITTED: 'externalParticipation.problems.applicationNotPermitted',
  SOURCE_APPLICATION_ALREADY_COMPLETED: 'externalParticipation.problems.alreadyApplied',
  SOURCE_APPLICATION_CONFLICT: 'externalParticipation.problems.unrevalidatedConflict',
  SOURCE_APPLICATION_NOT_REVALIDATABLE: 'externalParticipation.problems.notRevalidatable',
  SOURCE_RECORD_NOT_FOUND: 'externalParticipation.problems.sourceGone',
  IDEMPOTENCY_KEY_REUSED: 'externalParticipation.problems.keyReused',
  CONFIGURATION_MISSING: 'externalParticipation.problems.configurationMissing',
  INVALID_TRANSITION: 'externalParticipation.problems.invalidTransition',
  TERMINAL_STATE: 'externalParticipation.problems.terminalState',
  PERMISSION_DENIED: 'externalParticipation.problems.permissionDenied',
};

export function participationProblemMessage(error: unknown, t: Translate): string {
  const key = error instanceof ApiError ? PROBLEMS[error.code] : undefined;
  return key === undefined ? problemMessage(error, t) : t(key);
}

/**
 * The record moved on since it was read (412; a transition no longer allowed; final; no longer editable or open; already
 * submitted, decided or applied; an unrevalidated conflict; gone): the screen reads it again instead of retrying.
 */
export function isStale(error: unknown): boolean {
  return (
    error instanceof ApiError &&
    [
      'PRECONDITION_FAILED',
      'INVALID_TRANSITION',
      'TERMINAL_STATE',
      'NOT_FOUND',
      'EXTERNAL_REQUEST_NOT_EDITABLE',
      'EXTERNAL_REQUEST_NOT_OPEN',
      'EXTERNAL_REQUEST_RESPONDED',
      'CONTRIBUTION_NOT_EDITABLE',
      'CONTRIBUTION_ALREADY_SUBMITTED',
      'CONTRIBUTION_ALREADY_DECIDED',
      'SOURCE_APPLICATION_NOT_PERMITTED',
      'SOURCE_APPLICATION_ALREADY_COMPLETED',
      'SOURCE_APPLICATION_CONFLICT',
      'SOURCE_APPLICATION_NOT_REVALIDATABLE',
    ].includes(error.code)
  );
}

export function isForbidden(error: unknown): boolean {
  return error instanceof ApiError && error.status === 403;
}

/** A record outside the caller's scope is 404, as a nonexistent id (R-47). */
export function isNotFound(error: unknown): boolean {
  return error instanceof ApiError && error.status === 404;
}

const FIELD_MESSAGES: Partial<Record<string, Partial<Record<string, TranslationKey>>>> = {
  contributionTypeItemId: {
    NOT_FOUND: 'externalParticipation.fieldErrors.typeUnknown',
    NOT_ALLOWED: 'externalParticipation.fieldErrors.typeNotAvailable',
  },
  targetId: {
    REQUIRED: 'externalParticipation.fieldErrors.targetRequired',
    NOT_ALLOWED: 'externalParticipation.fieldErrors.targetNotAllowed',
    NOT_FOUND: 'externalParticipation.fieldErrors.targetNotFound',
  },
  externalEntityId: { INACTIVE: 'externalParticipation.fieldErrors.entityInactive' },
  responsibleUserId: {
    NOT_ALLOWED: 'externalParticipation.fieldErrors.responderIneligible',
    REQUIRED: 'externalParticipation.fieldErrors.responderRequired',
  },
  reviewerUserId: {
    NOT_ALLOWED: 'externalParticipation.fieldErrors.reviewerIneligible',
    REQUIRED: 'externalParticipation.fieldErrors.reviewerRequired',
  },
  dueDate: { NOT_ALLOWED: 'externalParticipation.fieldErrors.dueDatePast' },
};

/** A request form's field message: the API's codes for a field, or the platform's wording. */
export function requestFieldMessage(field: string, code: string, t: Translate): string {
  const key = FIELD_MESSAGES[field]?.[code];
  return key === undefined ? fieldMessage(code, t) : t(key);
}

const RESPONSE_MESSAGES: Partial<Record<string, TranslationKey>> = {
  REQUIRED: 'externalParticipation.fieldErrors.answerRequired',
  MALFORMED: 'externalParticipation.fieldErrors.answerMalformed',
  TOO_PRECISE: 'externalParticipation.fieldErrors.answerTooPrecise',
  OUT_OF_RANGE: 'externalParticipation.fieldErrors.answerOutOfRange',
  NOT_ALLOWED: 'externalParticipation.fieldErrors.answerNotAccepted',
  DUPLICATE: 'externalParticipation.fieldErrors.answerNotAccepted',
};

/** An answered field's message; the range is the schema's, so the message names it where there is one. */
export function responseFieldMessage(code: string, t: Translate): string {
  const key = RESPONSE_MESSAGES[code];
  return key === undefined ? fieldMessage(code, t) : t(key);
}
