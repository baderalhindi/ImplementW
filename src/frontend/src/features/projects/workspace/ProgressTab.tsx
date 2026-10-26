import { type ReactElement } from 'react';

import { ProgressHistory } from '@/features/progress/ProgressHistory.tsx';
import { ProjectProgress } from '@/features/progress/ProjectProgress.tsx';

import { useWorkspace } from './workspaceContext.ts';

/** SCR-048 Project Progress, as WF-02 shows it (TASK-045), inside the workspace. */
export function ProgressTab(): ReactElement {
  const { project, user } = useWorkspace();
  return <ProjectProgress key={project.id} project={project} user={user} />;
}

/** SCR-070 Progress Update History, under the Progress tab. */
export function ProgressHistoryTab(): ReactElement {
  const { project } = useWorkspace();
  return <ProgressHistory key={project.id} project={project} />;
}
