import { type CSSProperties } from 'react';

import { checkScript } from '@/features/identity-access/forms.ts';
import { type Language, translate, type TranslationKey } from '@/shared/i18n/i18n.ts';
import { type Theme } from '@/shared/theme/theme.ts';

import {
  type TextAlign,
  type TextBackground,
  type TextBox,
  type TextSize,
  type TextVerticalAlign,
  type Visual,
} from './designApi.ts';

// The text box (20 Nov 2026, docs/architecture/dashboard-designer-map-text.md): a visual of the designer's own words, a heading or a note,
// drawn in the page's language with its size, alignment and weight. The same rules the API checks (PortfolioDesignService).

export const TEXT_SIZES: readonly TextSize[] = ['SMALL', 'NORMAL', 'LARGE', 'HEADING'];
export const TEXT_ALIGNS: readonly TextAlign[] = ['START', 'CENTER', 'END'];
export const TEXT_VERTICAL_ALIGNS: readonly TextVerticalAlign[] = ['TOP', 'MIDDLE', 'BOTTOM'];

/** Each size preset in px, as styles.css draws it (0.875, 1, 1.25 and 1.5 rem): what a custom size field shows before one is typed. */
export const PRESET_FONT_SIZES: Readonly<Record<TextSize, number>> = {
  SMALL: 14,
  NORMAL: 16,
  LARGE: 20,
  HEADING: 24,
};

/** The least and most a custom font size is, in whole px (the API's rule). */
export const MIN_FONT_SIZE = 8;
export const MAX_FONT_SIZE = 96;

/** A colour as the API stores it: `#RRGGBB`. */
const HEX_COLOUR = /^#[0-9A-Fa-f]{6}$/;

export function isHexColour(value: string): boolean {
  return HEX_COLOUR.test(value);
}

/** Whether `typed` is a custom font size the API takes: a whole number of px from 8 to 96. */
export function fontSizeAccepted(typed: string): boolean {
  const size = Number(typed);
  return (
    /^\d+$/.test(typed.trim()) &&
    Number.isInteger(size) &&
    size >= MIN_FONT_SIZE &&
    size <= MAX_FONT_SIZE
  );
}

/** A colour the settings offer as a swatch: its value as stored and its name for a screen reader. */
export interface Swatch {
  name:
    | 'GREEN'
    | 'AMBER'
    | 'RED'
    | 'BLUE'
    | 'TEAL'
    | 'ORANGE'
    | 'PURPLE'
    | 'PINK'
    | 'GREY'
    | 'BLACK'
    | 'WHITE';
  value: string;
}

/**
 * The swatches, from the app's chart and status palette (styles.css, the light theme's --chart-* and --color-text and --color-background), upper-case as
 * the API stores them. A colour is stored as chosen: it is the same in both themes.
 */
export const SWATCHES: readonly Swatch[] = [
  { name: 'GREEN', value: '#3FB27F' },
  { name: 'AMBER', value: '#E2A12B' },
  { name: 'RED', value: '#B8322A' },
  { name: 'BLUE', value: '#3B78C4' },
  { name: 'TEAL', value: '#1F8A70' },
  { name: 'ORANGE', value: '#E07B39' },
  { name: 'PURPLE', value: '#8A5CC9' },
  { name: 'PINK', value: '#C2557A' },
  { name: 'GREY', value: '#7D8894' },
  { name: 'BLACK', value: '#1B1F23' },
  { name: 'WHITE', value: '#FFFFFF' },
];

/** A text box's format with every part present: what is absent is drawn as before the format was added (no background, at the top). */
export interface TextFormat {
  fontSize: number | null;
  italic: boolean;
  underline: boolean;
  color: string | null;
  background: TextBackground;
  border: boolean;
  borderColor: string | null;
  verticalAlign: TextVerticalAlign;
}

export function formatOf(text: TextBox): TextFormat {
  return {
    fontSize: text.fontSize ?? null,
    italic: text.italic ?? false,
    underline: text.underline ?? false,
    color: text.color ?? null,
    background: text.background ?? 'NONE',
    border: text.border ?? false,
    borderColor: text.borderColor ?? null,
    verticalAlign: text.verticalAlign ?? 'TOP',
  };
}

/**
 * A text box as it is sent and compared: every part present, in one order (so a design read back compares equal to the one sent), colours
 * upper-case as the API stores them.
 */
export function sentText(text: TextBox): TextBox {
  const format = formatOf(text);
  const upper = (colour: string | null) => (colour === null ? null : colour.toUpperCase());
  return {
    ar: text.ar,
    en: text.en,
    size: text.size,
    align: text.align,
    bold: text.bold,
    fontSize: format.fontSize,
    italic: format.italic,
    underline: format.underline,
    color: upper(format.color),
    background: isHexColour(format.background)
      ? format.background.toUpperCase()
      : format.background,
    border: format.border,
    borderColor: upper(format.borderColor),
    verticalAlign: format.verticalAlign,
  };
}

/** How the words are drawn beyond their preset (styles.css): a custom size, italic, underlined, in a colour. */
export function wordsStyle(text: TextBox): CSSProperties {
  const format = formatOf(text);
  return {
    ...(format.fontSize === null ? {} : { fontSize: `${String(format.fontSize)}px` }),
    ...(format.italic ? { fontStyle: 'italic' } : {}),
    ...(format.underline ? { textDecoration: 'underline' } : {}),
    ...(format.color === null ? {} : { color: format.color }),
  };
}

/**
 * The box round the words (styles.css): the card colour (drawn as the visuals' cards are, without their shadow), a colour, or none; a border
 * in its colour or the theme's.
 */
export function frameOf(text: TextBox | null | undefined): {
  className: string;
  style: CSSProperties;
} {
  if (text === null || text === undefined) {
    return { className: '', style: {} };
  }
  const format = formatOf(text);
  const filled = isHexColour(format.background);
  return {
    className: `${format.background === 'CARD' ? ' visual--text-card' : filled ? ' visual--text-fill' : ''}${
      format.border ? ' visual--text-border' : ''
    }`,
    style: {
      ...(filled ? { '--text-box-fill': format.background } : {}),
      ...(format.border && format.borderColor !== null
        ? { '--text-box-border': format.borderColor }
        : {}),
    } as CSSProperties,
  };
}

/** The colours a text box is drawn on in each theme (styles.css): the card's, or with no background the dashboard's ground. */
const GROUNDS: Readonly<Record<Theme, { CARD: string; NONE: string }>> = {
  light: { CARD: '#FFFFFF', NONE: '#F5F7F9' },
  dark: { CARD: '#1A2128', NONE: '#11161B' },
};

/** The least contrast a chosen text colour should keep against its background (WCAG 2.1 non-text and large text). */
export const MIN_TEXT_CONTRAST = 3;

function luminance(hex: string): number {
  const channel = (at: number) => {
    const value = Number.parseInt(hex.slice(at, at + 2), 16) / 255;
    return value <= 0.039_28 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4;
  };
  return 0.2126 * channel(1) + 0.7152 * channel(3) + 0.0722 * channel(5);
}

/** The WCAG contrast ratio of two `#RRGGBB` colours, 1 to 21. */
export function contrastRatio(first: string, second: string): number {
  const [lighter, darker] = [luminance(first), luminance(second)].sort((a, b) => b - a);
  return ((lighter ?? 0) + 0.05) / ((darker ?? 0) + 0.05);
}

/** Whether the text box's chosen text colour reads at under 3:1 on its background in `theme`; the theme's own text colour always reads. */
export function lowContrast(text: TextBox, theme: Theme): boolean {
  const format = formatOf(text);
  if (format.color === null || !isHexColour(format.color)) {
    return false;
  }
  const ground = isHexColour(format.background)
    ? format.background
    : GROUNDS[theme][format.background === 'CARD' ? 'CARD' : 'NONE'];
  return contrastRatio(format.color, ground) < MIN_TEXT_CONTRAST;
}

/** The most characters each language's text takes. */
export const MAX_TEXT_LENGTH = 2000;

export function isTextBox(visual: { visualType: string }): boolean {
  return visual.visualType === 'TEXT';
}

/**
 * A new text box's words, "Text" / "نص" until edited, at the normal size from the reading start, on the card colour (the user, 20 Nov 2026:
 * "background to be white").
 */
export function defaultTextBox(): TextBox {
  return sentText({
    ar: translate('ar', 'dashboards.designer.textDefault'),
    en: translate('en', 'dashboards.designer.textDefault'),
    size: 'NORMAL',
    align: 'START',
    bold: false,
    background: 'CARD',
  });
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
