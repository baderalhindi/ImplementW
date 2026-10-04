import { type Translate } from '@/features/identity-access/problems.ts';
import { type StatusTone } from '@/shared/ui/StatusBadge.tsx';

import { type MilestoneAchievementStatus, type ProjectMilestoneStatus } from './api/types.ts';
import { type AchievementState } from './milestoneRules.ts';

// How milestone values read on screen. Each state has its own word; the colour only repeats it (WCAG 1.4.1).

const MILESTONE_TONES: Record<ProjectMilestoneStatus, StatusTone> = {
  PLANNED: 'neutral',
  ACHIEVED: 'positive',
  CANCELLED: 'neutral',
};

export function milestoneTone(status: ProjectMilestoneStatus): StatusTone {
  return MILESTONE_TONES[status];
}

const REVISION_TONES: Record<MilestoneAchievementStatus, StatusTone> = {
  DRAFT: 'neutral',
  SUBMITTED: 'info',
  RETURNED: 'warning',
  ACCEPTED: 'positive',
  SUPERSEDED: 'neutral',
};

export function revisionTone(status: MilestoneAchievementStatus): StatusTone {
  return REVISION_TONES[status];
}

const STATE_TONES: Record<AchievementState['kind'], StatusTone> = {
  unclaimed: 'neutral',
  draft: 'neutral',
  submitted: 'info',
  returned: 'warning',
  accepted: 'positive',
};

export function achievementTone(state: AchievementState): StatusTone {
  return STATE_TONES[state.kind];
}

/** "Not claimed", "Claim in draft", "Correction under review", "Returned", "Accepted"… in words. */
export function achievementLabel(state: AchievementState, t: Translate): string {
  switch (state.kind) {
    case 'unclaimed':
      return t('milestones.achievement.unclaimed');
    case 'accepted':
      return t('milestones.achievement.accepted');
    case 'draft':
    case 'submitted':
    case 'returned':
      return t(
        `milestones.achievement.${state.accepted === null ? 'claim' : 'correction'}.${state.kind}`,
      );
  }
}
