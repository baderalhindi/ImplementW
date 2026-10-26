import { type ReactElement } from 'react';

import { Detail } from '@/features/projects/components/Detail.tsx';
import { useI18n } from '@/shared/i18n/i18n.ts';

import { type FinancialProgressUpdateDetail } from '../api/types.ts';
import { figureOf } from '../financialKpiRules.ts';

import { AmountFigure, ProvenanceLine, ValueStateFlag } from './Figures.tsx';

/**
 * One revision's figures as a `.details` list: what is known of the period (measured, or why not), the two figures,
 * their provenance and the narrative.
 */
export function UpdateFigures({
  update,
  personName,
}: {
  update: FinancialProgressUpdateDetail;
  personName: (id: string | null) => string;
}): ReactElement {
  const { t } = useI18n();
  return (
    <dl className="details">
      <Detail term={t('financialKpi.figures.valueStatus')}>
        {update.valueStatus === 'MEASURED' ? (
          t('financialKpi.value.MEASURED')
        ) : (
          <ValueStateFlag status={update.valueStatus} />
        )}
      </Detail>
      <Detail term={t('financialKpi.figures.actual')}>
        <AmountFigure
          state={figureOf(update, 'actualExpenditureToDateSar', update.valueStatus)}
          absent="financialKpi.value.MISSING"
        />
      </Detail>
      <Detail term={t('financialKpi.figures.forecast')}>
        <AmountFigure
          state={figureOf(update, 'forecastAtCompletionSar', update.valueStatus)}
          absent="financialKpi.value.noForecast"
        />
      </Detail>
      <Detail term={t('financialKpi.figures.source')}>
        <ProvenanceLine
          provenance={update}
          maskedFields={update.maskedFields}
          personName={personName}
        />
      </Detail>
      {update.narrative !== null && update.narrative !== undefined && (
        <Detail term={t('financialKpi.update.narrative')}>
          <span dir="auto">{update.narrative.text}</span>
        </Detail>
      )}
    </dl>
  );
}
