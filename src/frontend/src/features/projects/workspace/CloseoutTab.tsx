import { type ReactElement } from 'react';

import { ProjectCloseout } from '@/features/suspension-closure/ProjectCloseout.tsx';

import { useWorkspace } from './workspaceContext.ts';

/** SCR-111 for the project: its two-stage closeout and obligations, as WF-10 shows it (TASK-064), inside the workspace. */
export function CloseoutTab(): ReactElement {
  const { project, user } = useWorkspace();
  return <ProjectCloseout key={project.id} project={project} user={user} />;
}
