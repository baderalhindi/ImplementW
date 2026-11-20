import { translate } from '@/shared/i18n/i18n.ts';

import { type DashboardPlace } from '../api/types.ts';
import { type GroupBy, type MapOptions, type Visual, type VisualGroup } from './designApi.ts';

// The map visual (20 Nov 2026, docs/architecture/dashboard-designer-map-visual.md): one figure per city or region, drawn on the dashboard's map.
// Each Map tile adds a new one; the Projects by location section stays a section.

/** What a map places its figure by: a city (the default) or a region. */
export const MAP_PLACES: readonly GroupBy[] = ['CITY', 'REGION'];

/** A map's options as a new map has them, and as a map saved without them draws. */
export const DEFAULT_MAP_OPTIONS: MapOptions = { showList: true, showCounts: true };

/** A map visual's options, each that is absent at its default. */
export function mapOptionsOf(visual: Pick<Visual, 'map'>): MapOptions {
  return {
    showList: visual.map?.showList ?? DEFAULT_MAP_OPTIONS.showList,
    showCounts: visual.map?.showCounts ?? DEFAULT_MAP_OPTIONS.showCounts,
  };
}

/** A new map at `place`: the projects per city, the list beside the map and the counts on its markers. */
export function mapVisual(
  place: { column: number; row: number },
  width: number,
  height: number,
): Visual {
  return {
    title: {
      ar: translate('ar', 'dashboards.locations.title'),
      en: translate('en', 'dashboards.locations.title'),
    },
    visualType: 'MAP',
    measures: ['PROJECTS'],
    groupBy: 'CITY',
    layoutColumn: place.column,
    layoutRow: place.row,
    layoutWidth: width,
    layoutHeight: height,
    map: { ...DEFAULT_MAP_OPTIONS },
  };
}

export interface Position {
  latitude: number;
  longitude: number;
}

/** One of a map's groups in its place: its city or region, the region it is in, its figure and where it is drawn (null: not on the map). */
export interface MapPlace {
  /** The city's or region's item id; null for the projects with none. */
  key: string | null;
  regionItemId: string | null;
  value: number | null;
  projects: number;
  position: Position | null;
}

function positionOf(place: DashboardPlace | undefined): Position | null {
  const latitude = place?.latitude ?? null;
  const longitude = place?.longitude ?? null;
  return latitude === null || longitude === null ? null : { latitude, longitude };
}

/** The middle of the places that have a position; null when none has. */
function centreOf(places: readonly DashboardPlace[]): Position | null {
  const positions = places.flatMap((place) => {
    const position = positionOf(place);
    return position === null ? [] : [position];
  });
  return positions.length === 0
    ? null
    : {
        latitude: positions.reduce((sum, at) => sum + at.latitude, 0) / positions.length,
        longitude: positions.reduce((sum, at) => sum + at.longitude, 0) / positions.length,
      };
}

/**
 * A map's groups in their places, the largest figure first. A city is in its region (its parent in the view's `places`) and drawn where it is; a
 * region is drawn where it is, or, as regions carry no position of their own, in the middle of its cities.
 */
export function mapPlaces(
  groups: readonly VisualGroup[],
  groupBy: GroupBy,
  places: readonly DashboardPlace[],
): MapPlace[] {
  return groups
    .map((group) => {
      const place =
        group.key === null ? undefined : places.find((candidate) => candidate.id === group.key);
      const value = group.values[0] ?? null;
      if (groupBy === 'REGION') {
        return {
          key: group.key,
          regionItemId: group.key,
          value,
          projects: group.projects,
          position:
            positionOf(place) ??
            (group.key === null
              ? null
              : centreOf(places.filter((candidate) => candidate.parentItemId === group.key))),
        };
      }
      return {
        key: group.key,
        regionItemId: place?.parentItemId ?? null,
        value,
        projects: group.projects,
        position: positionOf(place),
      };
    })
    .sort((a, b) => (b.value ?? 0) - (a.value ?? 0));
}
