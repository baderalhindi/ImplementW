import { type RouteObject } from 'react-router';

import { type TranslationKey } from '@/shared/i18n/i18n.ts';

import { RoleAssignmentPage } from './assignments/RoleAssignmentPage.tsx';
import { DepartmentListPage } from './organization/DepartmentListPage.tsx';
import { ExternalEntityListPage } from './organization/ExternalEntityListPage.tsx';
import { OrganizationStructurePage } from './organization/OrganizationStructurePage.tsx';
import { PermissionMatrixPage } from './roles/PermissionMatrixPage.tsx';
import { RoleDetailPage, RoleEditPage, RoleListPage } from './roles/RolePages.tsx';
import { UserDetailPage } from './users/UserDetailPage.tsx';
import { UserCreatePage, UserEditPage } from './users/UserFormPage.tsx';
import { UserListPage } from './users/UserListPage.tsx';

/** FG-03 administration, mounted under /admin behind the R01 guard (TASK-032 acceptance criterion 1). */
export const identityAccessRoutes: RouteObject[] = [
  { path: 'users', element: <UserListPage /> }, // ADM-002
  { path: 'users/new', element: <UserCreatePage /> }, // ADM-004
  { path: 'users/:userId', element: <UserDetailPage /> }, // ADM-003, MOD-080, MOD-081, MOD-082
  { path: 'users/:userId/edit', element: <UserEditPage /> }, // ADM-005
  { path: 'roles', element: <RoleListPage /> }, // ADM-006
  { path: 'roles/:roleId', element: <RoleDetailPage /> }, // ADM-007
  { path: 'roles/:roleId/edit', element: <RoleEditPage /> }, // ADM-008
  { path: 'permission-matrix', element: <PermissionMatrixPage /> }, // ADM-009
  { path: 'role-assignments', element: <RoleAssignmentPage /> }, // ADM-010, MOD-081, MOD-082
  { path: 'organization', element: <OrganizationStructurePage /> }, // ADM-011
  { path: 'organization/departments', element: <DepartmentListPage /> }, // ADM-012
  { path: 'organization/entities', element: <ExternalEntityListPage /> }, // ADM-013
];

export interface NavigationItem {
  to: string;
  label: TranslationKey;
  /** Exact match only, so /admin/organization is not highlighted on its sub-pages. */
  end?: boolean;
}

export const identityAccessNavigation: NavigationItem[] = [
  { to: '/admin/users', label: 'identityAccess.nav.users' },
  { to: '/admin/roles', label: 'identityAccess.nav.roles' },
  { to: '/admin/permission-matrix', label: 'identityAccess.nav.permissionMatrix' },
  { to: '/admin/role-assignments', label: 'identityAccess.nav.roleAssignments' },
  { to: '/admin/organization', label: 'identityAccess.nav.organization', end: true },
  { to: '/admin/organization/departments', label: 'identityAccess.nav.departments' },
  { to: '/admin/organization/entities', label: 'identityAccess.nav.entities' },
];
