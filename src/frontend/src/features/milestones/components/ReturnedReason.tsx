import { type ReactElement } from 'react';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { languageTag } from '@/features/projects/presentation.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';

import { type MilestoneAchievementDetail } from '../api/types.ts';

/**
 * A returned claim's reason, shown where the claim is, without a click (acceptance criterion 2): a bordered callout with
 * a heading naming who returned it and when, and the reason in the language it was written in. A return with no reason
 * (the claimant withdrew it from WF-11, TASK-050 D-7) says so.
 */
export function ReturnedReason({
  revision,
  headingLevel = 3,
}: {
  revision: MilestoneAchievementDetail;
  headingLevel?: 3 | 4;
}): ReactElement {
  const { t } = useI18n();
  const personName = usePersonNames([revision.reviewedByUserId]);
  const Heading = headingLevel === 3 ? 'h3' : 'h4';
  const headingId = `returned-${revision.id}`;
  return (
    <section className="returned-reason" aria-labelledby={headingId}>
      <Heading id={headingId} className="returned-reason__title">
        {revision.reviewedByUserId === null
          ? t('milestones.returned.title')
          : t('milestones.returned.titleBy', {
              reviewer: personName(revision.reviewedByUserId),
              date: (revision.reviewedAt ?? '').slice(0, 10),
            })}
      </Heading>
      {revision.returnReason === null ? (
        <p>{t('milestones.returned.noReason')}</p>
      ) : (
        <p
          className="returned-reason__text pre-line"
          lang={languageTag(revision.returnReason.language)}
          dir="auto"
        >
          {revision.returnReason.text}
        </p>
      )}
    </section>
  );
}

/** The reason on a table row: one line under the milestone's title, in words, so the register shows it at a glance. */
export function ReturnedReasonLine({
  revision,
}: {
  revision: MilestoneAchievementDetail;
}): ReactElement {
  const { t } = useI18n();
  return (
    <span className="returned-reason-line" dir="auto">
      {revision.returnReason === null
        ? t('milestones.returned.noReason')
        : t('milestones.returned.line', { reason: revision.returnReason.text })}
    </span>
  );
}
