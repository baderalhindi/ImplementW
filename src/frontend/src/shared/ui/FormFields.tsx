import { type ReactElement, useId } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';

interface FieldBaseProps {
  label: string;
  name: string;
  error?: string | undefined;
  hint?: string | undefined;
  required?: boolean;
  disabled?: boolean;
}

function describedBy(hintId: string, errorId: string, hint?: string, error?: string) {
  const ids = [hint === undefined ? null : hintId, error === undefined ? null : errorId].filter(
    (id): id is string => id !== null,
  );
  return ids.length === 0 ? undefined : ids.join(' ');
}

function FieldLabel({
  htmlFor,
  label,
  required,
}: {
  htmlFor: string;
  label: string;
  required: boolean;
}) {
  const { t } = useI18n();
  return (
    <label className="field__label" htmlFor={htmlFor}>
      {label}
      {required && <span className="field__required">{` (${t('common.form.required')})`}</span>}
    </label>
  );
}

function FieldMessages({
  hintId,
  errorId,
  hint,
  error,
}: {
  hintId: string;
  errorId: string;
  hint?: string | undefined;
  error?: string | undefined;
}) {
  return (
    <>
      {hint !== undefined && (
        <p id={hintId} className="field__hint">
          {hint}
        </p>
      )}
      {error !== undefined && (
        <p id={errorId} className="field__error">
          {error}
        </p>
      )}
    </>
  );
}

interface TextFieldProps extends FieldBaseProps {
  value: string;
  onChange: (value: string) => void;
  type?: 'text' | 'email' | 'tel' | 'password' | 'search' | 'date' | 'datetime-local';
  autoComplete?: string | undefined;
  /** Latin-script values (usernames, emails, codes, numbers) stay left-to-right inside an Arabic form. */
  dir?: 'ltr' | 'rtl' | 'auto';
  inputMode?: 'text' | 'numeric' | 'decimal' | 'tel' | 'email';
}

export function TextField({
  label,
  name,
  value,
  onChange,
  error,
  hint,
  required = false,
  disabled = false,
  type = 'text',
  autoComplete,
  dir,
  inputMode,
}: TextFieldProps): ReactElement {
  const id = useId();
  const hintId = `${id}-hint`;
  const errorId = `${id}-error`;
  return (
    <div className="field">
      <FieldLabel htmlFor={id} label={label} required={required} />
      <input
        id={id}
        name={name}
        className="field__input"
        type={type}
        value={value}
        onChange={(event) => {
          onChange(event.target.value);
        }}
        required={required}
        disabled={disabled}
        autoComplete={autoComplete}
        dir={dir}
        inputMode={inputMode}
        aria-invalid={error === undefined ? undefined : true}
        aria-describedby={describedBy(hintId, errorId, hint, error)}
      />
      <FieldMessages hintId={hintId} errorId={errorId} hint={hint} error={error} />
    </div>
  );
}

interface ReadOnlyFieldProps {
  label: string;
  name: string;
  value: string;
  /** Why it cannot be changed here, and how it does change. */
  hint: string;
}

/**
 * A value shown in a form that the person cannot change there (a server-computed figure): a labelled read-only input,
 * so it is announced as read-only and still reached and read in the form's order.
 */
export function ReadOnlyField({ label, name, value, hint }: ReadOnlyFieldProps): ReactElement {
  const id = useId();
  const hintId = `${id}-hint`;
  return (
    <div className="field">
      <label className="field__label" htmlFor={id}>
        {label}
      </label>
      <input
        id={id}
        name={name}
        className="field__input field__input--readonly"
        type="text"
        value={value}
        readOnly
        dir="auto"
        aria-describedby={hintId}
      />
      <p id={hintId} className="field__hint">
        {hint}
      </p>
    </div>
  );
}

interface TextAreaFieldProps extends FieldBaseProps {
  value: string;
  onChange: (value: string) => void;
  rows?: number;
  /** The API's limit, announced to the browser so a paste cannot silently exceed it. */
  maxLength?: number;
}

/** Free text a person writes (a reason, a note). `dir="auto"` follows the language the person types in. */
export function TextAreaField({
  label,
  name,
  value,
  onChange,
  error,
  hint,
  required = false,
  disabled = false,
  rows = 4,
  maxLength,
}: TextAreaFieldProps): ReactElement {
  const id = useId();
  const hintId = `${id}-hint`;
  const errorId = `${id}-error`;
  return (
    <div className="field">
      <FieldLabel htmlFor={id} label={label} required={required} />
      <textarea
        id={id}
        name={name}
        className="field__input"
        rows={rows}
        maxLength={maxLength}
        dir="auto"
        value={value}
        onChange={(event) => {
          onChange(event.target.value);
        }}
        required={required}
        disabled={disabled}
        aria-invalid={error === undefined ? undefined : true}
        aria-describedby={describedBy(hintId, errorId, hint, error)}
      />
      <FieldMessages hintId={hintId} errorId={errorId} hint={hint} error={error} />
    </div>
  );
}

interface CheckboxFieldProps extends FieldBaseProps {
  checked: boolean;
  onChange: (checked: boolean) => void;
}

/** A yes/no a person states (a flag), its label beside the box and its hint and error under it. */
export function CheckboxField({
  label,
  name,
  checked,
  onChange,
  error,
  hint,
  required = false,
  disabled = false,
}: CheckboxFieldProps): ReactElement {
  const id = useId();
  const hintId = `${id}-hint`;
  const errorId = `${id}-error`;
  return (
    <div className="field">
      <div className="field__checkbox">
        <input
          id={id}
          name={name}
          type="checkbox"
          checked={checked}
          onChange={(event) => {
            onChange(event.target.checked);
          }}
          required={required}
          disabled={disabled}
          aria-invalid={error === undefined ? undefined : true}
          aria-describedby={describedBy(hintId, errorId, hint, error)}
        />
        <FieldLabel htmlFor={id} label={label} required={required} />
      </div>
      <FieldMessages hintId={hintId} errorId={errorId} hint={hint} error={error} />
    </div>
  );
}

interface FileFieldProps extends FieldBaseProps {
  /** The chosen file, or null when the choice is cleared. */
  onChange: (file: File | null) => void;
}

/** One file to upload. The input keeps its own value (a browser never lets a script set it). */
export function FileField({
  label,
  name,
  onChange,
  error,
  hint,
  required = false,
  disabled = false,
}: FileFieldProps): ReactElement {
  const id = useId();
  const hintId = `${id}-hint`;
  const errorId = `${id}-error`;
  return (
    <div className="field">
      <FieldLabel htmlFor={id} label={label} required={required} />
      <input
        id={id}
        name={name}
        className="field__input field__input--file"
        type="file"
        onChange={(event) => {
          onChange(event.target.files?.[0] ?? null);
        }}
        required={required}
        disabled={disabled}
        aria-invalid={error === undefined ? undefined : true}
        aria-describedby={describedBy(hintId, errorId, hint, error)}
      />
      <FieldMessages hintId={hintId} errorId={errorId} hint={hint} error={error} />
    </div>
  );
}

export interface SelectOption {
  value: string;
  label: string;
}

interface SelectFieldProps extends FieldBaseProps {
  value: string;
  onChange: (value: string) => void;
  options: SelectOption[];
  /** Label of the empty first option; omit when a value is always chosen. */
  placeholder?: string | undefined;
}

export function SelectField({
  label,
  name,
  value,
  onChange,
  options,
  placeholder,
  error,
  hint,
  required = false,
  disabled = false,
}: SelectFieldProps): ReactElement {
  const id = useId();
  const hintId = `${id}-hint`;
  const errorId = `${id}-error`;
  return (
    <div className="field">
      <FieldLabel htmlFor={id} label={label} required={required} />
      <select
        id={id}
        name={name}
        className="field__input"
        value={value}
        onChange={(event) => {
          onChange(event.target.value);
        }}
        required={required}
        disabled={disabled}
        aria-invalid={error === undefined ? undefined : true}
        aria-describedby={describedBy(hintId, errorId, hint, error)}
      >
        {placeholder !== undefined && <option value="">{placeholder}</option>}
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
      <FieldMessages hintId={hintId} errorId={errorId} hint={hint} error={error} />
    </div>
  );
}

interface RadioGroupFieldProps extends FieldBaseProps {
  value: string;
  onChange: (value: string) => void;
  options: SelectOption[];
}

export function RadioGroupField({
  label,
  name,
  value,
  onChange,
  options,
  error,
  hint,
  required = false,
  disabled = false,
}: RadioGroupFieldProps): ReactElement {
  const id = useId();
  const hintId = `${id}-hint`;
  const errorId = `${id}-error`;
  const { t } = useI18n();
  return (
    <fieldset
      className="field field--group"
      aria-invalid={error === undefined ? undefined : true}
      aria-describedby={describedBy(hintId, errorId, hint, error)}
      disabled={disabled}
    >
      <legend className="field__label">
        {label}
        {required && <span className="field__required">{` (${t('common.form.required')})`}</span>}
      </legend>
      <div className="field__options">
        {options.map((option) => (
          <label key={option.value} className="field__option">
            <input
              type="radio"
              name={name}
              value={option.value}
              checked={value === option.value}
              onChange={() => {
                onChange(option.value);
              }}
            />
            {option.label}
          </label>
        ))}
      </div>
      <FieldMessages hintId={hintId} errorId={errorId} hint={hint} error={error} />
    </fieldset>
  );
}
