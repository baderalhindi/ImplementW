import { type CSSProperties, type ReactElement, useRef } from 'react';
import { Link } from 'react-router';

import { useI18n } from '@/shared/i18n/i18n.ts';

import { type ChartBucket } from '../charts.ts';

import { DonutChart } from './Charts.tsx';
import { useDonutSize } from './donutSize.ts';

/**
 * Every donut and pie of the dashboards in one design (the user, 20 Nov 2026: "all the donut charts the same, with the legends"): the ring
 * (or disc) with its total in the centre, and beside it the legend as a table — each value with its colour key, the count (or its
 * formatted value), and the value's link where it opens records. Hovering a row lifts its segment, and the reverse. The governed widgets and
 * the designed visuals both draw it.
 */
export function DonutWithLegend({
  buckets,
  caption,
  countHeading,
  totalLabel,
  active,
  onActive,
  pie = false,
}: {
  buckets: ChartBucket[];
  /** The table's name for a screen reader: the chart's title. */
  caption: string;
  /** The heading of the legend's second column: what is counted ("Projects"). */
  countHeading: string;
  /** The centre's word under the total. */
  totalLabel: string;
  active: string | null;
  onActive: (key: string | null) => void;
  pie?: boolean;
}): ReactElement {
  const { t } = useI18n();
  const lift = (key: string) => (active === null ? '' : active === key ? 'is-active' : 'is-dimmed');
  // The ring grows with the room it is given (a visual made larger), as long as the legend keeps its own room beside it (20 Nov 2026).
  // Only in a designed visual, which gives the donut its room; elsewhere a donut keeps its usual size.
  const box = useRef<HTMLDivElement>(null);
  const size = useDonutSize(box);
  return (
    <div
      ref={box}
      className="distribution distribution--donut"
      style={size === null ? undefined : ({ '--donut-size': `${String(size)}px` } as CSSProperties)}
    >
      <DonutChart
        buckets={buckets}
        active={active}
        onActive={onActive}
        totalLabel={totalLabel}
        pie={pie}
      />
      <table className="widget__distribution distribution__donut">
        <caption className="visually-hidden">{caption}</caption>
        <thead>
          <tr>
            <th scope="col">{t('dashboards.distribution.value')}</th>
            <th scope="col">{countHeading}</th>
          </tr>
        </thead>
        <tbody>
          {buckets.map((bucket) => (
            <tr
              key={bucket.key}
              data-bucket={bucket.key}
              className={lift(bucket.key)}
              style={{ '--chart-color': bucket.color } as CSSProperties}
              onPointerEnter={() => {
                onActive(bucket.key);
              }}
              onPointerLeave={() => {
                onActive(null);
              }}
            >
              <th scope="row">
                <span className="distribution__key" aria-hidden="true" />
                {bucket.href === undefined ? (
                  bucket.text
                ) : (
                  <Link className="drill-link" to={bucket.href} title={bucket.hint}>
                    {bucket.text}
                  </Link>
                )}
              </th>
              <td>
                <span
                  className="widget__count figure"
                  dir={bucket.value === undefined ? 'ltr' : undefined}
                >
                  {bucket.value ?? bucket.count}
                </span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
