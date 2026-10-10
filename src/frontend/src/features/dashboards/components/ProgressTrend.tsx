import { type ReactElement } from 'react';

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

/** Consecutive points with a value joined; a point without one breaks the line. */
function paths(points: WidgetSeriesPoint[], measure: Series): string[] {
  const result: string[] = [];
  let current: string[] = [];
  points.forEach((point, index) => {
    const value = valueOf(point, measure);
    if (value === null) {
      if (current.length > 1) {
        result.push(current.join(' '));
      }
      current = [];
      return;
    }
    current.push(
      `${current.length === 0 ? 'M' : 'L'}${String(x(index, points.length))} ${String(y(value))}`,
    );
  });
  if (current.length > 1) {
    result.push(current.join(' '));
  }
  return result;
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
      <svg
        className="trend__chart"
        viewBox={`0 0 ${String(WIDTH)} ${String(HEIGHT)}`}
        role="img"
        aria-label={t('dashboards.trend.chartLabel', { title, count: points.length })}
      >
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
        {SERIES.map((measure) => (
          <g key={measure}>
            {paths(points, measure).map((path) => (
              <path key={path} d={path} className={`trend__line trend__line--${measure}`} />
            ))}
            {points.map((point, index) => {
              const value = valueOf(point, measure);
              return value === null ? null : (
                <circle
                  key={point.asOf}
                  className={`trend__point trend__point--${measure}`}
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
      </svg>
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
    </div>
  );
}
