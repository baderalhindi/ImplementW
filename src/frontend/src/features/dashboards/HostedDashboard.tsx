import { type ReactElement, useCallback, useState } from 'react';

import { usePermissions } from '@/features/identity-access/permissions/permissions.ts';
import { ApiError } from '@/shared/api/httpClient.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
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
import { mayPersonalize } from './landing.ts';
import { PortfolioInsightsSection } from './insights/PortfolioInsightsSection.tsx';
import { hasNoData } from './presentation.ts';
import { PersonalizeDialog } from './PersonalizeDialog.tsx';
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
}: {
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
  const [personalizing, setPersonalizing] = useState(false);
  const [notice, setNotice] = useState<Notice | null>(null);

  // The options do not depend on the filters (the API lists them before narrowing), so the last ones read stay offered
  // while another choice loads: a select never disappears under a keyboard user.
  if (view.data !== undefined && !sameOptions(optionsOf(view.data), options)) {
    setOptions(optionsOf(view.data));
  }

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
              {mayPersonalize(permissions, entry) && (
                <button
                  type="button"
                  className="button"
                  onClick={() => {
                    setPersonalizing(true);
                  }}
                >
                  {t('dashboards.personalize.open')}
                </button>
              )}
            </p>
          )
        }
      />
      <PageNotice notice={notice} />
      {entry.code === 'PORTFOLIO' && (
        <PortfolioInsightsSection
          filters={filters}
          emptySaidElsewhere={view.data !== undefined && hasNoData(view.data)}
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
      ) : hasNoData(view.data) ? (
        <EmptyState title={t('dashboards.noData')} />
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
          <DashboardCanvas view={view.data} headingLevel={2} />
          <PersonalizeDialog
            open={personalizing}
            view={view.data}
            onClose={() => {
              setPersonalizing(false);
            }}
            onSaved={(reset) => {
              setPersonalizing(false);
              setNotice({
                tone: 'success',
                message: t(
                  reset ? 'dashboards.personalize.wasReset' : 'dashboards.personalize.saved',
                ),
              });
              view.reload();
            }}
          />
        </>
      )}
    </div>
  );
}
