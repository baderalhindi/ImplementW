import { type ReactElement } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { CloseoutRegisterView } from './components/CloseoutRegisterView.tsx';
import { suspensionClosureProblemMessage } from './problems.ts';
import { useCloseoutPortfolio } from './useSuspensionClosureData.ts';

/**
 * SCR-111 Closure Requests across the projects the person may see: completion and closure cases, each naming its stage
 * and project, most recently changed first. Cases are raised from a project's Closeout tab (SCR-112 needs the project).
 */
export function CloseoutRegisterPage(): ReactElement {
  const { t } = useI18n();
  const portfolio = useCloseoutPortfolio();

  return (
    <>
      <PageHeader
        title={t('suspensionClosure.closeout.title')}
        description={t('suspensionClosure.closeout.description')}
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
        <EmptyState title={t('suspensionClosure.closeout.emptyTitle')}>
          <p>{t('suspensionClosure.closeout.emptyAcross')}</p>
        </EmptyState>
      ) : (
        <CloseoutRegisterView
          entries={portfolio.data}
          showProject
          caption={t('suspensionClosure.closeout.caption')}
        />
      )}
    </>
  );
}
