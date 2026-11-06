import { useListParams } from '@/shared/api/useListParams.ts';

import { isProjectStatus } from '../presentation.ts';

/** The register's filter bar parameters besides `q`, `status` and the two organization ones (the 2 Nov 2026 review, R3-1). */
export const ITEM_FILTERS = [
  'classificationItemId',
  'strategicPortfolioItemId',
  'strategicInitiativeItemId',
  'priorityItemId',
  'regionItemId',
  'cityItemId',
  'period',
] as const;

export interface ProjectFilterValues {
  q: string;
  status: ReturnType<typeof statusOf>;
  departmentId: string | undefined;
  externalEntityId: string | undefined;
  itemFilters: Record<(typeof ITEM_FILTERS)[number], string | undefined>;
  /** The item filters as one string, so a loader changes only when their values do. */
  itemKey: string;
}

function statusOf(param: string) {
  return isProjectStatus(param) ? param : undefined;
}

/** The register's filters as the address holds them; `mine` keeps only the search and the status. */
export function useProjectFilterValues(mine = false): ProjectFilterValues {
  const { params } = useListParams();
  const itemFilters = Object.fromEntries(
    ITEM_FILTERS.map((name) => [name, mine ? undefined : (params.get(name) ?? undefined)]),
  ) as Record<(typeof ITEM_FILTERS)[number], string | undefined>;
  return {
    q: params.get('q') ?? '',
    status: statusOf(params.get('status') ?? ''),
    departmentId: mine ? undefined : (params.get('departmentId') ?? undefined),
    externalEntityId: mine ? undefined : (params.get('externalEntityId') ?? undefined),
    itemFilters,
    itemKey: ITEM_FILTERS.map((name) => itemFilters[name] ?? '').join('|'),
  };
}
