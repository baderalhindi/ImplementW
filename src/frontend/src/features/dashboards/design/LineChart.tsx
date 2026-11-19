import { type CSSProperties, type ReactElement, useId } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';

import { type AxisProps } from './AxisCharts.tsx';

// A designed line chart (TASK-112, more fields): one line per field value over the visual's groups, which run left to right in both languages
// (a period's order is the clock's, not the reading direction). Series are told apart by line style as well as colour (solid, dashed, dotted)
// and named in the legend; each point carries its value on hover; a group without a value breaks its line instead of dropping to 0. Its axes
// (axes.ts) are drawn as the bar and column charts' are; a value past a given start or end is clipped at the plot's edge. The chart
// is a picture with a name: the table under it (VisualBody) carries the same figures for a screen reader.

const WIDTH = 640;
const TOP = 12;
/** The plot's height; the room under it holds the groups' words and the category axis's title. */
const PLOT = 172;
const BOTTOM = TOP + PLOT;
const RIGHT = 16;
/** Above this many groups only every n-th is labelled, so the labels do not overlap. */
const MAX_LABELS = 8;
const DASHES = [undefined, '7 4', '2 4'];

export interface LinePoint {
  key: string;
  text: string;
  values: (number | null)[];
}

/** A group's place along the plot, from `left` to the right edge. */
const xAt = (left: number, index: number, count: number) =>
  count === 1 ? (left + WIDTH - RIGHT) / 2 : left + ((WIDTH - left - RIGHT) * index) / (count - 1);

/** Consecutive points with a value form a run; a point without one breaks the line. */
function runs(
  values: (number | null)[],
  x: (index: number) => number,
  y: (value: number) => number,
): string[] {
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
    current.push(`${String(x(index))} ${String(y(value))}`);
  });
  close();
  return result;
}

export function LineChart({
  title,
  points,
  series,
  format,
  scale,
  tick,
  axes,
  categoryTitle,
  valueTitle,
}: AxisProps & {
  title: string;
  points: LinePoint[];
  series: { key: string; text: string; color: string }[];
  /** Writes a value of the series at `index`. */
  format: (value: number, index: number) => string;
}): ReactElement {
  const { t } = useI18n();
  const clip = `line-plot-${useId().replace(/[^a-zA-Z0-9-]/g, '')}`;
  const valueShown = axes.value.show;
  const categoryShown = axes.category.show;
  const valueTitleShown = valueShown && valueTitle !== null;
  const categoryTitleShown = categoryShown && categoryTitle !== null;
  // Room at the start for the ticks and the value axis's title, and under the plot for the groups' words and the category axis's title.
  const left = valueShown ? 64 + (valueTitleShown ? 20 : 0) : RIGHT;
  const height = BOTTOM + (categoryShown ? 36 + (categoryTitleShown ? 20 : 0) : 12);
  const x = (index: number) => xAt(left, index, points.length);
  // A value past a given start or end is drawn outside the plot, which clips it.
  const y = (value: number) => BOTTOM - ((value - scale.low) / (scale.high - scale.low)) * PLOT;
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
        viewBox={`0 0 ${String(WIDTH)} ${String(height)}`}
        role="img"
        aria-label={t('dashboards.designer.lineLabel', { title, count: points.length })}
      >
        <defs>
          <clipPath id={clip}>
            <rect x={left} y={TOP - 6} width={WIDTH - RIGHT - left} height={PLOT + 12} />
          </clipPath>
        </defs>
        {axes.value.gridlines &&
          scale.ticks.map((value) => (
            <line
              key={value}
              className="line-chart__gridline"
              x1={left}
              x2={WIDTH - RIGHT}
              y1={y(value)}
              y2={y(value)}
            />
          ))}
        {valueShown && (
          <g className="line-chart__axis">
            <line x1={left} x2={left} y1={TOP} y2={BOTTOM} />
            {scale.ticks.map((value) => (
              <text key={value} x={left - 8} y={y(value) + 4} textAnchor="end">
                {tick(value)}
              </text>
            ))}
            {valueTitleShown && (
              <text
                className="line-chart__title"
                transform="rotate(-90)"
                x={-(TOP + BOTTOM) / 2}
                y={14}
                textAnchor="middle"
              >
                {valueTitle}
              </text>
            )}
          </g>
        )}
        {categoryShown && (
          <g className="line-chart__axis">
            <line x1={left} x2={WIDTH - RIGHT} y1={BOTTOM} y2={BOTTOM} />
            {points.map((point, index) =>
              index % every === 0 || index === points.length - 1 ? (
                <text
                  key={point.key}
                  className="trend__label"
                  x={x(index)}
                  y={BOTTOM + 24}
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
            {categoryTitleShown && (
              <text
                className="line-chart__title"
                x={(left + WIDTH - RIGHT) / 2}
                y={BOTTOM + 48}
                textAnchor="middle"
              >
                {categoryTitle}
              </text>
            )}
          </g>
        )}
        <g clipPath={`url(#${clip})`}>
          {series.map((entry, column) => {
            const values = points.map((point) => point.values[column] ?? null);
            const style = { '--chart-color': entry.color } as CSSProperties;
            return (
              <g key={entry.key}>
                {runs(values, x, y).map((path) => (
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
                      cx={x(index)}
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
        </g>
      </svg>
    </div>
  );
}
