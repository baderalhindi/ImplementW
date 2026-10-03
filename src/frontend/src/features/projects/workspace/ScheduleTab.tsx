import { type ReactElement } from 'react';

import { BaselineView } from '@/features/schedule/BaselineView.tsx';
import { GanttView } from '@/features/schedule/GanttView.tsx';
import { ScheduleManager } from '@/features/schedule/ScheduleManager.tsx';

import { useWorkspace } from './workspaceContext.ts';

/** SCR-060 Schedule Manager, as WF-03 shows it (TASK-047), inside the workspace. */
export function ScheduleTab(): ReactElement {
  const { project, user } = useWorkspace();
  return <ScheduleManager key={project.id} project={project} user={user} />;
}

/** SCR-045 Gantt View, under the Schedule tab. */
export function GanttTab(): ReactElement {
  const { project, user } = useWorkspace();
  return <GanttView key={project.id} project={project} user={user} />;
}

/** SCR-061 Baseline View, under the Schedule tab. */
export function BaselinesTab(): ReactElement {
  const { project, user } = useWorkspace();
  return <BaselineView key={project.id} project={project} user={user} />;
}
