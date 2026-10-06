import { type ReactElement, useId } from 'react';

import { type ApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { riskProblemMessage } from '../problems.ts';
import { ratingAt, type RiskMatrix, severityStep } from '../riskRules.ts';
import { type MatrixState } from '../useRiskData.ts';

interface RiskHeatMapProps {
  matrix: RiskMatrix;
  caption: string;
  /** Risks per cell (SCR-081's exposure); omitted when the map shows one risk. */
  countAt?: (probabilityLevel: number, impactLevel: number) => number;
  /** The cell to mark: a risk's levels (SCR-082) or the levels being chosen (MOD-032). */
  marked?: { probabilityLevel: number; impactLevel: number } | null;
  markedLabel?: string;
}

/**
 * WF-06 §8.7's Risk Matrix: the RISK_MATRIX version in force drawn as a heat-map, probability rising up the rows and
 * overall impact along the columns. Every axis, label and cell is the version's: a version with other levels or another
 * mapping draws another map (acceptance criterion 1). Each cell says its rating in words, and a marked cell says so,
 * so colour is never the only signal (WCAG 1.4.1, FE-AX-003); being a table, each cell is read with its row and column.
 */
export function RiskHeatMap({
  matrix,
  caption,
  countAt,
  marked = null,
  markedLabel,
}: RiskHeatMapProps): ReactElement {
  const { t, language } = useI18n();
  const legendId = useId();
  const captionId = useId();
  const probabilities = [...matrix.probabilityLevels].reverse();

  return (
    <figure className="risk-matrix">
      {/* A narrow screen or dialog scrolls the grid, not the page: the region is focusable and named, as TableContainer. */}
      <div className="risk-matrix__scroll" role="region" aria-labelledby={captionId} tabIndex={0}>
        <table className="risk-matrix__grid" aria-describedby={legendId}>
          <caption id={captionId} className="risk-matrix__caption">
            {caption}
          </caption>
          <thead>
            <tr>
              <th scope="col" className="risk-matrix__corner">
                {t('risks.matrix.axes')}
              </th>
              {matrix.impactLevels.map((level) => (
                <th key={level} scope="col">
                  {t('risks.matrix.impactLevel', { level })}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {probabilities.map((probability) => (
              <tr key={probability.level}>
                <th scope="row">
                  {t('risks.matrix.probabilityLevel', {
                    level: probability.level,
                    label: probability.label[language],
                  })}
                </th>
                {matrix.impactLevels.map((impactLevel) => {
                  const rating = ratingAt(matrix, probability.level, impactLevel);
                  const step = rating === null ? null : severityStep(matrix, rating.code);
                  const count = countAt?.(probability.level, impactLevel);
                  const isMarked =
                    marked !== null &&
                    marked.probabilityLevel === probability.level &&
                    marked.impactLevel === impactLevel;
                  return (
                    <td
                      key={impactLevel}
                      className={[
                        'risk-matrix__cell',
                        `risk-rating--${step === null ? 'none' : String(step)}`,
                        isMarked ? 'risk-matrix__cell--marked' : '',
                      ].join(' ')}
                      data-cell={`${String(probability.level)}:${String(impactLevel)}`}
                      data-rating={rating?.code ?? ''}
                    >
                      <span className="risk-matrix__rating">
                        {rating === null ? t('risks.matrix.unmapped') : rating.label[language]}
                      </span>
                      {count !== undefined && (
                        <span className="risk-matrix__count">
                          {t('risks.matrix.count', { count })}
                        </span>
                      )}
                      {isMarked && markedLabel !== undefined && (
                        <span className="risk-matrix__marker">{markedLabel}</span>
                      )}
                    </td>
                  );
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <figcaption id={legendId} className="risk-matrix__legend">
        <span>
          {t('risks.matrix.version', {
            version: matrix.versionNo,
            date: matrix.effectiveFrom.slice(0, 10),
          })}
        </span>
        <span>{t('risks.matrix.legend')}</span>
        <ol className="risk-matrix__ratings">
          {matrix.ratings.map((rating) => (
            <li
              key={rating.code}
              className={`badge risk-rating risk-rating--${String(severityStep(matrix, rating.code))}`}
            >
              {rating.label[language]}
            </li>
          ))}
        </ol>
      </figcaption>
    </figure>
  );
}

/**
 * The matrix's place on a screen while it is read, or why it cannot be drawn: never a default grid in its stead
 * (acceptance criterion 1; TASK-055 F-4 — no version is published yet).
 */
export function MatrixUnavailable({
  state,
}: {
  state: ApiResource<MatrixState>;
}): ReactElement | null {
  const { t } = useI18n();
  if (state.data === undefined) {
    return state.loading ? (
      <LoadingState />
    ) : (
      <ErrorState message={riskProblemMessage(state.error, t)} onRetry={state.reload} />
    );
  }
  switch (state.data.kind) {
    case 'ready':
      return null;
    case 'forbidden':
      return <p className="state">{t('risks.matrix.forbidden')}</p>;
    case 'missing':
      return <p className="state">{t('risks.matrix.missing')}</p>;
  }
}
