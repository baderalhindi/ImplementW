import { type ReactElement, useState } from 'react';
import { Link } from 'react-router';

import { useI18n } from '@/shared/i18n/i18n.ts';

import { type PortfolioFilters } from '../api/dashboardsApi.ts';
import { type DashboardLocationCount, type DashboardPlace } from '../api/types.ts';
import { byRegion } from '../locations.ts';
import { usePlaceName } from '../usePlaceName.ts';

import { LocationDetails } from './LocationDetails.tsx';
import { LocationsMap, type MapPoint } from './LocationsMap.tsx';
import { RegionList } from './RegionList.tsx';

/**
 * Where the portfolio's projects are delivered (the 27 Oct 2026 review): each region with its project count and its cities as bars.
 * On the dashboard, choosing a region filters the whole dashboard by it (`onSelectRegion`); elsewhere a region links to the
 * dashboard filtered by it (`linkTo`). With `map`, the dashboard's map draws a circle per city, and pressing one or its row opens its details. The counts are of the projects the filters leave, the same population the widgets count.
 */
export function ProjectLocations({
  locations,
  places,
  selectedRegion,
  onSelectRegion,
  linkTo,
  map,
}: {
  locations: DashboardLocationCount[];
  places: DashboardPlace[];
  selectedRegion?: string | undefined;
  onSelectRegion?: (regionItemId: string | undefined) => void;
  linkTo?: (regionItemId: string) => string;
  /** The filters in force and, where the page can filter by a city, how to set one; for the map and its details. */
  map?: {
    filters: PortfolioFilters;
    onFilterCity?: (cityItemId: string | undefined) => void;
  };
}): ReactElement | null {
  const { t } = useI18n();
  const placeName = usePlaceName(places);
  const [selectedCity, setSelectedCity] = useState<string | null>(null);
  const groups = byRegion(locations, (location) => location.projectCount);
  if (groups.length === 0) {
    return null;
  }
  const largest = Math.max(1, ...locations.map((location) => location.projectCount));
  const points: MapPoint[] = locations.flatMap((location) => {
    const place = places.find((candidate) => candidate.id === location.cityItemId);
    return place === undefined ||
      location.cityItemId === null ||
      place.latitude === null ||
      place.latitude === undefined ||
      place.longitude === null ||
      place.longitude === undefined
      ? []
      : [
          {
            id: location.cityItemId,
            name: placeName(location.cityItemId),
            latitude: place.latitude,
            longitude: place.longitude,
            value: location.projectCount,
            figure: String(location.projectCount),
          },
        ];
  });
  const showMap = map !== undefined && points.length > 0;
  const unplaced =
    locations.reduce((sum, location) => sum + location.projectCount, 0) -
    points.reduce((sum, point) => sum + point.value, 0);
  const selected = locations.find((location) => location.cityItemId === selectedCity) ?? null;
  const regionList = (
    <RegionList
      regions={groups.map((group) => {
        const regionName =
          group.regionItemId === null
            ? t('dashboards.locations.noRegion')
            : placeName(group.regionItemId);
        const selected = group.regionItemId !== null && group.regionItemId === selectedRegion;
        return {
          key: group.regionItemId ?? 'none',
          name:
            group.regionItemId === null ||
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
            ),
          total: t('dashboards.locations.projects', { count: group.total }),
          cities: group.cities.map((city) => ({
            key: city.cityItemId ?? 'none',
            name:
              city.cityItemId === null ? (
                t('dashboards.locations.noCity')
              ) : showMap ? (
                <button
                  type="button"
                  className="button button--link"
                  aria-pressed={selectedCity === city.cityItemId}
                  onClick={() => {
                    setSelectedCity(city.cityItemId);
                  }}
                >
                  {placeName(city.cityItemId)}
                </button>
              ) : (
                placeName(city.cityItemId)
              ),
            figure: String(city.projectCount),
            share: city.projectCount / largest,
          })),
        };
      })}
    />
  );

  return (
    <section className="widget locations" aria-labelledby="portfolio-locations">
      <div className="widget__header">
        <h2 id="portfolio-locations" className="widget__title">
          {t('dashboards.locations.title')}
        </h2>
      </div>
      <p className="widget__explanation">
        {t(showMap ? 'dashboards.locations.mapDescription' : 'dashboards.locations.description')}
      </p>
      {showMap && (
        <div className="locations__map-row">
          <LocationsMap
            points={points}
            selectedId={selectedCity}
            label={t('dashboards.locations.mapLabel')}
            onSelect={setSelectedCity}
          />
          <div className="locations__side">
            {selected !== null && selected.cityItemId !== null ? (
              <LocationDetails
                key={selected.cityItemId}
                cityItemId={selected.cityItemId}
                name={placeName(selected.cityItemId)}
                regionName={
                  selected.regionItemId === null ? null : placeName(selected.regionItemId)
                }
                projectCount={selected.projectCount}
                filters={map.filters}
                filtered={map.filters.cityItemId === selected.cityItemId}
                onFilter={
                  map.onFilterCity === undefined
                    ? undefined
                    : () => {
                        map.onFilterCity?.(
                          map.filters.cityItemId === selected.cityItemId
                            ? undefined
                            : (selected.cityItemId ?? undefined),
                        );
                      }
                }
                onClose={() => {
                  setSelectedCity(null);
                }}
              />
            ) : (
              regionList
            )}
          </div>
        </div>
      )}
      {showMap && unplaced > 0 && (
        <p className="insights__coverage">
          {t('dashboards.locations.unplaced', { count: unplaced })}
        </p>
      )}
      {!showMap && regionList}
    </section>
  );
}
