import { type ReactElement } from 'react';

import {
  impactLabel,
  ratingAt,
  type RiskMatrix,
  severityStep,
} from '@/features/risks/riskRules.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';

import { fieldName } from './fields.ts';
import { RISK_HEATMAP_COLUMNS, RISK_HEATMAP_ROWS } from './heatmap.ts';

/**
 * A heatmap of risks by probability and impact on the dashboard (24 Nov 2026, as the risk methodology guide draws its matrix): each cell is its
 * rating's colour from the RISK_MATRIX in force with only its number of risks in it; probability rises up the rows, each named "(5) Likely", the
 * word "Probability" beside them; impact runs along the bottom, each named "(1) Negligible", the word "Impact" under them; the ratings' legend last.
 * A table, so each cell is read with its row and column, and its rating is said to a screen reader and on hover: colour is never the only signal.
 */
export function RiskMatrixVisual({
  matrix,
  caption,
  countAt,
}: {
  matrix: RiskMatrix;
  caption: string;
  countAt: (probabilityLevel: number, impactLevel: number) => number;
}): ReactElement {
  const { t, language } = useI18n();
  const probabilities = [...matrix.probabilityLevels].reverse();
  const levelName = (level: number, label: { ar: string; en: string } | null) =>
    label === null ? `(${String(level)})` : `(${String(level)}) ${label[language]}`;
  return (
    <figure className="risk-grid">
      <div className="risk-grid__body">
        <span className="risk-grid__axis risk-grid__axis--probability" aria-hidden="true">
          {fieldName(RISK_HEATMAP_ROWS, language)}
        </span>
        <table className="risk-grid__table">
          <caption className="visually-hidden">{caption}</caption>
          <tbody>
            {probabilities.map((probability) => (
              <tr key={probability.level}>
                <th scope="row" className="risk-grid__level">
                  {levelName(probability.level, probability.label)}
                </th>
                {matrix.impactLevels.map((impact) => {
                  const rating = ratingAt(matrix, probability.level, impact);
                  const step = rating === null ? null : severityStep(matrix, rating.code);
                  const count = countAt(probability.level, impact);
                  const ratingName =
                    rating === null ? t('risks.matrix.unmapped') : rating.label[language];
                  return (
                    <td
                      key={impact}
                      className={`risk-grid__cell risk-rating--${step === null ? 'none' : String(step)}`}
                      data-cell={`${String(probability.level)}:${String(impact)}`}
                      data-rating={rating?.code ?? ''}
                      title={`${ratingName} · ${t('risks.matrix.count', { count })}`}
                    >
                      <span className="visually-hidden">{ratingName}: </span>
                      {count}
                    </td>
                  );
                })}
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr>
              <td />
              {matrix.impactLevels.map((impact) => (
                <th key={impact} scope="col" className="risk-grid__level risk-grid__level--impact">
                  {levelName(impact, impactLabel(matrix, impact))}
                </th>
              ))}
            </tr>
          </tfoot>
        </table>
      </div>
      <p className="risk-grid__axis risk-grid__axis--impact" aria-hidden="true">
        {fieldName(RISK_HEATMAP_COLUMNS, language)}
      </p>
      <ol className="risk-grid__legend" aria-label={t('risks.matrix.legend')}>
        {matrix.ratings.map((rating) => (
          <li
            key={rating.code}
            className={`badge risk-rating risk-rating--${String(severityStep(matrix, rating.code))}`}
          >
            {rating.label[language]}
          </li>
        ))}
      </ol>
    </figure>
  );
}
