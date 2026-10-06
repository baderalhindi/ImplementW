import { type ReactElement } from 'react';

import { useSession } from '@/features/identity-access/session/useSession.ts';
import { isForbidden } from '@/features/risks/problems.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { type ConcernType } from './api/types.ts';
import { ConcernRegisterView } from './components/ConcernRegisterView.tsx';
import { concernProblemMessage } from './problems.ts';
import { useConcernLookups, useConcernPortfolio } from './useConcernData.ts';

const PAGE_TEXT = {
  ISSUE: {
    title: 'issuesChallenges.issues.title',
    description: 'issuesChallenges.issues.description',
    caption: 'issuesChallenges.issues.caption',
    emptyTitle: 'issuesChallenges.issues.emptyTitle',
    emptyBody: 'issuesChallenges.issues.emptyAcross',
  },
  CHALLENGE: {
    title: 'issuesChallenges.challenges.title',
    description: 'issuesChallenges.challenges.description',
    caption: 'issuesChallenges.challenges.caption',
    emptyTitle: 'issuesChallenges.challenges.emptyTitle',
    emptyBody: 'issuesChallenges.challenges.emptyAcross',
  },
} as const;

/**
 * SCR-083 Issue Register or SCR-085 Challenge Register across the projects the person may see: the same summary,
 * filters and table as a project's register, each concern naming its project. Read one project at a time (the API
 * requires `projectId`). Concerns are raised from a project's own register. Under RequireSession, so a session is held.
 */
export function ConcernRegisterPage({ concernType }: { concernType: ConcernType }): ReactElement {
  const { t } = useI18n();
  const { session } = useSession();
  const portfolio = useConcernPortfolio(concernType);
  const lookups = useConcernLookups();
  const text = PAGE_TEXT[concernType];

  const header = <PageHeader title={t(text.title)} description={t(text.description)} />;
  if (portfolio.data === undefined) {
    return (
      <>
        {header}
        {portfolio.loading ? (
          <LoadingState />
        ) : isForbidden(portfolio.error) ? (
          <p className="state">{t('issuesChallenges.forbidden')}</p>
        ) : (
          <ErrorState
            message={concernProblemMessage(portfolio.error, t)}
            onRetry={portfolio.reload}
          />
        )}
      </>
    );
  }

  return (
    <>
      {header}
      {portfolio.data.length === 0 ? (
        <EmptyState title={t(text.emptyTitle)}>
          <p>{t(text.emptyBody)}</p>
        </EmptyState>
      ) : (
        <ConcernRegisterView
          entries={portfolio.data}
          lookups={lookups}
          userId={session?.user.id ?? ''}
          showProject
          caption={t(text.caption)}
        />
      )}
    </>
  );
}
