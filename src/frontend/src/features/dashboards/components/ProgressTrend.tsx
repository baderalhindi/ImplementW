import { type PointerEvent, type ReactElement, useId, useState } from 'react';

import { formatPercent } from '@/features/progress/presentation.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { type WidgetData, type WidgetFigure, type WidgetSeriesPoint } from '../api/types.ts';
import { labelOfValue } from '../presentation.ts';

import { FigureValue } from './WidgetBodies.tsx';

// LINE_TREND over WF-02's own published history (HISTORICAL/SNAPSHOT, BR-DSH-018): each published period's actual and
// planned percentage, never rebuilt from current records. Two series, told apart by line style as well as colour
// (planned is dashed, its points rings) and named in a legend and at their last point; each point carries its values
// on hover; time runs left to right in both languages; the table under the chart carries the same data. A period
// without a figure breaks its line instead of dropping to 0.

const WIDTH = 640;
const HEIGHT = 200;
const TOP = 16;
const BOTTOM = 172;
const LEFT = 48;
const RIGHT = 88;
const TICKS = [0, 25, 50, 75, 100];
/** Above this many periods only every n-th is labelled, so the dates do not overlap. */
const MAX_LABELS = 8;
/** Rows apart, in user units, that two end labels need so they never overlap. */
const LABEL_GAP = 14;

type Series = 'ACTUAL_PERCENT' | 'PLANNED_PERCENT';
const SERIES: Series[] = ['ACTUAL_PERCENT', 'PLANNED_PERCENT'];

function valueOf(point: WidgetSeriesPoint, measure: Series): number | null {
  const figure = point.figures.find((candidate) => candidate.measure === measure);
  const value = figure?.isMasked === false ? figure.value : null;
  return value === null ? null : Number(value);
}

function figureOf(point: WidgetSeriesPoint, measure: Series): WidgetFigure {
  return (
    point.figures.find((candidate) => candidate.measure === measure) ?? {
      measure,
      value: null,
      unit: 'PERCENT',
      isMasked: false,
    }
  );
}

const x = (index: number, count: number) =>
  count === 1 ? (LEFT + WIDTH - RIGHT) / 2 : LEFT + ((WIDTH - LEFT - RIGHT) * index) / (count - 1);
const y = (value: number) => BOTTOM - (Math.min(100, Math.max(0, value)) / 100) * (BOTTOM - TOP);

type Point = [number, number];

/** Consecutive points with a value form a run; a point without one breaks the line. */
function runs(points: WidgetSeriesPoint[], measure: Series): Point[][] {
  const result: Point[][] = [];
  let current: Point[] = [];
  points.forEach((point, index) => {
    const value = valueOf(point, measure);
    if (value === null) {
      if (current.length > 1) {
        result.push(current);
      }
      current = [];
      return;
    }
    current.push([x(index, points.length), y(value)]);
  });
  if (current.length > 1) {
    result.push(current);
  }
  return result;
}

/**
 * A run as a smooth curve through every point (monotone cubic, Fritsch–Carlson): it never swings above or below its
 * neighbours, so the drawn line never shows a percentage the source did not publish.
 */
function curve(run: Point[]): string {
  const slopes = run.slice(1).map(([x1, y1], index) => {
    const [x0, y0] = run[index] ?? [x1, y1];
    return (y1 - y0) / (x1 - x0);
  });
  const tangents = run.map((_, index) => {
    const before = slopes[index - 1];
    const after = slopes[index];
    if (before === undefined) {
      return after ?? 0;
    }
    if (after === undefined) {
      return before;
    }
    return before * after <= 0 ? 0 : (before + after) / 2;
  });
  slopes.forEach((slope, index) => {
    if (slope === 0) {
      tangents[index] = 0;
      tangents[index + 1] = 0;
      return;
    }
    const a = (tangents[index] ?? 0) / slope;
    const b = (tangents[index + 1] ?? 0) / slope;
    const length = a * a + b * b;
    if (length > 9) {
      const scale = 3 / Math.sqrt(length);
      tangents[index] = scale * a * slope;
      tangents[index + 1] = scale * b * slope;
    }
  });
  return run
    .map(([px, py], index) => {
      if (index === 0) {
        return `M${String(px)} ${String(py)}`;
      }
      const [qx, qy] = run[index - 1] ?? [px, py];
      const third = (px - qx) / 3;
      const c1 = [qx + third, qy + (tangents[index - 1] ?? 0) * third];
      const c2 = [px - third, py - (tangents[index] ?? 0) * third];
      return `C${c1.join(' ')} ${c2.join(' ')} ${String(px)} ${String(py)}`;
    })
    .join(' ');
}

/** The wash under a run, down to the 0% line. */
function area(run: Point[]): string {
  const [firstX] = run[0] ?? [0, 0];
  const [lastX] = run.at(-1) ?? [0, 0];
  return `${curve(run)} L${String(lastX)} ${String(BOTTOM)} L${String(firstX)} ${String(BOTTOM)} Z`;
}

/**
 * Each series named at its last point: the higher value's label above its point, the lower's below, pushed apart when
 * the two points are close, so neither covers the other.
 */
function endLabels(points: WidgetSeriesPoint[]): { measure: Series; labelY: number }[] {
  const last = points.at(-1);
  if (last === undefined) {
    return [];
  }
  const [upper, lower] = SERIES.flatMap((measure) => {
    const value = valueOf(last, measure);
    return value === null ? [] : [{ measure, pointY: y(value) }];
  }).sort((a, b) => a.pointY - b.pointY);
  if (upper === undefined) {
    return [];
  }
  const upperY = upper.pointY - 4;
  return lower === undefined
    ? [{ measure: upper.measure, labelY: upperY }]
    : [
        { measure: upper.measure, labelY: upperY },
        { measure: lower.measure, labelY: Math.max(lower.pointY + 12, upperY + LABEL_GAP) },
      ];
}

export function ProgressTrend({
  projectionCode,
  data,
  title,
}: {
  projectionCode: string;
  data: WidgetData;
  title: string;
}): ReactElement {
  const { t, language, formatDateTime } = useI18n();
  const points = data.series;
  const every = Math.ceil(points.length / MAX_LABELS);
  const period = (point: WidgetSeriesPoint) =>
    point.periodStart === null || point.periodEnd === null
      ? formatDateTime(point.asOf)
      : t('dashboards.trend.range', { start: point.periodStart, end: point.periodEnd });
  const percent = (value: number | null) => formatPercent(value) ?? t('dashboards.figure.none');
  const names: Record<Series, string> = {
    ACTUAL_PERCENT: t('dashboards.measures.ACTUAL_PERCENT'),
    PLANNED_PERCENT: t('dashboards.measures.PLANNED_PERCENT'),
  };
  const tableLabel = t('dashboards.trend.table', { title });
  const gradientId = useId();
  const [hovered, setHovered] = useState<number | null>(null);
  const hoveredPoint = hovered === null ? undefined : points[hovered];
  /** The crosshair snaps to the period nearest the pointer, so a reader aims at a date, never at a 2px line. */
  const track = (event: PointerEvent<SVGRectElement>) => {
    const box = event.currentTarget.ownerSVGElement?.getBoundingClientRect();
    if (box === undefined || box.width === 0 || points.length === 0) {
      return;
    }
    const at = ((event.clientX - box.left) / box.width) * WIDTH;
    const step = points.length === 1 ? 1 : (WIDTH - LEFT - RIGHT) / (points.length - 1);
    setHovered(Math.min(points.length - 1, Math.max(0, Math.round((at - LEFT) / step))));
  };

  return (
    <div className="widget__body">
      <ul className="trend__legend">
        {SERIES.map((measure) => (
          <li key={measure}>
            <svg width="24" height="8" aria-hidden="true" focusable="false">
              <line
                x1="0"
                y1="4"
                x2="24"
                y2="4"
                className={`trend__line trend__line--${measure}`}
              />
            </svg>
            {names[measure]}
          </li>
        ))}
      </ul>
      <div className="trend__plot">
        <svg
          className="trend__chart"
          viewBox={`0 0 ${String(WIDTH)} ${String(HEIGHT)}`}
          role="img"
          aria-label={t('dashboards.trend.chartLabel', { title, count: points.length })}
        >
          <defs>
            <linearGradient id={gradientId} x1="0" y1="0" x2="0" y2="1">
              <stop offset="0" className="trend__wash-top" />
              <stop offset="1" className="trend__wash-bottom" />
            </linearGradient>
          </defs>
          {TICKS.map((tick) => (
            <g key={tick} className="trend__tick">
              <line x1={LEFT} x2={WIDTH - RIGHT} y1={y(tick)} y2={y(tick)} />
              <text x={LEFT - 8} y={y(tick) + 4} textAnchor="end">
                {`${String(tick)}%`}
              </text>
            </g>
          ))}
          {points.map((point, index) =>
            index % every === 0 || index === points.length - 1 ? (
              <text
                key={point.asOf}
                className="trend__label"
                x={x(index, points.length)}
                y={HEIGHT - 8}
                textAnchor="middle"
              >
                {point.periodEnd ?? point.asOf.slice(0, 10)}
              </text>
            ) : null,
          )}
          {runs(points, 'ACTUAL_PERCENT').map((run) => (
            <path
              key={`area-${String(run[0]?.[0])}`}
              d={area(run)}
              className="trend__area"
              fill={`url(#${gradientId})`}
            />
          ))}
          {hoveredPoint !== undefined && hovered !== null && (
            <line
              className="trend__crosshair"
              x1={x(hovered, points.length)}
              x2={x(hovered, points.length)}
              y1={TOP}
              y2={BOTTOM}
            />
          )}
          {SERIES.map((measure) => (
            <g key={measure}>
              {runs(points, measure).map((run) => {
                const path = curve(run);
                return (
                  <path
                    key={path}
                    d={path}
                    pathLength={1}
                    className={`trend__line trend__line--${measure}`}
                  />
                );
              })}
              {points.map((point, index) => {
                const value = valueOf(point, measure);
                return value === null ? null : (
                  <circle
                    key={point.asOf}
                    className={`trend__point trend__point--${measure}${hovered === index ? ' is-active' : ''}`}
                    cx={x(index, points.length)}
                    cy={y(value)}
                    r={measure === 'PLANNED_PERCENT' ? 6 : 4}
                  >
                    <title>{`${period(point)} · ${names[measure]}: ${percent(value)}`}</title>
                  </circle>
                );
              })}
            </g>
          ))}
          {endLabels(points).map(({ measure, labelY }) => (
            <text
              key={measure}
              className="trend__end-label"
              x={x(points.length - 1, points.length) + 10}
              y={labelY}
            >
              {names[measure]}
            </text>
          ))}
          <rect
            className="trend__hit"
            x={LEFT}
            y={TOP}
            width={WIDTH - LEFT - RIGHT}
            height={BOTTOM - TOP}
            onPointerMove={track}
            onPointerLeave={() => {
              setHovered(null);
            }}
          />
        </svg>
        {hoveredPoint !== undefined && hovered !== null && (
          <div
            className="trend__tooltip"
            aria-hidden="true"
            dir={language === 'ar' ? 'rtl' : 'ltr'}
            style={{
              left: `${String((x(hovered, points.length) / WIDTH) * 100)}%`,
            }}
          >
            <p className="trend__tooltip-period">{period(hoveredPoint)}</p>
            {SERIES.map((measure) => (
              <p key={measure} className="trend__tooltip-row">
                <span className={`trend__key trend__key--${measure}`} />
                <span>{names[measure]}</span>
                <strong dir="ltr">{percent(valueOf(hoveredPoint, measure))}</strong>
              </p>
            ))}
          </div>
        )}
      </div>
      <details className="widget__data">
        <summary>{t('dashboards.trend.showTable')}</summary>
        <div className="table-container" role="region" aria-label={tableLabel} tabIndex={0}>
          <table className="table">
            <caption className="visually-hidden">{tableLabel}</caption>
            <thead>
              <tr>
                <th scope="col">{t('dashboards.trend.period')}</th>
                <th scope="col">{t('dashboards.trend.health')}</th>
                <th scope="col">{names.ACTUAL_PERCENT}</th>
                <th scope="col">{names.PLANNED_PERCENT}</th>
                <th scope="col">{t('dashboards.trend.publishedAt')}</th>
              </tr>
            </thead>
            <tbody>
              {points.map((point) => {
                const health =
                  point.state === null
                    ? null
                    : labelOfValue(projectionCode, point.state, null, language, t);
                return (
                  <tr key={point.asOf}>
                    <td className="cell--ltr">{period(point)}</td>
                    <td>
                      {health === null ? (
                        t('dashboards.figure.none')
                      ) : (
                        <StatusBadge label={health.text} tone={health.tone} />
                      )}
                    </td>
                    <td>
                      <FigureValue figure={figureOf(point, 'ACTUAL_PERCENT')} />
                    </td>
                    <td>
                      <FigureValue figure={figureOf(point, 'PLANNED_PERCENT')} />
                    </td>
                    <td>{formatDateTime(point.asOf)}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </details>
    </div>
  );
}
