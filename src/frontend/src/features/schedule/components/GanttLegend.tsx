import { type ReactElement } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';

import { type ProjectBaselineDetail } from '../api/types.ts';
import { baselineName } from '../presentation.ts';

/**
 * What each mark on the Gantt means. The Approved Baseline and the Current Forecast differ in shape, fill and position
 * as well as colour (WCAG 1.4.1): the forecast is a solid bar, the baseline a thinner hatched bar beneath it.
 */
export function GanttLegend({
  active,
  conflicts,
}: {
  active: ProjectBaselineDetail | null;
  /** Whether any row is marked as starting before its predecessors allow. */
  conflicts: boolean;
}): ReactElement {
  const { t } = useI18n();
  return (
    <section className="gantt-legend" aria-labelledby="gantt-legend-title">
      <h3 id="gantt-legend-title" className="gantt-legend__title">
        {t('schedule.legend.title')}
      </h3>
      <ul className="gantt-legend__items">
        <li>
          <span className="gantt-legend__swatch gantt__bar--forecast" aria-hidden="true" />
          <span>
            <strong>{t('schedule.legend.forecast')}</strong>
            <span className="cell__aside">
              {active === null
                ? t('schedule.legend.forecastFollowsPlan')
                : t('schedule.legend.forecastDetail')}
            </span>
          </span>
        </li>
        <li>
          <span className="gantt-legend__swatch gantt__bar--baseline" aria-hidden="true" />
          <span>
            <strong>{t('schedule.legend.baseline')}</strong>
            <span className="cell__aside">
              {active === null ? t('schedule.legend.baselineNone') : baselineName(active, t)}
            </span>
          </span>
        </li>
        <li>
          <span className="gantt-legend__swatch gantt__bar--summary" aria-hidden="true" />
          <span>
            <strong>{t('schedule.legend.summary')}</strong>
            <span className="cell__aside">{t('schedule.legend.summaryDetail')}</span>
          </span>
        </li>
        {conflicts && (
          <li>
            <span
              className="gantt-legend__swatch gantt__bar--forecast gantt__bar--conflict"
              aria-hidden="true"
            />
            <span>
              <strong>{t('schedule.legend.conflict')}</strong>
              <span className="cell__aside">{t('schedule.legend.conflictDetail')}</span>
            </span>
          </li>
        )}
      </ul>
    </section>
  );
}
