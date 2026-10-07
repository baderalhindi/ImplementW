import { type ReactElement } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { ChangeRequestRegisterView } from './components/ChangeRequestRegisterView.tsx';
import { changeRequestProblemMessage } from './problems.ts';
import { useChangeRequestPortfolio } from './useChangeRequestData.ts';

/**
 * SCR-105 Change Requests across the projects the person may see, each naming its project, most recently changed first.
 * Requests are raised from a project's workspace (SCR-106 needs the project).
 */
export function ChangeRequestRegisterPage(): ReactElement {
  const { t } = useI18n();
  const portfolio = useChangeRequestPortfolio();

  return (
    <>
      <PageHeader
        title={t('changeRequests.list.title')}
        description={t('changeRequests.list.description')}
      />
      {portfolio.data === undefined ? (
        portfolio.loading ? (
          <LoadingState />
        ) : (
          <ErrorState
            message={changeRequestProblemMessage(portfolio.error, t)}
            onRetry={portfolio.reload}
          />
        )
      ) : portfolio.data.length === 0 ? (
        <EmptyState title={t('changeRequests.list.emptyTitle')}>
          <p>{t('changeRequests.list.emptyAcross')}</p>
        </EmptyState>
      ) : (
        <ChangeRequestRegisterView
          entries={portfolio.data}
          showProject
          caption={t('changeRequests.list.caption')}
        />
      )}
    </>
  );
}
