import { type ReactElement } from 'react';
import { Link, Navigate, useSearchParams } from 'react-router';

import { useSession } from '@/features/identity-access/session/useSession.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { dashboardsApi } from './api/dashboardsApi.ts';
import { type DashboardCatalogueEntry } from './api/types.ts';
import { type PortfolioFilters } from './api/dashboardsApi.ts';
import { type FilterName } from './components/PortfolioFilterBar.tsx';
import { HostedDashboard } from './HostedDashboard.tsx';
import { hostedEntries, landingRole, ROLE_LANDINGS, selectedEntry } from './landing.ts';
import { DASHBOARDS_PATH } from './paths.ts';
import { dashboardProblemMessage } from './problems.ts';
import { ProjectLanding } from './ProjectLanding.tsx';

const DASHBOARD_PARAM = 'dashboard';

/** Each portfolio filter's address parameter, kept in the URL so a filtered view can be shared and reloaded. */
const FILTER_PARAMS: Record<FilterName, string> = {
  departmentId: 'department',
  externalEntityId: 'entity',
  regionItemId: 'region',
  cityItemId: 'city',
  classificationItemId: 'sector',
  status: 'status',
  priorityItemId: 'priority',
  strategicPortfolioItemId: 'portfolio',
  strategicInitiativeItemId: 'initiative',
  period: 'period',
};

/** A child list's filter, dropped when its parent's changes: a city lies in a region, an initiative in a portfolio. */
const CHILD_FILTER: Partial<Record<FilterName, FilterName>> = {
  regionItemId: 'cityItemId',
  strategicPortfolioItemId: 'strategicInitiativeItemId',
};

/** The filters a person last used on a dashboard, kept in this browser only (review §1: remembered per user per dashboard). */
function storageKey(userId: string, dashboard: string): string {
  return `pmplatform.dashboardFilters.${userId}.${dashboard}`;
}

function readRemembered(key: string): Record<string, string> {
  try {
    const stored = localStorage.getItem(key);
    const parsed: unknown = stored === null ? null : JSON.parse(stored);
    return parsed !== null && typeof parsed === 'object'
      ? Object.fromEntries(
          Object.entries(parsed).filter(
            (entry): entry is [string, string] => typeof entry[1] === 'string',
          ),
        )
      : {};
  } catch {
    return {};
  }
}

function remember(key: string, params: URLSearchParams): void {
  try {
    const kept = Object.fromEntries(
      Object.values(FILTER_PARAMS).flatMap((param) => {
        const value = params.get(param);
        return value === null ? [] : [[param, value]];
      }),
    );
    localStorage.setItem(key, JSON.stringify(kept));
  } catch {
    // Storage may be unavailable (a private window); the URL still carries the filters.
  }
}

/**
 * The role dashboards (FG-01 §5.1), at /dashboards since the Home became the person's own work (the 24 Oct 2026 review, H2:
 * "Home and Portfolio need distinct jobs"). Step 16 binds DSH-001 to DSH-008 and DSH-010 to DSH-012 to the authorised shell, and
 * they are renderings of ADR-006's three dashboards (TASK-069 D-2). The person opens on the dashboard the API marks as their
 * landing, named as Blueprint §20.2 names it for their role; the other dashboards their roles may open are one link away — the
 * Portfolio Dashboard among them for its audience, the Portfolio menu's analysis. A landing on the Project Dashboard (DSH-008)
 * lists the person's projects, each opening SCR-040, where DSH-009 renders — never here.
 */
export function DashboardsPage(): ReactElement {
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
            error={catalogue.error}
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
  // A link with filters shows those; one without shows the filters this person last used here.
  const rememberedKey = storageKey(session.user.id, entry.code);
  const linked = Object.values(FILTER_PARAMS).some((param) => params.has(param));
  const current = new URLSearchParams(params);
  if (!linked) {
    Object.entries(readRemembered(rememberedKey)).forEach(([param, value]) => {
      if (Object.values(FILTER_PARAMS).includes(param)) {
        current.set(param, value);
      }
    });
  }
  const filters: PortfolioFilters = Object.fromEntries(
    Object.entries(FILTER_PARAMS).map(([name, param]) => [name, current.get(param) ?? undefined]),
  );
  const setFilter = (name: FilterName, value: string | undefined) => {
    const next = new URLSearchParams(current);
    if (value === undefined) {
      next.delete(FILTER_PARAMS[name]);
    } else {
      next.set(FILTER_PARAMS[name], value);
    }
    // A city belongs to a region, an initiative to a portfolio: another parent drops the child chosen under the last one.
    const child = CHILD_FILTER[name];
    if (child !== undefined) {
      next.delete(FILTER_PARAMS[child]);
    }
    remember(rememberedKey, next);
    setParams(next);
  };
  const clearFilters = () => {
    const next = new URLSearchParams(current);
    Object.values(FILTER_PARAMS).forEach((param) => {
      next.delete(param);
    });
    remember(rememberedKey, next);
    setParams(next);
  };

  return (
    <>
      <PageHeader
        title={rendering === undefined ? entry.name[language] : t(rendering.name)}
        description={
          rendering === undefined
            ? entry.description?.[language]
            : t('dashboards.home.rendering', { dashboard: entry.name[language] })
        }
      />
      {others.length > 1 && <DashboardSwitch entries={others} current={entry} />}
      {entry.contextKind === 'PROJECT' ? (
        <ProjectLanding />
      ) : (
        <HostedDashboard
          key={entry.code}
          entry={entry}
          filters={filters}
          onFilterChange={setFilter}
          onClearFilters={clearFilters}
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
    <nav className="tabs dashboard__switch" aria-label={t('dashboards.home.switch')}>
      <ul>
        {[...entries]
          .sort((a, b) => Number(b.isDefaultLanding) - Number(a.isDefaultLanding))
          .map((candidate) => (
            <li key={candidate.code}>
              <Link
                to={
                  candidate.isDefaultLanding
                    ? DASHBOARDS_PATH
                    : `${DASHBOARDS_PATH}?${DASHBOARD_PARAM}=${candidate.code}`
                }
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
