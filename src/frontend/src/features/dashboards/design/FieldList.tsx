import { type ReactElement, useId } from 'react';

// The Data panel's field list parts (TASK-112): a source's group that opens and closes, a ticked row, and a remove button.

/** A source's fields under a heading that opens and closes them, with how many of them are chosen. */
export function FieldGroup({
  name,
  hint,
  chosen,
  open,
  onToggle,
  children,
}: {
  name: string;
  /** A word beside the name: "over time" for the published history's fields. */
  hint: string | null;
  chosen: number;
  open: boolean;
  onToggle: () => void;
  children: ReactElement[];
}): ReactElement {
  const id = useId();
  return (
    <div className="designer-data__group">
      <button
        type="button"
        className="designer-data__group-head"
        aria-expanded={open}
        aria-controls={id}
        onClick={onToggle}
      >
        <span className="designer-data__chevron" aria-hidden="true" />
        <span className="designer-data__group-name">{name}</span>
        {hint !== null && <span className="designer-data__over-time">{hint}</span>}
        {chosen > 0 && <span className="designer-data__count">{chosen}</span>}
      </button>
      {open && (
        <ul id={id} className="designer-data__fields">
          {children}
        </ul>
      )}
    </div>
  );
}

export function FieldRow({
  name,
  hint,
  checked,
  disabled,
  onChange,
}: {
  name: string;
  hint: string | null;
  checked: boolean;
  disabled: boolean;
  onChange: (on: boolean) => void;
}): ReactElement {
  return (
    <li>
      <label className="designer-data__field">
        <input
          type="checkbox"
          checked={checked}
          disabled={disabled}
          onChange={(event) => {
            onChange(event.target.checked);
          }}
        />
        <span className="designer-data__field-name" dir="auto">
          {name}
        </span>
        {hint !== null && <span className="designer-data__type">{hint}</span>}
      </label>
    </li>
  );
}

export function RemoveButton({
  label,
  disabled,
  onClick,
}: {
  label: string;
  disabled: boolean;
  onClick: () => void;
}): ReactElement {
  return (
    <button
      type="button"
      className="designer-data__remove"
      aria-label={label}
      title={label}
      disabled={disabled}
      onClick={onClick}
    >
      <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">
        <path d="M6 6l12 12M18 6L6 18" />
      </svg>
    </button>
  );
}
