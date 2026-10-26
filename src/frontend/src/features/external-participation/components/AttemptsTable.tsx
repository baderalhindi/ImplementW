import { type ReactElement } from 'react';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { TableContainer } from '@/shared/ui/Layout.tsx';

import { type SourceApplicationDetail } from '../api/types.ts';
import { useFailureText } from '../labels.ts';

import { AttemptStatusBadge } from './Badges.tsx';

/**
 * The applied lineage of one accepted answer: every attempt, newest first, with the source version it expected and the
 * one it found, its safe failure, who made it, its revalidation and its correlation id.
 */
export function AttemptsTable({
  attempts,
  caption,
}: {
  attempts: readonly SourceApplicationDetail[];
  caption: string;
}): ReactElement {
  const { t, formatDateTime } = useI18n();
  const failureText = useFailureText();
  const personName = usePersonNames(
    attempts.flatMap((attempt) => [attempt.attemptedByUserId, attempt.revalidatedByUserId]),
  );
  return (
    <TableContainer caption={caption}>
      <thead>
        <tr>
          <th scope="col">{t('externalParticipation.attempt.number')}</th>
          <th scope="col">{t('externalParticipation.attempt.status')}</th>
          <th scope="col">{t('externalParticipation.attempt.expected')}</th>
          <th scope="col">{t('externalParticipation.attempt.found')}</th>
          <th scope="col">{t('externalParticipation.attempt.failure')}</th>
          <th scope="col">{t('externalParticipation.attempt.attempted')}</th>
          <th scope="col">{t('externalParticipation.attempt.revalidated')}</th>
          <th scope="col">{t('externalParticipation.attempt.correlation')}</th>
        </tr>
      </thead>
      <tbody>
        {attempts.map((attempt) => (
          <tr key={attempt.id} data-attempt={attempt.attemptNo}>
            <td dir="ltr">{attempt.attemptNo}</td>
            <td>
              <AttemptStatusBadge status={attempt.status} />
            </td>
            <td dir="ltr">{attempt.expectedTargetRevisionNo ?? '—'}</td>
            <td dir="ltr">{attempt.actualTargetRevisionNo ?? '—'}</td>
            <td>{attempt.failureCode === null ? '—' : failureText(attempt.failureCode)}</td>
            <td>
              {formatDateTime(attempt.attemptedAt)}
              <span className="cell__aside">{personName(attempt.attemptedByUserId)}</span>
            </td>
            <td>
              {attempt.revalidatedAt === null ? (
                '—'
              ) : (
                <>
                  {formatDateTime(attempt.revalidatedAt)}
                  <span className="cell__aside">
                    {t('externalParticipation.attempt.revalidatedVersion', {
                      version: attempt.revalidatedTargetRevisionNo ?? '—',
                      person: personName(attempt.revalidatedByUserId),
                    })}
                  </span>
                </>
              )}
            </td>
            <td>
              <span className="break-all" dir="ltr">
                {attempt.correlationId}
              </span>
            </td>
          </tr>
        ))}
      </tbody>
    </TableContainer>
  );
}
