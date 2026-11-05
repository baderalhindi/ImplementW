import {
  DATE_LOCALES,
  type Language,
  type TranslationParams,
  useI18n,
} from '@/shared/i18n/i18n.ts';

// Counted nouns in both languages: Arabic has six plural forms (Intl.PluralRules), of which these words need five.

type PluralForm = 'one' | 'two' | 'few' | 'many' | 'other';
type CountedNoun = 'projects' | 'dueProjects' | 'entities' | 'requests';

export function pluralForm(language: Language, count: number): PluralForm {
  const form = new Intl.PluralRules(language).select(count);
  return form === 'zero' ? 'other' : form;
}

/** `count` with its noun, agreeing in the interface's language: "3 مشاريع", "27 مشروعًا", "1 project". */
export function useCounted(): (noun: CountedNoun, count: number) => string {
  const { t, language } = useI18n();
  return (noun, count) => {
    const params: TranslationParams = { count };
    return t(`externalParticipation.counts.${noun}.${pluralForm(language, count)}`, params);
  };
}

const UTC = 'UTC';

function useDateLocale(): { locale: string } {
  const { language } = useI18n();
  return { locale: DATE_LOCALES[language] };
}

/** A due date as "10 November", Gregorian with Latin digits like every date the platform shows. */
export function useDayAndMonth(): (isoDate: string) => string {
  const { locale } = useDateLocale();
  return (isoDate) =>
    new Intl.DateTimeFormat(locale, { day: 'numeric', month: 'long', timeZone: UTC }).format(
      new Date(`${isoDate}T00:00:00Z`),
    );
}

/** A due date's month alone: "November". */
export function useMonthName(): (isoDate: string) => string {
  const { locale } = useDateLocale();
  return (isoDate) =>
    new Intl.DateTimeFormat(locale, { month: 'long', timeZone: UTC }).format(
      new Date(`${isoDate}T00:00:00Z`),
    );
}
