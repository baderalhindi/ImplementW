import { type ReactElement } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { type MilestoneAchievementStatus, type ProjectMilestoneStatus } from '../api/types.ts';
import { type AchievementState } from '../milestoneRules.ts';
import { achievementLabel, achievementTone, milestoneTone, revisionTone } from '../presentation.ts';

/** WF-03's status of the milestone: Planned, Achieved, Cancelled. */
export function MilestoneStatusBadge({ status }: { status: ProjectMilestoneStatus }): ReactElement {
  const { t } = useI18n();
  return <StatusBadge label={t(`milestones.status.${status}`)} tone={milestoneTone(status)} />;
}

/** One revision's status: Draft, Submitted, Returned, Accepted, Superseded. */
export function RevisionStatusBadge({
  status,
}: {
  status: MilestoneAchievementStatus;
}): ReactElement {
  const { t } = useI18n();
  return (
    <StatusBadge label={t(`milestones.revisionStatus.${status}`)} tone={revisionTone(status)} />
  );
}

/** Where the milestone's claim stands, from its newest revision. */
export function AchievementBadge({ state }: { state: AchievementState }): ReactElement {
  const { t } = useI18n();
  return <StatusBadge label={achievementLabel(state, t)} tone={achievementTone(state)} />;
}
