import { type CSSProperties, type ReactElement, type ReactNode } from 'react';
import { Link, useNavigate } from 'react-router';

import { anyLinked, type ChartBucket } from '../charts.ts';

// The drawn half of a widget: every chart here only repeats values the widget's table states in words, so each is
// hidden from assistive technology and the table stays the text alternative — unless its values link to the records
// they count (review round 8, point 01). Then each bar or column is a link named by its words and count, reached by
// Tab and opened by Enter; a donut's segments open on click, and its legend or table carries the same links.

/** A value's marks, as a link to its records when it has one. */
function Marks({
  href,
  hint,
  className,
  children,
}: {
  href: string | undefined;
  hint: string | undefined;
  className: string;
  children: ReactNode;
}): ReactElement {
  return href === undefined ? (
    <>{children}</>
  ) : (
    <Link className={className} to={href} title={hint}>
      {children}
    </Link>
  );
}

const SIZE = 168;
const RADIUS = 66;
const STROKE = 20;
/** The surface between two segments, along the ring. */
const GAP = 3;
/** The shortest arc drawn, so a very small value stays visible. */
const MIN_ARC = 1;
const CIRCUMFERENCE = 2 * Math.PI * RADIUS;

/**
 * A donut with flat-ended segments and a gap between them, from twelve o'clock clockwise in the source's order. Each
 * segment stays inside its own share of the ring (round caps spread a small value over its neighbours). The
 * centre shows the total of the counts drawn; hovering a value (on the ring or in its legend) shows that value's count
 * and words instead, on a tint of its colour. The number stays in text colour: amber and orange fail contrast as text.
 */
export function DonutChart({
  buckets,
  active,
  onActive,
  totalLabel,
  summary,
}: {
  buckets: ChartBucket[];
  active: string | null;
  onActive: (key: string | null) => void;
  totalLabel: string;
  /** What the centre shows while nothing is hovered, in place of the total, such as an overall rate. */
  summary?: { value: string; label: string };
}): ReactElement {
  const navigate = useNavigate();
  const drawn = buckets.filter((bucket) => bucket.count > 0);
  const total = drawn.reduce((sum, bucket) => sum + bucket.count, 0);
  const hovered = buckets.find((bucket) => bucket.key === active);
  const centre = SIZE / 2;
  // Where each segment starts along the ring: the lengths of those before it.
  const lengths = drawn.map((bucket) => (bucket.count / total) * CIRCUMFERENCE);
  const starts = lengths.map((_, index) =>
    lengths.slice(0, index).reduce((sum, length) => sum + length, 0),
  );
  return (
    <div className="donut" aria-hidden="true">
      <svg viewBox={`0 0 ${String(SIZE)} ${String(SIZE)}`} focusable="false">
        <circle className="donut__track" cx={centre} cy={centre} r={RADIUS} strokeWidth={STROKE} />
        <g transform={`rotate(-90 ${String(centre)} ${String(centre)})`}>
          {drawn.map((bucket, index) => {
            const length = lengths[index] ?? 0;
            // A lone value is the whole ring; otherwise half the gap is taken from each end of its share.
            const dash = drawn.length === 1 ? CIRCUMFERENCE : Math.max(MIN_ARC, length - GAP);
            const offset = drawn.length === 1 ? 0 : -((starts[index] ?? 0) + GAP / 2);
            const state =
              active === null ? '' : active === bucket.key ? ' is-active' : ' is-dimmed';
            return (
              <circle
                key={bucket.key}
                className={`donut__segment${state}${bucket.href === undefined ? '' : ' is-linked'}`}
                cx={centre}
                cy={centre}
                r={RADIUS}
                strokeWidth={STROKE}
                strokeDasharray={`${String(dash)} ${String(CIRCUMFERENCE)}`}
                strokeDashoffset={offset}
                style={{ stroke: bucket.color, '--dash-total': CIRCUMFERENCE } as CSSProperties}
                onPointerEnter={() => {
                  onActive(bucket.key);
                }}
                onPointerLeave={() => {
                  onActive(null);
                }}
                onClick={
                  bucket.href === undefined
                    ? undefined
                    : () => {
                        void navigate(bucket.href ?? '');
                      }
                }
              >
                {bucket.hint !== undefined && <title>{bucket.hint}</title>}
              </circle>
            );
          })}
        </g>
      </svg>
      <p
        className={hovered === undefined ? 'donut__centre' : 'donut__centre is-active'}
        style={
          hovered === undefined ? undefined : ({ '--chart-color': hovered.color } as CSSProperties)
        }
      >
        <span className="donut__value" dir="ltr">
          {hovered === undefined ? (summary?.value ?? total) : (hovered.value ?? hovered.count)}
        </span>
        <span className="donut__label">
          {hovered !== undefined && <span className="distribution__key" />}
          {hovered === undefined ? (summary?.label ?? totalLabel) : hovered.text}
        </span>
      </p>
    </div>
  );
}

/** Rounded columns over a light track, each count on its cap and its words under it; they mirror in Arabic. */
export function ColumnChart({
  buckets,
  active,
  onActive,
}: {
  buckets: ChartBucket[];
  active: string | null;
  onActive: (key: string | null) => void;
}): ReactElement {
  const largest = Math.max(1, ...buckets.map((bucket) => bucket.count));
  return (
    <div className="column-chart" aria-hidden={anyLinked(buckets) ? undefined : true}>
      {buckets.map((bucket) => (
        <div
          key={bucket.key}
          className={`column-chart__item${active === null ? '' : active === bucket.key ? ' is-active' : ' is-dimmed'}`}
          onPointerEnter={() => {
            onActive(bucket.key);
          }}
          onPointerLeave={() => {
            onActive(null);
          }}
        >
          <Marks href={bucket.href} hint={bucket.hint} className="column-chart__link">
            <div className="column-chart__track">
              <span className="column-chart__value" dir="ltr">
                {bucket.count}
              </span>
              <span
                className="column-chart__bar"
                style={
                  {
                    blockSize: `${String((bucket.count / largest) * 100)}%`,
                    '--chart-color': bucket.color,
                  } as CSSProperties
                }
              />
            </div>
            <span className="column-chart__label">{bucket.text}</span>
          </Marks>
        </div>
      ))}
    </div>
  );
}

/** A ranked list of rounded bars, each value's words and count above its bar; a long distribution's form. */
export function BarChart({
  buckets,
  active,
  onActive,
}: {
  buckets: ChartBucket[];
  active: string | null;
  onActive: (key: string | null) => void;
}): ReactElement {
  const largest = Math.max(1, ...buckets.map((bucket) => bucket.count));
  return (
    <ul className="bars" aria-hidden={anyLinked(buckets) ? undefined : true}>
      {buckets.map((bucket) => (
        <li
          key={bucket.key}
          className={`bars__item${active === null ? '' : active === bucket.key ? ' is-active' : ' is-dimmed'}`}
          onPointerEnter={() => {
            onActive(bucket.key);
          }}
          onPointerLeave={() => {
            onActive(null);
          }}
        >
          <Marks href={bucket.href} hint={bucket.hint} className="bars__link">
            <span className="bars__label">{bucket.text}</span>
            <span className="bars__value" dir="ltr">
              {bucket.count}
            </span>
            <span className="widget__bar-track">
              <span
                className="widget__bar"
                style={
                  {
                    inlineSize: `${String((bucket.count / largest) * 100)}%`,
                    '--chart-color': bucket.color,
                  } as CSSProperties
                }
              />
            </span>
          </Marks>
        </li>
      ))}
    </ul>
  );
}

/** Two or more percentages side by side for each group, on one 0–100 scale; a value not measured has no bar and says so. */
export function GroupedColumnChart({
  groups,
  series,
  missing,
}: {
  groups: {
    key: string;
    text: string;
    values: (number | null)[];
    /** The group's records (review round 8, point 01). */
    href?: string | undefined;
    hint?: string | undefined;
  }[];
  series: { key: string; text: string; color: string }[];
  missing: string;
}): ReactElement {
  const linked = groups.some((group) => group.href !== undefined);
  return (
    <div className="grouped-columns" aria-hidden={linked ? undefined : true}>
      <ul className="grouped-columns__legend">
        {series.map((entry) => (
          <li key={entry.key}>
            <span
              className="distribution__key"
              style={{ '--chart-color': entry.color } as CSSProperties}
            />
            {entry.text}
          </li>
        ))}
      </ul>
      <div className="grouped-columns__plot">
        {groups.map((group) => (
          <div key={group.key} className="grouped-columns__group">
            <Marks href={group.href} hint={group.hint} className="grouped-columns__link">
              <div className="grouped-columns__bars">
                {series.map((entry, index) => {
                  const value = group.values[index] ?? null;
                  return (
                    <div key={entry.key} className="grouped-columns__track">
                      <span className="grouped-columns__value" dir="ltr">
                        {value === null ? missing : `${String(Math.round(value * 10) / 10)}%`}
                      </span>
                      {value !== null && (
                        <span
                          className="grouped-columns__bar"
                          style={
                            {
                              blockSize: `${String(Math.min(100, Math.max(0, value)))}%`,
                              '--chart-color': entry.color,
                            } as CSSProperties
                          }
                        />
                      )}
                    </div>
                  );
                })}
              </div>
              <span className="column-chart__label">{group.text}</span>
            </Marks>
          </div>
        ))}
      </div>
    </div>
  );
}

const GAUGE_SIZE = 152;
const RINGS = [
  { radius: 64, stroke: 14 },
  { radius: 44, stroke: 10 },
] as const;

/** Concentric rings, one per percentage (the first outermost), each filled to its value from twelve o'clock. */
export function GaugeChart({
  rings,
  lead,
}: {
  rings: { key: string; value: number | null; color: string }[];
  lead: string | null;
}): ReactElement {
  const centre = GAUGE_SIZE / 2;
  return (
    <div className="gauge" aria-hidden="true">
      <svg viewBox={`0 0 ${String(GAUGE_SIZE)} ${String(GAUGE_SIZE)}`} focusable="false">
        <g transform={`rotate(-90 ${String(centre)} ${String(centre)})`}>
          {rings.slice(0, RINGS.length).map((ring, index) => {
            const { radius, stroke } = RINGS[index] ?? RINGS[0];
            const circumference = 2 * Math.PI * radius;
            const filled =
              ring.value === null
                ? 0
                : (Math.min(100, Math.max(0, ring.value)) / 100) * circumference;
            return (
              <g key={ring.key}>
                <circle
                  className="gauge__track"
                  cx={centre}
                  cy={centre}
                  r={radius}
                  strokeWidth={stroke}
                  style={{ stroke: ring.color }}
                />
                {filled > 0 && (
                  <circle
                    className="gauge__fill"
                    cx={centre}
                    cy={centre}
                    r={radius}
                    strokeWidth={stroke}
                    strokeLinecap="round"
                    strokeDasharray={`${String(filled)} ${String(circumference)}`}
                    style={{ stroke: ring.color, '--dash-total': circumference } as CSSProperties}
                  />
                )}
              </g>
            );
          })}
        </g>
      </svg>
      {lead !== null && (
        <p className="gauge__centre" dir="ltr">
          {lead}
        </p>
      )}
    </div>
  );
}
