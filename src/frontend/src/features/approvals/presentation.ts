import { type StatusTone } from '@/shared/ui/StatusBadge.tsx';

import {
  type ApprovalDelegationStatus,
  type ApprovalInstanceStatus,
  type ApprovalTaskDetail,
  type ApprovalTaskStatus,
} from './api/types.ts';

// How WF-11 states read on screen. Pending, approved, rejected and returned each have their own tone, so the four
// outcomes the workbook names are told apart at a glance; the label always says the same thing in words.

export const INSTANCE_STATUSES: ApprovalInstanceStatus[] = [
  'PENDING',
  'APPROVED',
  'REJECTED',
  'RETURNED',
  'WITHDRAWN',
];

const OUTCOME_TONES: Record<ApprovalInstanceStatus, StatusTone> = {
  PENDING: 'info',
  APPROVED: 'positive',
  REJECTED: 'negative',
  RETURNED: 'warning',
  WITHDRAWN: 'neutral',
};

export function instanceTone(status: ApprovalInstanceStatus): StatusTone {
  return OUTCOME_TONES[status];
}

export function taskTone(status: ApprovalTaskStatus): StatusTone {
  switch (status) {
    case 'PENDING':
    case 'APPROVED':
    case 'REJECTED':
    case 'RETURNED':
      return OUTCOME_TONES[status];
    default:
      return 'neutral';
  }
}

export function delegationTone(status: ApprovalDelegationStatus): StatusTone {
  return status === 'ACTIVE' ? 'positive' : 'neutral';
}

/** A task is overdue once its stage's due instant has passed; a later stage's task has no due instant yet. */
export function isOverdue(dueAt: string | null, now: Date = new Date()): boolean {
  return dueAt !== null && new Date(dueAt).getTime() < now.getTime();
}

/** The stage being decided: the lowest sequence number that still has a PENDING task; null once the run ended. */
export function currentStage(tasks: ApprovalTaskDetail[]): number | null {
  const pending = tasks.filter((task) => task.status === 'PENDING').map((task) => task.sequenceNo);
  return pending.length === 0 ? null : Math.min(...pending);
}

/** The run's tasks by stage, in stage order, each stage's tasks in the order the API listed them. */
export function tasksByStage(tasks: ApprovalTaskDetail[]): [number, ApprovalTaskDetail[]][] {
  const stages = new Map<number, ApprovalTaskDetail[]>();
  for (const task of tasks) {
    stages.set(task.sequenceNo, [...(stages.get(task.sequenceNo) ?? []), task]);
  }
  return [...stages.entries()].sort(([a], [b]) => a - b);
}

/** The API writes a reason's language as `EN`/`AR` (TASK-035 F-12); the `lang` attribute wants `en`/`ar`. */
export function languageTag(language: string): string {
  return language.toLowerCase();
}
