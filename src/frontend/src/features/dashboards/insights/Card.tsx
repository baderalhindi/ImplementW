import { type ReactElement, type ReactNode } from 'react';
import { Link } from 'react-router';

import { type StatusTone } from '@/shared/ui/StatusBadge.tsx';

/** A headline figure's card: its label (a link to the records it counts, when it has one), the figure and its notes. */
export function Card({
  label,
  children,
  tone,
  to,
  hint,
}: {
  label: string;
  children: ReactNode;
  tone?: StatusTone | undefined;
  to?: string | undefined;
  hint?: string | undefined;
}): ReactElement {
  return (
    <li
      className={`home__tile insights__card${to === undefined ? '' : ' home__tile--linked'}`}
      data-tone={tone}
      title={hint}
    >
      {to === undefined ? (
        <span className="home__tile-label">{label}</span>
      ) : (
        <Link className="home__tile-label home__tile-link" to={to}>
          {label}
        </Link>
      )}
      {children}
    </li>
  );
}
