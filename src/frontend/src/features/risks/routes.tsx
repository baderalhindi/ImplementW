import { type RouteObject } from 'react-router';

import { type NavigationItem } from '@/features/identity-access/routes.tsx';

import { CriticalRisksPage } from './CriticalRisksPage.tsx';
import { RiskRegisterPage } from './RiskRegisterPage.tsx';

/**
 * WF-06's registers across projects, mounted under /risks for any signed-in person. No role is checked here: which
 * risks a person sees is the API's answer (RISK_VIEW). A project's register (SCR-080) and a risk's detail (SCR-082) are
 * in the project workspace, /projects/:projectId/risks.
 */
export const riskRoutes: RouteObject[] = [
  { index: true, element: <RiskRegisterPage /> }, // SCR-080
  { path: 'critical', element: <CriticalRisksPage /> }, // SCR-081
];

export const riskNavigation: NavigationItem[] = [
  { to: '/risks', label: 'risks.nav.register', end: true },
  { to: '/risks/critical', label: 'risks.nav.critical' },
];
