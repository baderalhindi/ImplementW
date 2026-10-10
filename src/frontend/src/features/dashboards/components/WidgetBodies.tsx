import { type ReactElement } from 'react';

import { OverriddenBadge } from '@/features/progress/components/ProgressBadges.tsx';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';
import { ValueStateFlag } from '@/shared/ui/ValueState.tsx';

import { type WidgetData, type WidgetFigure } from '../api/types.ts';
import {
  countedKey,
  formatFigure,
  isOverridden,
  labelOfValue,
  measureLabel,
  shownFigures,
} from '../presentation.ts';

// The allowlisted visualisations of FG-01 §10 (TASK-069 `DashboardWidgetType`). Each presents the source's values as
// the source states them; none computes one. A figure with no value says why — "Restricted" when it is withheld
// from the caller's audience (ADR-010), "No data" when the source holds none — and is never drawn as 0.

interface BodyProps {
  projectionCode: string;
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

/** METRIC_CARD: a single state (a health, a lifecycle state, a condition) and its figures. */
export function MetricBody({ projectionCode, data }: BodyProps): ReactElement {
  const { t, language } = useI18n();
  const state =
    data.state === null ? null : labelOfValue(projectionCode, data.state, null, language, t);
  return (
    <div className="widget__body">
      {state !== null && (
        <p className="widget__state">
          <StatusBadge label={state.text} tone={state.tone} />
          {isOverridden(data.figures) && <OverriddenBadge />}
        </p>
      )}
      <Figures figures={data.figures} />
    </div>
  );
}

/**
 * STATUS_DISTRIBUTION, BAR_COLUMN and DONUT_PIE: a count by a source-owned value, as a table whose bars only repeat
 * the counts (the table is the chart's text alternative and the chart). A source count of 0 is a count (F-13).
 */
export function DistributionBody({ projectionCode, data, title }: BodyProps): ReactElement {
  const { t, language } = useI18n();
  const largest = Math.max(1, ...data.distribution.map((bucket) => bucket.count));
  return (
    <div className="widget__body">
      {data.distribution.length > 0 && (
        <table className="widget__distribution">
          <caption className="visually-hidden">{title}</caption>
          <thead>
            <tr>
              <th scope="col">{t('dashboards.distribution.value')}</th>
              <th scope="col">{t(countedKey(projectionCode))}</th>
            </tr>
          </thead>
          <tbody>
            {data.distribution.map((bucket) => {
              const label = labelOfValue(projectionCode, bucket.key, bucket.label, language, t);
              return (
                <tr key={bucket.key} data-bucket={bucket.key}>
                  <th scope="row">
                    <StatusBadge label={label.text} tone={label.tone} />
                  </th>
                  <td>
                    <span className="widget__count figure" dir="ltr">
                      {bucket.count}
                    </span>
                    <span className="widget__bar-track" aria-hidden="true">
                      <span
                        className={`widget__bar widget__bar--${label.tone}`}
                        style={{ inlineSize: `${String((bucket.count / largest) * 100)}%` }}
                      />
                    </span>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      )}
      <Figures figures={data.figures} />
      {data.distribution.length === 0 && shownFigures(data.figures).length === 0 && (
        <p className="widget__explanation">{t('dashboards.distribution.empty')}</p>
      )}
    </div>
  );
}

/** PROGRESS_INDICATOR: source-owned percentages only, each with a bar that repeats it (never an invented ratio). */
export function ProgressBody({ data }: BodyProps): ReactElement {
  const { t } = useI18n();
  const percentages = shownFigures(data.figures).filter((figure) => figure.unit === 'PERCENT');
  return (
    <div className="widget__body">
      <dl className="widget__figures">
        {percentages.map((figure) => {
          const value = figure.value === null || figure.isMasked ? null : Number(figure.value);
          return (
            <div key={figure.measure} className="widget__figure">
              <dt>{measureLabel(figure.measure, t)}</dt>
              <dd>
                <FigureValue figure={figure} />
                {value !== null && (
                  <span className="widget__bar-track" aria-hidden="true">
                    <span
                      className={`widget__bar widget__bar--${figure.measure === 'PLANNED_PERCENT' ? 'planned' : 'actual'}`}
                      style={{ inlineSize: `${String(Math.min(100, Math.max(0, value)))}%` }}
                    />
                  </span>
                )}
              </dd>
            </div>
          );
        })}
      </dl>
      {isOverridden(data.figures) && (
        <p className="widget__state">
          <OverriddenBadge />
        </p>
      )}
    </div>
  );
}
