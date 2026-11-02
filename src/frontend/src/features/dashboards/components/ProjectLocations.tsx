import { type CSSProperties, type ReactElement } from 'react';
import { Link } from 'react-router';

import { useI18n } from '@/shared/i18n/i18n.ts';

import { type DashboardLocationCount, type DashboardPlace } from '../api/types.ts';

import { byRegion } from '../locations.ts';
import { usePlaceName } from '../usePlaceName.ts';

/**
 * Where the portfolio's projects are delivered (the 27 Oct 2026 review): each region with its project count and its cities as bars.
 * On the dashboard, choosing a region filters the whole dashboard by it (`onSelectRegion`); elsewhere a region links to the
 * dashboard filtered by it (`linkTo`). The counts are of the projects the filters leave, the same population the widgets count.
 */
export function ProjectLocations({
  locations,
  places,
  selectedRegion,
  onSelectRegion,
  linkTo,
}: {
  locations: DashboardLocationCount[];
  places: DashboardPlace[];
  selectedRegion?: string | undefined;
  onSelectRegion?: (regionItemId: string | undefined) => void;
  linkTo?: (regionItemId: string) => string;
}): ReactElement | null {
  const { t } = useI18n();
  const placeName = usePlaceName(places);
  const groups = byRegion(locations);
  if (groups.length === 0) {
    return null;
  }
  const largest = Math.max(1, ...locations.map((location) => location.projectCount));
  return (
    <section className="widget locations" aria-labelledby="portfolio-locations">
      <div className="widget__header">
        <h2 id="portfolio-locations" className="widget__title">
          {t('dashboards.locations.title')}
        </h2>
      </div>
      <p className="widget__explanation">{t('dashboards.locations.description')}</p>
      <ul className="locations__regions">
        {groups.map((group) => {
          const regionName =
            group.regionItemId === null
              ? t('dashboards.locations.noRegion')
              : placeName(group.regionItemId);
          const selected = group.regionItemId !== null && group.regionItemId === selectedRegion;
          return (
            <li key={group.regionItemId ?? 'none'} className="locations__region">
              <div className="locations__region-header">
                {group.regionItemId === null ||
                (onSelectRegion === undefined && linkTo === undefined) ? (
                  <span className="locations__region-name">{regionName}</span>
                ) : linkTo !== undefined ? (
                  <Link className="locations__region-name" to={linkTo(group.regionItemId)}>
                    {regionName}
                  </Link>
                ) : (
                  <button
                    type="button"
                    className="button button--link locations__region-name"
                    aria-pressed={selected}
                    onClick={() => {
                      onSelectRegion?.(selected ? undefined : (group.regionItemId ?? undefined));
                    }}
                  >
                    {regionName}
                  </button>
                )}
                <span className="locations__total">
                  {t('dashboards.locations.projects', { count: group.total })}
                </span>
              </div>
              <ul className="bars">
                {group.cities.map((city) => (
                  <li key={city.cityItemId ?? 'none'} className="bars__item">
                    <span className="bars__label">
                      {city.cityItemId === null
                        ? t('dashboards.locations.noCity')
                        : placeName(city.cityItemId)}
                    </span>
                    <span className="bars__value" dir="ltr">
                      {city.projectCount}
                    </span>
                    <span className="widget__bar-track" aria-hidden="true">
                      <span
                        className="widget__bar"
                        style={
                          {
                            inlineSize: `${String((city.projectCount / largest) * 100)}%`,
                            '--chart-color': 'var(--chart-category-1)',
                          } as CSSProperties
                        }
                      />
                    </span>
                  </li>
                ))}
              </ul>
            </li>
          );
        })}
      </ul>
    </section>
  );
}
