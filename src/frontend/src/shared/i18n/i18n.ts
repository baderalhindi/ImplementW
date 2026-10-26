import { createContext, useContext } from 'react';

import { resources, type Language, type TranslationKey } from './resources.ts';

export type { Language, TranslationKey };

export type TranslationParams = Record<string, string | number>;

export interface I18nContextValue {
  language: Language;
  direction: 'rtl' | 'ltr';
  setLanguage: (language: Language) => void;
  t: (key: TranslationKey, params?: TranslationParams) => string;
  formatDateTime: (value: string) => string;
}

export const LANGUAGE_STORAGE_KEY = 'pmplatform.language';
// Before anyone chooses, Arabic: the API's default preferred language for a new user (UserCreateRequest).
export const DEFAULT_LANGUAGE: Language = 'ar';

// Gregorian calendar and Latin digits in both languages (TASK-032 D-5): the dates an administrator reads match the
// ISO values they enter and the values the API returns.
export const DATE_LOCALES: Record<Language, string> = {
  ar: 'ar-u-ca-gregory-nu-latn',
  en: 'en-GB',
};

export const I18nContext = createContext<I18nContextValue | null>(null);

export function isLanguage(value: unknown): value is Language {
  return value === 'ar' || value === 'en';
}

export function readStoredLanguage(): Language {
  try {
    const stored = localStorage.getItem(LANGUAGE_STORAGE_KEY);
    return isLanguage(stored) ? stored : DEFAULT_LANGUAGE;
  } catch {
    return DEFAULT_LANGUAGE;
  }
}

function lookup(language: Language, key: string): string | undefined {
  let node: unknown = resources[language];
  for (const segment of key.split('.')) {
    if (typeof node !== 'object' || node === null) {
      return undefined;
    }
    node = (node as Record<string, unknown>)[segment];
  }
  return typeof node === 'string' ? node : undefined;
}

export function translate(language: Language, key: string, params?: TranslationParams): string {
  const template = lookup(language, key) ?? key;
  if (params === undefined) {
    return template;
  }
  // Each value is bidi-isolated (FSI…PDI): a Latin name or id inside Arabic text, or an Arabic name inside English,
  // keeps its own direction instead of reordering the sentence around it.
  return template.replace(/\{(\w+)\}/g, (placeholder, name: string) =>
    name in params ? `\u2068${String(params[name])}\u2069` : placeholder,
  );
}

export function useI18n(): I18nContextValue {
  const context = useContext(I18nContext);
  if (context === null) {
    throw new Error('useI18n must be used inside I18nProvider.');
  }
  return context;
}
