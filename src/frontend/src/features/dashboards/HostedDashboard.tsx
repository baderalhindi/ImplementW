import { type ReactElement, useCallback, useState } from 'react';

import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { ApiError } from '@/shared/api/httpClient.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { SelectField } from '@/shared/ui/FormFields.tsx';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
import { ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { dashboardsApi } from './api/dashboardsApi.ts';
import { type DashboardCatalogueEntry } from './api/types.ts';
import { DashboardCanvas } from './components/DashboardCanvas.tsx';
import { mayPersonalize } from './landing.ts';
import { PersonalizeDialog } from './PersonalizeDialog.tsx';
import { dashboardProblemMessage } from './problems.ts';
import { useDepartmentNames } from './useDepartmentNames.ts';

/**
 * A dashboard the Home hosts (Portfolio, Governance), read over every project the caller may see. The department
 * filter offers only the departments of those projects (`departmentOptions`, DSH-CC-17): it narrows the population and
 * can never widen it — a department outside them is refused by the API (422), and the filter is then cleared.
 */
export function HostedDashboard({
  entry,
  user,
  departmentId,
  onDepartmentChange,
}: {
  entry: DashboardCatalogueEntry;
  user: SessionUser;
  departmentId: string | undefined;
  onDepartmentChange: (departmentId: string | undefined) => void;
}): ReactElement {
  const { t, formatDateTime } = useI18n();
  const departmentName = useDepartmentNames();
  const load = useCallback(
    (signal: AbortSignal) => dashboardsApi.get(entry.code, { departmentId }, signal),
    [entry.code, departmentId],
  );
  const view = useApiResource(load, { keepWhileReloading: true });
  const [options, setOptions] = useState<string[]>([]);
  const [personalizing, setPersonalizing] = useState(false);
  const [notice, setNotice] = useState<Notice | null>(null);

  // The options do not depend on the filter (the API lists them before narrowing), so the last ones read stay offered
  // while another department loads: the select never disappears under a keyboard user.
  const loaded = view.data?.departmentOptions;
  if (loaded !== undefined && loaded.join() !== options.join()) {
    setOptions(loaded);
  }

  const filterRefused =
    view.error instanceof ApiError && view.error.code === 'DASHBOARD_FILTER_VALUE_UNAUTHORIZED';

  return (
    <div className="dashboard">
      <div className="dashboard__toolbar">
        {options.length > 0 && (
          <div className="filters filters--inline">
            <SelectField
              label={t('dashboards.filter.department')}
              name="department"
              value={
                departmentId !== undefined && options.includes(departmentId) ? departmentId : ''
              }
              placeholder={t('dashboards.filter.allDepartments')}
              options={options
                .map((id) => ({ value: id, label: departmentName(id) }))
                .sort((a, b) => a.label.localeCompare(b.label))}
              onChange={(value) => {
                onDepartmentChange(value === '' ? undefined : value);
              }}
            />
          </div>
        )}
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
            message={dashboardProblemMessage(view.error, t)}
            onRetry={
              filterRefused
                ? () => {
                    onDepartmentChange(undefined);
                  }
                : view.reload
            }
          />
        )
      ) : (
        <>
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
