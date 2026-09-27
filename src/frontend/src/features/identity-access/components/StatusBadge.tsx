import { type ReactElement } from 'react';

type Tone = 'positive' | 'neutral' | 'negative';

/** A status as text with a tone; the colour only repeats what the text says (WCAG 1.4.1). */
export function StatusBadge({ label, tone }: { label: string; tone: Tone }): ReactElement {
  return <span className={`badge badge--${tone}`}>{label}</span>;
}
