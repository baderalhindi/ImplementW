import { type ReactElement } from 'react';

import { type VisualType } from './designApi.ts';

/** What a gallery tile places: a visual type, or the map (a section). */
export type GalleryIcon = VisualType | 'MAP';

/** Each visual type's picture in the gallery: what it draws, in the current colour, beside its name (which carries the meaning). */
const SHAPES: Record<GalleryIcon, ReactElement> = {
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
  // A frame with a title bar over two visuals.
  CONTAINER: (
    <>
      <rect x="2.5" y="3.5" width="19" height="17" rx="2" />
      <path d="M2.5 8h19M6 6h5" />
      <rect x="5.5" y="11" width="5.5" height="6.5" rx="0.8" />
      <rect x="13" y="11" width="5.5" height="6.5" rx="0.8" />
    </>
  ),
  // A map pin over a folded map.
  MAP: (
    <>
      <path d="M3 8.5l5-2 8 3 5-2v12l-5 2-8-3-5 2z" />
      <path d="M8 6.5v12M16 9.5v12" />
      <path
        d="M12 12.5s3.5-3.4 3.5-6a3.5 3.5 0 0 0-7 0c0 2.6 3.5 6 3.5 6z"
        fill="var(--color-background, #fff)"
      />
      <circle cx="12" cy="6.5" r="1.1" />
    </>
  ),
  // A "T" over lines of text.
  TEXT: (
    <>
      <path d="M4 4.5h10M9 4.5v9" />
      <path d="M4 17h16M4 20.5h11" />
    </>
  ),
};

export function VisualIcon({ type }: { type: GalleryIcon }): ReactElement {
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
