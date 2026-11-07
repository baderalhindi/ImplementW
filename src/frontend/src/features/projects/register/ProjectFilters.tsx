import { type ReactElement } from 'react';
import { useNavigate } from 'react-router';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { FilterBar, type FilterBarField } from '@/shared/ui/FilterBar.tsx';
import { TextField } from '@/shared/ui/FormFields.tsx';
import { useSearchText } from '@/shared/ui/useSearchText.ts';

import { PROJECT_STATUSES } from '../presentation.ts';
import { type ProjectCatalogueCode, type ProjectLookups } from '../useProjectLookups.ts';

interface ProjectFiltersProps {
  q: string;
  values: Partial<Record<string, string | undefined>>;
  organizationFilters: boolean;
  /** An external user sees one entity's projects only (ADR-013), so has nothing to filter by. */
  entityFilter: boolean;
  lookups: ProjectLookups;
  basePath: string;
  onSearch: (text: string) => void;
  onFilter: (changes: Record<string, string>) => void;
}

/**
 * The register's search and the one filter bar the dashboards use (the 2 Nov 2026 review, R3-1): sector, entity, department, region and
 * city in its first row, status, priority, strategic portfolio and initiative under More filters. A filter applies as it is chosen, the search
 * as it is typed.
 */
export function ProjectFilters({
  q,
  values,
  organizationFilters,
  entityFilter,
  lookups,
  basePath,
  onSearch,
  onFilter,
}: ProjectFiltersProps): ReactElement {
  const { t } = useI18n();
  const navigate = useNavigate();
  const [text, setText] = useSearchText(q, onSearch);

  // A department or entity filter needs their names; without them it is not offered (F-1).
  const organizationReadable = lookups.organizationError === null;
  const items = (code: ProjectCatalogueCode, parent?: string) =>
    lookups
      .options(code, parent)
      .filter((option) => parent === undefined || lookups.parentOf(option.value) === parent);
  const fields: FilterBarField[] = organizationFilters
    ? [
        {
          name: 'classificationItemId',
          label: t('dashboards.filter.classificationItemId'),
          all: t('dashboards.filter.all.classificationItemId'),
          options: items('PROJECT_CLASSIFICATION'),
        },
        ...(entityFilter && organizationReadable
          ? [
              {
                name: 'externalEntityId',
                label: t('dashboards.filter.externalEntityId'),
                all: t('dashboards.filter.all.externalEntityId'),
                options: lookups.entityOptions,
              },
            ]
          : []),
        ...(organizationReadable
          ? [
              {
                name: 'departmentId',
                label: t('dashboards.filter.departmentId'),
                all: t('dashboards.filter.all.departmentId'),
                options: lookups.departmentOptions,
              },
            ]
          : []),
        {
          name: 'regionItemId',
          label: t('dashboards.filter.regionItemId'),
          all: t('dashboards.filter.all.regionItemId'),
          options: items('REGION'),
        },
        {
          name: 'cityItemId',
          label: t('dashboards.filter.cityItemId'),
          all: t('dashboards.filter.all.cityItemId'),
          options: items('CITY', values.regionItemId),
        },
        {
          name: 'status',
          more: true,
          label: t('dashboards.filter.status'),
          all: t('dashboards.filter.all.status'),
          options: STATUS_OPTIONS(t),
        },
        {
          name: 'priorityItemId',
          more: true,
          label: t('dashboards.filter.priorityItemId'),
          all: t('dashboards.filter.all.priorityItemId'),
          options: items('PRIORITY'),
        },
        {
          name: 'strategicPortfolioItemId',
          more: true,
          label: t('dashboards.filter.strategicPortfolioItemId'),
          all: t('dashboards.filter.all.strategicPortfolioItemId'),
          options: items('STRATEGIC_PORTFOLIO'),
        },
        {
          name: 'strategicInitiativeItemId',
          more: true,
          label: t('dashboards.filter.strategicInitiativeItemId'),
          all: t('dashboards.filter.all.strategicInitiativeItemId'),
          options:
            values.strategicPortfolioItemId === undefined
              ? []
              : items('STRATEGIC_INITIATIVE', values.strategicPortfolioItemId),
        },
        {
          name: 'period',
          more: true,
          label: t('dashboards.filter.period'),
          all: t('dashboards.filter.all.period'),
          options: PERIOD_OPTIONS,
        },
      ]
    : [
        {
          name: 'status',
          label: t('dashboards.filter.status'),
          all: t('dashboards.filter.all.status'),
          options: STATUS_OPTIONS(t),
        },
      ];
  return (
    <>
      <form
        className="filters filters--inline"
        role="search"
        aria-label={t('common.actions.search')}
        onSubmit={(event) => {
          event.preventDefault();
        }}
      >
        <TextField
          label={t('projects.filters.q')}
          name="q"
          type="search"
          dir="auto"
          value={text}
          onChange={setText}
        />
      </form>
      <FilterBar
        fields={[
          ...fields,
          {
            name: 'sort',
            label: t('common.filters.sort'),
            all: t('projects.sort.UpdatedDescending'),
            options: [
              { value: 'updatedAt:asc', label: t('projects.sort.UpdatedAscending') },
              { value: 'title:asc', label: t('projects.sort.TitleAscending') },
              { value: 'title:desc', label: t('projects.sort.TitleDescending') },
              { value: 'formalProjectId:asc', label: t('projects.sort.FormalIdAscending') },
              { value: 'formalProjectId:desc', label: t('projects.sort.FormalIdDescending') },
            ],
          },
        ]}
        values={values}
        onChange={(name, value) => {
          // A city lies in a region, an initiative in a portfolio: another parent drops the child chosen under the last one.
          onFilter({
            [name]: value ?? '',
            ...(name === 'regionItemId' ? { cityItemId: '' } : {}),
            ...(name === 'strategicPortfolioItemId' ? { strategicInitiativeItemId: '' } : {}),
          });
        }}
        onClearAll={() => {
          void navigate(basePath);
        }}
      />
    </>
  );
}

/** The register cannot count projects per year, so it offers the ten years either side of this one. */
const PERIOD_OPTIONS = Array.from({ length: 21 }, (_, i) =>
  String(new Date().getFullYear() + 10 - i),
).map((year) => ({
  value: year,
  label: year,
}));

function STATUS_OPTIONS(t: ReturnType<typeof useI18n>['t']) {
  return PROJECT_STATUSES.map((value) => ({ value, label: t(`projects.status.${value}`) }));
}
