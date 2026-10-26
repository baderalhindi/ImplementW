import { type ProjectStatus } from '@/features/projects/api/types.ts';
import { ApiError } from '@/shared/api/httpClient.ts';
import { type FieldCodes } from '@/shared/forms/useFieldErrors.ts';

import {
  type ContributionFieldDefinition,
  type ContributionFieldRequest,
  type ContributionFieldValue,
  type ExternalContributionDetail,
  type ExternalContributionStatus,
  type ExternalUpdateRequestDetail,
  type ExternalUpdateRequestStatus,
  type ResponseDueCondition,
  type SourceApplicationDetail,
} from './api/types.ts';

// WF-13's rules as the screens need them (TASK-066 §3, D-4, D-8, D-10). The API decides every one of them again; these
// only choose what a screen shows and offers. Nothing here computes a business value the server owns: the due
// condition, the schema and the application outcome are the server's.

export const REQUEST_STATUSES: readonly ExternalUpdateRequestStatus[] = [
  'DRAFT',
  'ISSUED',
  'IN_PROGRESS',
  'RESPONDED',
  'CLOSED',
  'CANCELLED',
];

export const DUE_CONDITIONS: readonly ResponseDueCondition[] = [
  'OVERDUE',
  'DUE',
  'NOT_DUE',
  'NOT_APPLICABLE',
];

/** A request is drafted and issued while the project is APPROVED_PLANNED, ACTIVE or SUSPENDED (TASK-066 D-12). */
export function isRequestableProjectStatus(status: ProjectStatus): boolean {
  return status === 'APPROVED_PLANNED' || status === 'ACTIVE' || status === 'SUSPENDED';
}

/** The entity owes the answer: issued and not yet answered, or a draft (first or after a return) in progress. */
export function awaitsEntity(status: ExternalUpdateRequestStatus): boolean {
  return status === 'ISSUED' || status === 'IN_PROGRESS';
}

export function isFinalRequest(status: ExternalUpdateRequestStatus): boolean {
  return status === 'CLOSED' || status === 'CANCELLED';
}

/** Cancelled while the entity owes the answer; never once it is with its reviewer (409 EXTERNAL_REQUEST_RESPONDED). */
export function isCancellable(status: ExternalUpdateRequestStatus): boolean {
  return awaitsEntity(status);
}

/** Responder and reviewer are replaced on an issued request that is not final (WF-13 §9.4). */
export function isReassignable(status: ExternalUpdateRequestStatus): boolean {
  return status === 'ISSUED' || status === 'IN_PROGRESS' || status === 'RESPONDED';
}

/** A request open in any sense: issued and not closed or cancelled. */
export function isOpenRequest(status: ExternalUpdateRequestStatus): boolean {
  return status === 'ISSUED' || status === 'IN_PROGRESS' || status === 'RESPONDED';
}

/**
 * The typed schemas the platform defines (`ContributionSchemas`), by CONTRIBUTION_TYPE code: whether a request in it
 * names a source record of the project. A code with no schema is refused by the API (EXTERNAL_REQUEST_SCHEMA_INVALID).
 */
const SCHEMA_TARGETS: Readonly<Record<string, boolean>> = {
  TASK_PROGRESS: true,
  PROJECT_INFORMATION: false,
};

/** Whether the schema of this code names a source record; undefined for a code the platform defines no schema for. */
export function schemaNeedsTarget(code: string): boolean | undefined {
  return SCHEMA_TARGETS[code];
}

export function isKnownSchema(code: string): boolean {
  return code in SCHEMA_TARGETS;
}

/**
 * What an entity reads of a revision's state. AHDA's application of an accepted answer is internal (§9.3; attempts are
 * AHDA's only, D-6), so every accepted outcome reads Accepted on an external view.
 */
export type ExternalOutcome =
  'DRAFT' | 'SUBMITTED' | 'UNDER_REVIEW' | 'RETURNED' | 'REJECTED' | 'ACCEPTED';

export function externalOutcomeOf(status: ExternalContributionStatus): ExternalOutcome {
  switch (status) {
    case 'ACCEPTED_PENDING_APPLICATION':
    case 'APPLIED':
    case 'APPLICATION_FAILED':
      return 'ACCEPTED';
    default:
      return status;
  }
}

/** A revision waiting on its reviewer: submitted, or under review. */
export function awaitsReview(status: ExternalContributionStatus): boolean {
  return status === 'SUBMITTED' || status === 'UNDER_REVIEW';
}

/** The request's revision not yet final, if any: a draft or one with its reviewer (at most one, TASK-066 D-13). */
export function currentRevision(
  revisions: readonly ExternalContributionDetail[],
): ExternalContributionDetail | null {
  return (
    revisions.find((revision) => revision.status === 'DRAFT' || awaitsReview(revision.status)) ??
    null
  );
}

// ---- The response form (SCR-162 external): one input per schema field, values as text. ----

/** The form's text per field code, from a stored revision's canonical values. */
export function responseValuesOf(
  fields: readonly ContributionFieldValue[],
): Record<string, string> {
  return Object.fromEntries(fields.map((field) => [field.fieldCode, field.value]));
}

const DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/;
const NUMBER_PATTERN = /^[+-]?(\d+(\.\d*)?|\.\d+)$/;
/** `ContributionValues.MaxFractionDigits`. */
const MAX_FRACTION_DIGITS = 4;

function isCalendarDate(value: string): boolean {
  if (!DATE_PATTERN.test(value)) {
    return false;
  }
  const date = new Date(`${value}T00:00:00Z`);
  return !Number.isNaN(date.getTime()) && date.toISOString().startsWith(value);
}

/**
 * The code of what is wrong with one answered value, or null: REQUIRED (only when submitting — a draft may be
 * partial), MALFORMED, TOO_PRECISE or OUT_OF_RANGE. The API's canonical rule (`ContributionValues`) stays the authority.
 */
export function responseFieldCode(
  definition: ContributionFieldDefinition,
  raw: string,
  submitting: boolean,
): string | null {
  const value = raw.trim();
  if (value === '') {
    return submitting && definition.required ? 'REQUIRED' : null;
  }
  switch (definition.fieldType) {
    case 'NARRATIVE':
      return null;
    case 'DATE':
      return isCalendarDate(value) ? null : 'MALFORMED';
    case 'NUMBER': {
      if (!NUMBER_PATTERN.test(value)) {
        return 'MALFORMED';
      }
      const fraction = value.split('.')[1] ?? '';
      if (fraction.length > MAX_FRACTION_DIGITS) {
        return 'TOO_PRECISE';
      }
      const number = Number(value);
      return (definition.minimum !== null && number < definition.minimum) ||
        (definition.maximum !== null && number > definition.maximum)
        ? 'OUT_OF_RANGE'
        : null;
    }
  }
}

export function responseFieldCodes(
  definitions: readonly ContributionFieldDefinition[],
  values: Readonly<Record<string, string>>,
  submitting: boolean,
): FieldCodes {
  return Object.fromEntries(
    definitions.map((definition) => [
      definition.fieldCode,
      responseFieldCode(definition, values[definition.fieldCode] ?? '', submitting),
    ]),
  );
}

/**
 * The answered fields as the API takes them: only those with a value (an empty optional field is not sent), each in
 * the schema's order; a narrative tagged with the language it was typed in, any other value with none.
 */
export function responseFieldsToSend(
  definitions: readonly ContributionFieldDefinition[],
  values: Readonly<Record<string, string>>,
  language: 'ar' | 'en',
): ContributionFieldRequest[] {
  return definitions.flatMap((definition) => {
    const value = (values[definition.fieldCode] ?? '').trim();
    return value === ''
      ? []
      : [
          {
            fieldCode: definition.fieldCode,
            value,
            language: definition.fieldType === 'NARRATIVE' ? language : null,
          },
        ];
  });
}

const INDEXED_FIELD = /^fields\[(\d+)\]\.(value|fieldCode)$/;

/**
 * The API's field codes of a refused answer, by schema field: a value's refusal names `fields[i].value` by the index
 * sent, and a missing required field names its field code (CONTRIBUTION_REQUIRED_ITEM_MISSING).
 */
export function responseIssuesOf(
  error: unknown,
  sent: readonly ContributionFieldRequest[],
): FieldCodes {
  const codes: FieldCodes = {};
  if (!(error instanceof ApiError)) {
    return codes;
  }
  for (const issue of error.fieldIssues) {
    const indexed = INDEXED_FIELD.exec(issue.field);
    const fieldCode = indexed === null ? issue.field : sent[Number(indexed[1])]?.fieldCode;
    if (fieldCode !== undefined) {
      codes[fieldCode] ??= issue.code;
    }
  }
  return codes;
}

// ---- Source application (SCR-166). ----

/**
 * Where an accepted revision of a typed source stands (TASK-066 D-10), from its status and its attempts (newest
 * first). Each is shown in its own words, never as a generic error (acceptance criterion 2):
 * PENDING — accepted, no attempt yet; CONFLICT — the source changed since the answer, revalidation needed;
 * REVALIDATED — a conflict confirmed, ready to apply again; RETRYABLE — the source refused the change for now (its
 * project or state), may be applied again; APPLIED; FAILED — the source is gone or terminal, nothing can be applied.
 */
export type ApplicationState =
  'PENDING' | 'CONFLICT' | 'REVALIDATED' | 'RETRYABLE' | 'APPLIED' | 'FAILED';

export const APPLICATION_STATES: readonly ApplicationState[] = [
  'CONFLICT',
  'RETRYABLE',
  'REVALIDATED',
  'PENDING',
  'FAILED',
  'APPLIED',
];

export function applicationStateOf(
  status: ExternalContributionStatus,
  attempts: readonly SourceApplicationDetail[],
): ApplicationState {
  if (status === 'APPLIED') {
    return 'APPLIED';
  }
  if (status === 'APPLICATION_FAILED') {
    return 'FAILED';
  }
  const latest = attempts[0];
  switch (latest?.status) {
    case undefined:
      return 'PENDING';
    case 'CONFLICT':
      return latest.revalidatedAt === null ? 'CONFLICT' : 'REVALIDATED';
    case 'FAILED':
      return 'RETRYABLE';
    case 'APPLIED':
      return 'APPLIED';
  }
}

/** The one action a state takes: apply, revalidate the conflict, apply again; none once applied or failed. */
export type ApplicationAction = 'apply' | 'revalidate' | 'retry';

export function applicationActionOf(state: ApplicationState): ApplicationAction | null {
  switch (state) {
    case 'PENDING':
      return 'apply';
    case 'CONFLICT':
      return 'revalidate';
    case 'REVALIDATED':
    case 'RETRYABLE':
      return 'retry';
    case 'APPLIED':
    case 'FAILED':
      return null;
  }
}

/** The revisions SCR-166 follows: accepted answers of a typed source. A reference-only answer is applied on acceptance. */
export function isApplicationCase(
  request: Pick<ExternalUpdateRequestDetail, 'applicationMode'>,
  revision: Pick<ExternalContributionDetail, 'status'>,
): boolean {
  return (
    request.applicationMode === 'UPDATE_ALLOWED_SOURCE_FIELDS' &&
    (revision.status === 'ACCEPTED_PENDING_APPLICATION' ||
      revision.status === 'APPLIED' ||
      revision.status === 'APPLICATION_FAILED')
  );
}

/** A strong ETag `"123"` as the row version it carries (R-21: the API's ETag is the row version), or null. */
export function versionOfEtag(etag: string | null): number | null {
  const match = etag === null ? null : /^"(\d+)"$/.exec(etag);
  return match === null ? null : Number(match[1]);
}

// ---- SCR-167: participation by project and entity, counted from the requests the person may see. ----

export interface ParticipationRow {
  projectId: string;
  formalProjectId: string | null;
  externalEntityId: string;
  /** AHDA's drafts, not yet issued. */
  drafts: number;
  /** Issued, no answer started. */
  notStarted: number;
  /** The entity is drafting (a first answer, or a correction after a return). */
  drafting: number;
  /** An answer is with AHDA: under review or accepted pending application. */
  withAhda: number;
  overdue: number;
  due: number;
  closed: number;
  cancelled: number;
  /** Distinct named responders on open requests. */
  responders: number;
  lastChangedAt: string;
}

/** Rows by project and entity, those with overdue answers first, then the most open work. */
export function participationRows(
  requests: readonly ExternalUpdateRequestDetail[],
): ParticipationRow[] {
  const rows = new Map<string, ParticipationRow & { responderIds: Set<string> }>();
  for (const request of requests) {
    const key = `${request.projectId}|${request.externalEntityId}`;
    const row = rows.get(key) ?? {
      projectId: request.projectId,
      formalProjectId: request.formalProjectId,
      externalEntityId: request.externalEntityId,
      drafts: 0,
      notStarted: 0,
      drafting: 0,
      withAhda: 0,
      overdue: 0,
      due: 0,
      closed: 0,
      cancelled: 0,
      responders: 0,
      lastChangedAt: request.updatedAt,
      responderIds: new Set<string>(),
    };
    switch (request.status) {
      case 'DRAFT':
        row.drafts += 1;
        break;
      case 'ISSUED':
        row.notStarted += 1;
        break;
      case 'IN_PROGRESS':
        row.drafting += 1;
        break;
      case 'RESPONDED':
        row.withAhda += 1;
        break;
      case 'CLOSED':
        row.closed += 1;
        break;
      case 'CANCELLED':
        row.cancelled += 1;
        break;
    }
    if (request.dueCondition === 'OVERDUE') {
      row.overdue += 1;
    } else if (request.dueCondition === 'DUE') {
      row.due += 1;
    }
    if (isOpenRequest(request.status) && request.responsibleUserId !== null) {
      row.responderIds.add(request.responsibleUserId);
    }
    if (request.updatedAt > row.lastChangedAt) {
      row.lastChangedAt = request.updatedAt;
    }
    rows.set(key, row);
  }
  return [...rows.values()]
    .map(({ responderIds, ...row }) => ({ ...row, responders: responderIds.size }))
    .sort(
      (a, b) =>
        b.overdue - a.overdue ||
        b.notStarted + b.drafting + b.withAhda - (a.notStarted + a.drafting + a.withAhda) ||
        b.lastChangedAt.localeCompare(a.lastChangedAt),
    );
}
