import { type ReactElement, type ReactNode, useId } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';

import { isHexColour, SWATCHES } from './textBox.ts';

/** A choice before the swatches that is not a colour of its own: the theme's ("Default"), the card colour, none. */
export interface ColourSpecial {
  value: string | null;
  label: string;
}

/**
 * A colour chooser of the text box's format pane (20 Nov 2026, docs/architecture/dashboard-designer-text-format.md): its `specials`, a row of
 * swatches from the app's chart and status palette, each named for screen readers, and "Custom…" (the browser's colour picker). The chosen one
 * is pressed; a swatch or custom colour is named beside the label. `note` shows under the row (the low-contrast note).
 */
export function ColourChooser({
  label,
  value,
  specials,
  note,
  labelHidden = false,
  onChange,
}: {
  label: string;
  /** Where the fieldset round it already names it: no label or group of its own. */
  labelHidden?: boolean;
  value: string | null;
  specials: readonly ColourSpecial[];
  note?: ReactNode;
  onChange: (next: string | null) => void;
}): ReactElement {
  const { t } = useI18n();
  const id = useId();
  const hex = value !== null && isHexColour(value) ? value.toUpperCase() : null;
  const special = specials.find((option) => option.value === value);
  const swatch = SWATCHES.find((option) => option.value === hex);
  const custom = hex !== null && swatch === undefined;
  // The chosen colour's name beside the label; a special's own button says it.
  const chosen =
    special !== undefined
      ? null
      : swatch === undefined
        ? hex
        : t(`dashboards.designer.colours.${swatch.name}`);
  return (
    <div
      className="designer-colour"
      {...(labelHidden ? {} : { role: 'group', 'aria-labelledby': `${id}-label` })}
    >
      {(!labelHidden || chosen !== null) && (
        <p className="designer-colour__label">
          {!labelHidden && (
            <span id={`${id}-label`} className="designer-text__row-label">
              {label}
            </span>
          )}
          {chosen !== null && <span className="designer-colour__chosen">{chosen}</span>}
        </p>
      )}
      <div className="designer-colour__row">
        {specials.map((option) => (
          <button
            key={option.value ?? 'default'}
            type="button"
            className="designer-toggle designer-colour__special"
            aria-pressed={option.value === value}
            onClick={() => {
              onChange(option.value);
            }}
          >
            {option.label}
          </button>
        ))}
        <label
          className={`designer-colour__custom${custom ? ' is-chosen' : ''}`}
          title={t('dashboards.designer.colourCustomLabel', { label })}
        >
          <input
            type="color"
            className="designer-colour__picker"
            aria-label={t('dashboards.designer.colourCustomLabel', { label })}
            value={(hex ?? '#000000').toLowerCase()}
            onChange={(event) => {
              onChange(event.target.value.toUpperCase());
            }}
          />
          <span
            className="designer-colour__dot"
            aria-hidden="true"
            style={custom ? { background: hex } : undefined}
          />
          <span aria-hidden="true">{t('dashboards.designer.colourCustom')}</span>
        </label>
      </div>
      <div className="designer-colour__swatches">
        {SWATCHES.map((option) => {
          const name = t(`dashboards.designer.colours.${option.name}`);
          return (
            <button
              key={option.value}
              type="button"
              className="designer-colour__swatch"
              style={{ backgroundColor: option.value }}
              aria-label={name}
              title={name}
              aria-pressed={option.value === hex}
              onClick={() => {
                onChange(option.value);
              }}
            />
          );
        })}
      </div>
      {note}
    </div>
  );
}
