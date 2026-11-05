import { type ReactElement } from 'react';
import { Link } from 'react-router';

import { useCan } from '@/features/identity-access/permissions/permissions.ts';
import { useSession } from '@/features/identity-access/session/useSession.ts';
import { useListParams } from '@/shared/api/useListParams.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { RequestRegisterView } from './components/RequestRegisterView.tsx';
import {
  APPLICATION_MONITOR_PATH,
  EXTERNAL_REQUESTS_PATH,
  NEW_UPDATE_BATCH_PATH,
  PARTICIPATION_MONITOR_PATH,
  UPDATE_BATCHES_PATH,
} from './paths.ts';
import { participationProblemMessage } from './problems.ts';
import { useEntityNames, useRequestList } from './useExternalParticipationData.ts';

/**
 * SCR-160 External Update Requests: AHDA's register of every request the person may see (DEPT, OWN or ALL as the API
 * scopes it), drafts included, with their status, due condition, responder and reviewer. Narrowed to one project and
 * entity from SCR-167 (`?projectId=&externalEntityId=`). Its "new" is the monthly update to one or many projects (the 5 Nov 2026 review
 * §2.2); any other request is created from its project's workspace (SCR-161 needs the project). Cancelling and reassigning are on its
 * detail (SCR-162).
 */
export function ExternalRequestRegisterPage(): ReactElement | null {
  const { t } = useI18n();
  const { session } = useSession();
  const { params } = useListParams();
  const projectId = params.get('projectId') ?? undefined;
  const externalEntityId = params.get('externalEntityId') ?? undefined;
  const requests = useRequestList({ projectId, externalEntityId });
  const entityName = useEntityNames();
  const maySend = useCan('UPDATE_REQUEST_SEND');

  if (session === null) {
    return null;
  }
  const narrowed = projectId !== undefined || externalEntityId !== undefined;
  const sample = requests.data?.[0];

  return (
    <>
      <PageHeader
        title={t('externalParticipation.register.title')}
        description={t('externalParticipation.register.description')}
        actions={
          <>
            {maySend && (
              <>
                <Link className="button button--primary" to={NEW_UPDATE_BATCH_PATH}>
                  {t('externalParticipation.batch.new')}
                </Link>
                <Link className="button" to={UPDATE_BATCHES_PATH}>
                  {t('externalParticipation.nav.batches')}
                </Link>
              </>
            )}
            <Link className="button" to={APPLICATION_MONITOR_PATH}>
              {t('externalParticipation.nav.applications')}
            </Link>
            <Link className="button" to={PARTICIPATION_MONITOR_PATH}>
              {t('externalParticipation.nav.monitor')}
            </Link>
          </>
        }
      />
      {narrowed && (
        <p className="form__note">
          {t('externalParticipation.register.narrowed', {
            project: sample?.formalProjectId ?? t('externalParticipation.project.noFormalId'),
            entity: externalEntityId === undefined ? '—' : entityName(externalEntityId),
          })}{' '}
          <Link to={EXTERNAL_REQUESTS_PATH}>{t('externalParticipation.register.showAll')}</Link>
        </p>
      )}
      {requests.data === undefined ? (
        requests.loading ? (
          <LoadingState />
        ) : (
          <ErrorState
            error={requests.error}
            message={participationProblemMessage(requests.error, t)}
            onRetry={requests.reload}
          />
        )
      ) : requests.data.length === 0 ? (
        <EmptyState title={t('externalParticipation.register.emptyTitle')}>
          <p>{t('externalParticipation.register.emptyHint')}</p>
        </EmptyState>
      ) : (
        <RequestRegisterView
          requests={requests.data}
          audience="internal"
          userId={session.user.id}
          caption={t('externalParticipation.register.caption')}
        />
      )}
    </>
  );
}
