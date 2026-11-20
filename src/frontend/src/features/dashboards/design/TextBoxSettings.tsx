import { type ReactElement, type ReactNode, useId, useState } from 'react';

import { type Language, useI18n } from '@/shared/i18n/i18n.ts';
import { useTheme } from '@/shared/theme/theme.ts';

import { ColourChooser } from './ColourChooser.tsx';
import { type TextBox } from './designApi.ts';
import {
  defaultTextBox,
  fontSizeAccepted,
  formatOf,
  lowContrast,
  MAX_FONT_SIZE,
  MAX_TEXT_LENGTH,
  MIN_FONT_SIZE,
  PRESET_FONT_SIZES,
  TEXT_ALIGNS,
  TEXT_ISSUE_MESSAGES,
  TEXT_SIZES,
  TEXT_VERTICAL_ALIGNS,
  textIssue,
  withLineBreaks,
} from './textBox.ts';

/** A group of the format pane: a framed fieldset under its legend. */
function FormatGroup({ legend, children }: { legend: string; children: ReactNode }): ReactElement {
  return (
    <fieldset className="field field--group designer-well designer-text__group">
      <legend className="field__label designer-well__label">{legend}</legend>
      {children}
    </fieldset>
  );
}

/** A row of toggle buttons under a small label, one pressed (or, for styles, each on or off). */
function ToggleRow({ label, children }: { label: string; children: ReactNode }): ReactElement {
  const id = useId();
  return (
    <div className="designer-text__row">
      <span id={id} className="designer-text__row-label">
        {label}
      </span>
      <div className="designer-toggles" role="group" aria-labelledby={id}>
        {children}
      </div>
    </div>
  );
}

/** An alignment's icon: lines set to the start, centre or end; or a box's line at its top, middle or bottom. */
function AlignIcon({ at }: { at: string }): ReactElement {
  const lines: Record<string, string> = {
    START: 'M4 6h16M4 10h10M4 14h16M4 18h10',
    CENTER: 'M4 6h16M7 10h10M4 14h16M7 18h10',
    END: 'M4 6h16M10 10h10M4 14h16M10 18h10',
    TOP: 'M4 4h16M8 8h8M8 11h8',
    MIDDLE: 'M4 4h16M4 20h16M8 10.5h8M8 13.5h8',
    BOTTOM: 'M4 20h16M8 13h8M8 16h8',
  };
  return (
    <svg
      className={`designer-toggle__icon${at === 'START' || at === 'END' ? ' designer-toggle__icon--mirrored' : ''}`}
      viewBox="0 0 24 24"
      aria-hidden="true"
      focusable="false"
    >
      <path d={lines[at] ?? ''} />
    </svg>
  );
}

/**
 * A text box's own settings, grouped as Power BI's format pane (20 Nov 2026, docs/architecture/dashboard-designer-map-text.md and
 * dashboard-designer-text-format.md): its words in Arabic and in English, each required, line breaks kept; its font (a size preset or its own
 * 8–96 px, bold, italic, underline, colour), alignment across and down, background and border. Each change is applied as it is made. Words the
 * API would refuse (textIssue) say why under their field, as does a refusal of the preview the designer could not foresee (`refused`, by
 * language); a chosen text colour that reads at under 3:1 on its background in the current theme says so (lowContrast), and is kept.
 */
export function TextBoxSettings({
  text,
  refused = {},
  onChange,
}: {
  text: TextBox | null | undefined;
  refused?: Partial<Record<Language, string>>;
  onChange: (next: TextBox) => void;
}): ReactElement {
  const { t } = useI18n();
  const { theme } = useTheme();
  const id = useId();
  const current = text ?? defaultTextBox();
  const format = formatOf(current);
  // The custom size as typed, until it is one the API takes; null shows the size the text box has.
  const [typedSize, setTypedSize] = useState<string | null>(null);
  const [sizeLeft, setSizeLeft] = useState(false);
  const shownSize = typedSize ?? (format.fontSize === null ? '' : String(format.fontSize));
  const sizeInvalid =
    shownSize !== '' && !fontSizeAccepted(shownSize) && (sizeLeft || shownSize.length > 1);
  const change = (next: Partial<TextBox>) => {
    onChange({ ...current, ...next });
  };

  const words = (language: Language) => {
    const issue = textIssue(current[language], language);
    const error = issue === null ? refused[language] : t(TEXT_ISSUE_MESSAGES[issue]);
    const invalid = error !== undefined;
    const fieldId = `${id}-text-${language}`;
    return (
      <div className="field">
        <label className="field__label" htmlFor={fieldId}>
          {t(language === 'ar' ? 'dashboards.designer.textAr' : 'dashboards.designer.textEn')}
        </label>
        <textarea
          id={fieldId}
          className="field__input designer-text__words"
          dir={language === 'ar' ? 'rtl' : 'ltr'}
          lang={language}
          rows={3}
          maxLength={MAX_TEXT_LENGTH}
          aria-invalid={invalid || undefined}
          aria-describedby={invalid ? `${fieldId}-error` : undefined}
          value={current[language]}
          onChange={(event) => {
            change({ [language]: withLineBreaks(event.target.value) });
          }}
        />
        {invalid && (
          <p id={`${fieldId}-error`} className="field__error">
            {error}
          </p>
        )}
      </div>
    );
  };

  const sizeId = `${id}-font-size`;
  return (
    <div className="designer-format designer-text">
      <p className="designer__hint">{t('dashboards.designer.textSettings')}</p>
      <FormatGroup legend={t('dashboards.designer.textGroupText')}>
        {words('ar')}
        {words('en')}
      </FormatGroup>

      <FormatGroup legend={t('dashboards.designer.textGroupFont')}>
        <ToggleRow label={t('dashboards.designer.textSize')}>
          {TEXT_SIZES.map((size) => (
            <button
              key={size}
              type="button"
              className="designer-toggle"
              aria-pressed={format.fontSize === null && current.size === size}
              onClick={() => {
                setTypedSize(null);
                change({ size, fontSize: null });
              }}
            >
              {t(`dashboards.designer.textSizes.${size}`)}
            </button>
          ))}
        </ToggleRow>
        <div className="designer-text__row designer-text__size">
          <label className="designer-text__row-label" htmlFor={sizeId}>
            {t('dashboards.designer.textCustomSize')}
          </label>
          <input
            id={sizeId}
            className="field__input"
            type="number"
            inputMode="numeric"
            min={MIN_FONT_SIZE}
            max={MAX_FONT_SIZE}
            step={1}
            placeholder={String(PRESET_FONT_SIZES[current.size])}
            value={shownSize}
            aria-invalid={sizeInvalid || undefined}
            aria-describedby={`${sizeId}-${sizeInvalid ? 'error' : 'hint'}`}
            onChange={(event) => {
              const typed = event.target.value;
              setTypedSize(typed);
              setSizeLeft(false);
              if (typed.trim() === '') {
                change({ fontSize: null });
              } else if (fontSizeAccepted(typed)) {
                change({ fontSize: Number(typed) });
              }
            }}
            onBlur={() => {
              setSizeLeft(true);
            }}
          />
          {sizeInvalid ? (
            <p id={`${sizeId}-error`} className="field__error designer-text__size-note">
              {t('dashboards.designer.textCustomSizeError')}
            </p>
          ) : (
            <p id={`${sizeId}-hint`} className="field__hint designer-text__size-note">
              {t('dashboards.designer.textCustomSizeHint')}
            </p>
          )}
        </div>
        <ToggleRow label={t('dashboards.designer.textStyle')}>
          <button
            type="button"
            className="designer-toggle designer-toggle--glyph"
            aria-pressed={current.bold}
            aria-label={t('dashboards.designer.textBold')}
            title={t('dashboards.designer.textBold')}
            onClick={() => {
              change({ bold: !current.bold });
            }}
          >
            <b aria-hidden="true">B</b>
          </button>
          <button
            type="button"
            className="designer-toggle designer-toggle--glyph"
            aria-pressed={format.italic}
            aria-label={t('dashboards.designer.textItalic')}
            title={t('dashboards.designer.textItalic')}
            onClick={() => {
              change({ italic: !format.italic });
            }}
          >
            <i aria-hidden="true">I</i>
          </button>
          <button
            type="button"
            className="designer-toggle designer-toggle--glyph"
            aria-pressed={format.underline}
            aria-label={t('dashboards.designer.textUnderline')}
            title={t('dashboards.designer.textUnderline')}
            onClick={() => {
              change({ underline: !format.underline });
            }}
          >
            <u aria-hidden="true">U</u>
          </button>
        </ToggleRow>
        <ColourChooser
          label={t('dashboards.designer.textColour')}
          value={format.color}
          specials={[{ value: null, label: t('dashboards.designer.colourDefault') }]}
          note={
            lowContrast(current, theme) && (
              <p className="field__hint designer-colour__note" role="status">
                {t('dashboards.designer.textLowContrast')}
              </p>
            )
          }
          onChange={(color) => {
            change({ color });
          }}
        />
      </FormatGroup>

      <FormatGroup legend={t('dashboards.designer.textAlign')}>
        <ToggleRow label={t('dashboards.designer.textAlignHorizontal')}>
          {TEXT_ALIGNS.map((align) => (
            <button
              key={align}
              type="button"
              className="designer-toggle designer-toggle--glyph"
              aria-pressed={current.align === align}
              aria-label={t(`dashboards.designer.textAligns.${align}`)}
              title={t(`dashboards.designer.textAligns.${align}`)}
              onClick={() => {
                change({ align });
              }}
            >
              <AlignIcon at={align} />
            </button>
          ))}
        </ToggleRow>
        <ToggleRow label={t('dashboards.designer.textAlignVertical')}>
          {TEXT_VERTICAL_ALIGNS.map((verticalAlign) => (
            <button
              key={verticalAlign}
              type="button"
              className="designer-toggle designer-toggle--glyph"
              aria-pressed={format.verticalAlign === verticalAlign}
              aria-label={t(`dashboards.designer.textVerticalAligns.${verticalAlign}`)}
              title={t(`dashboards.designer.textVerticalAligns.${verticalAlign}`)}
              onClick={() => {
                change({ verticalAlign });
              }}
            >
              <AlignIcon at={verticalAlign} />
            </button>
          ))}
        </ToggleRow>
      </FormatGroup>

      <FormatGroup legend={t('dashboards.designer.textBackground')}>
        <ColourChooser
          label={t('dashboards.designer.textBackground')}
          labelHidden
          value={format.background}
          specials={[
            { value: 'CARD', label: t('dashboards.designer.textBackgrounds.CARD') },
            { value: 'NONE', label: t('dashboards.designer.textBackgrounds.NONE') },
          ]}
          onChange={(background) => {
            change({ background: background ?? 'NONE' });
          }}
        />
      </FormatGroup>

      <FormatGroup legend={t('dashboards.designer.textBorder')}>
        <label className="field__checkbox">
          <input
            type="checkbox"
            checked={format.border}
            onChange={(event) => {
              change({ border: event.target.checked });
            }}
          />
          {t('dashboards.designer.textBorderShow')}
        </label>
        {format.border && (
          <ColourChooser
            label={t('dashboards.designer.textBorderColour')}
            value={format.borderColor}
            specials={[{ value: null, label: t('dashboards.designer.colourDefault') }]}
            onChange={(borderColor) => {
              change({ borderColor });
            }}
          />
        )}
      </FormatGroup>
    </div>
  );
}
