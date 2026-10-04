import { type ReactElement } from 'react';

import { ProjectMilestones } from '@/features/milestones/ProjectMilestones.tsx';

import { useWorkspace } from './workspaceContext.ts';

/** SCR-046 Project Milestones, as WF-05 shows it (TASK-051), inside the workspace. */
export function MilestonesTab(): ReactElement {
  const { project, user } = useWorkspace();
  return <ProjectMilestones key={project.id} project={project} user={user} />;
}
