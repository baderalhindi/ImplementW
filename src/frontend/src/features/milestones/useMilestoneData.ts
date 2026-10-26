import { useCallback } from 'react';

import { projectsApi } from '@/features/projects/api/projectsApi.ts';
import { type ProjectSummary } from '@/features/projects/api/types.ts';
import { shortId } from '@/features/projects/presentation.ts';
import { scheduleApi } from '@/features/schedule/api/scheduleApi.ts';
import { type ScheduleActivityDetail } from '@/features/schedule/api/types.ts';
import {
  isPublished,
  type MasterDataItemSummary,
  readCatalogueItems,
} from '@/shared/api/masterData.ts';
import { type ApiResource, useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { type SelectOption } from '@/shared/ui/FormFields.tsx';

import { milestonesApi } from './api/milestonesApi.ts';
import { type MilestoneAchievementDetail, type ProjectMilestoneDetail } from './api/types.ts';
import { type MilestoneEntry, revisionsOf } from './milestoneRules.ts';
import { isForbidden } from './problems.ts';

/** A read the caller may be refused (403): null then, so the screen says what it cannot show instead of failing. */
async function unlessForbidden<T>(read: Promise<T>): Promise<T | null> {
  try {
    return await read;
  } catch (error) {
    if (isForbidden(error)) {
      return null;
    }
    throw error;
  }
}

/** A project's milestones as SCR-046 reads them: whole, with every revision of every claim. */
export interface ProjectMilestones {
  /** Earliest forecast first, cancelled ones included. */
  milestones: ProjectMilestoneDetail[];
  /** Every revision, newest first; null when the caller may not read claims (no MILESTONE_VIEW). */
  achievements: MilestoneAchievementDetail[] | null;
}

/** The milestones stay on screen while they are read again, so a dialog open over them is not unmounted. */
export function useProjectMilestones(projectId: string): ApiResource<ProjectMilestones> {
  const load = useCallback(
    async (signal: AbortSignal): Promise<ProjectMilestones> => {
      const [milestones, achievements] = await Promise.all([
        milestonesApi.milestones(projectId, signal),
        unlessForbidden(milestonesApi.achievements(projectId, signal)),
      ]);
      return { milestones, achievements };
    },
    [projectId],
  );
  return useApiResource(load, { keepWhileReloading: true });
}

/**
 * The schedule's activities, to name and choose the one a milestone completes (MOD-016, MOD-019); null when the caller
 * may not read the schedule (403).
 */
export function useScheduleActivities(
  projectId: string,
): ApiResource<ScheduleActivityDetail[] | null> {
  const load = useCallback(
    (signal: AbortSignal) => unlessForbidden(scheduleApi.activities(projectId, signal)),
    [projectId],
  );
  return useApiResource(load);
}

/**
 * The project states a milestone can exist in: a schedule is initialized from APPROVED_PLANNED (TASK-046) and kept
 * after. An earlier project has none, so it is not read.
 */
const MILESTONE_PROJECT_STATUSES = 'APPROVED_PLANNED,ACTIVE,SUSPENDED,COMPLETED,CLOSED';

/** Projects read at once: enough to be quick, few enough not to flood the API. */
const PARALLEL_READS = 6;

export interface MilestonePortfolio {
  projects: ProjectSummary[];
  entries: MilestoneEntry[];
}

/**
 * SCR-062 reads every milestone the caller may see: the API lists milestones and claims one project at a time
 * (TASK-050 D-14), so the projects the caller may see are read first, then each one's milestones and claims. A caller
 * without SCHEDULE_VIEW is refused on the first project (403), which the screen shows as such; one without
 * MILESTONE_VIEW sees the milestones without their claims.
 */
async function readPortfolio(signal: AbortSignal): Promise<MilestonePortfolio> {
  const projects = await projectsApi.listAll({ status: MILESTONE_PROJECT_STATUSES }, signal);
  const entries: MilestoneEntry[] = [];
  for (let index = 0; index < projects.length; index += PARALLEL_READS) {
    const batch = projects.slice(index, index + PARALLEL_READS);
    const read = await Promise.all(
      batch.map(async (project) => {
        const [milestones, achievements] = await Promise.all([
          milestonesApi.milestones(project.id, signal),
          unlessForbidden(milestonesApi.achievements(project.id, signal)),
        ]);
        return milestones.map((milestone) => ({
          milestone,
          project,
          revisions: achievements === null ? null : revisionsOf(milestone.id, achievements),
        }));
      }),
    );
    entries.push(...read.flat());
  }
  return { projects, entries };
}

export function useMilestonePortfolio(): ApiResource<MilestonePortfolio> {
  return useApiResource(readPortfolio, { keepWhileReloading: true });
}

const CATALOGUE_CODES = ['MILESTONE_CATEGORY', 'EVIDENCE_TYPE'] as const;

function readCatalogues(signal: AbortSignal) {
  return readCatalogueItems(CATALOGUE_CODES, signal);
}

export interface MilestoneLookups {
  /** PUBLISHED MILESTONE_CATEGORY items, in display order. */
  categoryOptions: SelectOption[];
  /** PUBLISHED EVIDENCE_TYPE items, in display order. */
  evidenceTypeOptions: SelectOption[];
  /** An item's label in the interface language; "Item 1a2b3c4d" when the catalogues cannot be read. */
  itemLabel: (id: string) => string;
  /** False when the catalogues were refused (MASTER_DATA_VIEW, R01 only: TASK-038 F-1). */
  readable: boolean;
}

/** Names and choices for the catalogue items a milestone and its evidence name. */
export function useMilestoneLookups(): MilestoneLookups {
  const { t, language } = useI18n();
  const catalogues = useApiResource(readCatalogues);
  const options = (items: MasterDataItemSummary[] | undefined) =>
    (items ?? [])
      .filter(isPublished)
      .map((item) => ({ value: item.id, label: item.label[language] }));
  const all = catalogues.data === undefined ? [] : Object.values(catalogues.data).flat();
  return {
    categoryOptions: options(catalogues.data?.MILESTONE_CATEGORY),
    evidenceTypeOptions: options(catalogues.data?.EVIDENCE_TYPE),
    itemLabel: (id) => {
      const item = all.find((candidate) => candidate.id === id);
      return item === undefined
        ? t('projects.lookups.unknownItem', { id: shortId(id) })
        : item.label[language];
    },
    readable: catalogues.data !== undefined,
  };
}
