import { type RouteObject } from 'react-router';

import { type NavigationItem } from '@/features/identity-access/routes.tsx';

import { FaqPage, KnowledgeBasePage, UserGuideArticlePage, UserGuidePage } from './HelpPages.tsx';

/** Help & Knowledge (Blueprint §20.1 menu 11), mounted under /help for any signed-in person (TASK-096). */
export const helpRoutes: RouteObject[] = [
  { index: true, element: <UserGuidePage /> }, // SCR-011
  { path: 'guide', element: <UserGuidePage /> }, // SCR-011
  { path: 'guide/:articleId', element: <UserGuideArticlePage /> }, // SCR-011
  { path: 'faq', element: <FaqPage /> }, // SCR-012, SCR-126
  { path: 'knowledge-base', element: <KnowledgeBasePage /> }, // SCR-125
];

export const helpNavigation: NavigationItem[] = [
  { to: '/help/guide', label: 'common.help.nav.guide' },
  { to: '/help/faq', label: 'common.help.nav.faq' },
  { to: '/help/knowledge-base', label: 'common.help.nav.knowledgeBase' },
];
