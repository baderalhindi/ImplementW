import { type ReactElement } from 'react';

// A value that cannot be shown as a figure — missing, stale, not applicable, withheld, or from a source that did not
// answer — reads as that state: a bordered flag with its icon and its word, never blank, never 0, never green. The
// colour only repeats the word (WCAG 1.4.1). `data-value-state` names the state for tests and for a reviewer reading
// the DOM. Shared by WF-14's figures (TASK-053) and FG-01's widgets (TASK-070).

export type ValueStateIcon = 'missing' | 'stale' | 'notApplicable' | 'restricted' | 'unavailable';

function StateIcon({ kind }: { kind: ValueStateIcon }): ReactElement {
  return (
    <svg
      className="value-state__icon"
      viewBox="0 0 16 16"
      width="16"
      height="16"
      aria-hidden="true"
      focusable="false"
    >
      {kind === 'missing' && (
        <>
          <circle cx="8" cy="8" r="6.2" fill="none" stroke="currentColor" strokeWidth="1.6" />
          <path d="M3.6 12.4 12.4 3.6" stroke="currentColor" strokeWidth="1.6" />
        </>
      )}
      {kind === 'stale' && (
        <>
          <circle cx="8" cy="8" r="6.2" fill="none" stroke="currentColor" strokeWidth="1.6" />
          <path d="M8 4.2V8l2.6 1.8" fill="none" stroke="currentColor" strokeWidth="1.6" />
        </>
      )}
      {kind === 'notApplicable' && (
        <>
          <circle cx="8" cy="8" r="6.2" fill="none" stroke="currentColor" strokeWidth="1.6" />
          <path d="M4.8 8h6.4" stroke="currentColor" strokeWidth="1.6" />
        </>
      )}
      {kind === 'restricted' && (
        <>
          <rect
            x="3"
            y="7"
            width="10"
            height="7"
            rx="1"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.6"
          />
          <path
            d="M5.4 7V5a2.6 2.6 0 0 1 5.2 0v2"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.6"
          />
        </>
      )}
      {kind === 'unavailable' && (
        <>
          <path d="M8 1.8 14.6 13.8H1.4z" fill="none" stroke="currentColor" strokeWidth="1.6" />
          <path d="M8 6v3.8M8 11.2v1.2" stroke="currentColor" strokeWidth="1.6" />
        </>
      )}
    </svg>
  );
}

interface ValueStateFlagProps {
  icon: ValueStateIcon;
  label: string;
  /** The `data-value-state` the flag carries. */
  state: string;
  tone?: 'neutral' | 'warning';
}

export function ValueStateFlag({
  icon,
  label,
  state,
  tone = 'neutral',
}: ValueStateFlagProps): ReactElement {
  return (
    <span className={`value-state value-state--${tone}`} data-value-state={state}>
      <StateIcon kind={icon} />
      {label}
    </span>
  );
}
