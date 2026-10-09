import { type ReactElement } from 'react';
import { Link } from 'react-router';

import { useSession } from '@/features/identity-access/session/useSession.ts';
import { languageTag } from '@/features/projects/presentation.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader, TableContainer } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { DueBadge, OutcomeBadge } from './components/Badges.tsx';
import { ProjectReference } from './components/RequestTable.tsx';
import { usePurpose } from './labels.ts';
import { externalRequestPath, MY_EXTERNAL_REQUESTS_PATH } from './paths.ts';
import { disclosedContribution, disclosedRequest } from './projection.ts';
import { participationProblemMessage } from './problems.ts';
import { useMyContributions } from './useExternalParticipationData.ts';

/**
 * SCR-164 My Contributions: the entity user's answers — each revision, draft or submitted, with its outcome in the
 * entity's words (accepted, returned, rejected; AHDA's application is not theirs to see) and AHDA's reason — and the
 * requests waiting for them to start one. Each opens SCR-162, where the answer is started, continued or corrected.
 */
export function MyContributionsPage(): ReactElement | null {
  const { t, formatDateTime } = useI18n();
  const { session } = useSession();
  const purpose = usePurpose();
  const mine = useMyContributions(session?.user.id ?? '');

  if (session === null) {
    return null;
  }

  return (
    <>
      <PageHeader
        title={t('externalParticipation.contributions.title')}
        description={t('externalParticipation.contributions.description')}
        actions={
          <Link className="button" to={MY_EXTERNAL_REQUESTS_PATH}>
            {t('externalParticipation.nav.mine')}
          </Link>
        }
      />
      {mine.data === undefined ? (
        mine.loading ? (
          <LoadingState />
        ) : (
          <ErrorState message={participationProblemMessage(mine.error, t)} onRetry={mine.reload} />
        )
      ) : (
        <>
          <section className="section" aria-labelledby="contributions-awaiting">
            <h2 id="contributions-awaiting">
              {t('externalParticipation.contributions.awaitingTitle')}
            </h2>
            {mine.data.awaiting.length === 0 ? (
              <p className="form__note">{t('externalParticipation.contributions.noneAwaiting')}</p>
            ) : (
              <ul className="link-list">
                {mine.data.awaiting.map(disclosedRequest).map((request) => (
                  <li key={request.id}>
                    <Link to={externalRequestPath(request.id)}>
                      {t('externalParticipation.contributions.start', {
                        purpose: purpose(request),
                      })}
                    </Link>{' '}
                    <DueBadge dueDate={request.dueDate} condition={request.dueCondition} />
                  </li>
                ))}
              </ul>
            )}
          </section>
          <section className="section" aria-labelledby="contributions-history">
            <h2 id="contributions-history">
              {t('externalParticipation.contributions.historyTitle')}
            </h2>
            {mine.data.entries.length === 0 ? (
              <EmptyState title={t('externalParticipation.contributions.emptyTitle')} />
            ) : (
              <TableContainer caption={t('externalParticipation.contributions.caption')}>
                <thead>
                  <tr>
                    <th scope="col">{t('externalParticipation.table.request')}</th>
                    <th scope="col">{t('externalParticipation.table.project')}</th>
                    <th scope="col">{t('externalParticipation.table.revision')}</th>
                    <th scope="col">{t('externalParticipation.table.outcome')}</th>
                    <th scope="col">{t('externalParticipation.table.submitted')}</th>
                    <th scope="col">{t('externalParticipation.table.reason')}</th>
                  </tr>
                </thead>
                <tbody>
                  {mine.data.entries.map((entry) => {
                    const request = disclosedRequest(entry.request);
                    const revision = disclosedContribution(entry.revision);
                    return (
                      <tr key={revision.id}>
                        <td>
                          <Link className="cell__link" to={externalRequestPath(request.id)}>
                            {purpose(request)}
                          </Link>
                          {revision.status === 'DRAFT' && (
                            <span className="cell__aside">
                              {t('externalParticipation.contributions.continueDraft')}
                            </span>
                          )}
                        </td>
                        <td>
                          <ProjectReference formalProjectId={request.formalProjectId} />
                        </td>
                        <td dir="ltr">{revision.revisionNo}</td>
                        <td>
                          <OutcomeBadge status={revision.status} />
                        </td>
                        <td>
                          {revision.submittedAt === null
                            ? t('externalParticipation.revision.notSubmitted')
                            : formatDateTime(revision.submittedAt)}
                        </td>
                        <td>
                          {revision.reviewReason === null ? (
                            '—'
                          ) : (
                            <span
                              dir="auto"
                              lang={languageTag(revision.reviewReason.language)}
                              className="pre-line"
                            >
                              {revision.reviewReason.text}
                            </span>
                          )}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </TableContainer>
            )}
          </section>
        </>
      )}
    </>
  );
}
