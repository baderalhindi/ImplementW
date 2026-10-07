import { type ReactElement } from 'react';

import { ProjectChangeRequests } from '@/features/change-requests/ProjectChangeRequests.tsx';

import { useWorkspace } from './workspaceContext.ts';

/** SCR-105 Change Requests for the project, as WF-08 shows it (TASK-061), inside the workspace. */
export function ChangeRequestsTab(): ReactElement {
  const { project, user } = useWorkspace();
  return <ProjectChangeRequests key={project.id} project={project} user={user} />;
}
