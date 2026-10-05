import { type ReactElement, useId } from 'react';

import { SemanticStateBadge } from '@/features/progress/components/ProgressBadges.tsx';
import { Detail } from '@/features/projects/components/Detail.tsx';
import { useI18n } from '@/shared/i18n/i18n.ts';

import {
  type FinancialCommitmentDetail,
  type FinancialProgressUpdateDetail,
} from '../api/types.ts';
import { figureOf } from '../financialKpiRules.ts';
import { type FinancialOverview } from '../useFinancialData.ts';

import { FinancialStatusBadge, UpdateStatusBadge } from './Badges.tsx';
import { AmountFigure, FigureWithSource, ProvenanceLine } from './Figures.tsx';

type PersonName = (id: string | null) => string;

/** The Approved Budget's provenance is its version's (ADR-008 extended); a figure with no version has none to show. */
function BudgetSource({
  commitment,
  personName,
}: {
  commitment: FinancialCommitmentDetail | undefined;
  personName: PersonName;
}): ReactElement | null {
  return commitment === undefined ? null : (
    <ProvenanceLine
      provenance={commitment}
      maskedFields={commitment.maskedFields}
      personName={personName}
    />
  );
}

function UpdateSource({
  update,
  personName,
}: {
  update: FinancialProgressUpdateDetail | undefined;
  personName: PersonName;
}): ReactElement | null {
  const { t } = useI18n();
  return update === undefined ? (
    <span className="details__aside">{t('financialKpi.provenance.noUpdate')}</span>
  ) : (
    <ProvenanceLine
      provenance={update}
      maskedFields={update.maskedFields}
      personName={personName}
    />
  );
}

/**
 * SCR-049's two views of the project's finances, never merged (M-12, TASK-052 D-9): the latest PUBLISHED/OFFICIAL
 * snapshot, and the CURRENT/LIVE position computed now. Each badged, each figure with its source and as-of date, and
 * each Unknown figure in words (acceptance criterion 1). The commitments field is not shown (gate decision).
 */
export function FinancialStates({
  overview,
  periodLabel,
  personName,
}: {
  overview: FinancialOverview;
  periodLabel: (reportingCycleId: string) => string;
  personName: PersonName;
}): ReactElement {
  const { t, formatDateTime } = useI18n();
  const officialId = useId();
  const liveId = useId();
  const { official, live } = overview;
  const commitment = (id: string | null) =>
    overview.commitments.find((candidate) => candidate.id === id);
  const update = (id: string | null) => overview.updates.find((candidate) => candidate.id === id);

  return (
    <div className="columns">
      <section className="progress-state progress-state--official" aria-labelledby={officialId}>
        <h2 id={officialId} className="progress-state__title">
          <SemanticStateBadge state="official" />
          <span>{t('financialKpi.financials.official.title')}</span>
        </h2>
        {official === null ? (
          <p className="form__note">{t('financialKpi.financials.official.none')}</p>
        ) : (
          <dl className="details">
            <Detail term={t('financialKpi.figures.financialStatus')}>
              <FinancialStatusBadge status={official.financialStatus} />
            </Detail>
            <Detail term={t('financialKpi.figures.approvedBudget')}>
              <FigureWithSource
                figure={
                  <AmountFigure
                    state={figureOf(official, 'approvedBudgetSar', null)}
                    absent="financialKpi.value.noBudget"
                  />
                }
                provenance={
                  <BudgetSource
                    commitment={commitment(official.financialCommitmentId)}
                    personName={personName}
                  />
                }
              />
            </Detail>
            <Detail term={t('financialKpi.figures.actual')}>
              <FigureWithSource
                figure={
                  <AmountFigure
                    state={figureOf(official, 'actualExpenditureToDateSar', official.valueStatus)}
                    absent="financialKpi.value.MISSING"
                  />
                }
                provenance={
                  <ProvenanceLine
                    provenance={official}
                    maskedFields={official.maskedFields}
                    personName={personName}
                  />
                }
              />
            </Detail>
            <Detail term={t('financialKpi.figures.forecast')}>
              <FigureWithSource
                figure={
                  <AmountFigure
                    state={figureOf(official, 'forecastAtCompletionSar', official.valueStatus)}
                    absent="financialKpi.value.noForecast"
                  />
                }
                provenance={
                  <ProvenanceLine
                    provenance={official}
                    maskedFields={official.maskedFields}
                    personName={personName}
                  />
                }
              />
            </Detail>
            <Detail term={t('financialKpi.figures.period')}>
              <span dir="ltr">{periodLabel(official.reportingCycleId)}</span>
            </Detail>
            <Detail term={t('financialKpi.financials.official.publishedAt')}>
              {`${formatDateTime(official.publishedAt)} · ${personName(official.publishedByUserId)}`}
            </Detail>
          </dl>
        )}
      </section>

      <section className="progress-state progress-state--live" aria-labelledby={liveId}>
        <h2 id={liveId} className="progress-state__title">
          <SemanticStateBadge state="live" />
          <span>{t('financialKpi.financials.live.title')}</span>
        </h2>
        {live === null ? (
          <p className="form__note">{t('financialKpi.financials.live.none')}</p>
        ) : (
          <dl className="details">
            <Detail term={t('financialKpi.figures.financialStatus')}>
              <FinancialStatusBadge status={live.financialStatus} />
            </Detail>
            <Detail term={t('financialKpi.figures.approvedBudget')}>
              <FigureWithSource
                figure={
                  <AmountFigure
                    state={figureOf(live, 'approvedBudgetSar', null)}
                    absent="financialKpi.value.noBudget"
                  />
                }
                provenance={
                  <BudgetSource
                    commitment={commitment(live.financialCommitmentId)}
                    personName={personName}
                  />
                }
              />
            </Detail>
            <Detail term={t('financialKpi.figures.actual')}>
              <FigureWithSource
                figure={
                  <AmountFigure
                    state={figureOf(live, 'actualExpenditureToDateSar', live.valueStatus)}
                    absent="financialKpi.value.MISSING"
                  />
                }
                provenance={
                  <UpdateSource
                    update={update(live.financialProgressUpdateId)}
                    personName={personName}
                  />
                }
              />
            </Detail>
            <Detail term={t('financialKpi.figures.forecast')}>
              <FigureWithSource
                figure={
                  <AmountFigure
                    state={figureOf(live, 'forecastAtCompletionSar', live.valueStatus)}
                    absent="financialKpi.value.noForecast"
                  />
                }
                provenance={
                  <UpdateSource
                    update={update(live.financialProgressUpdateId)}
                    personName={personName}
                  />
                }
              />
            </Detail>
            {live.updateStatus !== null && (
              <Detail term={t('financialKpi.financials.live.updateStatus')}>
                <span className="figure-group">
                  <UpdateStatusBadge status={live.updateStatus} />
                  {live.updateStatus !== 'PUBLISHED' && (
                    <span className="details__aside">
                      {t('financialKpi.financials.live.unpublished')}
                    </span>
                  )}
                </span>
              </Detail>
            )}
            <Detail term={t('financialKpi.financials.live.computedAt')}>
              {formatDateTime(live.computedAt)}
            </Detail>
          </dl>
        )}
      </section>
    </div>
  );
}
