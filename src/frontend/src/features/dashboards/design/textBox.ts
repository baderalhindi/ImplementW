import { checkScript } from '@/features/identity-access/forms.ts';
import { type Language, translate, type TranslationKey } from '@/shared/i18n/i18n.ts';

import { type TextAlign, type TextBox, type TextSize, type Visual } from './designApi.ts';

// The text box (20 Nov 2026, docs/architecture/dashboard-designer-map-text.md): a visual of the designer's own words, a heading or a note,
// drawn in the page's language with its size, alignment and weight. The same rules the API checks (PortfolioDesignService).

export const TEXT_SIZES: readonly TextSize[] = ['SMALL', 'NORMAL', 'LARGE', 'HEADING'];
export const TEXT_ALIGNS: readonly TextAlign[] = ['START', 'CENTER', 'END'];

/** The most characters each language's text takes. */
export const MAX_TEXT_LENGTH = 2000;

export function isTextBox(visual: { visualType: string }): boolean {
  return visual.visualType === 'TEXT';
}

/** A new text box's words, "Text" / "نص" until edited, at the normal size from the reading start. */
export function defaultTextBox(): TextBox {
  return {
    ar: translate('ar', 'dashboards.designer.textDefault'),
    en: translate('en', 'dashboards.designer.textDefault'),
    size: 'NORMAL',
    align: 'START',
    bold: false,
  };
}

/** A new text box at `place`, `width` × `height`, titled "Text box" / "مربع نص" (its name in the designer and its accessible name). */
export function textBoxVisual(
  place: { column: number; row: number },
  width: number,
  height: number,
): Visual {
  return {
    title: {
      ar: translate('ar', 'dashboards.designer.textDefaultTitle'),
      en: translate('en', 'dashboards.designer.textDefaultTitle'),
    },
    visualType: 'TEXT',
    measures: [],
    groupBy: 'NONE',
    layoutColumn: place.column,
    layoutRow: place.row,
    layoutWidth: width,
    layoutHeight: height,
    text: defaultTextBox(),
  };
}

/** The words a text box draws: the page language's, or the other language's where that one is empty; with the language they are in. */
export function shownText(
  text: TextBox,
  language: Language,
): { words: string; language: Language } {
  const other: Language = language === 'ar' ? 'en' : 'ar';
  return text[language].trim() === ''
    ? { words: text[other], language: other }
    : { words: text[language], language };
}

/** A control character the API refuses in a text box (R-17): any but the line break. */
// eslint-disable-next-line no-control-regex -- the control characters are what is looked for.
const CONTROL_CHARACTER = /[\u0000-\u0009\u000B-\u001F\u007F]/;

/** What the API would refuse in one language's words, as its code; null when it takes them. */
export type TextIssue =
  'REQUIRED' | 'OUTSIDE_RANGE' | 'INVALID_CHARACTER' | 'NOT_ARABIC' | 'NOT_ENGLISH';

/**
 * The API's rules for a text box's words in `language`: 1 to 2000 characters after trimming, no control character but the line break, and
 * in the language's script as a visual's title is (Arabic letters in the Arabic, English letters and no Arabic in the English).
 */
export function textIssue(words: string, language: Language): TextIssue | null {
  const trimmed = words.trim();
  if (trimmed === '') {
    return 'REQUIRED';
  }
  if (trimmed.length > MAX_TEXT_LENGTH) {
    return 'OUTSIDE_RANGE';
  }
  if (CONTROL_CHARACTER.test(words)) {
    return 'INVALID_CHARACTER';
  }
  return checkScript(trimmed, language) === null
    ? null
    : language === 'ar'
      ? 'NOT_ARABIC'
      : 'NOT_ENGLISH';
}

/** What a person reads for each issue, the API's own or found before sending. */
export const TEXT_ISSUE_MESSAGES: Record<TextIssue, TranslationKey> = {
  REQUIRED: 'dashboards.designer.textRequired',
  OUTSIDE_RANGE: 'dashboards.designer.textTooLong',
  INVALID_CHARACTER: 'dashboards.designer.textInvalidCharacter',
  NOT_ARABIC: 'common.fieldErrors.notArabic',
  NOT_ENGLISH: 'common.fieldErrors.notEnglish',
};

/** A text as typed, its line breaks as the API takes them: `\n` only. */
export function withLineBreaks(words: string): string {
  return words.replace(/\r\n?/g, '\n');
}

/** Whether a text box's words are both ones the API takes. */
export function textAccepted(text: TextBox | null | undefined): boolean {
  return (
    text !== null &&
    text !== undefined &&
    textIssue(text.ar, 'ar') === null &&
    textIssue(text.en, 'en') === null
  );
}

/**
 * The design as previewed: a text box whose words the API would refuse is left out (it has no figures to calculate), so the rest of the design
 * is still calculated while its settings say what to fix.
 */
export function withoutRefusedText<
  T extends {
    visualType: string;
    text?: TextBox | null | undefined;
    children?: T[] | null | undefined;
  },
>(visuals: readonly T[]): T[] {
  const accepted = (visual: T) => !isTextBox(visual) || textAccepted(visual.text);
  return visuals
    .filter(accepted)
    .map((visual) =>
      visual.children === undefined || visual.children === null
        ? visual
        : { ...visual, children: visual.children.filter(accepted) },
    );
}
