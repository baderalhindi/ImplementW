import { createContext, useCallback, useContext } from 'react';

import { risksApi } from '@/features/risks/api/risksApi.ts';
import { type RiskMatrix, riskMatrixOf } from '@/features/risks/riskRules.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';

import { type Visual } from './designApi.ts';

// The risk matrix in force, which names a risk's rating and levels (riskFields.ts riskCodeText): read once by the screen drawing designed
// visuals, and only when one of them counts risks.

export const RiskMatrixContext = createContext<RiskMatrix | null>(null);

/** The matrix the screen read; null when none of its visuals counts risks, or the caller may not read it. */
export function useDesignRiskMatrix(): RiskMatrix | null {
  return useContext(RiskMatrixContext);
}

/** Whether any visual, a container's included, counts risks. */
export function countsRisks(visuals: readonly Pick<Visual, 'source' | 'children'>[]): boolean {
  return visuals.some((visual) => visual.source === 'RISKS' || countsRisks(visual.children ?? []));
}

/** Reads the matrix in force when `wanted`; a matrix the caller may not read, or none published, is null (the codes then read as they are). */
export function useRiskMatrixWhen(wanted: boolean): RiskMatrix | null {
  const matrix = useApiResource(
    useCallback(
      async (signal: AbortSignal) => (wanted ? riskMatrixOf(await risksApi.matrix(signal)) : null),
      [wanted],
    ),
  );
  return matrix.data ?? null;
}
