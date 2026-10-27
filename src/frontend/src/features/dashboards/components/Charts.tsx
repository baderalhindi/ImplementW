import { type CSSProperties, type ReactElement } from 'react';

import { type ChartBucket } from '../charts.ts';

// The drawn half of a widget: every chart here only repeats values the widget's table states in words, so each is
// hidden from assistive technology and the table stays the text alternative.

const SIZE = 168;
const RADIUS = 66;
const STROKE = 20;
/** The surface between two segments, beyond their round caps. */
const GAP = 4;
const CIRCUMFERENCE = 2 * Math.PI * RADIUS;

/**
 * A donut with round-ended segments and a gap between them, from twelve o'clock clockwise in the source's order. The
 * centre shows the total of the counts drawn; hovering a value (on the ring or in its legend) shows that value's count
 * and words instead, on a tint of its colour. The number stays in text colour: amber and orange fail contrast as text.
 */
export function DonutChart({
  buckets,
  active,
  onActive,
  totalLabel,
}: {
  buckets: ChartBucket[];
  active: string | null;
  onActive: (key: string | null) => void;
  totalLabel: string;
}): ReactElement {
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
            // A lone value is the whole ring; otherwise each round cap reaches half the stroke past its dash.
            const dash = drawn.length === 1 ? CIRCUMFERENCE : Math.max(0.01, length - STROKE - GAP);
            const offset = drawn.length === 1 ? 0 : -((starts[index] ?? 0) + (STROKE + GAP) / 2);
            const state =
              active === null ? '' : active === bucket.key ? ' is-active' : ' is-dimmed';
            return (
              <circle
                key={bucket.key}
                className={`donut__segment${state}`}
                cx={centre}
                cy={centre}
                r={RADIUS}
                strokeWidth={STROKE}
                strokeLinecap={drawn.length === 1 ? 'butt' : 'round'}
                strokeDasharray={`${String(dash)} ${String(CIRCUMFERENCE)}`}
                strokeDashoffset={offset}
                style={{ stroke: bucket.color, '--dash-total': CIRCUMFERENCE } as CSSProperties}
                onPointerEnter={() => {
                  onActive(bucket.key);
                }}
                onPointerLeave={() => {
                  onActive(null);
                }}
              />
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
          {hovered === undefined ? total : hovered.count}
        </span>
        <span className="donut__label">
          {hovered !== undefined && <span className="distribution__key" />}
          {hovered === undefined ? totalLabel : hovered.text}
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
    <div className="columns" aria-hidden="true">
      {buckets.map((bucket) => (
        <div
          key={bucket.key}
          className={`columns__item${active === null ? '' : active === bucket.key ? ' is-active' : ' is-dimmed'}`}
          onPointerEnter={() => {
            onActive(bucket.key);
          }}
          onPointerLeave={() => {
            onActive(null);
          }}
        >
          <div className="columns__track">
            <span className="columns__value" dir="ltr">
              {bucket.count}
            </span>
            <span
              className="columns__bar"
              style={
                {
                  blockSize: `${String((bucket.count / largest) * 100)}%`,
                  '--chart-color': bucket.color,
                } as CSSProperties
              }
            />
          </div>
          <span className="columns__label">{bucket.text}</span>
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
    <ul className="bars" aria-hidden="true">
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
        </li>
      ))}
    </ul>
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
