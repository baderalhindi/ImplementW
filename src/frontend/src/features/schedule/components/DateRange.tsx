import { type ReactElement } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';

/** A start and a finish on two lines, so the schedule's date columns stay narrow; read as "start to finish". */
export function DateRange({
  start,
  finish,
}: {
  start: string | null;
  finish: string | null;
}): ReactElement {
  const { t } = useI18n();
  if (start === null || finish === null) {
    return <span className="figure figure--none">{t('schedule.activities.notBaselined')}</span>;
  }
  return (
    <span className="date-range" dir="ltr">
      <span>{start}</span>
      <span className="visually-hidden">{` ${t('schedule.dates.to')} `}</span>
      <span>{finish}</span>
    </span>
  );
}
