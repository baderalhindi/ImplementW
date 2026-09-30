import { problemMessage, type Translate } from '@/features/identity-access/problems.ts';
import { ApiError } from '@/shared/api/httpClient.ts';
import { type TranslationKey } from '@/shared/i18n/i18n.ts';

// What an approver or requester reads for each WF-11 refusal (TASK-035 approval-framework.md §3, §4). Codes not
// listed here fall back to the platform messages.

const APPROVAL_PROBLEMS: Record<string, TranslationKey> = {
  TERMINAL_STATE: 'approvals.problems.terminalState',
  INVALID_TRANSITION: 'approvals.problems.stageNotReached',
  PERMISSION_DENIED: 'approvals.problems.permissionDenied',
  CONFIGURATION_MISSING: 'approvals.problems.configurationMissing',
  APPROVAL_CONCURRENT_DECISION: 'approvals.problems.concurrentDecision',
  APPROVAL_REASON_REQUIRED: 'approvals.problems.reasonRequired',
  APPROVAL_ESCALATION_NOT_ALLOWED: 'approvals.problems.escalationNotAllowed',
  APPROVAL_DELEGATE_INVALID: 'approvals.problems.delegateInvalid',
  APPROVAL_DELEGATION_PERIOD_INVALID: 'approvals.problems.delegationPeriodInvalid',
};

/**
 * Refusals meaning the task or run moved on since the list was read: decided by someone else, already decided by a
 * retried request (TASK-035 F-10), or no longer visible. The screen reloads instead of offering a retry.
 */
const STALE_CODES = new Set(['TERMINAL_STATE', 'APPROVAL_CONCURRENT_DECISION', 'NOT_FOUND']);

export function approvalProblemMessage(error: unknown, t: Translate): string {
  const key = error instanceof ApiError ? APPROVAL_PROBLEMS[error.code] : undefined;
  return key === undefined ? problemMessage(error, t) : t(key);
}

export function isStaleApproval(error: unknown): boolean {
  return error instanceof ApiError && STALE_CODES.has(error.code);
}
