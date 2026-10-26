import { type PlatformApi } from './api.ts';
import { utcDate } from './journey.ts';

// What UJ-005 and the ADR-013 participation specs share beyond projects.ts and reference.ts: the WF-04 task an external
// contribution is applied to (external-participation.md D-8, D-10).

export interface ProjectTask {
  id: string;
  status: string;
  actualPercentComplete: number | null;
}

/** A WF-04 task created and started by the project's manager: a leaf IN_PROGRESS task takes a percentage. */
export async function startedTask(
  manager: PlatformApi,
  projectId: string,
  title: string,
  assigneeUserId: string | null = null,
): Promise<ProjectTask> {
  const task = await manager.post<ProjectTask>('/project-tasks', {
    projectId,
    title: { text: title, language: 'en' },
    assigneeUserId,
    plannedStartDate: utcDate(0),
    plannedFinishDate: utcDate(9),
  });
  return manager.post<ProjectTask>(`/project-tasks/${task.id}/start`);
}
