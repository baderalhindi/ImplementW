import { type ReactElement } from 'react';

import { useOrganizationUnits } from '@/features/identity-access/names/useOrganizationUnits.ts';
import { shortId } from '@/features/projects/presentation.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { SelectField } from '@/shared/ui/FormFields.tsx';

import { type PortfolioFilters } from '../api/dashboardsApi.ts';
import { type DashboardPlace } from '../api/types.ts';
import { useDepartmentNames } from '../useDepartmentNames.ts';
import { usePlaceName } from '../usePlaceName.ts';

/** The options each filter offers: the view's own, never more (DSH-CC-17). */
export interface FilterOptions {
  departments: string[];
  entities: string[];
  regions: string[];
  cities: string[];
  places: DashboardPlace[];
}

export type FilterName = keyof PortfolioFilters;

/**
 * The portfolio's filters (the 27 Oct 2026 review): sector, delivering entity, region and city, each offering only the values of the
 * projects the person may see there. A city is offered under the region chosen. A filter with nothing to choose between is not shown.
 */
export function PortfolioFilterBar({
  options,
  filters,
  onChange,
}: {
  options: FilterOptions;
  filters: PortfolioFilters;
  onChange: (name: FilterName, value: string | undefined) => void;
}): ReactElement | null {
  const { t, language } = useI18n();
  const departmentName = useDepartmentNames();
  const { externalEntities } = useOrganizationUnits();
  const placeName = usePlaceName(options.places);
  const entityName = (id: string) =>
    externalEntities.find((entity) => entity.id === id)?.name[language] ??
    t('projects.lookups.unknownItem', { id: shortId(id) });
  const cities = options.cities.filter((id) => {
    const parent = options.places.find((place) => place.id === id)?.parentItemId ?? null;
    return filters.regionItemId === undefined || parent === null || parent === filters.regionItemId;
  });

  const fields: {
    name: FilterName;
    label: string;
    all: string;
    ids: string[];
    nameOf: (id: string) => string;
  }[] = [
    {
      name: 'departmentId',
      label: t('dashboards.filter.department'),
      all: t('dashboards.filter.allDepartments'),
      ids: options.departments,
      nameOf: departmentName,
    },
    {
      name: 'externalEntityId',
      label: t('dashboards.filter.entity'),
      all: t('dashboards.filter.allEntities'),
      ids: options.entities,
      nameOf: entityName,
    },
    {
      name: 'regionItemId',
      label: t('dashboards.filter.region'),
      all: t('dashboards.filter.allRegions'),
      ids: options.regions,
      nameOf: placeName,
    },
    {
      name: 'cityItemId',
      label: t('dashboards.filter.city'),
      all: t('dashboards.filter.allCities'),
      ids: cities,
      nameOf: placeName,
    },
  ];
  const shown = fields.filter(
    (field) =>
      field.ids.length > 1 || (field.ids.length === 1 && filters[field.name] !== undefined),
  );
  if (shown.length === 0) {
    return null;
  }
  return (
    <div className="filters filters--inline" role="search" aria-label={t('common.filters.label')}>
      {shown.map((field) => {
        const value = filters[field.name];
        return (
          <SelectField
            key={field.name}
            label={field.label}
            name={field.name}
            value={value !== undefined && field.ids.includes(value) ? value : ''}
            placeholder={field.all}
            options={field.ids
              .map((id) => ({ value: id, label: field.nameOf(id) }))
              .sort((a, b) => a.label.localeCompare(b.label))}
            onChange={(chosen) => {
              onChange(field.name, chosen === '' ? undefined : chosen);
            }}
          />
        );
      })}
    </div>
  );
}
