import { type ReactElement } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';

import { formatPoints } from '@/features/projects/presentation.ts';

/** A percentage, or "not available" when nothing is measured: never a made-up zero. */
export function Percent({ value }: { value: number | null | undefined }): ReactElement {
  const { t } = useI18n();
  return value === null || value === undefined ? (
    <span className="value--unavailable">{t('dashboards.insights.notAvailable')}</span>
  ) : (
    <span dir="ltr">{`${formatPoints(value)}%`}</span>
  );
}

/** "Value entered for 167 of 167" under a figure. */
export function Coverage({
  kind,
  count,
  of,
}: {
  kind: 'value' | 'progress' | 'spend';
  count: number;
  of: number;
}): ReactElement {
  const { t } = useI18n();
  return (
    <span className="insights__coverage">
      {t(`dashboards.insights.coverage.${kind}`, { count, total: of })}
    </span>
  );
}
