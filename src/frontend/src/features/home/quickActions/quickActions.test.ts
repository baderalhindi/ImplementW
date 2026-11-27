import { describe, expect, test } from 'vitest';

import { menusFor } from '@/app/menus.ts';
import { type AuditAccessView } from '@/features/audit-activity/api/types.ts';
import { type DashboardCatalogueEntry } from '@/features/dashboards/api/types.ts';
import { type PermissionCode } from '@/features/identity-access/permissions/permissionCodes.ts';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { type MenuScreens } from '@/shared/navigation/menuScreens.ts';

import {
  availableQuickActions,
  defaultQuickActionCodes,
  type QuickActionContext,
  quickActionGroups,
  screenCode,
  shownQuickActions,
} from './quickActions.ts';

// The banner's quick actions (21 Nov 2026: "add all the possible options, for each user all the buttons of their user, and organize them"): what
// the person may create or submit first, then each of their menus' screens under the menu's name.

const holding =
  (...held: PermissionCode[]) =>
  (...codes: readonly PermissionCode[]) =>
    codes.length === 0 || codes.some((code) => held.includes(code));

const MENUS: MenuScreens[] = [
  {
    label: 'common.menu.projects',
    screens: [
      { to: '/projects', label: 'projects.nav.register' },
      { to: '/projects/new', label: 'projects.nav.create' },
      { to: '/projects/mine', label: 'projects.nav.mine' },
    ],
  },
  {
    label: 'common.menu.administration',
    screens: [{ to: '/admin/organization/entities', label: 'identityAccess.nav.entities' }],
  },
];

const context = (...held: PermissionCode[]): QuickActionContext => ({
  can: holding(...held),
  menus: MENUS,
});

describe('the quick actions catalogue', () => {
  test('lists what the person may create or submit first, then each menu with its screens, in the menus’ order', () => {
    const groups = quickActionGroups(
      availableQuickActions(context('PROJECT_REGISTER', 'RISK_MANAGE')),
    );

    expect(groups.map(({ group, actions }) => [group.label, actions.map((a) => a.code)])).toEqual([
      ['home.quickActions.groups.create', ['createProject', 'registerRisk']],
      ['common.menu.projects', ['projectRegister', 'projectsMine']],
      ['common.menu.administration', ['adminOrganizationEntities']],
    ]);
  });

  test('offers a create action only to someone allowed to do it, and a screen a create action opens only once', () => {
    const codes = availableQuickActions(context()).map((entry) => entry.code);

    expect(codes).not.toContain('createProject');
    expect(codes).not.toContain('projectsNew');
    expect(codes).toEqual(['projectRegister', 'projectsMine', 'adminOrganizationEntities']);
  });

  test('a choice saved before keeps its buttons, and a new screen can be chosen; what the person may no longer open is left out', () => {
    const shown = shownQuickActions(
      ['adminOrganizationEntities', 'projectRegister', 'createProject', 'approvalsInbox'],
      context(),
    );

    expect(shown.map((entry) => entry.code)).toEqual([
      'adminOrganizationEntities',
      'projectRegister',
    ]);
  });

  test('the role’s set is what it was, of the actions the person may do', () => {
    expect(defaultQuickActionCodes(context('PROJECT_VIEW', 'PROJECT_REGISTER'))).toEqual([
      'createProject',
      'projectRegister',
    ]);
  });

  test('an administrator with no business data has the administration’s ways in as their set', () => {
    const admin: QuickActionContext = {
      can: holding('USER_MANAGE', 'ROLE_ASSIGN'),
      menus: [
        {
          label: 'common.menu.administration',
          screens: [
            { to: '/admin/role-assignments', label: 'identityAccess.nav.roleAssignments' },
            { to: '/admin/data-migration', label: 'dataImport.nav' },
          ],
        },
        {
          label: 'common.menu.notifications',
          screens: [{ to: '/notifications/preferences', label: 'notifications.nav.preferences' }],
        },
      ],
    };

    expect(defaultQuickActionCodes(admin)).toEqual([
      'createUser',
      'adminRoleAssignments',
      'adminDataMigration',
      'notificationPreferences',
    ]);
  });
});

describe('every screen of the real menus is offered, with a code the API keeps', () => {
  const user = (userType: SessionUser['userType'], role: string): SessionUser => ({
    id: 'user-1',
    userType,
    username: 'someone',
    displayName: 'Someone',
    preferredLanguage: 'en',
    authenticationMethod: 'DIRECTORY',
    multiFactorAuthenticated: true,
    authenticatedAt: '2026-11-21T09:00:00Z',
    roleAssignments: [{ roleCode: role } as SessionUser['roleAssignments'][number]],
  });
  const everything = {
    loaded: true,
    can: () => true,
    scopesOf: () => [],
    showCreateInHeader: true,
  };
  const audit: AuditAccessView = {
    categories: [
      'ADMINISTRATION',
      'SECURITY',
      'INTEGRATION',
      'BUSINESS',
    ] as AuditAccessView['categories'],
    requestContextVisible: true,
    activityVisible: true,
    operationsPermitted: true,
  };
  const portfolio = [{ code: 'PORTFOLIO' } as DashboardCatalogueEntry];

  test.each([
    ['an AHDA user', user('INTERNAL', 'R01')],
    ['the Executive', user('INTERNAL', 'R07')],
    ['someone outside AHDA', user('EXTERNAL', 'R08')],
  ])('for %s', (_, who) => {
    const menus = menusFor(who, everything, audit, portfolio).map((menu) => ({
      label: menu.label,
      screens: menu.items.map((item) => ({ to: item.to, label: item.label })),
    }));
    const actions = availableQuickActions({ can: () => true, menus });
    const codes = actions.map((entry) => entry.code);

    // Each screen of each menu is there, under its menu (Create project and Import projects under Create and submit).
    for (const menu of menus) {
      for (const screen of menu.screens) {
        expect(
          actions.some((entry) => 'to' in entry.target && entry.target.to === screen.to),
          screen.to,
        ).toBe(true);
      }
    }
    expect(new Set(codes).size).toBe(codes.length);
    for (const code of codes) {
      expect(code).toMatch(/^[a-z][A-Za-z]{0,39}$/);
    }
  });

  test('a screen’s code is its path in camel case', () => {
    expect(screenCode('/external-requests/batches')).toBe('externalRequestsBatches');
    expect(screenCode('/projects')).toBe('projectRegister');
  });
});
