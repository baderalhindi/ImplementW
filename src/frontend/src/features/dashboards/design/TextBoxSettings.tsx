import { type ReactElement, useId } from 'react';

import { type Language, useI18n } from '@/shared/i18n/i18n.ts';

import { type TextBox, type TextSize } from './designApi.ts';
import {
  defaultTextBox,
  MAX_TEXT_LENGTH,
  TEXT_ALIGNS,
  TEXT_ISSUE_MESSAGES,
  TEXT_SIZES,
  textIssue,
  withLineBreaks,
} from './textBox.ts';

/**
 * A text box's own settings (20 Nov 2026, docs/architecture/dashboard-designer-map-text.md): its words in Arabic and in English, each required,
 * line breaks kept; its size, alignment and weight. Each change is applied as it is made. Words the API would refuse (textIssue) say why under
 * their field, as does a refusal of the preview the designer could not foresee (`refused`, by language).
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
  const id = useId();
  const current = text ?? defaultTextBox();
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
          rows={4}
          maxLength={MAX_TEXT_LENGTH}
          aria-invalid={invalid || undefined}
          aria-describedby={invalid ? `${fieldId}-error` : undefined}
          value={current[language]}
          onChange={(event) => {
            onChange({ ...current, [language]: withLineBreaks(event.target.value) });
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
  return (
    <>
      <p className="designer__hint">{t('dashboards.designer.textSettings')}</p>
      {words('ar')}
      {words('en')}
      <div className="field">
        <label className="field__label" htmlFor={`${id}-size`}>
          {t('dashboards.designer.textSize')}
        </label>
        <select
          id={`${id}-size`}
          className="field__input"
          value={current.size}
          onChange={(event) => {
            onChange({ ...current, size: event.target.value as TextSize });
          }}
        >
          {TEXT_SIZES.map((size) => (
            <option key={size} value={size}>
              {t(`dashboards.designer.textSizes.${size}`)}
            </option>
          ))}
        </select>
      </div>
      <fieldset className="field field--group designer-text__align">
        <legend className="field__label">{t('dashboards.designer.textAlign')}</legend>
        <div className="designer-text__choices">
          {TEXT_ALIGNS.map((align) => (
            <label key={align} className="field__checkbox">
              <input
                type="radio"
                name={`${id}-align`}
                value={align}
                checked={current.align === align}
                onChange={() => {
                  onChange({ ...current, align });
                }}
              />
              {t(`dashboards.designer.textAligns.${align}`)}
            </label>
          ))}
        </div>
      </fieldset>
      <label className="field__checkbox">
        <input
          type="checkbox"
          checked={current.bold}
          onChange={(event) => {
            onChange({ ...current, bold: event.target.checked });
          }}
        />
        {t('dashboards.designer.textBold')}
      </label>
    </>
  );
}
