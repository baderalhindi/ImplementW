import { checkText } from '@/features/identity-access/forms.ts';
import { narrativeRequest } from '@/features/progress/progressUpdate.ts';
import { moneyString, TEXT_LENGTH } from '@/features/projects/registration.ts';
import { type FieldCodes } from '@/shared/forms/useFieldErrors.ts';
import { type Language } from '@/shared/i18n/i18n.ts';

import {
  type ChangeRequestDetail,
  type ChangeRequestRequest,
  type ChangeRequestStatus,
  type ChangeType,
} from './api/types.ts';

// WF-08's rules as the screens apply them (TASK-060 change-request.md). The server is the authority: these checks only
// catch, before a round trip, what it would refuse; the materiality is never computed here (CHG-GP-08).

export const CHANGE_TYPES: ChangeType[] = [
  'SCHEDULE',
  'COST',
  'SCOPE',
  'CONTRACTUAL_OBLIGATION',
  'GOVERNANCE_PROFILE',
];

/** In lifecycle order, the final ones last. */
export const CHANGE_REQUEST_STATUSES: ChangeRequestStatus[] = [
  'DRAFT',
  'SUBMITTED',
  'UNDER_REVIEW',
  'RETURNED',
  'APPROVED',
  'IMPLEMENTATION',
  'IMPLEMENTED',
  'CLOSED',
  'REJECTED',
  'WITHDRAWN',
];

/** ChangeRequestRequest.MaxScheduleImpactDays on the API: ten years either way. */
export const MAX_SCHEDULE_IMPACT_DAYS = 3650;

/** R-16: SAR with at most two fraction digits, 16 whole digits; negative for a reduction. */
const SAR_PATTERN = /^-?\d{1,16}(\.\d{1,2})?$/;
const DAYS_PATTERN = /^-?\d+$/;

// ---------------------------------------------------------------------------------------------------------------------
// The form (SCR-106)

export interface ChangeRequestFormValues {
  changeType: ChangeType | '';
  title: string;
  justification: string;
  costImpactSar: string;
  scheduleImpactDays: string;
  scopeImpact: string;
  isContractualObligation: boolean;
  requestedGovernanceProfileItemId: string;
}

export function emptyChangeRequestForm(): ChangeRequestFormValues {
  return {
    changeType: '',
    title: '',
    justification: '',
    costImpactSar: '',
    scheduleImpactDays: '',
    scopeImpact: '',
    isContractualObligation: false,
    requestedGovernanceProfileItemId: '',
  };
}

export function changeRequestFormValuesOf(request: ChangeRequestDetail): ChangeRequestFormValues {
  return {
    changeType: request.changeType,
    title: request.title.text,
    justification: request.justification.text,
    costImpactSar: request.costImpactSar ?? '',
    scheduleImpactDays:
      request.scheduleImpactDays === null ? '' : String(request.scheduleImpactDays),
    scopeImpact: request.scopeImpact?.text ?? '',
    isContractualObligation: request.isContractualObligation,
    requestedGovernanceProfileItemId: request.requestedGovernanceProfileItemId ?? '',
  };
}

function checkCost(value: string): string | null {
  const trimmed = value.trim();
  if (trimmed === '') {
    return null;
  }
  if (!SAR_PATTERN.test(trimmed)) {
    return 'MALFORMED';
  }
  return Number(trimmed) === 0 ? 'OUT_OF_RANGE' : null;
}

function checkDays(value: string): string | null {
  const trimmed = value.trim();
  if (trimmed === '') {
    return null;
  }
  if (!DAYS_PATTERN.test(trimmed)) {
    return 'MALFORMED';
  }
  const days = Number(trimmed);
  return days === 0 || Math.abs(days) > MAX_SCHEDULE_IMPACT_DAYS ? 'OUT_OF_RANGE' : null;
}

/** What saving a draft needs: a type, a title and a justification, and impacts of the right shape (a draft may lack them). */
export function checkDraft(values: ChangeRequestFormValues): FieldCodes {
  return {
    changeType: values.changeType === '' ? 'REQUIRED' : null,
    title: checkText(values.title, { required: true, maxLength: TEXT_LENGTH }),
    justification: checkText(values.justification, { required: true, maxLength: TEXT_LENGTH }),
    costImpactSar: checkCost(values.costImpactSar),
    scheduleImpactDays: checkDays(values.scheduleImpactDays),
    scopeImpact: checkText(values.scopeImpact, { maxLength: TEXT_LENGTH }),
  };
}

/**
 * What classifying and submitting need besides: what the change type needs (TASK-060 D-12: SCHEDULE its days, COST its
 * amount, SCOPE its scope impact, CONTRACTUAL_OBLIGATION the flag, GOVERNANCE_PROFILE the profile asked for). The API
 * refuses a submission without it (422 CHANGE_REQUEST_INCOMPLETE) on the same field.
 */
export function checkForSubmission(values: ChangeRequestFormValues): FieldCodes {
  const codes = checkDraft(values);
  const missing = (value: string) => (value.trim() === '' ? 'REQUIRED' : null);
  switch (values.changeType) {
    case 'SCHEDULE':
      codes.scheduleImpactDays ??= missing(values.scheduleImpactDays);
      break;
    case 'COST':
      codes.costImpactSar ??= missing(values.costImpactSar);
      break;
    case 'SCOPE':
      codes.scopeImpact ??= missing(values.scopeImpact);
      break;
    case 'CONTRACTUAL_OBLIGATION':
      codes.isContractualObligation = values.isContractualObligation ? null : 'REQUIRED';
      break;
    case 'GOVERNANCE_PROFILE':
      codes.requestedGovernanceProfileItemId = missing(values.requestedGovernanceProfileItemId);
      break;
    case '':
      break;
  }
  return codes;
}

/** The field the change type makes required at submission, to mark it so on the form. */
export function requiredImpactOf(
  changeType: ChangeType | '',
): keyof ChangeRequestFormValues | null {
  switch (changeType) {
    case 'SCHEDULE':
      return 'scheduleImpactDays';
    case 'COST':
      return 'costImpactSar';
    case 'SCOPE':
      return 'scopeImpact';
    case 'CONTRACTUAL_OBLIGATION':
      return 'isContractualObligation';
    case 'GOVERNANCE_PROFILE':
      return 'requestedGovernanceProfileItemId';
    case '':
      return null;
  }
}

/**
 * The request's own fields as the API takes them. Free text is tagged with the interface language unless an edit left it
 * unchanged (ADR-012 extension, ERD D-7). A profile is sent only for a governance-profile change. No materiality, band
 * or cumulative figure is ever part of it: the server computes them (TASK-060 D-4).
 */
export function toChangeRequestRequest(
  values: ChangeRequestFormValues,
  language: Language,
  before: ChangeRequestDetail | null,
): ChangeRequestRequest {
  const cost = values.costImpactSar.trim();
  const days = values.scheduleImpactDays.trim();
  const scope = values.scopeImpact.trim();
  return {
    title: narrativeRequest(values.title, language, before?.title ?? null),
    justification: narrativeRequest(values.justification, language, before?.justification ?? null),
    costImpactSar: cost === '' ? null : moneyString(cost),
    scheduleImpactDays: days === '' ? null : Number(days),
    scopeImpact:
      scope === '' ? null : narrativeRequest(scope, language, before?.scopeImpact ?? null),
    isContractualObligation: values.isContractualObligation,
    requestedGovernanceProfileItemId:
      values.changeType === 'GOVERNANCE_PROFILE' && values.requestedGovernanceProfileItemId !== ''
        ? values.requestedGovernanceProfileItemId
        : null,
  };
}

// ---------------------------------------------------------------------------------------------------------------------
// The lifecycle

/** Fields change while DRAFT or RETURNED only (TASK-060 D-3). */
export function isEditable(status: ChangeRequestStatus): boolean {
  return status === 'DRAFT' || status === 'RETURNED';
}

/**
 * WF-11's decision on the request, one dimension of its state (WF-08 §4.1): whether it was approved, and nothing about
 * whether the change has been made. An approved request stays APPROVED here through its implementation and closure.
 */
export type ApprovalState =
  | 'NOT_SUBMITTED'
  | 'AWAITING_REVIEW'
  | 'IN_REVIEW'
  | 'RETURNED'
  | 'APPROVED'
  | 'REJECTED'
  | 'WITHDRAWN';

export function approvalStateOf(status: ChangeRequestStatus): ApprovalState {
  switch (status) {
    case 'DRAFT':
      return 'NOT_SUBMITTED';
    case 'SUBMITTED':
      return 'AWAITING_REVIEW';
    case 'UNDER_REVIEW':
      return 'IN_REVIEW';
    case 'RETURNED':
      return 'RETURNED';
    case 'APPROVED':
    case 'IMPLEMENTATION':
    case 'IMPLEMENTED':
    case 'CLOSED':
      return 'APPROVED';
    case 'REJECTED':
      return 'REJECTED';
    case 'WITHDRAWN':
      return 'WITHDRAWN';
  }
}

/**
 * Whether the approved change has been made in its target modules, the other dimension (WF-08 §4.1, §9): approval issues
 * authorisations and changes nothing (TASK-060 D-8); the change is IMPLEMENTED only once every authorisation is applied
 * and AHDA marks it so.
 */
export type ImplementationState = 'NOT_APPLICABLE' | 'NOT_STARTED' | 'IN_PROGRESS' | 'IMPLEMENTED';

export function implementationStateOf(status: ChangeRequestStatus): ImplementationState {
  switch (status) {
    case 'APPROVED':
      return 'NOT_STARTED';
    case 'IMPLEMENTATION':
      return 'IN_PROGRESS';
    case 'IMPLEMENTED':
    case 'CLOSED':
      return 'IMPLEMENTED';
    default:
      return 'NOT_APPLICABLE';
  }
}

/** How many of the request's authorisations their target modules have applied. */
export function appliedCount(request: Pick<ChangeRequestDetail, 'authorizations'>): number {
  return request.authorizations.filter((authorization) => authorization.status === 'APPLIED')
    .length;
}

/** AHDA may mark it implemented once every authorisation is applied (409 CHANGE_REQUEST_AUTHORIZATION_PENDING before). */
export function allAuthorizationsApplied(
  request: Pick<ChangeRequestDetail, 'authorizations'>,
): boolean {
  return appliedCount(request) === request.authorizations.length;
}

/** The project states a change request is raised, edited, submitted and reviewed in (TASK-060 D-12). */
export function isRaisableProjectStatus(status: string): boolean {
  return status === 'APPROVED_PLANNED' || status === 'ACTIVE' || status === 'SUSPENDED';
}
