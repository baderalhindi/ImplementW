import { type ApiResponse, type PlatformApi } from './api.ts';

// FG-04 configuration (docs/architecture/master-data-configuration.md §4) as its author, reviewer and publisher call it.
// There are no FG-04 screens (docs/testing/test-strategy.md §6), so the journeys author and publish through the API; what a
// published version governs is observed in the screens of the module that resolves it.

export type ConfigurationLifecycle = 'DRAFT' | 'VALIDATED' | 'PUBLISHED' | 'RETIRED';

export interface RiskMatrixCell {
  probabilityLevel: number;
  impactLevel: number;
  ratingCode: string;
}

export interface RiskRating {
  code: string;
  label: { ar: string; en: string };
  sortOrder: number;
}

export interface RiskMatrixContent {
  riskRatings: RiskRating[];
  riskMatrixCells: RiskMatrixCell[];
  [section: string]: unknown;
}

export interface ResolvedConfiguration<C = Record<string, unknown>> {
  versionId: string;
  familyCode: string;
  versionNo: number;
  effectiveFrom: string;
  effectiveTo: string | null;
  content: C;
}

export interface ConfigurationVersion<C = Record<string, unknown>> {
  id: string;
  familyId: string;
  familyCode: string;
  versionNo: number;
  effectiveFrom: string | null;
  effectiveTo: string | null;
  content: C;
  governance: { lifecycleState: ConfigurationLifecycle };
}

/** The version of a family in force at `asOf` (now when omitted), as every consumer resolves it (D-5). */
export function resolve<C>(
  api: PlatformApi,
  familyCode: string,
  asOf?: string,
): Promise<ResolvedConfiguration<C>> {
  const query = asOf === undefined ? '' : `?asOf=${encodeURIComponent(asOf)}`;
  return api.get<ResolvedConfiguration<C>>(`/configuration-resolutions/${familyCode}${query}`);
}

export function readVersion<C>(
  api: PlatformApi,
  versionId: string,
): Promise<ApiResponse<ConfigurationVersion<C>>> {
  return api.ok<ConfigurationVersion<C>>('GET', `/configuration-versions/${versionId}`);
}

/** A new DRAFT of the family, copied from `basedOnVersionId`, by its author. */
export async function createDraft<C>(
  author: PlatformApi,
  familyCode: string,
  basedOnVersionId: string,
  summary: string,
): Promise<ApiResponse<ConfigurationVersion<C>>> {
  const families = await author.get<{ id: string; code: string }[]>('/configuration-families');
  const familyId = families.find((family) => family.code === familyCode)?.id;
  if (familyId === undefined) throw new Error(`No configuration family ${familyCode}.`);
  return author.ok<ConfigurationVersion<C>>('POST', '/configuration-versions', {
    familyId,
    basedOnVersionId,
    changeSummary: { text: summary, language: 'en' },
  });
}

/** The rating a RISK_MATRIX content gives a cell; fails when the cell is not mapped. */
export function ratingAt(
  content: RiskMatrixContent,
  probability: number,
  impact: number,
): RiskRating {
  const code = content.riskMatrixCells.find(
    (cell) => cell.probabilityLevel === probability && cell.impactLevel === impact,
  )?.ratingCode;
  const rating = content.riskRatings.find((r) => r.code === code);
  if (rating === undefined)
    throw new Error(`The matrix maps no rating at ${String(probability)} × ${String(impact)}.`);
  return rating;
}

/** The same matrix with one cell rated `ratingCode`: levels, dimensions and ratings unchanged, so it stays valid. */
export function remapCell(
  content: RiskMatrixContent,
  probability: number,
  impact: number,
  ratingCode: string,
): RiskMatrixContent {
  return {
    ...content,
    riskMatrixCells: content.riskMatrixCells.map((cell) =>
      cell.probabilityLevel === probability && cell.impactLevel === impact
        ? { ...cell, ratingCode }
        : cell,
    ),
  };
}
