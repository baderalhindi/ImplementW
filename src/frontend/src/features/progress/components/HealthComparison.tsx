import { type ReactElement, useId } from 'react';

import { Detail } from '@/features/projects/components/Detail.tsx';
import { useI18n } from '@/shared/i18n/i18n.ts';

import { type HealthView } from '../useHealthView.ts';

import { FigureDetails, type IntakeDate } from './Figures.tsx';
import { HealthBadge, SemanticStateBadge } from './ProgressBadges.tsx';

/**
 * The official value first, as the read side renders it, and the live one beside it (progress-update.md §6 item 3).
 * Each carries its own badge and its freshness: when it was published, or when it was computed (F-10).
 */
export function HealthComparison({
  view,
  intakeDate,
}: {
  view: HealthView;
  intakeDate: IntakeDate;
}): ReactElement {
  const { t, formatDateTime } = useI18n();
  const officialId = useId();
  const liveId = useId();
  return (
    <div className="columns progress-states">
      <section className="progress-state progress-state--official" aria-labelledby={officialId}>
        <h2 id={officialId} className="progress-state__title">
          <SemanticStateBadge state="official" />
          <span>{t('progress.official.title')}</span>
        </h2>
        {view.official === null ? (
          <p className="form__note">{t('progress.official.none')}</p>
        ) : (
          <dl className="details">
            <Detail term={t('progress.figures.overallHealth')}>
              <HealthBadge health={view.official.overallHealth} />
            </Detail>
            <FigureDetails
              figures={{
                plannedPercent: view.official.plannedPercent,
                actualPercent: view.official.actualPercent,
                isOverridden: view.official.isOverridden,
              }}
              intakeDate={intakeDate}
            />
            <Detail term={t('progress.official.publishedAt')}>
              {formatDateTime(view.official.publishedAt)}
            </Detail>
          </dl>
        )}
      </section>
      <section className="progress-state progress-state--live" aria-labelledby={liveId}>
        <h2 id={liveId} className="progress-state__title">
          <SemanticStateBadge state="live" />
          <span>{t('progress.live.title')}</span>
        </h2>
        {view.live === null ? (
          <p className="form__note">{t('progress.live.none')}</p>
        ) : (
          <dl className="details">
            <Detail term={t('progress.figures.overallHealth')}>
              <HealthBadge health={view.live.overallHealth} />
            </Detail>
            <FigureDetails
              figures={{
                plannedPercent: view.live.plannedPercent,
                actualPercent: view.live.actualPercent,
                // The live value is computed from the derived figures, never from an unpublished override (D-3).
                isOverridden: false,
              }}
              intakeDate={intakeDate}
            />
            <Detail term={t('progress.live.computedAt')}>
              {formatDateTime(view.live.computedAt)}
            </Detail>
          </dl>
        )}
      </section>
    </div>
  );
}
