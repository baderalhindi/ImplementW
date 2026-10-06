import { useCallback } from 'react';

import { rolesApi } from '@/features/identity-access/api/identityAccessApi.ts';
import { projectsApi } from '@/features/projects/api/projectsApi.ts';
import { type ProjectDetail, type ProjectSummary } from '@/features/projects/api/types.ts';
import { shortId } from '@/features/projects/presentation.ts';
import { isForbidden } from '@/features/risks/problems.ts';
import { type ApiResponse } from '@/shared/api/httpClient.ts';
import {
  isPublished,
  type MasterDataItemSummary,
  readCatalogueItems,
} from '@/shared/api/masterData.ts';
import { type ApiResource, useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { type SelectOption } from '@/shared/ui/FormFields.tsx';

import { concernsApi } from './api/concernsApi.ts';
import { type ConcernDetail, type ConcernEscalationDetail, type ConcernType } from './api/types.ts';
import { type ConcernEntry, type EscalationEntry, openEscalationsOf } from './concernRules.ts';

/** A project's concerns of one type. They stay on screen while read again, so a dialog over them is not unmounted. */
export function useProjectConcerns(
  projectId: string,
  concernType: ConcernType,
): ApiResource<ConcernDetail[]> {
  const load = useCallback(
    (signal: AbortSignal) => concernsApi.concerns(projectId, concernType, signal),
    [projectId, concernType],
  );
  return useApiResource(load, { keepWhileReloading: true });
}

/** The project states a concern can exist in: raised from APPROVED_PLANNED (TASK-057 D-11) and kept after. */
const CONCERN_PROJECT_STATUSES = 'APPROVED_PLANNED,ACTIVE,SUSPENDED,COMPLETED,CLOSED';

/** Reads at once: enough to be quick, few enough not to flood the API. */
const PARALLEL_READS = 6;

async function inBatches<T, R>(items: T[], read: (item: T) => Promise<R[]>): Promise<R[]> {
  const results: R[] = [];
  for (let index = 0; index < items.length; index += PARALLEL_READS) {
    const batch = await Promise.all(items.slice(index, index + PARALLEL_READS).map(read));
    results.push(...batch.flat());
  }
  return results;
}

/**
 * The concerns across the projects the caller may see. The API lists them one project at a time (`projectId` is
 * required, TASK-057 D-13), so the projects are read first, then each one's concerns; a project whose concerns the
 * caller may not see answers an empty page.
 */
async function readConcernEntries(
  concernType: ConcernType | null,
  signal: AbortSignal,
): Promise<ConcernEntry[]> {
  const projects = await projectsApi.listAll({ status: CONCERN_PROJECT_STATUSES }, signal);
  return inBatches(projects, async (project: ProjectSummary) =>
    (await concernsApi.concerns(project.id, concernType, signal)).map((concern) => ({
      concern,
      project,
    })),
  );
}

/** SCR-083 or SCR-085 across projects. */
export function useConcernPortfolio(concernType: ConcernType): ApiResource<ConcernEntry[]> {
  const load = useCallback(
    (signal: AbortSignal) => readConcernEntries(concernType, signal),
    [concernType],
  );
  return useApiResource(load, { keepWhileReloading: true });
}

/**
 * SCR-087's escalations. Every concern carries its OPEN escalation, so the default view reads the concerns only. The
 * ended ones are in each concern's history (`/concern-escalations` needs `managementConcernId`), read only when a view
 * asks for them.
 */
async function readEscalations(
  withHistory: boolean,
  signal: AbortSignal,
): Promise<EscalationEntry[]> {
  const concerns = await readConcernEntries(null, signal);
  if (!withHistory) {
    return openEscalationsOf(concerns);
  }
  return inBatches(concerns, async ({ concern, project }) =>
    (await concernsApi.escalations(concern.id, signal)).map((escalation) => ({
      escalation,
      concern,
      project,
    })),
  );
}

export function useEscalationQueue(withHistory: boolean): ApiResource<EscalationEntry[]> {
  const load = useCallback(
    (signal: AbortSignal) => readEscalations(withHistory, signal),
    [withHistory],
  );
  return useApiResource(load);
}

const CATALOGUE_CODES = [
  'CONCERN_CATEGORY',
  'PRIORITY',
  'CONCERN_SEVERITY',
  'IMPACT_DIMENSION',
] as const;

function readCatalogues(signal: AbortSignal) {
  return readCatalogueItems(CATALOGUE_CODES, signal);
}

export interface ConcernLookups {
  /** PUBLISHED CONCERN_CATEGORY items, in display order. */
  categoryOptions: SelectOption[];
  /** PUBLISHED PRIORITY items, in display order. */
  priorityOptions: SelectOption[];
  /** A category's, priority's, severity's or impact dimension's label; "Item 1a2b3c4d" when it cannot be read. */
  itemLabel: (id: string) => string;
  /** False when the catalogues were refused (MASTER_DATA_VIEW, R01 only: TASK-038 F-1). */
  readable: boolean;
}

function publishedOptions(items: MasterDataItemSummary[] | undefined, language: 'ar' | 'en') {
  return (items ?? [])
    .filter(isPublished)
    .map((item) => ({ value: item.id, label: item.label[language] }));
}

/** Names and choices for the catalogue items a concern names. */
export function useConcernLookups(): ConcernLookups {
  const { t, language } = useI18n();
  const catalogues = useApiResource(readCatalogues);
  const all: MasterDataItemSummary[] =
    catalogues.data === undefined ? [] : Object.values(catalogues.data).flat();
  return {
    categoryOptions: publishedOptions(catalogues.data?.CONCERN_CATEGORY, language),
    priorityOptions: publishedOptions(catalogues.data?.PRIORITY, language),
    itemLabel: (id) => {
      const item = all.find((candidate) => candidate.id === id);
      return item === undefined
        ? t('projects.lookups.unknownItem', { id: shortId(id) })
        : item.label[language];
    },
    readable: catalogues.data !== undefined,
  };
}

/** Module-level, so useApiResource reads the roles once per mount. Without ROLE_VIEW (R01 only) there are none. */
async function readRoles(signal: AbortSignal) {
  try {
    return await rolesApi.list(signal);
  } catch {
    return [];
  }
}

export interface RoleNames {
  /** "Department Manager (R03)", or "Role 1a2b3c4d" when the roles cannot be read. */
  name: (id: string) => string;
  /** The role's code, or null when the roles cannot be read. */
  code: (id: string) => string | null;
}

/** An escalation names the role it is addressed to by id (TASK-057 D-6). */
export function useRoleNames(): RoleNames {
  const { t, language } = useI18n();
  const roles = useApiResource(readRoles).data ?? [];
  const find = (id: string) => roles.find((candidate) => candidate.id === id);
  return {
    name: (id) => {
      const role = find(id);
      return role === undefined
        ? t('issuesChallenges.escalation.unknownRole', { id: shortId(id) })
        : `${role.name[language]} (${role.code})`;
    },
    code: (id) => find(id)?.code ?? null,
  };
}

/** SCR-084 and SCR-086: the concern with its ETag, and its escalations, newest first. */
export interface ConcernRecord {
  concern: ApiResponse<ConcernDetail>;
  escalations: ConcernEscalationDetail[];
}

export function useConcernRecord(concernId: string): ApiResource<ConcernRecord> {
  const load = useCallback(
    async (signal: AbortSignal): Promise<ConcernRecord> => {
      const [concern, escalations] = await Promise.all([
        concernsApi.concern(concernId, signal),
        concernsApi.escalations(concernId, signal),
      ]);
      return { concern, escalations };
    },
    [concernId],
  );
  return useApiResource(load, { keepWhileReloading: true });
}

/**
 * SCR-088: the escalation and the concern it escalates. The project is read for its title only: the role an
 * escalation is addressed to may not see projects at all (the Department Manager holds no PROJECT_VIEW), so a refusal
 * leaves it null.
 */
export interface EscalationRecord {
  escalation: ConcernEscalationDetail;
  concern: ConcernDetail;
  project: ProjectDetail | null;
}

export function useEscalationRecord(escalationId: string): ApiResource<EscalationRecord> {
  const load = useCallback(
    async (signal: AbortSignal): Promise<EscalationRecord> => {
      const escalation = await concernsApi.escalation(escalationId, signal);
      const { data: concern } = await concernsApi.concern(escalation.managementConcernId, signal);
      let project: ProjectDetail | null = null;
      try {
        project = (await projectsApi.get(concern.projectId, signal)).data;
      } catch (error) {
        if (!isForbidden(error)) {
          throw error;
        }
      }
      return { escalation, concern, project };
    },
    [escalationId],
  );
  return useApiResource(load, { keepWhileReloading: true });
}
