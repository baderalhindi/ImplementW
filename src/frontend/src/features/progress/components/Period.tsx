import { type Translate } from '@/features/identity-access/problems.ts';
import { shortId } from '@/features/projects/presentation.ts';

import { type ReportingCycleSummary } from '../api/types.ts';

/** A period as its dates (UTC calendar dates, TASK-044 F-7), or its shortened id when the periods could not be read. */
export function periodLabel(cycle: ReportingCycleSummary | null, id: string, t: Translate): string {
  return cycle === null
    ? t('progress.period.unknown', { id: shortId(id) })
    : t('progress.period.range', { start: cycle.periodStart, end: cycle.periodEnd });
}
