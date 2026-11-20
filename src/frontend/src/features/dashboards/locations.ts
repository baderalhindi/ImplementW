export interface RegionGroup<T> {
  regionItemId: string | null;
  total: number;
  cities: T[];
}

/** Places grouped by region, the largest region first (by `valueOf` summed), each region's places in the order given. */
export function byRegion<T extends { regionItemId: string | null }>(
  items: readonly T[],
  valueOf: (item: T) => number,
): RegionGroup<T>[] {
  const groups: RegionGroup<T>[] = [];
  for (const item of items) {
    let group = groups.find((candidate) => candidate.regionItemId === item.regionItemId);
    if (group === undefined) {
      group = { regionItemId: item.regionItemId, total: 0, cities: [] };
      groups.push(group);
    }
    group.total += valueOf(item);
    group.cities.push(item);
  }
  return groups.sort((a, b) => b.total - a.total);
}
