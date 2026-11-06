import { type ReactElement, useCallback } from 'react';
import { Link } from 'react-router';

import { useCan } from '@/features/identity-access/permissions/permissions.ts';
import { projectsApi } from '@/features/projects/api/projectsApi.ts';
import { OnPlanBadge } from '@/features/projects/components/FollowUpBadges.tsx';
import { ProjectStatusBadge } from '@/features/projects/components/ProjectStatusBadge.tsx';
import { formatSar } from '@/features/projects/presentation.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { LoadingState } from '@/shared/ui/States.tsx';

import { dashboardsApi, type PortfolioFilters } from '../api/dashboardsApi.ts';
import { Percent } from '../insights/figures.tsx';

const SHOWN_PROJECTS = 8;

/**
 * What a pressed circle opens: the place's own figures, under the filters in force, and its first projects. Each part is what the person's
 * permissions give: without PORTFOLIO_SUMMARY_VIEW only the count shows, without PROJECT_VIEW no project is listed.
 */
export function LocationDetails({
  cityItemId,
  name,
  regionName,
  projectCount,
  filters,
  filtered,
  onFilter,
  onClose,
}: {
  cityItemId: string;
  name: string;
  regionName: string | null;
  projectCount: number;
  filters: PortfolioFilters;
  /** The dashboard is already filtered to this place. */
  filtered: boolean;
  /** Filters the page to this place; a page with no such filter leaves it out, and the button with it. */
  onFilter: (() => void) | undefined;
  onClose: () => void;
}): ReactElement {
  const { t } = useI18n();
  const mayListProjects = useCan('PROJECT_VIEW');
  const scoped = JSON.stringify({ ...filters, cityItemId });
  const loadFigures = useCallback(
    (signal: AbortSignal) => dashboardsApi.insights(JSON.parse(scoped) as PortfolioFilters, signal),
    [scoped],
  );
  const loadProjects = useCallback(
    (signal: AbortSignal) =>
      mayListProjects
        ? projectsApi.list(
            { ...(JSON.parse(scoped) as PortfolioFilters), page: 1, pageSize: SHOWN_PROJECTS },
            signal,
          )
        : Promise.resolve(null),
    [scoped, mayListProjects],
  );
  const figures = useApiResource(loadFigures);
  const projects = useApiResource(loadProjects);
  const headline = figures.data?.headline ?? null;
  const onPlan = figures.data?.onPlan ?? null;

  return (
    <div className="location-details" role="status">
      <div className="location-details__header">
        <div>
          <h3>{name}</h3>
          {regionName !== null && <p className="location-details__region">{regionName}</p>}
        </div>
        <button type="button" className="button button--quiet" onClick={onClose}>
          {t('dashboards.locations.close')}
        </button>
      </div>
      <p className="location-details__count">
        {t('dashboards.locations.projects', { count: projectCount })}
      </p>
      {figures.loading && figures.data === undefined && <LoadingState />}
      {headline !== null && (
        <dl className="insights__counts">
          {headline.totalValueSar !== null && (
            <div>
              <dt>{t('dashboards.insights.totalValue')}</dt>
              <dd>{t('projects.money.sar', { amount: formatSar(headline.totalValueSar) })}</dd>
            </div>
          )}
          <div>
            <dt>{t('dashboards.insights.averagePlanned')}</dt>
            <dd>
              <Percent value={headline.averagePlannedPercent} />
            </dd>
          </div>
          <div>
            <dt>{t('dashboards.insights.averageActual')}</dt>
            <dd>
              <Percent value={headline.averageActualPercent} />
            </dd>
          </div>
          <div>
            <dt>{t('dashboards.locations.status')}</dt>
            <dd>
              <OnPlanBadge status={headline.onPlan} />
            </dd>
          </div>
          <div>
            <dt>{t('dashboards.insights.needingFollowUp')}</dt>
            <dd>{headline.needingFollowUp}</dd>
          </div>
        </dl>
      )}
      {headline === null && onPlan !== null && (
        <dl className="insights__counts">
          <div>
            <dt>{t('dashboards.locations.status')}</dt>
            <dd>
              {t('dashboards.locations.onPlanSummary', {
                onTrack: onPlan.onTrack,
                late: onPlan.late,
                stalled: onPlan.stalled,
              })}
            </dd>
          </div>
        </dl>
      )}
      {projects.data !== undefined && projects.data !== null && projects.data.items.length > 0 && (
        <>
          <h4>{t('dashboards.locations.projectsHeading')}</h4>
          <ul className="location-details__projects">
            {projects.data.items.map((project) => (
              <li key={project.id}>
                <Link to={`/projects/${project.id}`} dir="auto">
                  {project.title.text}
                </Link>
                <ProjectStatusBadge status={project.status} legacyIntakeDate={null} />
              </li>
            ))}
          </ul>
          {projects.data.totalCount > SHOWN_PROJECTS && (
            <Link to={`/projects?cityItemId=${encodeURIComponent(cityItemId)}`}>
              {t('dashboards.locations.allProjects', { count: projects.data.totalCount })}
            </Link>
          )}
        </>
      )}
      {onFilter !== undefined && (
        <button type="button" className="button" aria-pressed={filtered} onClick={onFilter}>
          {t(filtered ? 'dashboards.locations.clearFilter' : 'dashboards.locations.filter')}
        </button>
      )}
    </div>
  );
}
