import { type CSSProperties, type ReactElement } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';

// A designed line chart (TASK-112, more fields): one line per field value over the visual's groups, which run left to right in both languages
// (a period's order is the clock's, not the reading direction). Series are told apart by line style as well as colour (solid, dashed, dotted)
// and named in the legend; each point carries its value on hover; a group without a value breaks its line instead of dropping to 0. The chart
// is a picture with a name: the table under it (VisualBody) carries the same figures for a screen reader.

const WIDTH = 640;
const HEIGHT = 220;
const TOP = 12;
const BOTTOM = 184;
const LEFT = 64;
const RIGHT = 16;
/** Above this many groups only every n-th is labelled, so the labels do not overlap. */
const MAX_LABELS = 8;
const TICKS = 4;
const DASHES = [undefined, '7 4', '2 4'];

export interface LinePoint {
  key: string;
  text: string;
  values: (number | null)[];
}

/** The smallest round step (1, 2, 2.5 or 5 times a power of ten) at least `raw`. */
function roundStep(raw: number): number {
  const power = 10 ** Math.floor(Math.log10(raw));
  return (
    [1, 2, 2.5, 5, 10].map((factor) => factor * power).find((candidate) => candidate >= raw) ??
    10 * power
  );
}

const x = (index: number, count: number) =>
  count === 1 ? (LEFT + WIDTH - RIGHT) / 2 : LEFT + ((WIDTH - LEFT - RIGHT) * index) / (count - 1);

/** Consecutive points with a value form a run; a point without one breaks the line. */
function runs(values: (number | null)[], y: (value: number) => number): string[] {
  const result: string[] = [];
  let current: string[] = [];
  const close = () => {
    if (current.length > 1) {
      result.push(`M${current.join(' L')}`);
    }
    current = [];
  };
  values.forEach((value, index) => {
    if (value === null) {
      close();
      return;
    }
    current.push(`${String(x(index, values.length))} ${String(y(value))}`);
  });
  close();
  return result;
}

export function LineChart({
  title,
  points,
  series,
  percent,
  format,
  axis,
}: {
  title: string;
  points: LinePoint[];
  series: { key: string; text: string; color: string }[];
  /** Every series is a percentage: the scale runs from 0 to 100. */
  percent: boolean;
  /** Writes a value of the series at `index`. */
  format: (value: number, index: number) => string;
  /** Writes a value on the scale: as the series write theirs where they all read alike, else as a plain number. */
  axis: (value: number) => string;
}): ReactElement {
  const { t } = useI18n();
  const all = points.flatMap((point) =>
    point.values.filter((value): value is number => value !== null),
  );
  // The scale: 0 to 100 for percentages, else from 0 (or the lowest value below it) to the highest, in round steps.
  const step = percent
    ? 100 / TICKS
    : roundStep((Math.max(1, ...all) - Math.min(0, ...all)) / TICKS);
  const low = percent ? 0 : Math.floor(Math.min(0, ...all) / step) * step;
  const high = percent ? 100 : Math.max(low + step, Math.ceil(Math.max(...all, 0) / step) * step);
  const y = (value: number) =>
    BOTTOM - ((Math.min(high, Math.max(low, value)) - low) / (high - low)) * (BOTTOM - TOP);
  const ticks = Array.from(
    { length: Math.round((high - low) / step) + 1 },
    (_, index) => low + step * index,
  );
  const every = Math.ceil(points.length / MAX_LABELS);

  return (
    <div className="line-chart">
      <ul className="trend__legend">
        {series.map((entry, index) => (
          <li key={entry.key}>
            <svg width="24" height="8" aria-hidden="true" focusable="false">
              <line
                x1="0"
                y1="4"
                x2="24"
                y2="4"
                className="line-chart__line"
                style={{ '--chart-color': entry.color } as CSSProperties}
                strokeDasharray={DASHES[index]}
              />
            </svg>
            {entry.text}
          </li>
        ))}
      </ul>
      <svg
        className="trend__chart line-chart__chart"
        viewBox={`0 0 ${String(WIDTH)} ${String(HEIGHT)}`}
        role="img"
        aria-label={t('dashboards.designer.lineLabel', { title, count: points.length })}
      >
        {ticks.map((tick) => (
          <g key={tick} className="trend__tick">
            <line x1={LEFT} x2={WIDTH - RIGHT} y1={y(tick)} y2={y(tick)} />
            <text x={LEFT - 8} y={y(tick) + 4} textAnchor="end">
              {axis(Math.round(tick * 100) / 100)}
            </text>
          </g>
        ))}
        {points.map((point, index) =>
          index % every === 0 || index === points.length - 1 ? (
            <text
              key={point.key}
              className="trend__label"
              x={x(index, points.length)}
              y={HEIGHT - 12}
              textAnchor={
                points.length === 1
                  ? 'middle'
                  : index === 0
                    ? 'start'
                    : index === points.length - 1
                      ? 'end'
                      : 'middle'
              }
            >
              {point.text}
            </text>
          ) : null,
        )}
        {series.map((entry, column) => {
          const values = points.map((point) => point.values[column] ?? null);
          const style = { '--chart-color': entry.color } as CSSProperties;
          return (
            <g key={entry.key}>
              {runs(values, y).map((path) => (
                <path
                  key={path}
                  d={path}
                  className="line-chart__line"
                  style={style}
                  strokeDasharray={DASHES[column]}
                />
              ))}
              {values.map((value, index) =>
                value === null ? null : (
                  <circle
                    key={points[index]?.key ?? index}
                    className="line-chart__point"
                    style={style}
                    cx={x(index, values.length)}
                    cy={y(value)}
                    r={4}
                  >
                    <title>{`${points[index]?.text ?? ''} · ${entry.text}: ${format(value, column)}`}</title>
                  </circle>
                ),
              )}
            </g>
          );
        })}
      </svg>
    </div>
  );
}
