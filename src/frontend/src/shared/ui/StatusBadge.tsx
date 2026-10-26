import { type ReactElement } from 'react';

/** `info` is work in progress (pending); `warning` is sent back for another attempt (returned). */
export type StatusTone = 'positive' | 'info' | 'warning' | 'negative' | 'neutral';

/** A status as text with a tone; the colour only repeats what the text says (WCAG 1.4.1). */
export function StatusBadge({ label, tone }: { label: string; tone: StatusTone }): ReactElement {
  return <span className={`badge badge--${tone}`}>{label}</span>;
}
