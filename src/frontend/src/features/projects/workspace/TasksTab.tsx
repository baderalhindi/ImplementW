import { type ReactElement } from 'react';

import { ProjectTasks } from '@/features/tasks/ProjectTasks.tsx';

import { useWorkspace } from './workspaceContext.ts';

/** SCR-047 Project Tasks, as WF-04 shows it (TASK-049), inside the workspace. */
export function TasksTab(): ReactElement {
  const { project, user } = useWorkspace();
  return <ProjectTasks key={project.id} project={project} user={user} />;
}
