import { type CSSProperties, type ReactElement, type ReactNode } from 'react';

/** One city in a region's block: its name (text, a link or a button), its figure and its bar's share of the largest city's. */
export interface CityRow {
  key: string;
  name: ReactNode;
  figure: string;
  share: number;
}

/** One region's block: its name, its total, and its cities as bars (none where the list is of regions only). */
export interface RegionRow {
  key: string;
  name: ReactNode;
  total: string;
  cities: CityRow[];
}

/** The regions of a locations map, each a block with its total over its cities as bars: the map's list, and the list where no map is drawn. */
export function RegionList({ regions }: { regions: RegionRow[] }): ReactElement {
  return (
    <ul className="locations__regions">
      {regions.map((region) => (
        <li key={region.key} className="locations__region">
          <div className="locations__region-header">
            {region.name}
            <span className="locations__total">{region.total}</span>
          </div>
          {region.cities.length > 0 && (
            <ul className="bars">
              {region.cities.map((city) => (
                <li key={city.key} className="bars__item">
                  <span className="bars__label">{city.name}</span>
                  <span className="bars__value" dir="ltr">
                    {city.figure}
                  </span>
                  <span className="widget__bar-track" aria-hidden="true">
                    <span
                      className="widget__bar"
                      style={
                        {
                          inlineSize: `${String(city.share * 100)}%`,
                          '--chart-color': 'var(--chart-category-1)',
                        } as CSSProperties
                      }
                    />
                  </span>
                </li>
              ))}
            </ul>
          )}
        </li>
      ))}
    </ul>
  );
}
