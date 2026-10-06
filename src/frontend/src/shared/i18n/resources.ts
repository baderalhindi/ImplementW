import approvalsAr from '@/features/approvals/i18n/ar.json';
import approvalsEn from '@/features/approvals/i18n/en.json';
import documentsAr from '@/features/documents/i18n/ar.json';
import documentsEn from '@/features/documents/i18n/en.json';
import financialKpiAr from '@/features/financial-kpi/i18n/ar.json';
import financialKpiEn from '@/features/financial-kpi/i18n/en.json';
import identityAccessAr from '@/features/identity-access/i18n/ar.json';
import identityAccessEn from '@/features/identity-access/i18n/en.json';
import milestonesAr from '@/features/milestones/i18n/ar.json';
import milestonesEn from '@/features/milestones/i18n/en.json';
import notificationsAr from '@/features/notifications/i18n/ar.json';
import notificationsEn from '@/features/notifications/i18n/en.json';
import progressAr from '@/features/progress/i18n/ar.json';
import progressEn from '@/features/progress/i18n/en.json';
import projectsAr from '@/features/projects/i18n/ar.json';
import projectsEn from '@/features/projects/i18n/en.json';
import risksAr from '@/features/risks/i18n/ar.json';
import risksEn from '@/features/risks/i18n/en.json';
import scheduleAr from '@/features/schedule/i18n/ar.json';
import scheduleEn from '@/features/schedule/i18n/en.json';
import tasksAr from '@/features/tasks/i18n/ar.json';
import tasksEn from '@/features/tasks/i18n/en.json';

import commonAr from './locales/ar.json';
import commonEn from './locales/en.json';

export type Language = 'ar' | 'en';

// One namespace per resource file: `common` for the application shell, one per feature module.
const en = {
  common: commonEn,
  identityAccess: identityAccessEn,
  approvals: approvalsEn,
  documents: documentsEn,
  notifications: notificationsEn,
  projects: projectsEn,
  progress: progressEn,
  schedule: scheduleEn,
  tasks: tasksEn,
  milestones: milestonesEn,
  financialKpi: financialKpiEn,
  risks: risksEn,
};

// English is the reference shape; resources.test.ts fails if Arabic lacks or adds a key.
type Resources = typeof en;

export const resources: Record<Language, Resources> = {
  en,
  ar: {
    common: commonAr,
    identityAccess: identityAccessAr,
    approvals: approvalsAr,
    documents: documentsAr,
    notifications: notificationsAr,
    projects: projectsAr,
    progress: progressAr,
    schedule: scheduleAr,
    tasks: tasksAr,
    milestones: milestonesAr,
    financialKpi: financialKpiAr,
    risks: risksAr,
  },
};

type LeafKeys<T, Prefix extends string = ''> = {
  [K in keyof T & string]: T[K] extends string ? `${Prefix}${K}` : LeafKeys<T[K], `${Prefix}${K}.`>;
}[keyof T & string];

export type TranslationKey = LeafKeys<Resources>;
