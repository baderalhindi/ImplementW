import {
  type ReactElement,
  type ReactNode,
  useCallback,
  useEffect,
  useMemo,
  useState,
} from 'react';

import {
  DATE_LOCALES,
  I18nContext,
  type I18nContextValue,
  type Language,
  LANGUAGE_STORAGE_KEY,
  readStoredLanguage,
  translate,
} from './i18n.ts';

interface I18nProviderProps {
  children: ReactNode;
  initialLanguage?: Language;
}

/** Holds the interface language and mirrors it onto <html lang dir> (ADR-012), so the whole layout flips for Arabic. */
export function I18nProvider({ children, initialLanguage }: I18nProviderProps): ReactElement {
  const [language, setLanguageState] = useState<Language>(
    () => initialLanguage ?? readStoredLanguage(),
  );
  const direction = language === 'ar' ? 'rtl' : 'ltr';

  useEffect(() => {
    document.documentElement.lang = language;
    document.documentElement.dir = direction;
  }, [language, direction]);

  const setLanguage = useCallback((next: Language) => {
    setLanguageState(next);
    try {
      localStorage.setItem(LANGUAGE_STORAGE_KEY, next);
    } catch {
      // Storage may be unavailable (private mode); the choice then lasts for this page only.
    }
  }, []);

  const value = useMemo<I18nContextValue>(() => {
    const dateFormat = new Intl.DateTimeFormat(DATE_LOCALES[language], {
      dateStyle: 'medium',
      timeStyle: 'short',
    });
    return {
      language,
      direction,
      setLanguage,
      t: (key, params) => translate(language, key, params),
      formatDateTime: (iso) => dateFormat.format(new Date(iso)),
    };
  }, [language, direction, setLanguage]);

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}
