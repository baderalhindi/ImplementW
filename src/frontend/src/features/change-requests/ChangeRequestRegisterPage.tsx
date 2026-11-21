import { useRowActions } from '@/features/identity-access/components/RowActions.tsx';
import { type ReactElement } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { ChangeRequestRegisterView } from './components/ChangeRequestRegisterView.tsx';
import { changeRequestProblemMessage } from './problems.ts';
import { useChangeRequestPortfolio, type ChangeRequestEntry } from './useChangeRequestData.ts';
import { editChangeRequestPath } from './paths.ts';
import { changeRequestsApi } from './api/changeRequestsApi.ts';

/**
 * SCR-105 Change Requests across the projects the person may see, each naming its project, most recently changed first.
 * Requests are raised from a project's workspace (SCR-106 needs the project).
 */
export function ChangeRequestRegisterPage(): ReactElement {
  const { t } = useI18n();
  const portfolio = useChangeRequestPortfolio();
  const rowActions = useRowActions<ChangeRequestEntry>({
    permission: 'CHANGE_REQUEST_RAISE',
    name: ({ request }) => request.title.text,
    // Only a draft or a returned request is edited; the others are decided on their page.
    editTo: ({ request }) =>
      request.status === 'DRAFT' || request.status === 'RETURNED'
        ? editChangeRequestPath(request.id)
        : null,
    remove: ({ request }) => changeRequestsApi.remove(request.id, null),
    onDeleted: portfolio.reload,
  });

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
            error={portfolio.error}
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
          actions={rowActions}
          entries={portfolio.data}
          showProject
          caption={t('changeRequests.list.caption')}
        />
      )}
    </>
  );
}
