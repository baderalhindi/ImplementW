import { type ReactElement } from 'react';
import { Link, Navigate, useSearchParams } from 'react-router';

import { useSession } from '@/features/identity-access/session/useSession.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { dashboardsApi } from './api/dashboardsApi.ts';
import { type DashboardCatalogueEntry } from './api/types.ts';
import { HostedDashboard } from './HostedDashboard.tsx';
import { hostedEntries, landingRole, ROLE_LANDINGS, selectedEntry } from './landing.ts';
import { dashboardProblemMessage } from './problems.ts';
import { ProjectLanding } from './ProjectLanding.tsx';

const DASHBOARD_PARAM = 'dashboard';
const DEPARTMENT_PARAM = 'department';

/**
 * The role-aware Home (FG-01 §5.1; Blueprint §20.1 "Home: my/role dashboard"). It has no screen id and no dashboard
 * route of its own: Step 16 binds DSH-001 to DSH-008 and DSH-010 to DSH-012 to the authorised shell, and they are
 * renderings of ADR-006's three dashboards (TASK-069 D-2). The person lands on the dashboard the API marks as their
 * landing, named as Blueprint §20.2 names it for their role; the other dashboards their roles may open are one link
 * away. A landing on the Project Dashboard (DSH-008) lists the person's projects, each opening SCR-040, where DSH-009
 * renders — never here.
 */
export function HomeDashboardPage(): ReactElement {
  const { t, language } = useI18n();
  const { session } = useSession();
  const [params, setParams] = useSearchParams();
  const catalogue = useApiResource(dashboardsApi.catalogue);

  if (session === null) {
    return <Navigate to="/sign-in" replace />;
  }
  if (catalogue.data === undefined) {
    return (
      <>
        <PageHeader title={t('dashboards.home.title')} />
        {catalogue.loading ? (
          <LoadingState />
        ) : (
          <ErrorState
            message={dashboardProblemMessage(catalogue.error, t)}
            onRetry={catalogue.reload}
          />
        )}
      </>
    );
  }

  const entries = catalogue.data;
  const entry = selectedEntry(entries, params.get(DASHBOARD_PARAM));
  if (entry === null) {
    return (
      <>
        <PageHeader title={t('dashboards.home.title')} />
        <EmptyState title={t('dashboards.home.none')}>
          <p>{t('dashboards.home.noneHint')}</p>
        </EmptyState>
      </>
    );
  }

  const role = landingRole(session.user);
  const rendering = entry.isDefaultLanding && role !== null ? ROLE_LANDINGS[role] : undefined;
  const others = hostedEntries(entries);
  const setParam = (name: string, value: string | undefined) => {
    const next = new URLSearchParams(params);
    if (value === undefined) {
      next.delete(name);
    } else {
      next.set(name, value);
    }
    if (name === DASHBOARD_PARAM) {
      next.delete(DEPARTMENT_PARAM);
    }
    setParams(next);
  };
  const departmentId = params.get(DEPARTMENT_PARAM) ?? undefined;

  return (
    <>
      <PageHeader
        title={rendering === undefined ? entry.name[language] : t(rendering.name)}
        description={
          rendering === undefined
            ? entry.description?.[language]
            : t('dashboards.home.rendering', {
                dashboardId: rendering.dashboardId,
                dashboard: entry.name[language],
                version: entry.versionNo,
              })
        }
      />
      {others.length > 1 && <DashboardSwitch entries={others} current={entry} />}
      {entry.contextKind === 'PROJECT' ? (
        <ProjectLanding />
      ) : (
        <HostedDashboard
          key={entry.code}
          entry={entry}
          user={session.user}
          departmentId={departmentId}
          onDepartmentChange={(value) => {
            setParam(DEPARTMENT_PARAM, value);
          }}
        />
      )}
    </>
  );
}

/** The other dashboards the person's roles may open (FG-01 §5.1), the landing one marked. */
function DashboardSwitch({
  entries,
  current,
}: {
  entries: DashboardCatalogueEntry[];
  current: DashboardCatalogueEntry;
}): ReactElement {
  const { t, language } = useI18n();
  return (
    <nav className="tabs" aria-label={t('dashboards.home.switch')}>
      <ul>
        {[...entries]
          .sort((a, b) => Number(b.isDefaultLanding) - Number(a.isDefaultLanding))
          .map((candidate) => (
            <li key={candidate.code}>
              <Link
                to={candidate.isDefaultLanding ? '/' : `/?${DASHBOARD_PARAM}=${candidate.code}`}
                aria-current={candidate.code === current.code ? 'page' : undefined}
              >
                {candidate.name[language]}
                {candidate.isDefaultLanding && (
                  <span className="details__aside">{t('dashboards.home.landingMarker')}</span>
                )}
              </Link>
            </li>
          ))}
      </ul>
    </nav>
  );
}
