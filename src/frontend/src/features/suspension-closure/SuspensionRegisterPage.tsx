import { type ReactElement } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { SuspensionRegisterView } from './components/SuspensionRegisterView.tsx';
import { suspensionClosureProblemMessage } from './problems.ts';
import { useSuspensionPortfolio } from './useSuspensionClosureData.ts';

/**
 * SCR-108 Suspension Requests across the projects the person may see, each naming its project, most recently changed
 * first. Requests are raised from a project's Suspension tab (SCR-109 needs the project).
 */
export function SuspensionRegisterPage(): ReactElement {
  const { t } = useI18n();
  const portfolio = useSuspensionPortfolio();

  return (
    <>
      <PageHeader
        title={t('suspensionClosure.suspension.title')}
        description={t('suspensionClosure.suspension.description')}
      />
      {portfolio.data === undefined ? (
        portfolio.loading ? (
          <LoadingState />
        ) : (
          <ErrorState
            message={suspensionClosureProblemMessage(portfolio.error, t)}
            onRetry={portfolio.reload}
          />
        )
      ) : portfolio.data.length === 0 ? (
        <EmptyState title={t('suspensionClosure.suspension.emptyTitle')}>
          <p>{t('suspensionClosure.suspension.emptyAcross')}</p>
        </EmptyState>
      ) : (
        <SuspensionRegisterView
          entries={portfolio.data}
          showProject
          caption={t('suspensionClosure.suspension.caption')}
        />
      )}
    </>
  );
}
