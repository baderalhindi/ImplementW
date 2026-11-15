import { type PortfolioFilters } from './api/dashboardsApi.ts';

// Review round 8, point 01: every count on a dashboard opens the records it counts. A link carries the dashboard's filters under the
// list's own names (the same as the API's) and adds what the clicked element counts, so the list shows the same records and the same
// number. Each list re-reads the caller's scope; a link widens nothing.

type Extra = Record<string, string | undefined>;

function withQuery(path: string, filters: PortfolioFilters, extra: Extra): string {
  const query = new URLSearchParams();
  Object.entries({ ...filters, ...extra }).forEach(([name, value]) => {
    if (value !== undefined && value !== '') {
      query.set(name, value);
    }
  });
  const text = query.toString();
  return text === '' ? path : `${path}?${text}`;
}

/** The project register with the dashboard's filters. The portfolio figures count projects past registration only, so the list does too. */
export function registerPath(filters: PortfolioFilters, extra: Extra = {}): string {
  return withQuery('/projects', filters, { registered: 'true', ...extra });
}

/** The project register as the Portfolio Dashboard's lifecycle widget counts: every state, so no registration filter. */
export function lifecycleRegisterPath(filters: PortfolioFilters, status: string): string {
  return withQuery('/projects', filters, { status });
}

/** The Follow-up list with the dashboard's filters; `mainReason` keeps the projects the dashboard counts under that reason. */
export function followUpPath(filters: PortfolioFilters, extra: Extra = {}): string {
  return withQuery('/projects/follow-up', filters, extra);
}

/** The risk register's open risks of one rating, across the projects the dashboard's filters keep, or in one project. */
export function riskRatingPath(
  filters: PortfolioFilters,
  rating: string,
  projectId: string | null,
): string {
  return projectId === null
    ? withQuery('/risks', filters, { rating })
    : withQuery(`/projects/${projectId}/risks`, {}, { rating });
}

/** What a widget's value opens, by the projection that counts it; null where no list filters by that value (health, KPI condition). */
export function widgetValuePath(
  projectionCode: string,
  key: string,
  filters: PortfolioFilters,
  projectId: string | null,
): { href: string; kind: 'projects' | 'risks' } | null {
  switch (projectionCode) {
    case 'PROJECT.LIFECYCLE_STATE':
      return projectId === null
        ? { href: lifecycleRegisterPath(filters, key), kind: 'projects' }
        : null;
    case 'RISK.RISK_EXPOSURE':
      return { href: riskRatingPath(filters, key, projectId), kind: 'risks' };
    default:
      return null;
  }
}
