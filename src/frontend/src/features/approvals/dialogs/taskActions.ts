import { type ApprovalDecision } from '../api/types.ts';

/** MOD-040 approve, MOD-041 reject, MOD-042 return (the approver's), MOD-045 escalate (the requester's). */
export type TaskAction = ApprovalDecision | 'escalate';

export interface TaskActionTarget {
  taskId: string;
  action: TaskAction;
}

/** NarrativeTextRequest.TextLength on the API. */
export const REASON_LENGTH = 2000;

/** Rejecting and returning end the run and need a reason the requester reads (TASK-036; the API refuses without one). */
export function reasonRequired(action: TaskAction): boolean {
  return action === 'reject' || action === 'return';
}
