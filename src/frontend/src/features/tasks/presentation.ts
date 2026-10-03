import { type Translate } from '@/features/identity-access/problems.ts';
import { type StatusTone } from '@/shared/ui/StatusBadge.tsx';

import { type ProjectTaskStatus, type TaskDependencyType } from './api/types.ts';
import { type UpdateReason } from './taskRules.ts';

// How WF-04 values read on screen. Each state has its own word; the colour only repeats it (WCAG 1.4.1).

export const TASK_STATUSES: readonly ProjectTaskStatus[] = [
  'NOT_STARTED',
  'IN_PROGRESS',
  'BLOCKED',
  'COMPLETED',
  'CANCELLED',
];

const STATUS_TONES: Record<ProjectTaskStatus, StatusTone> = {
  NOT_STARTED: 'neutral',
  IN_PROGRESS: 'info',
  BLOCKED: 'warning',
  COMPLETED: 'positive',
  CANCELLED: 'neutral',
};

export function taskStatusTone(status: ProjectTaskStatus): StatusTone {
  return STATUS_TONES[status];
}

export const DEPENDENCY_TYPES: readonly TaskDependencyType[] = ['FS', 'SS', 'FF', 'SF'];

/** "Overdue by 1 day", "Overdue by 3 days". */
export function overdueLabel(days: number, t: Translate): string {
  return days === 1 ? t('tasks.overdue.one') : t('tasks.overdue.many', { days });
}

/** Why SCR-066 lists a task, in words. */
export function updateReasonLabel(reason: UpdateReason, t: Translate): string {
  switch (reason.kind) {
    case 'notStarted':
      return t('tasks.updates.notStarted', { date: reason.since });
    case 'noProgress':
      return t('tasks.updates.noProgress');
    case 'staleProgress':
      return t('tasks.updates.staleProgress', { days: reason.days });
  }
}
