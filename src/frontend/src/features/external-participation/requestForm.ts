import { UUID_PATTERN } from '@/features/identity-access/forms.ts';
import { TEXT_LENGTH } from '@/features/projects/registration.ts';
import { type AssigneeValue, assigneeOf, assigneeValue } from '@/features/tasks/assignee.ts';
import { type FieldCodes } from '@/shared/forms/useFieldErrors.ts';

import {
  type ExternalUpdateRequestDetail,
  type ExternalUpdateRequestRequest,
} from './api/types.ts';

// SCR-161's values and checks. A DRAFT may leave its people and due date for later; issuing needs both people and a due
// date not in the past (TASK-066 §3). The API is the authority: eligibility, the schema and the source are its.

export interface RequestFormValues {
  externalEntityId: string;
  contributionTypeItemId: string;
  targetId: string;
  instructions: string;
  dueDate: string;
  responder: AssigneeValue;
  reviewer: AssigneeValue;
}

export const REQUEST_FORM_FIELDS = [
  'externalEntityId',
  'contributionTypeItemId',
  'targetId',
  'instructions',
  'dueDate',
  'responsibleUserId',
  'reviewerUserId',
] as const;

/** A new request: addressed to the project's delivering entity, reviewed by the person drafting it (when AHDA's). */
export function emptyRequestForm(
  externalEntityId: string | null,
  reviewerUserId: string | null,
): RequestFormValues {
  return {
    externalEntityId: externalEntityId ?? '',
    contributionTypeItemId: '',
    targetId: '',
    instructions: '',
    dueDate: '',
    responder: assigneeValue(null),
    reviewer: assigneeValue(reviewerUserId),
  };
}

export function requestFormValuesOf(request: ExternalUpdateRequestDetail): RequestFormValues {
  return {
    externalEntityId: request.externalEntityId,
    contributionTypeItemId: request.contributionTypeItemId,
    targetId: request.targetId ?? '',
    instructions: request.instructions.text,
    dueDate: request.dueDate ?? '',
    responder: assigneeValue(request.responsibleUserId),
    reviewer: assigneeValue(request.reviewerUserId ?? null),
  };
}

function idCode(value: string, required: boolean): string | null {
  const trimmed = value.trim();
  if (trimmed === '') {
    return required ? 'REQUIRED' : null;
  }
  return UUID_PATTERN.test(trimmed) ? null : 'MALFORMED';
}

interface CheckContext {
  /** Whether the chosen type's schema names a source record; undefined when it is not known (a typed id). */
  needsTarget: boolean | undefined;
  canSearch: boolean;
  /** Issuing as well as saving: the people and a due date not before `today` are required. */
  issuing: boolean;
  /** Today as the API dates it, `yyyy-MM-dd` (UTC). */
  today: string;
}

/** Each field's code: REQUIRED, MALFORMED, MAX_LENGTH, or NOT_ALLOWED for a due date in the past when issuing. */
export function checkRequestForm(values: RequestFormValues, context: CheckContext): FieldCodes {
  const responder = assigneeOf(values.responder, context.canSearch);
  const reviewer = assigneeOf(values.reviewer, context.canSearch);
  const instructions = values.instructions.trim();
  return {
    externalEntityId: idCode(values.externalEntityId, true),
    contributionTypeItemId: idCode(values.contributionTypeItemId, true),
    targetId: idCode(values.targetId, context.needsTarget === true),
    instructions:
      instructions === ''
        ? 'REQUIRED'
        : values.instructions.length > TEXT_LENGTH
          ? 'MAX_LENGTH'
          : null,
    dueDate:
      values.dueDate === ''
        ? context.issuing
          ? 'REQUIRED'
          : null
        : context.issuing && values.dueDate < context.today
          ? 'NOT_ALLOWED'
          : null,
    responsibleUserId:
      responder.code ?? (context.issuing && responder.userId === null ? 'REQUIRED' : null),
    reviewerUserId:
      reviewer.code ?? (context.issuing && reviewer.userId === null ? 'REQUIRED' : null),
  };
}

/** The request's own fields as the API takes them (R-5); the source only where the schema may name one. */
export function toRequestBody(
  values: RequestFormValues,
  context: Pick<CheckContext, 'needsTarget' | 'canSearch'>,
  language: 'ar' | 'en',
): ExternalUpdateRequestRequest {
  const target = values.targetId.trim();
  return {
    contributionTypeItemId: values.contributionTypeItemId.trim().toLowerCase(),
    targetId: context.needsTarget === false || target === '' ? null : target.toLowerCase(),
    instructions: { text: values.instructions.trim(), language },
    responsibleUserId: assigneeOf(values.responder, context.canSearch).userId,
    reviewerUserId: assigneeOf(values.reviewer, context.canSearch).userId,
    dueDate: values.dueDate === '' ? null : values.dueDate,
  };
}
