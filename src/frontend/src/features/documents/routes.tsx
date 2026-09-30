import { type RouteObject } from 'react-router';

import { type NavigationItem } from '@/features/identity-access/routes.tsx';

import { DocumentDetailPage } from './detail/DocumentDetailPage.tsx';
import { DocumentLibraryPage, ProjectDocumentsPage } from './library/DocumentLibraryPage.tsx';
import { RecentDocumentsPage } from './library/RecentDocumentsPage.tsx';
import { VersionHistoryPage } from './versions/VersionHistoryPage.tsx';

/**
 * WF-12, mounted under /documents for any signed-in person. No role is checked here: what a person may read, upload
 * or manage is the API's answer on each document (DOCUMENT_VIEW, DOCUMENT_UPLOAD, DOCUMENT_MANAGE; TASK-037 D-6),
 * shown as it comes. MOD-054 Attach has no route: the module that owns the record mounts it (TASK-037 F-8).
 */
export const documentRoutes: RouteObject[] = [
  { index: true, element: <DocumentLibraryPage /> }, // SCR-120, MOD-050
  { path: 'recent', element: <RecentDocumentsPage /> }, // SCR-122
  { path: 'projects/:projectId', element: <ProjectDocumentsPage /> }, // SCR-121, MOD-050
  { path: ':documentId', element: <DocumentDetailPage /> }, // SCR-123, MOD-051, MOD-052, MOD-053
  { path: ':documentId/versions', element: <VersionHistoryPage /> }, // SCR-124
];

export const documentNavigation: NavigationItem[] = [
  { to: '/documents', label: 'documents.nav.library', end: true },
  { to: '/documents/recent', label: 'documents.nav.recent' },
];
