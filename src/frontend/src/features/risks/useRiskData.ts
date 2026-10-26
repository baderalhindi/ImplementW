import { useCallback } from 'react';

import { projectsApi } from '@/features/projects/api/projectsApi.ts';
import { type ProjectSummary } from '@/features/projects/api/types.ts';
import { shortId } from '@/features/projects/presentation.ts';
import { type ApiResponse } from '@/shared/api/httpClient.ts';
import {
  isPublished,
  type MasterDataItemSummary,
  readCatalogueItems,
} from '@/shared/api/masterData.ts';
import { type ApiResource, useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { type SelectOption } from '@/shared/ui/FormFields.tsx';

import { risksApi } from './api/risksApi.ts';
import {
  type RiskAcceptanceDetail,
  type RiskAssessmentDetail,
  type RiskDetail,
  type RiskTreatmentActionDetail,
} from './api/types.ts';
import { isForbidden, isMatrixMissing } from './problems.ts';
import { type RiskEntry, type RiskMatrix, riskMatrixOf } from './riskRules.ts';

/** The risks stay on screen while they are read again, so a dialog open over them is not unmounted. */
export function useProjectRisks(projectId: string): ApiResource<RiskDetail[]> {
  const load = useCallback((signal: AbortSignal) => risksApi.risks(projectId, signal), [projectId]);
  return useApiResource(load, { keepWhileReloading: true });
}

/** The project states a risk can exist in: registered from APPROVED_PLANNED (TASK-055 §2) and kept after. */
const RISK_PROJECT_STATUSES = 'APPROVED_PLANNED,ACTIVE,SUSPENDED,COMPLETED,CLOSED';

/** Projects read at once: enough to be quick, few enough not to flood the API. */
const PARALLEL_READS = 6;

export interface RiskPortfolio {
  projects: ProjectSummary[];
  entries: RiskEntry[];
}

/**
 * SCR-080 and SCR-081 across projects: the API lists risks one project at a time (TASK-055 F-9), so the projects the
 * caller may see are read first, then each one's risks. A project whose risks the caller may not see answers an empty
 * page.
 */
async function readPortfolio(signal: AbortSignal): Promise<RiskPortfolio> {
  const projects = await projectsApi.listAll({ status: RISK_PROJECT_STATUSES }, signal);
  const entries: RiskEntry[] = [];
  for (let index = 0; index < projects.length; index += PARALLEL_READS) {
    const batch = projects.slice(index, index + PARALLEL_READS);
    const read = await Promise.all(
      batch.map(async (project) =>
        (await risksApi.risks(project.id, signal)).map((risk) => ({ risk, project })),
      ),
    );
    entries.push(...read.flat());
  }
  return { projects, entries };
}

export function useRiskPortfolio(): ApiResource<RiskPortfolio> {
  return useApiResource(readPortfolio, { keepWhileReloading: true });
}

/** The matrix in force, or why it cannot be drawn: not readable (CONFIGURATION_VIEW) or none published. */
export type MatrixState =
  { kind: 'ready'; matrix: RiskMatrix } | { kind: 'forbidden' } | { kind: 'missing' };

async function readMatrix(signal: AbortSignal): Promise<MatrixState> {
  try {
    return { kind: 'ready', matrix: riskMatrixOf(await risksApi.matrix(signal)) };
  } catch (error) {
    if (isForbidden(error)) {
      return { kind: 'forbidden' };
    }
    if (isMatrixMissing(error)) {
      return { kind: 'missing' };
    }
    throw error;
  }
}

/**
 * The RISK_MATRIX version in force, read whenever a screen opens (acceptance criterion 1: a newly published version
 * shows on the next load; nothing is cached or built in).
 */
export function useRiskMatrix(): ApiResource<MatrixState> {
  return useApiResource(readMatrix);
}

/** The matrix when it could be read, else null. */
export function matrixOf(state: ApiResource<MatrixState>): RiskMatrix | null {
  return state.data?.kind === 'ready' ? state.data.matrix : null;
}

const CATALOGUE_CODES = ['RISK_CATEGORY', 'IMPACT_DIMENSION'] as const;

function readCatalogues(signal: AbortSignal) {
  return readCatalogueItems(CATALOGUE_CODES, signal);
}

export interface RiskLookups {
  /** PUBLISHED RISK_CATEGORY items, in display order. */
  categoryOptions: SelectOption[];
  /** A category's or impact dimension's label; "Item 1a2b3c4d" when the catalogues cannot be read. */
  itemLabel: (id: string) => string;
  /** False when the catalogues were refused (MASTER_DATA_VIEW, R01 only: TASK-038 F-1). */
  readable: boolean;
}

/** Names and choices for the catalogue items a risk and its assessments name. */
export function useRiskLookups(): RiskLookups {
  const { t, language } = useI18n();
  const catalogues = useApiResource(readCatalogues);
  const all: MasterDataItemSummary[] =
    catalogues.data === undefined ? [] : Object.values(catalogues.data).flat();
  return {
    categoryOptions: (catalogues.data?.RISK_CATEGORY ?? [])
      .filter(isPublished)
      .map((item) => ({ value: item.id, label: item.label[language] })),
    itemLabel: (id) => {
      const item = all.find((candidate) => candidate.id === id);
      return item === undefined
        ? t('projects.lookups.unknownItem', { id: shortId(id) })
        : item.label[language];
    },
    readable: catalogues.data !== undefined,
  };
}

/** SCR-082's reads: the risk with its ETag, and its assessment, acceptance and treatment histories. */
export interface RiskRecord {
  risk: ApiResponse<RiskDetail>;
  /** Newest first. */
  assessments: RiskAssessmentDetail[];
  /** Newest first. */
  acceptances: RiskAcceptanceDetail[];
  /** Oldest first. */
  actions: RiskTreatmentActionDetail[];
}

export function useRiskRecord(riskId: string): ApiResource<RiskRecord> {
  const load = useCallback(
    async (signal: AbortSignal): Promise<RiskRecord> => {
      const [risk, assessments, acceptances, actions] = await Promise.all([
        risksApi.risk(riskId, signal),
        risksApi.assessments(riskId, signal),
        risksApi.acceptances(riskId, signal),
        risksApi.actions(riskId, signal),
      ]);
      return { risk, assessments, acceptances, actions };
    },
    [riskId],
  );
  return useApiResource(load, { keepWhileReloading: true });
}
