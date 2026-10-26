import { type ReactElement } from 'react';
import { Link } from 'react-router';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { Detail } from '@/features/projects/components/Detail.tsx';
import { languageTag } from '@/features/projects/presentation.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';

import { type ContributionFieldDefinition, type ExternalContributionDetail } from '../api/types.ts';
import { contributionReviewPath } from '../paths.ts';
import { disclosedContribution, isExternalView } from '../projection.ts';

import { ContributionStatusBadge, OutcomeBadge } from './Badges.tsx';
import { FieldValues } from './FieldValues.tsx';

interface RevisionHistoryProps {
  revisions: readonly ExternalContributionDetail[];
  definitions: readonly ContributionFieldDefinition[];
}

/**
 * A request's revisions, newest first, each with its values as submitted. The entity's view (each revision's own
 * projection) shows its outcome and AHDA's reason; AHDA's also shows who reviewed it, the source version it was
 * answered against, the internal note and the way to its review (SCR-165). A draft's values are the form's, not here.
 */
export function RevisionHistory({ revisions, definitions }: RevisionHistoryProps): ReactElement {
  const { t, formatDateTime } = useI18n();
  const shown = revisions.map(disclosedContribution);
  const personName = usePersonNames(
    shown.flatMap((revision) => [revision.contributorUserId, revision.reviewedByUserId ?? null]),
  );
  return (
    <ol className="revision-list" data-field="revisions">
      {shown.map((revision) => {
        const external = isExternalView(revision);
        return (
          <li key={revision.id} data-revision={revision.revisionNo}>
            <p>
              <span className="revision-list__number">
                {t('externalParticipation.revision.number', { revision: revision.revisionNo })}
              </span>{' '}
              {external ? (
                <OutcomeBadge status={revision.status} />
              ) : (
                <ContributionStatusBadge status={revision.status} />
              )}
            </p>
            <dl className="details">
              <Detail term={t('externalParticipation.revision.contributor')}>
                {personName(revision.contributorUserId)}
              </Detail>
              <Detail term={t('externalParticipation.revision.submitted')}>
                {revision.submittedAt === null
                  ? t('externalParticipation.revision.notSubmitted')
                  : formatDateTime(revision.submittedAt)}
              </Detail>
              {revision.targetVersion !== undefined && revision.targetVersion !== null && (
                <Detail term={t('externalParticipation.revision.answeredAgainst')}>
                  <span data-field="targetVersion">
                    {t('externalParticipation.revision.sourceVersion', {
                      version: revision.targetVersion,
                      state: revision.targetState ?? '—',
                    })}
                  </span>
                </Detail>
              )}
              {revision.reviewedByUserId !== undefined && revision.reviewedByUserId !== null && (
                <Detail term={t('externalParticipation.revision.reviewedBy')}>
                  <span data-field="reviewedBy">{personName(revision.reviewedByUserId)}</span>
                  {revision.reviewStartedAt !== undefined && revision.reviewStartedAt !== null && (
                    <span className="cell__aside" data-field="reviewStartedAt">
                      {t('externalParticipation.revision.reviewStarted', {
                        date: formatDateTime(revision.reviewStartedAt),
                      })}
                    </span>
                  )}
                </Detail>
              )}
              {revision.reviewedAt !== null && (
                <Detail term={t('externalParticipation.revision.decided')}>
                  {formatDateTime(revision.reviewedAt)}
                </Detail>
              )}
              {revision.reviewReason !== null && (
                <Detail term={t('externalParticipation.revision.reason')}>
                  <span
                    dir="auto"
                    lang={languageTag(revision.reviewReason.language)}
                    className="pre-line"
                  >
                    {revision.reviewReason.text}
                  </span>
                </Detail>
              )}
              {revision.reviewInternalNote !== undefined &&
                revision.reviewInternalNote !== null && (
                  <Detail term={t('externalParticipation.revision.internalNote')}>
                    <span
                      dir="auto"
                      lang={languageTag(revision.reviewInternalNote.language)}
                      className="pre-line"
                      data-field="internalNote"
                    >
                      {revision.reviewInternalNote.text}
                    </span>
                  </Detail>
                )}
            </dl>
            {revision.status !== 'DRAFT' && (
              <details>
                <summary>{t('externalParticipation.revision.values')}</summary>
                <FieldValues definitions={definitions} values={revision.fields} />
              </details>
            )}
            {!external && revision.status !== 'DRAFT' && (
              <p>
                <Link to={contributionReviewPath(revision.id)}>
                  {t('externalParticipation.revision.openReview')}
                </Link>
              </p>
            )}
          </li>
        );
      })}
    </ol>
  );
}
