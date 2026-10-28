import { type DashboardLocationCount } from './api/types.ts';

export interface RegionGroup {
  regionItemId: string | null;
  total: number;
  cities: DashboardLocationCount[];
}

/** The location counts grouped by region, the largest region first, each region's cities largest first. */
export function byRegion(locations: DashboardLocationCount[]): RegionGroup[] {
  const groups: RegionGroup[] = [];
  for (const location of locations) {
    let group = groups.find((candidate) => candidate.regionItemId === location.regionItemId);
    if (group === undefined) {
      group = { regionItemId: location.regionItemId, total: 0, cities: [] };
      groups.push(group);
    }
    group.total += location.projectCount;
    group.cities.push(location);
  }
  return groups.sort((a, b) => b.total - a.total);
}
