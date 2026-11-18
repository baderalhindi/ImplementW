import { type ReactElement, useCallback, useState } from 'react';
import { Link } from 'react-router';

import { usePermissions } from '@/features/identity-access/permissions/permissions.ts';
import { ApiError } from '@/shared/api/httpClient.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { dashboardsApi, type PortfolioFilters } from './api/dashboardsApi.ts';
import { type DashboardCatalogueEntry, type DashboardView } from './api/types.ts';
import { DashboardCanvas } from './components/DashboardCanvas.tsx';
import {
  type FilterName,
  type FilterOptions,
  PortfolioFilterBar,
} from './components/PortfolioFilterBar.tsx';
import { ProjectLocations } from './components/ProjectLocations.tsx';
import { DesignedVisuals } from './design/DesignedVisuals.tsx';
import { PortfolioInsightsSection } from './insights/PortfolioInsightsSection.tsx';
import { emptyPortfolioReason, hasNoData } from './presentation.ts';
import { DASHBOARD_DESIGNER_PATH } from './paths.ts';
import { dashboardProblemMessage } from './problems.ts';

function optionsOf(view: DashboardView): FilterOptions {
  return {
    departments: view.departmentOptions,
    entities: view.entityOptions,
    regions: view.regionOptions,
    cities: view.cityOptions,
    places: view.places,
    filters: view.filters ?? [],
  };
}

function sameOptions(a: FilterOptions, b: FilterOptions): boolean {
  return (
    a.departments.join() === b.departments.join() &&
    a.entities.join() === b.entities.join() &&
    a.regions.join() === b.regions.join() &&
    a.cities.join() === b.cities.join() &&
    JSON.stringify(a.filters) === JSON.stringify(b.filters) &&
    a.places.length === b.places.length
  );
}

const NO_OPTIONS: FilterOptions = {
  departments: [],
  entities: [],
  regions: [],
  cities: [],
  places: [],
  filters: [],
};

/**
 * A dashboard the page hosts (Portfolio, Governance), read over every project the caller may see. The filters — sector, entity,
 * region and city — offer only the values of those projects (DSH-CC-17): they narrow the population and can never widen it; a value
 * outside them is refused by the API (422), and the filters are then cleared. The Portfolio Dashboard also shows where its projects
 * are delivered.
 */
export function HostedDashboard({
  entry,
  filters,
  onFilterChange,
  onClearFilters,
  executive = false,
}: {
  /** Review round 8, point 14: the Executive's one dashboard, laid out for them. */
  executive?: boolean;
  entry: DashboardCatalogueEntry;
  filters: PortfolioFilters;
  onFilterChange: (name: FilterName, value: string | undefined) => void;
  onClearFilters: () => void;
}): ReactElement {
  const { t, formatDateTime } = useI18n();
  const permissions = usePermissions();
  const {
    departmentId,
    externalEntityId,
    regionItemId,
    cityItemId,
    classificationItemId,
    status,
    priorityItemId,
    strategicPortfolioItemId,
    strategicInitiativeItemId,
    period,
  } = filters;
  const load = useCallback(
    (signal: AbortSignal) =>
      dashboardsApi.get(
        entry.code,
        {
          departmentId,
          externalEntityId,
          regionItemId,
          cityItemId,
          classificationItemId,
          status,
          priorityItemId,
          strategicPortfolioItemId,
          strategicInitiativeItemId,
          period,
        },
        signal,
      ),
    [
      entry.code,
      departmentId,
      externalEntityId,
      regionItemId,
      cityItemId,
      classificationItemId,
      status,
      priorityItemId,
      strategicPortfolioItemId,
      strategicInitiativeItemId,
      period,
    ],
  );
  const view = useApiResource(load, { keepWhileReloading: true });
  const [options, setOptions] = useState<FilterOptions>(NO_OPTIONS);

  // The options do not depend on the filters (the API lists them before narrowing), so the last ones read stay offered
  // while another choice loads: a select never disappears under a keyboard user.
  if (view.data !== undefined && !sameOptions(optionsOf(view.data), options)) {
    setOptions(optionsOf(view.data));
  }

  // Review round 8, point 14 keeps the Executive's own layout; everyone else's Portfolio Dashboard is the design.
  const designed = entry.code === 'PORTFOLIO' && !executive;
  const filterRefused =
    view.error instanceof ApiError && view.error.code === 'DASHBOARD_FILTER_VALUE_UNAUTHORIZED';

  return (
    <div className="dashboard">
      <PortfolioFilterBar
        options={options}
        filters={filters}
        onChange={onFilterChange}
        onClearAll={onClearFilters}
        actions={
          view.data === undefined ? undefined : (
            <p className="dashboard__meta">
              <span>
                {t('dashboards.refreshedAt', { date: formatDateTime(view.data.refreshedAt) })}
              </span>
              <button type="button" className="button button--quiet" onClick={view.reload}>
                {t('dashboards.actions.refresh')}
              </button>
              {entry.code === 'PORTFOLIO' && permissions.can('DASHBOARD_DESIGN') && (
                <Link className="button" to={DASHBOARD_DESIGNER_PATH}>
                  {t('dashboards.designer.open')}
                </Link>
              )}
            </p>
          )
        }
      />
      {entry.code === 'PORTFOLIO' && executive && (
        <PortfolioInsightsSection
          filters={filters}
          emptySaidElsewhere={view.data !== undefined && hasNoData(view.data)}
          onClearFilters={onClearFilters}
        />
      )}
      {view.data === undefined ? (
        view.loading ? (
          <LoadingState />
        ) : (
          <ErrorState
            error={view.error}
            message={dashboardProblemMessage(view.error, t)}
            onRetry={filterRefused ? onClearFilters : view.reload}
          />
        )
      ) : designed ? (
        // The whole Portfolio Dashboard is the Portfolio Manager's design (17 and 18 Nov 2026): cards, charts, tables, map and widgets.
        <DesignedVisuals
          filters={filters}
          view={view.data}
          onClearFilters={onClearFilters}
          map={{
            selectedRegion: regionItemId,
            onSelectRegion: (region) => {
              onFilterChange('regionItemId', region);
            },
            onFilterCity: (city) => {
              onFilterChange('cityItemId', city);
            },
          }}
        />
      ) : hasNoData(view.data) ? (
        <EmptyState
          title={t('dashboards.noData')}
          reason={emptyPortfolioReason(filters)}
          action={
            emptyPortfolioReason(filters) === 'scope'
              ? undefined
              : { label: t('common.filters.clear'), onClick: onClearFilters }
          }
        />
      ) : (
        <>
          {entry.code === 'PORTFOLIO' && (
            <ProjectLocations
              locations={view.data.locations}
              places={view.data.places}
              selectedRegion={regionItemId}
              onSelectRegion={(region) => {
                onFilterChange('regionItemId', region);
              }}
              map={{
                filters,
                onFilterCity: (city) => {
                  onFilterChange('cityItemId', city);
                },
              }}
            />
          )}
          <DashboardCanvas view={view.data} headingLevel={2} filters={filters} />
        </>
      )}
    </div>
  );
}
