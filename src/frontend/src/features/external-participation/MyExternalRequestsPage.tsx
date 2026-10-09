import { type ReactElement } from 'react';
import { Link } from 'react-router';

import { useSession } from '@/features/identity-access/session/useSession.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { RequestRegisterView } from './components/RequestRegisterView.tsx';
import { MY_CONTRIBUTIONS_PATH } from './paths.ts';
import { participationProblemMessage } from './problems.ts';
import { useRequestList } from './useExternalParticipationData.ts';

/**
 * SCR-163 My External Requests: an entity user's inbox of AHDA's requests to their entity — those they answer, or every
 * one their entity holds on the projects they are assigned to — with status and due condition. The API lists only
 * those (ENTITY isolation, TASK-066 D-7) and never a draft; nothing else is counted. There is no "new request": AHDA
 * originates every request (gate decision).
 */
export function MyExternalRequestsPage(): ReactElement | null {
  const { t } = useI18n();
  const { session } = useSession();
  const requests = useRequestList({});

  if (session === null) {
    return null;
  }

  return (
    <>
      <PageHeader
        title={t('externalParticipation.mine.title')}
        description={t('externalParticipation.mine.description')}
        actions={
          <Link className="button" to={MY_CONTRIBUTIONS_PATH}>
            {t('externalParticipation.nav.contributions')}
          </Link>
        }
      />
      {requests.data === undefined ? (
        requests.loading ? (
          <LoadingState />
        ) : (
          <ErrorState
            message={participationProblemMessage(requests.error, t)}
            onRetry={requests.reload}
          />
        )
      ) : requests.data.length === 0 ? (
        <EmptyState title={t('externalParticipation.mine.emptyTitle')}>
          <p>{t('externalParticipation.mine.emptyHint')}</p>
        </EmptyState>
      ) : (
        <RequestRegisterView
          requests={requests.data}
          audience="external"
          userId={session.user.id}
          caption={t('externalParticipation.mine.caption')}
        />
      )}
    </>
  );
}
