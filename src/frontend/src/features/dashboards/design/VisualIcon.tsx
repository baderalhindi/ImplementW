import { type ReactElement } from 'react';

import { type VisualType } from './designApi.ts';

/** Each visual type's picture in the gallery: what it draws, in the current colour, beside its name (which carries the meaning). */
const SHAPES: Record<VisualType, ReactElement> = {
  CARD: (
    <>
      <rect x="3" y="5" width="18" height="14" rx="2" />
      <path d="M7 10h6M7 14h10" />
    </>
  ),
  BAR: <path d="M4 6h12M4 12h16M4 18h8" />,
  COLUMN: <path d="M6 20V12M12 20V5M18 20V9M3 20h18" />,
  PIE: (
    <>
      <circle cx="12" cy="12" r="8" />
      <path d="M12 4v8l6 5" />
    </>
  ),
  DONUT: (
    <>
      <circle cx="12" cy="12" r="8" />
      <circle cx="12" cy="12" r="3.5" />
    </>
  ),
  TABLE: (
    <>
      <rect x="3" y="5" width="18" height="14" rx="1" />
      <path d="M3 10h18M3 14.5h18M9 5v14" />
    </>
  ),
  LINE: (
    <>
      <path d="M3 20h18" />
      <path d="M4 16l5-6 4 3 7-8" />
    </>
  ),
  GAUGE: (
    <>
      <path d="M4 17a8 8 0 1 1 16 0" />
      <path d="M12 17l4-5" />
    </>
  ),
  SECTION: (
    <>
      <rect x="3" y="4" width="18" height="16" rx="2" />
      <path d="M3 9h18M8 13h8M8 16h5" />
    </>
  ),
};

export function VisualIcon({ type }: { type: VisualType }): ReactElement {
  return (
    <svg
      className="designer__type-icon"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
    >
      {SHAPES[type]}
    </svg>
  );
}
