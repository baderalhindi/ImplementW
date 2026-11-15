import { useListParams } from '@/shared/api/useListParams.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { type FilterBarChip } from '@/shared/ui/FilterBar.tsx';

import { isProjectStatus } from '../presentation.ts';
import { useProjectLookups } from '../useProjectLookups.ts';

/**
 * The dashboard's filter bar as a list carries it when a dashboard count opens the list (review round 8, point 01). The names are the
 * register's and the API's, so the list reads the same projects the count did.
 */
export const CARRIED_FILTERS = [
  'externalEntityId',
  'classificationItemId',
  'regionItemId',
  'cityItemId',
  'status',
  'priorityItemId',
  'strategicPortfolioItemId',
  'strategicInitiativeItemId',
  'period',
] as const;

export type CarriedFilter = (typeof CARRIED_FILTERS)[number];

export interface CarriedFilters {
  values: Record<CarriedFilter, string | undefined>;
  /** The values as one string, so a loader changes only when they do. */
  key: string;
  /** One removable chip per carried filter. */
  chips: FilterBarChip[];
  /** The changes that clear every carried filter, for a list's Clear all. */
  cleared: Record<CarriedFilter, ''>;
}

/** The carried filters in the page's URL, with a chip naming each one in words. */
export function useCarriedFilters(): CarriedFilters {
  const { t } = useI18n();
  const { params, setFilter } = useListParams();
  const lookups = useProjectLookups();
  const values = Object.fromEntries(
    CARRIED_FILTERS.map((name) => [name, params.get(name) ?? undefined]),
  ) as Record<CarriedFilter, string | undefined>;
  const text = (name: CarriedFilter, value: string): string => {
    switch (name) {
      case 'externalEntityId':
        return lookups.entityName(value) ?? value;
      case 'status':
        return isProjectStatus(value) ? t(`projects.status.${value}`) : value;
      case 'period':
        return value;
      default:
        return lookups.itemLabel(value) ?? value;
    }
  };
  return {
    values,
    key: CARRIED_FILTERS.map((name) => values[name] ?? '').join('|'),
    chips: CARRIED_FILTERS.flatMap((name) => {
      const value = values[name];
      return value === undefined
        ? []
        : [
            {
              name,
              text: t('common.filters.inline', {
                label: t(`dashboards.filter.${name}`),
                value: text(name, value),
              }),
              onRemove: () => {
                setFilter(name, '');
              },
            },
          ];
    }),
    cleared: Object.fromEntries(CARRIED_FILTERS.map((name) => [name, ''])) as Record<
      CarriedFilter,
      ''
    >,
  };
}
