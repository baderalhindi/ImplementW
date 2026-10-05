import { type ReactElement, useId } from 'react';

import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';

import { type TargetSegment, targetSegments, type TrendPoint } from '../financialKpiRules.ts';
import { formatKpiValue } from '../presentation.ts';

// SCR-073's trend (acceptance criterion 2): each period's value beside the target it was rated against when recorded.
// The target line is drawn from each measurement's own pinned version, one step per version, so approving a new target
// draws a new step from the next value on and leaves the earlier steps where they were — nothing older is redrawn
// against the target in force now. A period without a figure is marked in its own row under the plot, in words,
// never at 0. Time runs left to right in both languages; the table under the chart carries the same data.

const WIDTH = 640;
const PLOT_TOP = 24;
const PLOT_BOTTOM = 196;
const GAP_ROW = 222;
const LABEL_ROW = 252;
const HEIGHT = 268;
const LEFT = 64;
const RIGHT = 16;
const TICKS = 4;
/** Above this many periods only every n-th is labelled, so the dates do not overlap. */
const MAX_LABELS = 8;

const GAP_LABELS: Record<string, TranslationKey> = {
  MISSING: 'financialKpi.trend.gap.MISSING',
  STALE: 'financialKpi.trend.gap.STALE',
  NOT_APPLICABLE: 'financialKpi.trend.gap.NOT_APPLICABLE',
  RESTRICTED: 'financialKpi.trend.gap.RESTRICTED',
};

interface Scale {
  x: (index: number) => number;
  y: (value: number) => number;
  step: number;
  ticks: number[];
}

/** The value axis spans every value and every pinned target, padded; it starts at no presumed 0. */
function scaleOf(points: TrendPoint[]): Scale {
  const values = points.flatMap((point) =>
    point.value === null ? [point.targetValue] : [point.value, point.targetValue],
  );
  const low = Math.min(...values);
  const high = Math.max(...values);
  const pad = high === low ? Math.max(Math.abs(high) * 0.1, 1) : (high - low) * 0.1;
  const min = low - pad;
  const max = high + pad;
  const step = (WIDTH - LEFT - RIGHT) / points.length;
  return {
    x: (index) => LEFT + step * (index + 0.5),
    y: (value) => PLOT_BOTTOM - ((value - min) / (max - min)) * (PLOT_BOTTOM - PLOT_TOP),
    step,
    ticks: Array.from({ length: TICKS + 1 }, (_, index) => min + ((max - min) * index) / TICKS),
  };
}

/** Consecutive measured points joined; a period without a figure breaks the line. */
function valuePaths(points: TrendPoint[], scale: Scale): string[] {
  const paths: string[] = [];
  let current: string[] = [];
  points.forEach((point, index) => {
    if (point.value === null) {
      if (current.length > 1) {
        paths.push(current.join(' '));
      }
      current = [];
      return;
    }
    current.push(
      `${current.length === 0 ? 'M' : 'L'}${String(scale.x(index))} ${String(scale.y(point.value))}`,
    );
  });
  if (current.length > 1) {
    paths.push(current.join(' '));
  }
  return paths;
}

function gapKey(point: TrendPoint): string {
  return point.restricted ? 'RESTRICTED' : point.valueStatus;
}

export function KpiTrendChart({
  points,
  kpiName,
  unitLabel,
}: {
  points: TrendPoint[];
  kpiName: string;
  unitLabel: string;
}): ReactElement {
  const { t } = useI18n();
  const descriptionId = useId();
  const scale = scaleOf(points);
  const segments = targetSegments(points);
  const labelEvery = Math.ceil(points.length / MAX_LABELS);
  const gaps = points.filter((point) => point.value === null).length;
  const title = t('financialKpi.trend.title', { kpi: kpiName, unit: unitLabel });
  const segmentStart = (segment: TargetSegment) => scale.x(segment.first) - scale.step / 2;

  const description = [
    t('financialKpi.trend.summary', { count: points.length, gaps }),
    ...segments.map((segment) =>
      t('financialKpi.trend.segment', {
        version: segment.targetVersionNo,
        target: formatKpiValue(segment.targetValue),
        from: points[segment.first]?.periodStart ?? '',
        to: points[segment.last]?.periodEnd ?? '',
      }),
    ),
  ].join(' ');

  return (
    <figure className="kpi-trend" dir="ltr">
      <svg
        className="kpi-trend__chart"
        viewBox={`0 0 ${String(WIDTH)} ${String(HEIGHT)}`}
        role="img"
        aria-label={title}
        aria-describedby={descriptionId}
      >
        <title>{title}</title>
        <desc id={descriptionId}>{description}</desc>

        {scale.ticks.map((tick) => (
          <g key={tick} className="kpi-trend__tick">
            <line x1={LEFT} x2={WIDTH - RIGHT} y1={scale.y(tick)} y2={scale.y(tick)} />
            <text x={LEFT - 8} y={scale.y(tick)} textAnchor="end" dominantBaseline="middle">
              {formatKpiValue(Math.round(tick * 100) / 100)}
            </text>
          </g>
        ))}

        {segments.map((segment, index) => {
          const x1 = segmentStart(segment);
          const x2 = scale.x(segment.last) + scale.step / 2;
          const y = scale.y(segment.targetValue);
          return (
            <g
              key={`${String(segment.targetVersionNo)}-${String(segment.first)}`}
              className="kpi-trend__target"
              data-target-version={segment.targetVersionNo}
              data-target-value={segment.targetValue}
              data-first-period={points[segment.first]?.periodStart}
            >
              {index > 0 && (
                <line
                  className="kpi-trend__change"
                  x1={x1}
                  x2={x1}
                  y1={PLOT_TOP - 8}
                  y2={PLOT_BOTTOM}
                />
              )}
              <line x1={x1} x2={x2} y1={y} y2={y} />
              <text x={x1 + 4} y={y - 6}>
                {t('financialKpi.trend.targetLabel', {
                  version: segment.targetVersionNo,
                  target: formatKpiValue(segment.targetValue),
                })}
              </text>
            </g>
          );
        })}

        {valuePaths(points, scale).map((path) => (
          <path key={path} className="kpi-trend__line" d={path} />
        ))}

        {points.map((point, index) =>
          point.value === null ? (
            <g
              key={point.id}
              className="kpi-trend__gap"
              data-period={point.periodStart}
              data-value-state={gapKey(point)}
            >
              <rect x={scale.x(index) - 5} y={GAP_ROW - 5} width="10" height="10" />
              <text x={scale.x(index)} y={GAP_ROW + 18} textAnchor="middle">
                {t(GAP_LABELS[gapKey(point)] ?? 'financialKpi.trend.gap.MISSING')}
              </text>
            </g>
          ) : (
            <circle
              key={point.id}
              className={`kpi-trend__point kpi-trend__point--${point.ragStatus.toLowerCase()}`}
              cx={scale.x(index)}
              cy={scale.y(point.value)}
              r="5"
              data-period={point.periodStart}
              data-value={point.value}
              data-pinned-target-version={point.targetVersionNo}
              data-pinned-target={point.targetValue}
            >
              <title>
                {t('financialKpi.trend.pointTitle', {
                  period: point.periodStart,
                  value: formatKpiValue(point.value),
                  version: point.targetVersionNo,
                  target: formatKpiValue(point.targetValue),
                  rating: t(`financialKpi.rag.${point.ragStatus}`),
                })}
              </title>
            </circle>
          ),
        )}

        <text
          className="kpi-trend__row-label"
          x={LEFT - 8}
          y={GAP_ROW}
          textAnchor="end"
          dominantBaseline="middle"
        >
          {t('financialKpi.trend.gapRow')}
        </text>
        {points.map((point, index) =>
          index % labelEvery === 0 ? (
            <text
              key={point.id}
              className="kpi-trend__period"
              x={scale.x(index)}
              y={LABEL_ROW}
              textAnchor="middle"
            >
              {point.periodStart}
            </text>
          ) : null,
        )}
      </svg>
      <figcaption className="kpi-trend__legend">
        <span className="kpi-trend__key kpi-trend__key--value">
          {t('financialKpi.trend.legend.value')}
        </span>
        <span className="kpi-trend__key kpi-trend__key--target">
          {t('financialKpi.trend.legend.target')}
        </span>
        <span className="kpi-trend__key kpi-trend__key--gap">
          {t('financialKpi.trend.legend.gap')}
        </span>
      </figcaption>
    </figure>
  );
}
