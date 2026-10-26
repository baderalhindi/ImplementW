import { type ReactElement } from 'react';

import { formatPercent } from '@/features/progress/presentation.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { type ProjectTaskDetail } from '../api/types.ts';
import { overdueLabel, taskStatusTone } from '../presentation.ts';

export function TaskStatusBadge({
  task,
}: {
  task: Pick<ProjectTaskDetail, 'status'>;
}): ReactElement {
  const { t } = useI18n();
  return (
    <StatusBadge label={t(`tasks.status.${task.status}`)} tone={taskStatusTone(task.status)} />
  );
}

/**
 * An overdue task, told apart without colour (acceptance criterion 1, WCAG 1.4.1): a warning icon and the words
 * "Overdue by 3 days", on a bordered badge. The icon is decorative; the words are what a screen reader hears.
 */
export function OverdueFlag({ days }: { days: number }): ReactElement {
  const { t } = useI18n();
  return (
    <span className="overdue-flag">
      <svg
        className="overdue-flag__icon"
        viewBox="0 0 16 16"
        width="16"
        height="16"
        aria-hidden="true"
        focusable="false"
      >
        <path d="M8 1 15 14H1Z" fill="none" stroke="currentColor" strokeWidth="1.6" />
        <path d="M8 6v4" stroke="currentColor" strokeWidth="1.6" />
        <circle cx="8" cy="12" r="0.9" fill="currentColor" />
      </svg>
      {overdueLabel(days, t)}
    </span>
  );
}

/** ADR-009's figure: a leaf's own, a parent's rolled up from its subtasks. */
export function PercentFigure({
  task,
}: {
  task: Pick<ProjectTaskDetail, 'percentComplete'>;
}): ReactElement {
  return <span className="figure">{formatPercent(task.percentComplete)}</span>;
}
