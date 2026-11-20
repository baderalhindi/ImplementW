import { type ReactElement } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';

import { type TextBox } from './designApi.ts';
import { formatOf, shownText, wordsStyle } from './textBox.ts';

/**
 * A text box's words, in the page's language (the other where that one is empty), its line breaks kept, at its size (a preset or its own),
 * alignment, weight, style and colour, at the top, middle or bottom of its box (styles.css; the box's background and border are its frame's,
 * frameOf). Drawn alike in the designer and on the dashboard; never fitted to its box: longer than its visual, it scrolls.
 */
export function TextBoxBody({ text }: { text: TextBox | null | undefined }): ReactElement | null {
  const { language } = useI18n();
  if (text === null || text === undefined) {
    return null;
  }
  const shown = shownText(text, language);
  return (
    <p
      className={`visual-text visual-text--${text.size.toLowerCase()} visual-text--${text.align.toLowerCase()} visual-text--${formatOf(
        text,
      ).verticalAlign.toLowerCase()}${text.bold ? ' visual-text--bold' : ''}`}
      style={wordsStyle(text)}
      lang={shown.language === language ? undefined : shown.language}
      dir={shown.language === language ? undefined : shown.language === 'ar' ? 'rtl' : 'ltr'}
    >
      {shown.words}
    </p>
  );
}
