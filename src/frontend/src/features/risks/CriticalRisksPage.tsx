import { type ReactElement } from 'react';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { todayUtc } from '@/features/tasks/taskRules.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { MatrixUnavailable, RiskHeatMap } from './components/RiskHeatMap.tsx';
import { RiskTable } from './components/RiskTable.tsx';
import { isForbidden, riskProblemMessage } from './problems.ts';
import {
  byExposure,
  cellCount,
  criticalRating,
  isCritical,
  type RiskMatrix,
  riskSummary,
} from './riskRules.ts';
import {
  type RiskLookups,
  type RiskPortfolio,
  useRiskLookups,
  useRiskMatrix,
  useRiskPortfolio,
} from './useRiskData.ts';

/**
 * SCR-081 Critical Risks: the exposure of every open, assessed risk the person may see on the RISK_MATRIX version in
 * force, and the risks whose rating is that version's most severe (D-4: no threshold is configured yet, TBC-RSK-005).
 * Which ratings are critical is the published matrix's, so without a readable, published matrix the screen says it
 * cannot tell, and lists nothing as critical.
 */
export function CriticalRisksPage(): ReactElement {
  const { t } = useI18n();
  const portfolio = useRiskPortfolio();
  const matrixState = useRiskMatrix();
  const lookups = useRiskLookups();

  const header = (
    <PageHeader title={t('risks.critical.title')} description={t('risks.critical.description')} />
  );
  if (matrixState.data?.kind !== 'ready') {
    return (
      <>
        {header}
        <MatrixUnavailable state={matrixState} />
        {matrixState.data !== undefined && (
          <p className="form__note">{t('risks.critical.needsMatrix')}</p>
        )}
      </>
    );
  }
  if (portfolio.data === undefined) {
    return (
      <>
        {header}
        {portfolio.loading ? (
          <LoadingState />
        ) : isForbidden(portfolio.error) ? (
          <p className="state">{t('risks.forbidden')}</p>
        ) : (
          <ErrorState message={riskProblemMessage(portfolio.error, t)} onRetry={portfolio.reload} />
        )}
      </>
    );
  }
  return (
    <>
      {header}
      <CriticalBody portfolio={portfolio.data} matrix={matrixState.data.matrix} lookups={lookups} />
    </>
  );
}

function CriticalBody({
  portfolio,
  matrix,
  lookups,
}: {
  portfolio: RiskPortfolio;
  matrix: RiskMatrix;
  lookups: RiskLookups;
}): ReactElement {
  const { t, language } = useI18n();
  const today = todayUtc();
  const { entries } = portfolio;
  const summary = riskSummary(
    entries.map((entry) => entry.risk),
    matrix,
    today,
  );
  const critical = entries
    .filter((entry) => isCritical(entry.risk, matrix))
    .sort(byExposure(matrix));
  const personName = usePersonNames(critical.map((entry) => entry.risk.ownerUserId));
  const rating = criticalRating(matrix);

  return (
    <>
      <section className="section" aria-labelledby="risk-exposure">
        <h2 id="risk-exposure">{t('risks.critical.exposureTitle')}</h2>
        <p className="form__note">{t('risks.critical.exposureNote')}</p>
        <RiskHeatMap
          matrix={matrix}
          caption={t('risks.critical.matrixCaption')}
          countAt={(probability, impact) => cellCount(summary, probability, impact)}
        />
      </section>
      <section className="section" aria-labelledby="risk-critical">
        <h2 id="risk-critical">
          {t('risks.critical.listTitle', { count: summary.critical ?? 0 })}
        </h2>
        <p className="form__note">
          {rating === null
            ? t('risks.critical.noRatings')
            : t('risks.critical.criterion', {
                rating: rating.label[language],
                version: matrix.versionNo,
              })}
        </p>
        {critical.length === 0 ? (
          <EmptyState title={t('risks.empty.critical')} />
        ) : (
          <RiskTable
            caption={t('risks.critical.caption')}
            entries={critical}
            today={today}
            showProject
            matrix={matrix}
            categoryLabel={lookups.itemLabel}
            personName={personName}
          />
        )}
      </section>
    </>
  );
}
