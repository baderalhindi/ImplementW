import { type ReactElement } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { RiskRegisterView } from './components/RiskRegisterView.tsx';
import { isForbidden, riskProblemMessage } from './problems.ts';
import { matrixOf, useRiskLookups, useRiskMatrix, useRiskPortfolio } from './useRiskData.ts';

/**
 * SCR-080 Risk Register across the projects the person may see: the same summary, filters and table as a project's
 * register, each risk naming its project. Read one project at a time (TASK-055 F-9). Risks are registered from a
 * project's own register, where its Project Manager is.
 */
export function RiskRegisterPage(): ReactElement {
  const { t } = useI18n();
  const portfolio = useRiskPortfolio();
  const matrixState = useRiskMatrix();
  const lookups = useRiskLookups();

  const header = (
    <PageHeader title={t('risks.register.title')} description={t('risks.register.description')} />
  );
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

  const { entries } = portfolio.data;
  return (
    <>
      {header}
      {entries.length === 0 ? (
        <EmptyState title={t('risks.empty.register.title')}>
          <p>{t('risks.empty.register.body')}</p>
        </EmptyState>
      ) : (
        <RiskRegisterView
          entries={entries}
          matrix={matrixOf(matrixState)}
          lookups={lookups}
          showProject
          caption={t('risks.register.caption')}
        />
      )}
    </>
  );
}
