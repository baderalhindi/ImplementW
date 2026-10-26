import { type ReactElement } from 'react';

import { ProjectSuspension } from '@/features/suspension-closure/ProjectSuspension.tsx';

import { useWorkspace } from './workspaceContext.ts';

/** SCR-108 Suspension Requests for the project, as WF-09 shows it (TASK-064), inside the workspace. */
export function SuspensionTab(): ReactElement {
  const { project, user } = useWorkspace();
  return <ProjectSuspension key={project.id} project={project} user={user} />;
}
