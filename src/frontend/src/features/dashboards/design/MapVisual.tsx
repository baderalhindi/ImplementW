import { type ReactElement, type ReactNode } from 'react';
import { Link, useNavigate } from 'react-router';

import { useI18n } from '@/shared/i18n/i18n.ts';

import { type DashboardPlace } from '../api/types.ts';
import { LocationsMap, type MapPoint } from '../components/LocationsMap.tsx';
import { RegionList } from '../components/RegionList.tsx';
import { byRegion } from '../locations.ts';
import { usePlaceName } from '../usePlaceName.ts';
import { type GroupBy, type VisualResult } from './designApi.ts';
import { mapOptionsOf, mapPlaces } from './mapVisual.ts';

/**
 * A map visual's body (docs/architecture/dashboard-designer-map-visual.md): the dashboard's map and its list of regions and cities, drawn from
 * the visual's groups with its figure (`format`) in place of a project count. Each place opens the records its figure counts where `drill`
 * gives a path (on the dashboard; the designer gives none); without the list the map takes the width, without the counts its circles are
 * empty. The projects with no place on the map are counted under it, as the Projects by location section counts them.
 */
export function MapVisual({
  visual,
  places,
  figureName,
  format,
  drill,
}: {
  visual: VisualResult;
  places: DashboardPlace[];
  figureName: string;
  format: (value: number | null) => string;
  drill: (groupBy: GroupBy, key: string | null) => string | undefined;
}): ReactElement {
  const { t, language } = useI18n();
  const navigate = useNavigate();
  const placeName = usePlaceName(places);
  const { showList, showCounts } = mapOptionsOf(visual);
  const byCity = visual.groupBy !== 'REGION';
  const placed = mapPlaces(visual.groups ?? [], visual.groupBy, places);
  const points: MapPoint[] = placed.flatMap((place) =>
    place.key === null || place.position === null
      ? []
      : [
          {
            id: place.key,
            name: placeName(place.key),
            ...place.position,
            value: Math.max(0, place.value ?? 0),
            figure: format(place.value),
          },
        ],
  );
  const onMap = new Set(points.map((point) => point.id));
  const unplaced = placed
    .filter((place) => place.key === null || !onMap.has(place.key))
    .reduce((sum, place) => sum + place.projects, 0);
  const largest = Math.max(0, ...placed.map((place) => place.value ?? 0));
  // A region's project count reads as the section's does ("122 projects"); any other figure as it is formatted.
  const countsProjects = (visual.values?.length ?? 0) === 0 && visual.measures[0] === 'PROJECTS';
  const totalText = (value: number | null) =>
    countsProjects && value !== null
      ? t('dashboards.locations.projects', { count: value })
      : format(value);
  const named = (groupBy: GroupBy, key: string | null, name: string, className?: string) => {
    const href = key === null ? undefined : drill(groupBy, key);
    return href === undefined ? (
      <span className={className}>{name}</span>
    ) : (
      <Link className={className} to={href}>
        {name}
      </Link>
    );
  };
  const regionName = (key: string | null): ReactNode =>
    named(
      'REGION',
      key,
      key === null ? t('dashboards.locations.noRegion') : placeName(key),
      'locations__region-name',
    );
  const list = (
    <RegionList
      regions={byRegion(placed, (place) => place.value ?? 0).map((region) => ({
        key: region.regionItemId ?? 'none',
        name: regionName(region.regionItemId),
        total: totalText(byCity ? region.total : (region.cities[0]?.value ?? null)),
        cities: byCity
          ? region.cities.map((city) => ({
              key: city.key ?? 'none',
              name: named(
                'CITY',
                city.key,
                city.key === null ? t('dashboards.locations.noCity') : placeName(city.key),
              ),
              figure: format(city.value),
              share: largest > 0 ? Math.max(0, city.value ?? 0) / largest : 0,
            }))
          : [],
      }))}
    />
  );
  const showMap = points.length > 0;
  // Pressing a place opens its records only where the figure has a list (on the dashboard).
  const opens = placed.some(
    (place) => place.key !== null && drill(visual.groupBy, place.key) !== undefined,
  );

  return (
    <div className="widget locations">
      <p className="widget__explanation">
        {t('dashboards.designer.mapDescription', { figure: figureName })}
        {opens && <> {t('dashboards.designer.mapOpenHint')}</>}
      </p>
      {showMap ? (
        <div
          className={
            showList ? 'locations__map-row' : 'locations__map-row locations__map-row--wide'
          }
        >
          <LocationsMap
            points={points}
            selectedId={null}
            label={t('dashboards.designer.mapLabel', { title: visual.title[language] })}
            showCounts={showCounts}
            onSelect={(id) => {
              const href = id === null ? undefined : drill(visual.groupBy, id);
              if (href !== undefined) {
                void navigate(href);
              }
            }}
          />
          {showList && <div className="locations__side">{list}</div>}
        </div>
      ) : (
        list
      )}
      {showMap && unplaced > 0 && (
        <p className="insights__coverage">
          {t('dashboards.locations.unplaced', { count: unplaced })}
        </p>
      )}
    </div>
  );
}
