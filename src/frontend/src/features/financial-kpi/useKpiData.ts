import { useCallback } from 'react';

import { projectsApi } from '@/features/projects/api/projectsApi.ts';
import { type ProjectSummary } from '@/features/projects/api/types.ts';
import { shortId } from '@/features/projects/presentation.ts';
import { type MasterDataItemSummary, readCatalogueItems } from '@/shared/api/masterData.ts';
import { type ApiResource, useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';

import { financialKpiApi } from './api/financialKpiApi.ts';
import {
  type KpiAssignmentDetail,
  type KpiDefinitionSummary,
  type KpiDirection,
  type KpiMeasurementDetail,
  type KpiPortfolioAggregate,
  type KpiTargetVersionDetail,
} from './api/types.ts';
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

/** One KPI of a project with what its screens show: its target versions and every measurement. */
export interface KpiEntry {
  assignment: KpiAssignmentDetail;
  /** Newest first; empty on the register, which does not read them. */
  targets: KpiTargetVersionDetail[];
  /** Latest period first, each pinned to its own target version. */
  measurements: KpiMeasurementDetail[];
}

async function readEntry(
  assignment: KpiAssignmentDetail,
  withTargets: boolean,
  signal: AbortSignal,
): Promise<KpiEntry> {
  const [targets, measurements] = await Promise.all([
    withTargets ? financialKpiApi.targetVersions(assignment.id, signal) : Promise.resolve([]),
    financialKpiApi.measurements(assignment.id, signal),
  ]);
  return { assignment, targets, measurements };
}

/** SCR-050: every KPI assigned to the project, read whole. */
export function useProjectKpis(projectId: string): ApiResource<KpiEntry[]> {
  const load = useCallback(
    async (signal: AbortSignal) => {
      const assignments = await financialKpiApi.assignments(projectId, signal);
      return Promise.all(assignments.map((assignment) => readEntry(assignment, true, signal)));
    },
    [projectId],
  );
  return useApiResource(load, { keepWhileReloading: true });
}

/** SCR-073: one KPI of the project, read whole. */
export function useKpiEntry(assignmentId: string): ApiResource<KpiEntry> {
  const load = useCallback(
    async (signal: AbortSignal) =>
      readEntry((await financialKpiApi.assignment(assignmentId, signal)).data, true, signal),
    [assignmentId],
  );
  return useApiResource(load);
}

/** The project states a KPI can be assigned in and kept after (FINANCIAL_KPI_PROJECT_NOT_ELIGIBLE before). */
const KPI_PROJECT_STATUSES = 'APPROVED_PLANNED,ACTIVE,SUSPENDED,COMPLETED,CLOSED';

/** Projects read at once: enough to be quick, few enough not to flood the API. */
const PARALLEL_READS = 6;

/** The aggregate takes 1 to 200 projects (TASK-052 D-11). */
const AGGREGATE_PROJECT_LIMIT = 200;

export interface KpiRegisterEntry extends KpiEntry {
  project: ProjectSummary;
}

export interface KpiDefinitionAggregate {
  kpiDefinitionId: string;
  /** Null when the caller may not read it. */
  aggregate: KpiPortfolioAggregate | null;
}

export interface KpiPortfolio {
  projects: ProjectSummary[];
  entries: KpiRegisterEntry[];
  aggregates: KpiDefinitionAggregate[];
}

/**
 * SCR-072 reads every KPI the caller may see: the API lists assignments one project at a time, so the projects are read
 * first, then each one's assignments and their measurements (F-7). Then, for each KPI, the API's own aggregate of the
 * latest published values across the projects that carry it: combined only within one unit, else partial (D-11).
 */
async function readPortfolio(signal: AbortSignal): Promise<KpiPortfolio> {
  const projects = await projectsApi.listAll({ status: KPI_PROJECT_STATUSES }, signal);
  const entries: KpiRegisterEntry[] = [];
  for (let index = 0; index < projects.length; index += PARALLEL_READS) {
    const batch = projects.slice(index, index + PARALLEL_READS);
    const read = await Promise.all(
      batch.map(async (project) => {
        const assignments = await financialKpiApi.assignments(project.id, signal);
        const projectEntries = await Promise.all(
          assignments.map((assignment) => readEntry(assignment, false, signal)),
        );
        return projectEntries.map((entry) => ({ ...entry, project }));
      }),
    );
    entries.push(...read.flat());
  }
  const projectsByKpi = new Map<string, Set<string>>();
  for (const { assignment } of entries) {
    const set = projectsByKpi.get(assignment.kpiDefinitionId) ?? new Set<string>();
    set.add(assignment.projectId);
    projectsByKpi.set(assignment.kpiDefinitionId, set);
  }
  const aggregates = await Promise.all(
    [...projectsByKpi].map(async ([kpiDefinitionId, ids]) => ({
      kpiDefinitionId,
      aggregate: await unlessForbidden(
        financialKpiApi.kpiAggregate(
          kpiDefinitionId,
          [...ids].slice(0, AGGREGATE_PROJECT_LIMIT),
          signal,
        ),
      ),
    })),
  );
  return { projects, entries, aggregates };
}

export function useKpiPortfolio(): ApiResource<KpiPortfolio> {
  return useApiResource(readPortfolio, { keepWhileReloading: true });
}

const CATALOGUE_CODES = ['KPI_UNIT', 'MEASUREMENT_FREQUENCY'] as const;

interface LookupData {
  definitions: KpiDefinitionSummary[] | null;
  items: MasterDataItemSummary[];
}

async function readLookups(signal: AbortSignal): Promise<LookupData> {
  const [definitions, catalogues] = await Promise.all([
    unlessForbidden(financialKpiApi.definitions(signal)),
    unlessForbidden(readCatalogueItems(CATALOGUE_CODES, signal)),
  ]);
  return {
    definitions,
    items: catalogues === null ? [] : Object.values(catalogues).flat(),
  };
}

export interface KpiLookups {
  /** The KPI's name in the interface language; "KPI 1a2b3c4d" when the catalogue cannot be read (F-3). */
  kpiName: (kpiDefinitionId: string) => string;
  /** The KPI's direction, or null when the catalogue cannot be read. */
  direction: (kpiDefinitionId: string) => KpiDirection | null;
  /** A KPI_UNIT or MEASUREMENT_FREQUENCY item's label; "Item 1a2b3c4d" when unreadable. */
  itemLabel: (itemId: string) => string;
}

/** Names for what an assignment carries only by id: its KPI, unit and frequency (MASTER_DATA_VIEW, R01 only). */
export function useKpiLookups(): KpiLookups {
  const { t, language } = useI18n();
  const lookups = useApiResource(readLookups).data;
  const definition = (id: string) => lookups?.definitions?.find((candidate) => candidate.id === id);
  return {
    kpiName: (id) =>
      definition(id)?.name[language] ?? t('financialKpi.kpi.unnamed', { id: shortId(id) }),
    direction: (id) => definition(id)?.direction ?? null,
    itemLabel: (id) => {
      const item = lookups?.items.find((candidate) => candidate.id === id);
      return item === undefined
        ? t('projects.lookups.unknownItem', { id: shortId(id) })
        : item.label[language];
    },
  };
}
