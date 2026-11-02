import { type ReactElement, useCallback, useState } from 'react';

import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { ApiError } from '@/shared/api/httpClient.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
import { ErrorState, LoadingState } from '@/shared/ui/States.tsx';

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
import { PersonalizeDialog } from './PersonalizeDialog.tsx';
import { dashboardProblemMessage } from './problems.ts';

function optionsOf(view: DashboardView): FilterOptions {
  return {
    departments: view.departmentOptions,
    entities: view.entityOptions,
    regions: view.regionOptions,
    cities: view.cityOptions,
    places: view.places,
  };
}

function sameOptions(a: FilterOptions, b: FilterOptions): boolean {
  return (
    a.departments.join() === b.departments.join() &&
    a.entities.join() === b.entities.join() &&
    a.regions.join() === b.regions.join() &&
    a.cities.join() === b.cities.join()
  );
}

const NO_OPTIONS: FilterOptions = {
  departments: [],
  entities: [],
  regions: [],
  cities: [],
  places: [],
};

/**
 * A dashboard the page hosts (Portfolio, Governance), read over every project the caller may see. The filters — sector, entity,
 * region and city — offer only the values of those projects (DSH-CC-17): they narrow the population and can never widen it; a value
 * outside them is refused by the API (422), and the filters are then cleared. The Portfolio Dashboard also shows where its projects
 * are delivered.
 */
export function HostedDashboard({
  entry,
  user,
  filters,
  onFilterChange,
  onClearFilters,
}: {
  entry: DashboardCatalogueEntry;
  user: SessionUser;
  filters: PortfolioFilters;
  onFilterChange: (name: FilterName, value: string | undefined) => void;
  onClearFilters: () => void;
}): ReactElement {
  const { t, formatDateTime } = useI18n();
  const { departmentId, externalEntityId, regionItemId, cityItemId } = filters;
  const load = useCallback(
    (signal: AbortSignal) =>
      dashboardsApi.get(
        entry.code,
        { departmentId, externalEntityId, regionItemId, cityItemId },
        signal,
      ),
    [entry.code, departmentId, externalEntityId, regionItemId, cityItemId],
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
      <div className="dashboard__toolbar">
        <PortfolioFilterBar options={options} filters={filters} onChange={onFilterChange} />
        {view.data !== undefined && (
          <p className="dashboard__meta">
            <span>
              {t('dashboards.refreshedAt', { date: formatDateTime(view.data.refreshedAt) })}
            </span>
            <button type="button" className="button button--quiet" onClick={view.reload}>
              {t('dashboards.actions.refresh')}
            </button>
            {mayPersonalize(user, entry) && (
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
        )}
      </div>
      <PageNotice notice={notice} />
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
