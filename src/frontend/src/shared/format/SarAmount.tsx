import { type ReactElement } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';

import { sarCompact, sarExactLabel } from './money.ts';

/** A card's or chart's amount in compact form, "64.6 مليار ريال", with the full figure on hover (review round 8, point 11). */
export function SarAmount({
  amount,
  className,
}: {
  amount: string;
  className?: string | undefined;
}): ReactElement {
  const { t } = useI18n();
  return (
    <span className={className} title={sarExactLabel(amount, t)}>
      {sarCompact(amount, t)}
    </span>
  );
}
