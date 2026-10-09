import { type RouteObject } from 'react-router';

import { type NavigationItem } from '@/features/identity-access/routes.tsx';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { isInternal } from '@/features/projects/access.ts';

import { InternalOnly } from './components/InternalOnly.tsx';
import { ContributionReviewPage } from './ContributionReviewPage.tsx';
import { ExternalRequestDetailPage } from './ExternalRequestDetailPage.tsx';
import { CreateExternalRequestPage, EditExternalRequestPage } from './ExternalRequestFormPage.tsx';
import { ExternalRequestRegisterPage } from './ExternalRequestRegisterPage.tsx';
import { MyContributionsPage } from './MyContributionsPage.tsx';
import { MyExternalRequestsPage } from './MyExternalRequestsPage.tsx';
import { ParticipationMonitorPage } from './ParticipationMonitorPage.tsx';
import {
  APPLICATION_MONITOR_PATH,
  EXTERNAL_REQUESTS_PATH,
  MY_CONTRIBUTIONS_PATH,
  MY_EXTERNAL_REQUESTS_PATH,
  PARTICIPATION_MONITOR_PATH,
} from './paths.ts';
import { SourceApplicationMonitorPage } from './SourceApplicationMonitorPage.tsx';

/**
 * WF-13, mounted under /external-requests for any signed-in person. What a person sees and may do is the API's answer
 * (EXTERNAL_REQUEST_VIEW, _MANAGE; EXTERNAL_CONTRIBUTION_RESPOND, _REVIEW, _APPLY). AHDA's screens are wrapped in
 * InternalOnly; SCR-162 is both audiences', laid out by the record's projection. A project's requests are also its
 * workspace tab, /projects/:projectId/external-requests.
 */
export const externalRequestRoutes: RouteObject[] = [
  {
    index: true, // SCR-160
    element: (
      <InternalOnly>
        <ExternalRequestRegisterPage />
      </InternalOnly>
    ),
  },
  {
    path: 'new', // SCR-161, ?projectId=
    element: (
      <InternalOnly>
        <CreateExternalRequestPage />
      </InternalOnly>
    ),
  },
  {
    path: 'applications', // SCR-166
    element: (
      <InternalOnly>
        <SourceApplicationMonitorPage />
      </InternalOnly>
    ),
  },
  {
    path: 'monitor', // SCR-167
    element: (
      <InternalOnly>
        <ParticipationMonitorPage />
      </InternalOnly>
    ),
  },
  { path: ':requestId', element: <ExternalRequestDetailPage /> }, // SCR-162, both audiences
  {
    path: ':requestId/edit', // SCR-161 for a DRAFT
    element: (
      <InternalOnly>
        <EditExternalRequestPage />
      </InternalOnly>
    ),
  },
];

/** SCR-165, under /external-contributions. */
export const externalContributionRoutes: RouteObject[] = [
  {
    path: ':contributionId',
    element: (
      <InternalOnly>
        <ContributionReviewPage />
      </InternalOnly>
    ),
  },
];

/** SCR-163 and SCR-164, the entity user's own lists. */
export const myParticipationRoutes: RouteObject[] = [
  { path: MY_EXTERNAL_REQUESTS_PATH.slice(1), element: <MyExternalRequestsPage /> },
  { path: MY_CONTRIBUTIONS_PATH.slice(1), element: <MyContributionsPage /> },
];

const INTERNAL_NAVIGATION: NavigationItem[] = [
  { to: EXTERNAL_REQUESTS_PATH, label: 'externalParticipation.nav.register', end: true },
  { to: APPLICATION_MONITOR_PATH, label: 'externalParticipation.nav.applications' },
  { to: PARTICIPATION_MONITOR_PATH, label: 'externalParticipation.nav.monitor' },
];

const EXTERNAL_NAVIGATION: NavigationItem[] = [
  { to: MY_EXTERNAL_REQUESTS_PATH, label: 'externalParticipation.nav.mine' },
  { to: MY_CONTRIBUTIONS_PATH, label: 'externalParticipation.nav.contributions' },
];

/** AHDA's register and monitors, or an entity user's requests and contributions (WF-13 §12.2): never both. */
export function externalParticipationNavigation(user: SessionUser): NavigationItem[] {
  return isInternal(user) ? INTERNAL_NAVIGATION : EXTERNAL_NAVIGATION;
}
