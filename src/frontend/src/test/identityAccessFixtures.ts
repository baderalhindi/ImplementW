import {
  type AccessRelationshipSummary,
  type DepartmentSummary,
  type ExternalEntitySummary,
  type PermissionProfileDetail,
  type PermissionSummary,
  type RoleSummary,
  type UserDetail,
} from '@/features/identity-access/api/types.ts';
import { type Session } from '@/features/identity-access/session/sessionApi.ts';

import { type MockApi, page } from './mockApi.ts';

// Test data only: invented names, example.test addresses and fixed ids. No real person or credential.

export const ADMIN_ID = '00000000-0000-4000-8000-000000000001';
export const DEPARTMENT_ID = '00000000-0000-4000-8000-0000000000d1';
export const ENTITY_ID = '00000000-0000-4000-8000-0000000000e1';
export const ENTITY_TYPE_ID = '00000000-0000-4000-8000-0000000000f1';
export const PROJECT_ID = '00000000-0000-4000-8000-0000000000a1';

export const departments: DepartmentSummary[] = [
  {
    id: DEPARTMENT_ID,
    code: 'PMO',
    name: { ar: 'مكتب إدارة المشاريع', en: 'Project Management Office' },
    parentDepartmentId: null,
    isActive: true,
  },
  {
    id: '00000000-0000-4000-8000-0000000000d2',
    code: 'PMO-QA',
    name: { ar: 'ضمان الجودة', en: 'Quality Assurance' },
    parentDepartmentId: DEPARTMENT_ID,
    isActive: true,
  },
];

export const entities: ExternalEntitySummary[] = [
  {
    id: ENTITY_ID,
    code: 'CONTRACTOR-A',
    name: { ar: 'المقاول أ', en: 'Contractor A' },
    entityTypeItemId: ENTITY_TYPE_ID,
    status: 'ACTIVE',
    sponsorUserId: null,
  },
];

function user(
  overrides: Partial<UserDetail> & Pick<UserDetail, 'id' | 'displayName' | 'username'>,
): UserDetail {
  return {
    userType: 'INTERNAL',
    email: `${overrides.username}@example.test`,
    status: 'ACTIVE',
    departmentId: DEPARTMENT_ID,
    externalEntityId: null,
    directorySubjectId: `dir-${overrides.username}`,
    mobileNumber: null,
    mobileVerifiedAt: null,
    jobTitle: null,
    managerUserId: null,
    preferredLanguage: 'ar',
    disabledAt: null,
    multiFactorEnrolled: false,
    nafathVerifiedAt: null,
    createdAt: '2026-09-01T08:00:00Z',
    createdBy: ADMIN_ID,
    updatedAt: '2026-09-01T08:00:00Z',
    updatedBy: ADMIN_ID,
    ...overrides,
  };
}

export const internalUser = user({
  id: '00000000-0000-4000-8000-000000000010',
  displayName: 'Sara Internal',
  username: 'sara.internal',
});

export const sponsorUser = user({
  id: '00000000-0000-4000-8000-000000000011',
  displayName: 'Omar Sponsor',
  username: 'omar.sponsor',
});

export const externalUser = user({
  id: '00000000-0000-4000-8000-000000000020',
  displayName: 'Lina External',
  username: 'lina.external',
  userType: 'EXTERNAL',
  departmentId: null,
  externalEntityId: ENTITY_ID,
  directorySubjectId: null,
  jobTitle: 'Site engineer',
});

export const users = [internalUser, sponsorUser, externalUser];

function role(code: string, en: string, ar: string, isExternalEligible = false): RoleSummary {
  return { id: `role-${code}`, code, name: { ar, en }, isSystem: true, isExternalEligible };
}

export const roles: RoleSummary[] = [
  role('R01', 'System Administrator', 'مدير النظام'),
  role('R03', 'Department Manager', 'مدير الإدارة'),
  role('R04', 'Project Manager', 'مدير المشروع', true),
];

export const permissions: PermissionSummary[] = [
  {
    id: 'p-user-view',
    code: 'USER_VIEW',
    name: { ar: 'عرض المستخدمين', en: 'View users' },
    permissionGroup: 'IDENTITY_ACCESS',
    isPrivileged: false,
    dataClassificationItemId: null,
  },
  {
    id: 'p-user-manage',
    code: 'USER_MANAGE',
    name: { ar: 'إدارة المستخدمين', en: 'Manage users' },
    permissionGroup: 'IDENTITY_ACCESS',
    isPrivileged: true,
    dataClassificationItemId: null,
  },
];

function profile(
  roleSummary: RoleSummary,
  versionState: 'PUBLISHED' | 'DRAFT',
): PermissionProfileDetail {
  return {
    id: `profile-${roleSummary.code}`,
    code: `${roleSummary.code}_DEFAULT`,
    name: { ar: `الملف الافتراضي ${roleSummary.code}`, en: `${roleSummary.code} default` },
    baseRoleId: roleSummary.id,
    baseRoleCode: roleSummary.code,
    isShippedDefault: true,
    versions: [
      {
        id: `version-${roleSummary.code}-1`,
        versionNo: 1,
        lifecycleState: versionState,
        publishedAt: versionState === 'PUBLISHED' ? '2026-09-01T08:00:00Z' : null,
        retiredAt: null,
        grants:
          roleSummary.code === 'R01'
            ? [
                { permissionId: 'p-user-view', permissionCode: 'USER_VIEW', dataScope: 'ALL' },
                { permissionId: 'p-user-manage', permissionCode: 'USER_MANAGE', dataScope: 'ALL' },
              ]
            : [],
      },
    ],
  };
}

export const profiles = roles.map((r) => profile(r, 'PUBLISHED'));

export const assignment: AccessRelationshipSummary = {
  id: '00000000-0000-4000-8000-0000000000c1',
  userId: internalUser.id,
  roleCode: 'R03',
  permissionProfileVersionId: 'version-R03-1',
  departmentId: DEPARTMENT_ID,
  externalEntityId: null,
  projectId: null,
  sponsorUserId: null,
  startsAt: '2026-09-02T08:00:00Z',
  endsAt: null,
  endReason: null,
  status: 'ACTIVE',
};

export function sessionFor(roleCodes: string[]): Session {
  return {
    accessToken: 'test-access-token',
    accessTokenExpiresAt: '2099-01-01T00:00:00Z',
    refreshToken: 'test-refresh-token',
    refreshTokenExpiresAt: '2099-01-01T00:00:00Z',
    sessionExpiresAt: '2099-01-01T00:00:00Z',
    user: {
      id: ADMIN_ID,
      userType: 'INTERNAL',
      username: 'test.admin',
      displayName: 'Test Administrator',
      preferredLanguage: 'en',
      multiFactorAuthenticated: true,
      authenticatedAt: '2026-09-27T09:00:00Z',
      roleAssignments: roleCodes.map((roleCode) => ({
        roleCode,
        permissionProfileVersionId: `version-${roleCode}-1`,
        departmentId: null,
        externalEntityId: null,
        projectId: null,
      })),
    },
  };
}

/** The read side of FG-03 over the fixtures: enough for every ADM screen to render. */
export function withIdentityAccessData(api: MockApi): MockApi {
  return api
    .on('GET', /^\/departments$/, { body: page(departments) })
    .on('GET', /^\/external-entities$/, { body: page(entities) })
    .on('GET', /^\/users$/, (request) => {
      const q = request.query.get('q')?.toLowerCase();
      const userType = request.query.get('userType');
      return {
        body: page(
          users.filter(
            (u) =>
              (q === undefined || u.displayName.toLowerCase().includes(q)) &&
              (userType === null || u.userType === userType),
          ),
        ),
      };
    })
    .on('GET', /^\/users\/[^/]+$/, (request) => {
      const found = users.find((u) => request.path.endsWith(u.id));
      return found === undefined
        ? { status: 404, body: { code: 'NOT_FOUND' } }
        : { body: found, headers: { ETag: '"7"' } };
    })
    .on('GET', /^\/access-relationships$/, { body: page([assignment]) })
    .on('GET', /^\/roles$/, { body: roles })
    .on('GET', /^\/roles\/[^/]+$/, (request) => {
      const found = roles.find((r) => request.path.endsWith(r.id)) ?? roles[0];
      return {
        body: {
          ...found,
          profiles: profiles.filter((p) => p.baseRoleId === found?.id),
          updatedAt: '2026-09-01T08:00:00Z',
          updatedBy: ADMIN_ID,
        },
        headers: { ETag: '"3"' },
      };
    })
    .on('GET', /^\/permissions$/, { body: permissions })
    .on('GET', /^\/permission-profiles$/, { body: page(profiles, 200) })
    .on('GET', /^\/permission-profiles\/[^/]+$/, (request) => ({
      body: profiles.find((p) => request.path.endsWith(p.id)),
    }));
}
