import { type ReactElement } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { StatusBadge, type StatusTone } from '@/shared/ui/StatusBadge.tsx';

import { type OnPlanStatus } from '../api/types.ts';
import { formatPoints } from '../presentation.ts';

const ON_PLAN_TONES: Record<OnPlanStatus, StatusTone> = {
  ON_TRACK: 'positive',
  LATE: 'warning',
  STALLED: 'negative',
};

/** RPMO's on-plan indicator, or "Not available" when it cannot be calculated — never a made-up value. */
export function OnPlanBadge({ status }: { status: OnPlanStatus | null | undefined }): ReactElement {
  const { t } = useI18n();
  return status === null || status === undefined ? (
    <StatusBadge label={t('projects.onPlan.NONE')} tone="neutral" />
  ) : (
    <StatusBadge label={t(`projects.onPlan.${status}`)} tone={ON_PLAN_TONES[status]} />
  );
}

/** The gap between actual and planned progress, in points, signed. */
export function GapValue({ points }: { points: number | null | undefined }): ReactElement {
  const { t } = useI18n();
  return points === null || points === undefined ? (
    <span className="value--unavailable">{t('projects.followUp.notAvailable')}</span>
  ) : (
    <span dir="ltr">{t('projects.followUp.gapValue', { points: formatPoints(points, true) })}</span>
  );
}

/** The label a project an import batch created carries (R3-4). */
export function ImportedLabel({
  batchId,
}: {
  batchId: string | null | undefined;
}): ReactElement | null {
  const { t } = useI18n();
  return batchId === null || batchId === undefined ? null : (
    <span className="badge badge--info" title={t('projects.imported.title')}>
      {t('projects.imported.label')}
    </span>
  );
}
