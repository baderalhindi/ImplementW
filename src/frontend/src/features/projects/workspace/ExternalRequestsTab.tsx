import { type ReactElement } from 'react';

import { ProjectExternalRequests } from '@/features/external-participation/ProjectExternalRequests.tsx';

import { useWorkspace } from './workspaceContext.ts';

/** The project's WF-13 update requests (TASK-067), inside the workspace; SCR-161/162 are under /external-requests. */
export function ExternalRequestsTab(): ReactElement {
  const { project, user } = useWorkspace();
  return <ProjectExternalRequests key={project.id} project={project} user={user} />;
}
