import { useCallback } from 'react';

import { projectsApi } from '@/features/projects/api/projectsApi.ts';
import { type ProjectSummary } from '@/features/projects/api/types.ts';
import { shortId } from '@/features/projects/presentation.ts';
import { scheduleApi } from '@/features/schedule/api/scheduleApi.ts';
import { type ScheduleActivityDetail } from '@/features/schedule/api/types.ts';
import { activityLabel } from '@/features/schedule/presentation.ts';
import { isPublished, readCatalogueItems } from '@/shared/api/masterData.ts';
import { type ApiResource, useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { type SelectOption } from '@/shared/ui/FormFields.tsx';

import { tasksApi } from './api/tasksApi.ts';
import { type ProjectTaskDetail, type TaskDependencyDetail } from './api/types.ts';
import { isForbidden } from './problems.ts';
import { type TaskEntry } from './taskRules.ts';

/** A project's tasks as SCR-047 and the modals read them: whole, so every rule sees every row. */
export interface ProjectBoard {
  /** Each parent followed by its subtasks, cancelled ones included. */
  tasks: ProjectTaskDetail[];
  dependencies: TaskDependencyDetail[];
  /** The schedule's activities, to name a task's; null when the caller may not read the schedule (403). */
  activities: ScheduleActivityDetail[] | null;
}

async function readActivities(projectId: string, signal: AbortSignal) {
  try {
    return await scheduleApi.activities(projectId, signal);
  } catch (error) {
    if (isForbidden(error)) {
      return null;
    }
    throw error;
  }
}

/** The board stays on screen while it is read again, so a dialog open over it is not unmounted by the refresh. */
export function useProjectBoard(projectId: string): ApiResource<ProjectBoard> {
  const load = useCallback(
    async (signal: AbortSignal): Promise<ProjectBoard> => {
      const [tasks, dependencies, activities] = await Promise.all([
        tasksApi.tasks(projectId, signal),
        tasksApi.dependencies(projectId, signal),
        readActivities(projectId, signal),
      ]);
      return { tasks, dependencies, activities };
    },
    [projectId],
  );
  return useApiResource(load, { keepWhileReloading: true });
}

/**
 * The project states a task can exist in: tasks are planned from APPROVED_PLANNED (TASK-048 D-9) and kept after. An
 * earlier project has none, so it is not read.
 */
const TASK_PROJECT_STATUSES = 'APPROVED_PLANNED,ACTIVE,SUSPENDED,COMPLETED,CLOSED';

/** Projects read at once: enough to be quick, few enough not to flood the API. */
const PARALLEL_READS = 6;

/** Every task the caller may see, with its project, for SCR-063–066. */
export interface TaskPortfolio {
  projects: ProjectSummary[];
  entries: TaskEntry[];
}

/**
 * SCR-063–066 read every task the caller may see: the API lists tasks one project at a time (R-3; TASK-048 F-8), so
 * the projects the caller may see are read first and then each one's tasks. A caller without TASK_VIEW is refused on
 * the first project (403), which the screens show as such.
 */
async function readPortfolio(signal: AbortSignal): Promise<TaskPortfolio> {
  const projects = await projectsApi.listAll({ status: TASK_PROJECT_STATUSES }, signal);
  const entries: TaskEntry[] = [];
  for (let index = 0; index < projects.length; index += PARALLEL_READS) {
    const batch = projects.slice(index, index + PARALLEL_READS);
    const read = await Promise.all(
      batch.map(async (project) =>
        (await tasksApi.tasks(project.id, signal)).map((task) => ({ task, project })),
      ),
    );
    entries.push(...read.flat());
  }
  return { projects, entries };
}

export function useTaskPortfolio(): ApiResource<TaskPortfolio> {
  return useApiResource(readPortfolio, { keepWhileReloading: true });
}

function readPriorities(signal: AbortSignal) {
  return readCatalogueItems(['PRIORITY'] as const, signal);
}

export interface TaskLookups {
  /** PUBLISHED PRIORITY items, in display order; empty when they cannot be read. */
  priorityOptions: SelectOption[];
  /** A priority's label, "Item 1a2b3c4d" when it cannot be read, null when none. */
  priorityLabel: (id: string | null) => string | null;
  /** False when the catalogue was refused (MASTER_DATA_VIEW, R01 only: TASK-038 F-1). */
  prioritiesReadable: boolean;
}

/** Names and choices for what a task names besides its project's own rows. */
export function useTaskLookups(): TaskLookups {
  const { t, language } = useI18n();
  const catalogue = useApiResource(readPriorities);
  const items = catalogue.data?.PRIORITY ?? [];
  return {
    priorityOptions: items
      .filter(isPublished)
      .map((item) => ({ value: item.id, label: item.label[language] })),
    priorityLabel: (id) => {
      if (id === null) {
        return null;
      }
      const item = items.find((candidate) => candidate.id === id);
      return item === undefined
        ? t('projects.lookups.unknownItem', { id: shortId(id) })
        : item.label[language];
    },
    prioritiesReadable: catalogue.data !== undefined,
  };
}

/** A schedule activity a task names: "1.2 · Excavation", or its shortened id when the schedule cannot be read. */
export function activityName(id: string | null, activities: ScheduleActivityDetail[] | null) {
  if (id === null) {
    return null;
  }
  const activity = activities?.find((candidate) => candidate.id === id);
  return activity === undefined ? shortId(id) : activityLabel(activity);
}
