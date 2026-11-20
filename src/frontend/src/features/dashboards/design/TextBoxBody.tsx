import { type ReactElement } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';

import { type TextBox } from './designApi.ts';
import { shownText } from './textBox.ts';

/**
 * A text box's words, in the page's language (the other where that one is empty), its line breaks kept, at its size, alignment and weight
 * (styles.css). Drawn alike in the designer and on the dashboard; never fitted to its box: longer than its visual, it scrolls.
 */
export function TextBoxBody({ text }: { text: TextBox | null | undefined }): ReactElement | null {
  const { language } = useI18n();
  if (text === null || text === undefined) {
    return null;
  }
  const shown = shownText(text, language);
  return (
    <p
      className={`visual-text visual-text--${text.size.toLowerCase()} visual-text--${text.align.toLowerCase()}${
        text.bold ? ' visual-text--bold' : ''
      }`}
      lang={shown.language === language ? undefined : shown.language}
      dir={shown.language === language ? undefined : shown.language === 'ar' ? 'rtl' : 'ltr'}
    >
      {shown.words}
    </p>
  );
}
