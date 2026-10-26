import { type CSSProperties, type ReactElement, useState } from 'react';

import { OverriddenBadge } from '@/features/progress/components/ProgressBadges.tsx';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { type StatusTone } from '@/shared/ui/StatusBadge.tsx';
import { ValueStateFlag } from '@/shared/ui/ValueState.tsx';

import { type DashboardWidgetType, type WidgetData, type WidgetFigure } from '../api/types.ts';
import { bucketColors, type ChartBucket, chartForm } from '../charts.ts';
import {
  countedKey,
  formatFigure,
  isOverridden,
  isStatusScale,
  labelOfValue,
  measureLabel,
  shownFigures,
} from '../presentation.ts';

import { BarChart, ColumnChart, DonutChart, GaugeChart } from './Charts.tsx';

// The allowlisted visualisations of FG-01 §10 (TASK-069 `DashboardWidgetType`). Each presents the source's values as
// the source states them; none computes one. A figure with no value says why — "Restricted" when it is withheld
// from the caller's audience (ADR-010), "No data" when the source holds none — and is never drawn as 0.

interface BodyProps {
  projectionCode: string;
  widgetType: DashboardWidgetType;
  data: WidgetData;
  title: string;
}

/** One figure's value, or its state in words. */
export function FigureValue({ figure }: { figure: WidgetFigure }): ReactElement {
  const { t } = useI18n();
  const formatted = formatFigure(figure, t);
  if (figure.isMasked) {
    return (
      <ValueStateFlag icon="restricted" label={t('dashboards.figure.restricted')} state="MASKED" />
    );
  }
  if (formatted === null) {
    return <ValueStateFlag icon="missing" label={t('dashboards.figure.none')} state="MISSING" />;
  }
  return (
    <span className="figure" dir="ltr">
      {formatted}
    </span>
  );
}

function Figures({ figures }: { figures: WidgetFigure[] }): ReactElement | null {
  const { t } = useI18n();
  const shown = shownFigures(figures);
  if (shown.length === 0) {
    return null;
  }
  return (
    <dl className="widget__figures">
      {shown.map((figure) => (
        <div key={figure.measure} className="widget__figure">
          <dt>{measureLabel(figure.measure, t)}</dt>
          <dd>
            <FigureValue figure={figure} />
          </dd>
        </div>
      ))}
    </dl>
  );
}

const TONE_ICONS: Record<StatusTone, ReactElement> = {
  positive: <path d="m5 12.5 4.5 4.5L19 7.5" />,
  warning: <path d="M12 4 2.5 20h19zM12 10v4.5M12 17.5h.01" />,
  negative: <path d="M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18zM12 7.5V13M12 16.5h.01" />,
  info: <path d="M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18zM12 11v5.5M12 7.5h.01" />,
  neutral: <path d="M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18zM8 12h8" />,
};

/** A state's icon in its tone's colour; it only repeats the word beside it (WCAG 1.4.1). */
function ToneIcon({ tone }: { tone: StatusTone }): ReactElement {
  return (
    <svg
      className={`widget__icon widget__icon--${tone}`}
      viewBox="0 0 24 24"
      width="22"
      height="22"
      aria-hidden="true"
      focusable="false"
    >
      <g
        fill="none"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
      >
        {TONE_ICONS[tone]}
      </g>
    </svg>
  );
}

/** METRIC_CARD: a single state (a health, a lifecycle state, a condition) as the card's value, and its figures. */
export function MetricBody({ projectionCode, data }: BodyProps): ReactElement {
  const { t, language } = useI18n();
  const state =
    data.state === null ? null : labelOfValue(projectionCode, data.state, null, language, t);
  return (
    <div className="widget__body">
      {state !== null && (
        <p className="widget__state" data-tone={state.tone}>
          <ToneIcon tone={state.tone} />
          <span className="widget__state-text">{state.text}</span>
          {isOverridden(data.figures) && <OverriddenBadge />}
        </p>
      )}
      <Figures figures={data.figures} />
    </div>
  );
}

/**
 * STATUS_DISTRIBUTION, BAR_COLUMN and DONUT_PIE: a count by a source-owned value, drawn as a donut, columns or a bar
 * list (`chartForm`) beside a table of the same counts. The table is the text alternative: the legend of a donut, and
 * read only by assistive technology under columns and bars, which carry their own words. Hovering a
 * value lifts it in both. A source count of 0 is a count (F-13).
 */
export function DistributionBody({
  projectionCode,
  widgetType,
  data,
  title,
}: BodyProps): ReactElement {
  const { t, language } = useI18n();
  const [active, setActive] = useState<string | null>(null);
  const labels = data.distribution.map((bucket) =>
    labelOfValue(projectionCode, bucket.key, bucket.label, language, t),
  );
  const colors = bucketColors(
    labels.map((label) => label.tone),
    isStatusScale(projectionCode),
  );
  const buckets: ChartBucket[] = data.distribution.map((bucket, index) => ({
    key: bucket.key,
    count: bucket.count,
    text: labels[index]?.text ?? bucket.key,
    color: colors[index] ?? 'var(--chart-single)',
  }));
  const form = chartForm(widgetType, buckets.length);
  const lift = (key: string) => (active === null ? '' : active === key ? 'is-active' : 'is-dimmed');
  return (
    <div className="widget__body">
      {buckets.length > 0 && (
        <div className={`distribution distribution--${form}`}>
          {form === 'donut' && (
            <DonutChart
              buckets={buckets}
              active={active}
              onActive={setActive}
              totalLabel={t('dashboards.distribution.total')}
            />
          )}
          {form === 'columns' && (
            <ColumnChart buckets={buckets} active={active} onActive={setActive} />
          )}
          {form === 'bars' && <BarChart buckets={buckets} active={active} onActive={setActive} />}
          <table
            className={
              form === 'donut' ? 'widget__distribution distribution__donut' : 'visually-hidden'
            }
          >
            <caption className="visually-hidden">{title}</caption>
            <thead>
              <tr>
                <th scope="col">{t('dashboards.distribution.value')}</th>
                <th scope="col">{t(countedKey(projectionCode))}</th>
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
                    setActive(bucket.key);
                  }}
                  onPointerLeave={() => {
                    setActive(null);
                  }}
                >
                  <th scope="row">
                    <span className="distribution__key" aria-hidden="true" />
                    {bucket.text}
                  </th>
                  <td>
                    <span className="widget__count figure" dir="ltr">
                      {bucket.count}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <Figures figures={data.figures} />
      {data.distribution.length === 0 && shownFigures(data.figures).length === 0 && (
        <p className="widget__explanation">{t('dashboards.distribution.empty')}</p>
      )}
    </div>
  );
}

const SERIES_COLOR = (measure: string) =>
  measure === 'PLANNED_PERCENT' ? 'var(--series-planned)' : 'var(--series-actual)';

/**
 * PROGRESS_INDICATOR: source-owned percentages only (never an invented ratio), as concentric gauge rings — the first,
 * actual, outermost and named in the centre — with each figure listed beside its ring's key.
 */
export function ProgressBody({ data }: BodyProps): ReactElement {
  const { t } = useI18n();
  const percentages = shownFigures(data.figures).filter((figure) => figure.unit === 'PERCENT');
  const valueOf = (figure: WidgetFigure) =>
    figure.value === null || figure.isMasked ? null : Number(figure.value);
  const first = percentages[0];
  return (
    <div className="widget__body">
      <div className="progress-gauge">
        {percentages.some((figure) => valueOf(figure) !== null) && (
          <GaugeChart
            rings={percentages.map((figure) => ({
              key: figure.measure,
              value: valueOf(figure),
              color: SERIES_COLOR(figure.measure),
            }))}
            lead={first === undefined ? null : formatFigure(first, t)}
          />
        )}
        <dl className="widget__figures widget__figures--progress">
          {percentages.map((figure) => (
            <div key={figure.measure} className="widget__figure">
              <dt>
                <span
                  className="distribution__key"
                  aria-hidden="true"
                  style={{ '--chart-color': SERIES_COLOR(figure.measure) } as CSSProperties}
                />
                {measureLabel(figure.measure, t)}
              </dt>
              <dd>
                <FigureValue figure={figure} />
              </dd>
            </div>
          ))}
        </dl>
      </div>
      {isOverridden(data.figures) && (
        <p className="widget__state">
          <OverriddenBadge />
        </p>
      )}
    </div>
  );
}
