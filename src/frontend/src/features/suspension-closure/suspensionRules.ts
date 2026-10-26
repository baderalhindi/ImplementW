import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { narrativeRequest } from '@/features/progress/progressUpdate.ts';
import { TEXT_LENGTH } from '@/features/projects/registration.ts';
import { todayUtc } from '@/features/tasks/taskRules.ts';
import { checkText } from '@/features/identity-access/forms.ts';
import { type FieldCodes } from '@/shared/forms/useFieldErrors.ts';
import { type Language } from '@/shared/i18n/i18n.ts';

import {
  type ActiveSuspensionDetail,
  type SuspensionRequestDetail,
  type SuspensionRequestRequest,
  type SuspensionRequestType,
} from './api/types.ts';
import { isOpen, managesProject, type ProjectFacts } from './governedRequest.ts';

// WF-09's rules as the screens apply them (TASK-062 suspension.md). The server is the authority: these checks only
// catch, before a round trip, what it would refuse.

export const SUSPENSION_REQUEST_TYPES: SuspensionRequestType[] = ['SUSPEND', 'RESUME'];

/** SCR-108's views (WF-09 §13): what is being decided, approved but not yet in effect, in effect, and the rest. */
export type SuspensionView = 'OPEN' | 'PENDING_ACTIVATION' | 'IN_EFFECT' | 'HISTORICAL';

export const SUSPENSION_VIEWS: SuspensionView[] = [
  'OPEN',
  'PENDING_ACTIVATION',
  'IN_EFFECT',
  'HISTORICAL',
];

/** IN_EFFECT is a suspension whose period is still open; an effected resumption, or a period ended, is history. */
export function suspensionViewOf(
  request: Pick<SuspensionRequestDetail, 'status' | 'requestType' | 'suspension'>,
): SuspensionView {
  switch (request.status) {
    case 'DRAFT':
    case 'SUBMITTED':
    case 'UNDER_REVIEW':
    case 'RETURNED':
      return 'OPEN';
    case 'APPROVED':
      return 'PENDING_ACTIVATION';
    case 'EFFECTED':
      return request.requestType === 'SUSPEND' && request.suspension?.endedAt === null
        ? 'IN_EFFECT'
        : 'HISTORICAL';
    case 'REJECTED':
    case 'WITHDRAWN':
      return 'HISTORICAL';
  }
}

/** The project's open request of a type: at most one (BR-SUS-004, BR-SUS-005). */
export function openRequestOf(
  requests: readonly SuspensionRequestDetail[],
  type: SuspensionRequestType,
): SuspensionRequestDetail | null {
  return requests.find((request) => request.requestType === type && isOpen(request.status)) ?? null;
}

/** The suspension in effect: the project's period not yet ended. */
export function openPeriodOf(
  periods: readonly ActiveSuspensionDetail[],
): ActiveSuspensionDetail | null {
  return periods.find((period) => period.endedAt === null) ?? null;
}

/** The project state a request of each type is raised for (BR-SUS-001, BR-SUS-002). */
const RAISABLE_FROM: Record<SuspensionRequestType, string> = {
  SUSPEND: 'ACTIVE',
  RESUME: 'SUSPENDED',
};

/**
 * SCR-109 is offered to the project's Project Manager for a suspension of an ACTIVE project and a resumption of a
 * SUSPENDED one, while no request of that type is open (409 SUSPENSION_ALREADY_EXISTS / …_RESUMPTION_… otherwise).
 */
export function canRaiseSuspensionRequest(
  user: SessionUser,
  project: ProjectFacts,
  requests: readonly SuspensionRequestDetail[],
  type: SuspensionRequestType,
): boolean {
  return (
    managesProject(user, project) &&
    project.status === RAISABLE_FROM[type] &&
    openRequestOf(requests, type) === null
  );
}

/** WF-09 withdraws a request before review from SUBMITTED or RETURNED; a DRAFT is deleted instead (TASK-062 §3). */
export const SUSPENSION_WITHDRAWABLE = ['SUBMITTED', 'RETURNED'] as const;

// ---------------------------------------------------------------------------------------------------------------------
// The form (SCR-109)

export interface SuspensionFormValues {
  reason: string;
  requestedEffectiveDate: string;
  plannedResumptionDate: string;
}

const FIELDS = ['reason', 'requestedEffectiveDate', 'plannedResumptionDate'] as const;
export type SuspensionField = (typeof FIELDS)[number];

export function emptySuspensionForm(): SuspensionFormValues {
  return { reason: '', requestedEffectiveDate: '', plannedResumptionDate: '' };
}

export function suspensionFormValuesOf(request: SuspensionRequestDetail): SuspensionFormValues {
  return {
    reason: request.reason.text,
    requestedEffectiveDate: request.requestedEffectiveDate ?? '',
    plannedResumptionDate: request.plannedResumptionDate ?? '',
  };
}

export function sameSuspensionValues(a: SuspensionFormValues, b: SuspensionFormValues): boolean {
  return FIELDS.every((field) => a[field] === b[field]);
}

const DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/;

/**
 * What saving a draft needs: a reason (the API requires one at creation), dates of the right shape, and a planned
 * resumption — a suspension's only — after the effective date (422 SUSPENSION_DATE_INVALID).
 */
export function checkSuspensionDraft(
  values: SuspensionFormValues,
  type: SuspensionRequestType,
): FieldCodes {
  const effective = values.requestedEffectiveDate;
  const planned = type === 'SUSPEND' ? values.plannedResumptionDate : '';
  return {
    reason: checkText(values.reason, { required: true, maxLength: TEXT_LENGTH }),
    requestedEffectiveDate: effective === '' || DATE_PATTERN.test(effective) ? null : 'MALFORMED',
    plannedResumptionDate:
      planned === ''
        ? null
        : !DATE_PATTERN.test(planned)
          ? 'MALFORMED'
          : effective !== '' && planned <= effective
            ? 'OUT_OF_RANGE'
            : null,
  };
}

/** Submission also needs an effective date, not before today (the API's today, the UTC date; TASK-062 F-10). */
export function checkSuspensionSubmission(
  values: SuspensionFormValues,
  type: SuspensionRequestType,
  today: string = todayUtc(),
): FieldCodes {
  const draft = checkSuspensionDraft(values, type);
  const effective = values.requestedEffectiveDate;
  return {
    ...draft,
    requestedEffectiveDate:
      draft.requestedEffectiveDate ??
      (effective === '' ? 'REQUIRED' : effective < today ? 'NOT_ALLOWED' : null),
  };
}

export function suspensionErrorCount(codes: FieldCodes): number {
  return FIELDS.filter((field) => codes[field]).length;
}

/** The request's own fields; a resumption carries no planned resumption date. */
export function toSuspensionRequest(
  values: SuspensionFormValues,
  type: SuspensionRequestType,
  language: Language,
  before: SuspensionRequestDetail | null,
): SuspensionRequestRequest {
  return {
    reason: narrativeRequest(values.reason, language, before?.reason ?? null),
    requestedEffectiveDate:
      values.requestedEffectiveDate === '' ? null : values.requestedEffectiveDate,
    plannedResumptionDate:
      type === 'SUSPEND' && values.plannedResumptionDate !== ''
        ? values.plannedResumptionDate
        : null,
  };
}
