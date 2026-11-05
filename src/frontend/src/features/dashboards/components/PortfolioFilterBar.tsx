import { type ReactElement, type ReactNode } from 'react';

import { useOrganizationUnits } from '@/features/identity-access/names/useOrganizationUnits.ts';
import { type ProjectStatus } from '@/features/projects/api/types.ts';
import { shortId } from '@/features/projects/presentation.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { FilterBar, type FilterBarField } from '@/shared/ui/FilterBar.tsx';

import { type PortfolioFilters } from '../api/dashboardsApi.ts';
import { type DashboardFilter, type DashboardPlace } from '../api/types.ts';
import { useDepartmentNames } from '../useDepartmentNames.ts';
import { usePlaceName } from '../usePlaceName.ts';

/** The options each filter offers: the view's own, never more (DSH-CC-17). */
export interface FilterOptions {
  departments: string[];
  entities: string[];
  regions: string[];
  cities: string[];
  places: DashboardPlace[];
  /** Every filter with its counts, when the API gives them; the four lists above otherwise. */
  filters: DashboardFilter[];
}

export type FilterName = keyof PortfolioFilters;

/** The bar's order (review §1): sector, entity, department, region, city; then, under More filters, the rest. */
const ORDER: { name: FilterName; more: boolean }[] = [
  { name: 'classificationItemId', more: false },
  { name: 'externalEntityId', more: false },
  { name: 'departmentId', more: false },
  { name: 'regionItemId', more: false },
  { name: 'cityItemId', more: false },
  { name: 'status', more: true },
  { name: 'priorityItemId', more: true },
  { name: 'strategicPortfolioItemId', more: true },
  { name: 'strategicInitiativeItemId', more: true },
  { name: 'period', more: true },
];

/**
 * The portfolio's filters (the 27 Oct and 2 Nov 2026 reviews) on the one filter bar: each offers only the values of the projects the person may
 * see there, with how many have each. A city is offered under the region chosen, an initiative under the portfolio chosen.
 */
export function PortfolioFilterBar({
  options,
  filters,
  onChange,
  onClearAll,
  actions,
}: {
  options: FilterOptions;
  filters: PortfolioFilters;
  onChange: (name: FilterName, value: string | undefined) => void;
  onClearAll: () => void;
  /** The dashboard's "Updated…", Refresh and Customize layout, on the Filters button's row. */
  actions?: ReactNode;
}): ReactElement | null {
  const { t, language } = useI18n();
  const departmentName = useDepartmentNames();
  const { externalEntities } = useOrganizationUnits();
  const placeName = usePlaceName(options.places);
  const entityName = (id: string) =>
    externalEntities.find((entity) => entity.id === id)?.name[language] ??
    t('projects.lookups.unknownItem', { id: shortId(id) });
  const parentOf = (id: string) =>
    options.places.find((place) => place.id === id)?.parentItemId ?? null;

  const counted = (name: FilterName): { value: string; count?: number }[] => {
    const filter = options.filters.find((candidate) => candidate.field === name);
    if (filter !== undefined) {
      return filter.options.map((option) => ({ value: option.value, count: option.projectCount }));
    }
    const legacy: Partial<Record<FilterName, string[]>> = {
      departmentId: options.departments,
      externalEntityId: options.entities,
      regionItemId: options.regions,
      cityItemId: options.cities,
    };
    return (legacy[name] ?? []).map((value) => ({ value }));
  };

  const fields: FilterBarField[] = ORDER.map(({ name, more }) => {
    const nameOf =
      name === 'departmentId'
        ? departmentName
        : name === 'externalEntityId'
          ? entityName
          : name === 'status'
            ? (value: string) => t(`projects.status.${value as ProjectStatus}`)
            : name === 'period'
              ? (value: string) => value
              : placeName;
    const parentFilter =
      name === 'cityItemId'
        ? filters.regionItemId
        : name === 'strategicInitiativeItemId'
          ? filters.strategicPortfolioItemId
          : undefined;
    return {
      name,
      more,
      label: t(`dashboards.filter.${name}`),
      all: t(`dashboards.filter.all.${name}`),
      options: counted(name)
        .filter((option) => {
          const parent = parentOf(option.value);
          return parentFilter === undefined || parent === null || parent === parentFilter;
        })
        .map((option) => ({
          value: option.value,
          label: nameOf(option.value),
          count: option.count,
        }))
        .sort((a, b) =>
          name === 'period' ? b.label.localeCompare(a.label) : a.label.localeCompare(b.label),
        ),
    };
  });

  return (
    <FilterBar
      fields={fields}
      values={{ ...filters }}
      onChange={(name, value) => {
        onChange(name as FilterName, value);
      }}
      onClearAll={onClearAll}
      actions={actions}
    />
  );
}
