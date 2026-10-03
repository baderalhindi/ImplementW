import { type ReactElement } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { type ProjectBaselineDetail, type ScheduleHealth } from '../api/types.ts';
import { baselineTone, scheduleHealthTone, varianceLabel, varianceTone } from '../presentation.ts';

export function BaselineStatusBadge({
  baseline,
}: {
  baseline: Pick<ProjectBaselineDetail, 'status'>;
}): ReactElement {
  const { t } = useI18n();
  return (
    <StatusBadge
      label={t(`schedule.baselineStatus.${baseline.status}`)}
      tone={baselineTone(baseline.status)}
    />
  );
}

export function ScheduleHealthBadge({ health }: { health: ScheduleHealth }): ReactElement {
  const { t } = useI18n();
  return <StatusBadge label={t(`schedule.health.${health}`)} tone={scheduleHealthTone(health)} />;
}

/** A signed variance in working days as words, late in amber. No baseline entry reads as such, never as 0. */
export function Variance({ days }: { days: number | null }): ReactElement {
  const { t } = useI18n();
  if (days === null) {
    return <span className="figure figure--none">{varianceLabel(null, t)}</span>;
  }
  return <StatusBadge label={varianceLabel(days, t)} tone={varianceTone(days)} />;
}
