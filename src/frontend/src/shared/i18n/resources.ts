import identityAccessAr from '@/features/identity-access/i18n/ar.json';
import identityAccessEn from '@/features/identity-access/i18n/en.json';

import commonAr from './locales/ar.json';
import commonEn from './locales/en.json';

export type Language = 'ar' | 'en';

// One namespace per resource file: `common` for the application shell, one per feature module.
const en = { common: commonEn, identityAccess: identityAccessEn };

// English is the reference shape; resources.test.ts fails if Arabic lacks or adds a key.
type Resources = typeof en;

export const resources: Record<Language, Resources> = {
  en,
  ar: { common: commonAr, identityAccess: identityAccessAr },
};

type LeafKeys<T, Prefix extends string = ''> = {
  [K in keyof T & string]: T[K] extends string ? `${Prefix}${K}` : LeafKeys<T[K], `${Prefix}${K}.`>;
}[keyof T & string];

export type TranslationKey = LeafKeys<Resources>;
