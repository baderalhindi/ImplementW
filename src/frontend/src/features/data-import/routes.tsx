import { type RouteObject } from 'react-router';

import { type NavigationItem } from '@/features/identity-access/routes.tsx';

import { DataMigrationPage } from './DataMigrationPage.tsx';

/** TASK-102, mounted under /admin behind the administration guard, offered on DATA_IMPORT. */
export const dataImportRoutes: RouteObject[] = [
  { path: 'data-migration', element: <DataMigrationPage /> },
];

export const dataImportNavigation: NavigationItem[] = [
  { to: '/admin/data-migration', label: 'dataImport.nav', permissions: ['DATA_IMPORT'] },
];
