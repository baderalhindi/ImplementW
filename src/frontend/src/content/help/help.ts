import { PORTFOLIO_DASHBOARD_PATH } from '@/features/dashboards/paths.ts';
import { type Language } from '@/shared/i18n/i18n.ts';

import faqAr from './ar/faq.json';
import guideAr from './ar/guide.json';
import knowledgeBaseAr from './ar/knowledge-base.json';
import faqEn from './en/faq.json';
import guideEn from './en/guide.json';
import knowledgeBaseEn from './en/knowledge-base.json';

// The in-app help of Blueprint §20.1 menu 11 (TASK-096): the User Guide (SCR-011), the FAQ (SCR-012, SCR-126) and the
// Knowledge Base (SCR-125). Each language is written in its own file and reviewed by a bilingual reviewer
// (docs/architecture/user-guide-faq.md). Inside any text, `[[key]]` names an interface label by its translation key;
// the page shows that label in the reader's language, so the guide always names a control as the screen does.

export type RoleCode = 'R01' | 'R02' | 'R03' | 'R04' | 'R05' | 'R06' | 'R07' | 'R08';

export const ROLE_CODES: readonly RoleCode[] = [
  'R01',
  'R02',
  'R03',
  'R04',
  'R05',
  'R06',
  'R07',
  'R08',
];

export interface GuideSection {
  heading: string;
  paragraphs?: string[];
  /** A numbered procedure: one action per step. */
  steps?: string[];
  /** A point the reader must not miss: a rule the platform enforces or a common mistake. */
  note?: string;
}

export interface GuideArticle {
  title: string;
  summary: string;
  sections: GuideSection[];
}

export interface FaqEntry {
  question: string;
  answer: string[];
}

export interface FaqGroup {
  heading: string;
  entries: Record<string, FaqEntry>;
}

export interface KnowledgeBaseTerm {
  term: string;
  definition: string;
}

interface HelpContent {
  guide: Record<string, GuideArticle>;
  faq: Record<string, FaqGroup>;
  knowledgeBase: Record<string, KnowledgeBaseTerm>;
}

export const helpContent: Record<Language, HelpContent> = {
  en: { guide: guideEn, faq: faqEn, knowledgeBase: knowledgeBaseEn },
  ar: { guide: guideAr, faq: faqAr, knowledgeBase: knowledgeBaseAr },
};

export interface GuideArticleMeta {
  id: string;
  /** The roles the article is written for; `ALL` is every signed-in person. */
  audience: readonly RoleCode[] | 'ALL';
  /** Screens the article walks through: a path and the translation key of its menu label. */
  related: readonly { to: string; label: string }[];
}

/** The User Guide's articles in reading order. The text is in `<language>/guide.json` under the same id. */
export const GUIDE_ARTICLES: readonly GuideArticleMeta[] = [
  {
    id: 'getting-started',
    audience: 'ALL',
    related: [
      { to: '/', label: 'common.menu.home' },
      { to: '/notifications/preferences', label: 'notifications.nav.preferences' },
    ],
  },
  {
    id: 'portfolio-dashboard',
    audience: ['R02', 'R03', 'R06', 'R07'],
    related: [
      { to: PORTFOLIO_DASHBOARD_PATH, label: 'common.menu.portfolioDashboard' },
      { to: '/reports/portfolio', label: 'common.menu.portfolioReports' },
    ],
  },
  {
    id: 'administrator-home',
    audience: ['R01'],
    related: [
      { to: '/', label: 'common.menu.home' },
      { to: '/admin/users', label: 'identityAccess.nav.users' },
    ],
  },
  {
    id: 'project-manager-home',
    audience: ['R04'],
    related: [
      { to: '/', label: 'common.menu.home' },
      { to: '/tasks', label: 'tasks.nav.mine' },
      { to: '/projects/mine', label: 'projects.nav.mine' },
    ],
  },
  {
    id: 'liaison-home',
    audience: ['R05'],
    related: [
      { to: '/', label: 'common.menu.home' },
      { to: '/approvals/inbox', label: 'approvals.nav.inbox' },
      { to: '/tasks', label: 'tasks.nav.mine' },
    ],
  },
  {
    id: 'external-contributor-home',
    audience: ['R08'],
    related: [
      { to: '/', label: 'common.menu.home' },
      { to: '/my-external-requests', label: 'externalParticipation.nav.mine' },
      { to: '/my-contributions', label: 'externalParticipation.nav.contributions' },
    ],
  },
  {
    // PROJECT_REGISTER is shipped to R04 and, within its own entity, R08 (seed-master-data.sql).
    id: 'register-a-project',
    audience: ['R04', 'R08'],
    related: [
      { to: '/projects/new', label: 'projects.nav.create' },
      { to: '/projects', label: 'projects.nav.register' },
    ],
  },
  {
    id: 'submit-a-progress-update',
    audience: ['R04'],
    related: [{ to: '/projects/mine', label: 'projects.nav.mine' }],
  },
  {
    // Approval follows approval authority and delegation, not a role grant (WF-11), so any internal role may hold it.
    id: 'decide-an-approval',
    audience: ['R01', 'R02', 'R03', 'R04', 'R05', 'R06', 'R07'],
    related: [
      { to: '/approvals/inbox', label: 'approvals.nav.inbox' },
      { to: '/approvals/requests', label: 'approvals.nav.requests' },
    ],
  },
];

/**
 * Blueprint §20.2: the article that walks each role through its default landing page. Every role has one, in both
 * languages (content.test.ts).
 */
export const LANDING_ARTICLE: Readonly<Record<RoleCode, string>> = {
  R01: 'administrator-home',
  R02: 'portfolio-dashboard',
  R03: 'portfolio-dashboard',
  R04: 'project-manager-home',
  R05: 'liaison-home',
  R06: 'portfolio-dashboard',
  R07: 'portfolio-dashboard',
  R08: 'external-contributor-home',
};

export function isForRoles(article: GuideArticleMeta, roleCodes: readonly string[]): boolean {
  return article.audience === 'ALL' || article.audience.some((code) => roleCodes.includes(code));
}

/** An inline interface label: `[[common.menu.projects]]`. */
export const LABEL_REFERENCE = /\[\[([\w.]+)\]\]/g;
