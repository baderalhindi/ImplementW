import { type OnPlanStatus } from '@/features/projects/api/types.ts';
import { type StatusTone } from '@/shared/ui/StatusBadge.tsx';

/** The on-plan label's tone on a headline card: green on track, amber late, red stalled. */
export const ON_PLAN_TONES: Record<OnPlanStatus, StatusTone> = {
  ON_TRACK: 'positive',
  LATE: 'warning',
  STALLED: 'negative',
};
