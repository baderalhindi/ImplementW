import { type ReactElement } from 'react';

/**
 * The label of a button that flips between two words (Activate / Deactivate). Both words share one grid cell and
 * only `current` shows, so the button is as wide as the longer one in every row and the buttons after it line up.
 */
export function ToggleLabel({ current, other }: { current: string; other: string }): ReactElement {
  return (
    <span className="toggle-label">
      <span>{current}</span>
      <span className="toggle-label__reserve" aria-hidden="true">
        {other}
      </span>
    </span>
  );
}
