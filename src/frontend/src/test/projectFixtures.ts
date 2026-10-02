import {
  type DepartmentSummary,
  type ExternalEntitySummary,
} from '@/features/identity-access/api/types.ts';
import {
  type SessionRoleAssignment,
  type Session,
} from '@/features/identity-access/session/sessionApi.ts';
import {
  type GovernanceProfileSettings,
  type ProjectDetail,
  type ProjectSummary,
} from '@/features/projects/api/types.ts';
import { type MasterDataItemSummary } from '@/shared/api/masterData.ts';

import { sessionFor } from './identityAccessFixtures.ts';
import { type MockApi, page, problem } from './mockApi.ts';

// Test data only: invented names and fixed ids. Each id starts differently, so a shortened id is recognisable.

export const ENTITY_USER_ID = '1a1a1a1a-0000-4000-8000-000000000101';
export const REVIEWER_ID = '2b2b2b2b-0000-4000-8000-000000000102';
export const OTHER_PERSON_ID = '3c3c3c3c-0000-4000-8000-000000000103';
export const PROJECT_ID = '4d4d4d4d-0000-4000-8000-000000000104';
export const DEPARTMENT_ID = '5e5e5e5e-0000-4000-8000-000000000105';
export const OTHER_DEPARTMENT_ID = '5e5e5e5e-0000-4000-8000-000000000106';
export const ENTITY_ID = '6f6f6f6f-0000-4000-8000-000000000107';
export const CLASSIFICATION_ID = '7a000000-0000-4000-8000-000000000201';
export const LIGHT_PROFILE_ID = '7b000000-0000-4000-8000-000000000202';
export const FULL_PROFILE_ID = '7b000000-0000-4000-8000-000000000203';
export const RIYADH_REGION_ID = '7c000000-0000-4000-8000-000000000204';
export const MAKKAH_REGION_ID = '7c000000-0000-4000-8000-000000000205';
export const RIYADH_CITY_ID = '7d000000-0000-4000-8000-000000000206';
export const JEDDAH_CITY_ID = '7d000000-0000-4000-8000-000000000207';
export const RUN_ID = '8e8e8e8e-0000-4000-8000-000000000301';

export const ETAG = '"11"';

function assignment(overrides: Partial<SessionRoleAssignment>): SessionRoleAssignment {
  return {
    roleCode: 'R04',
    permissionProfileVersionId: 'version-1',
    departmentId: null,
    externalEntityId: null,
    projectId: null,
    ...overrides,
  };
}

function withUser(
  session: Session,
  user: Partial<Session['user']>,
  assignments: SessionRoleAssignment[],
): Session {
  return { ...session, user: { ...session.user, ...user, roleAssignments: assignments } };
}

/** An external entity's contributor (R08), the ADR-013 registrant: an external user anchored to their entity. */
export function entitySession(): Session {
  return withUser(
    sessionFor([]),
    { id: ENTITY_USER_ID, userType: 'EXTERNAL', displayName: 'Huda Contractor' },
    [assignment({ roleCode: 'R08', externalEntityId: ENTITY_ID })],
  );
}

/** An AHDA department manager (R03) over the projects' department: internal and reached. */
export function reviewerSession(departmentId = DEPARTMENT_ID): Session {
  return withUser(
    sessionFor([]),
    { id: REVIEWER_ID, userType: 'INTERNAL', displayName: 'Faisal Reviewer' },
    [assignment({ roleCode: 'R03', departmentId })],
  );
}

export function projectDetail(overrides: Partial<ProjectDetail> = {}): ProjectDetail {
  return {
    id: PROJECT_ID,
    formalProjectId: null,
    title: { text: 'Coastal road upgrade', language: 'EN' },
    description: { text: 'Widening the coastal road.', language: 'EN' },
    classificationItemId: CLASSIFICATION_ID,
    departmentId: DEPARTMENT_ID,
    externalEntityId: ENTITY_ID,
    projectManagerUserId: null,
    status: 'DRAFT',
    participationMode: 'ENTITY_MANAGED',
    legacyIntakeDate: null,
    updatedAt: '2026-10-01T09:00:00Z',
    revisionNo: 1,
    governanceProfileItemId: LIGHT_PROFILE_ID,
    registrationBudgetSar: '1500000.00',
    plannedStartDate: '2026-11-01',
    plannedEndDate: '2027-10-31',
    regionItemId: null,
    cityItemId: null,
    latitude: null,
    longitude: null,
    activatedAt: null,
    createdAt: '2026-09-30T09:00:00Z',
    createdBy: ENTITY_USER_ID,
    updatedBy: ENTITY_USER_ID,
    ...overrides,
  };
}

export function projectSummary(overrides: Partial<ProjectSummary> = {}): ProjectSummary {
  const detail = projectDetail();
  return {
    id: detail.id,
    formalProjectId: detail.formalProjectId,
    title: detail.title,
    classificationItemId: detail.classificationItemId,
    departmentId: detail.departmentId,
    externalEntityId: detail.externalEntityId,
    projectManagerUserId: detail.projectManagerUserId,
    status: detail.status,
    participationMode: detail.participationMode,
    legacyIntakeDate: detail.legacyIntakeDate,
    updatedAt: detail.updatedAt,
    ...overrides,
  };
}

function item(
  id: string,
  catalogueId: string,
  code: string,
  en: string,
  ar: string,
  parentItemId: string | null = null,
): MasterDataItemSummary {
  return {
    id,
    catalogueId,
    code,
    label: { en, ar },
    parentItemId,
    sortOrder: 1,
    lifecycleState: 'PUBLISHED',
    isSystem: false,
  };
}

const CATALOGUES = [
  { id: 'c0000000-0000-4000-8000-000000000001', code: 'PROJECT_CLASSIFICATION' },
  { id: 'c0000000-0000-4000-8000-000000000002', code: 'GOVERNANCE_PROFILE' },
  { id: 'c0000000-0000-4000-8000-000000000003', code: 'REGION' },
  { id: 'c0000000-0000-4000-8000-000000000004', code: 'CITY' },
];

const ITEMS: Record<string, MasterDataItemSummary[]> = {
  'c0000000-0000-4000-8000-000000000001': [
    item(CLASSIFICATION_ID, 'c1', 'INFRASTRUCTURE', 'Infrastructure', 'بنية تحتية'),
  ],
  'c0000000-0000-4000-8000-000000000002': [
    item(LIGHT_PROFILE_ID, 'c2', 'LIGHT', 'Light', 'مبسّط'),
    item(FULL_PROFILE_ID, 'c2', 'FULL', 'Full', 'شامل'),
  ],
  'c0000000-0000-4000-8000-000000000003': [
    item(RIYADH_REGION_ID, 'c3', 'RIYADH', 'Riyadh Region', 'منطقة الرياض'),
    item(MAKKAH_REGION_ID, 'c3', 'MAKKAH', 'Makkah Region', 'منطقة مكة المكرمة'),
  ],
  'c0000000-0000-4000-8000-000000000004': [
    item(RIYADH_CITY_ID, 'c4', 'RIYADH', 'Riyadh', 'الرياض', RIYADH_REGION_ID),
    item(JEDDAH_CITY_ID, 'c4', 'JEDDAH', 'Jeddah', 'جدة', MAKKAH_REGION_ID),
  ],
};

export const departments: DepartmentSummary[] = [
  {
    id: DEPARTMENT_ID,
    code: 'ROADS',
    name: { en: 'Roads Department', ar: 'إدارة الطرق' },
    parentDepartmentId: null,
    isActive: true,
  },
  {
    id: OTHER_DEPARTMENT_ID,
    code: 'PARKS',
    name: { en: 'Parks Department', ar: 'إدارة الحدائق' },
    parentDepartmentId: null,
    isActive: true,
  },
];

export const entities: ExternalEntitySummary[] = [
  {
    id: ENTITY_ID,
    code: 'BUILDCO',
    name: { en: 'Build Co', ar: 'شركة البناء' },
    entityTypeItemId: 'f0000000-0000-4000-8000-000000000001',
    status: 'ACTIVE',
    sponsorUserId: null,
  },
];

/** ADR-015: LIGHT asks for nothing more; FULL also needs a region and a description before submission. */
export const PROFILE_SETTINGS: GovernanceProfileSettings[] = [
  {
    governanceProfileItemId: LIGHT_PROFILE_ID,
    assignmentMinBudgetSar: null,
    assignmentMinDurationDays: null,
    mandatoryFieldCodes: [],
  },
  {
    governanceProfileItemId: FULL_PROFILE_ID,
    assignmentMinBudgetSar: 50000000,
    assignmentMinDurationDays: 365,
    mandatoryFieldCodes: ['REGION_ITEM_ID', 'DESCRIPTION', 'UNRELATED_CODE'],
  },
];

const PEOPLE: Record<string, string> = {
  [ENTITY_USER_ID]: 'Huda Contractor',
  [REVIEWER_ID]: 'Faisal Reviewer',
  [OTHER_PERSON_ID]: 'Nora Manager',
};

interface LookupOptions {
  /** MASTER_DATA_VIEW; false is 403, as today for everyone but R01 (F-1). */
  catalogues?: boolean;
  /** ORGANIZATION_VIEW. */
  organization?: boolean;
  /** CONFIGURATION_VIEW. */
  profiles?: boolean;
  /** USER_VIEW. */
  people?: boolean;
}

/** The FG-03 and FG-04 reads the WF-01 screens make for names and choices. Each can be refused on its own. */
export function withProjectLookups(
  api: MockApi,
  { catalogues = true, organization = true, profiles = true, people = true }: LookupOptions = {},
): MockApi {
  const forbidden = problem(403, 'PERMISSION_DENIED');
  api
    .on('GET', /^\/master-data-catalogues$/, catalogues ? { body: CATALOGUES } : forbidden)
    .on('GET', /^\/master-data-items$/, (request) => {
      const items = ITEMS[request.query.get('catalogueId') ?? ''] ?? [];
      return { body: page(items, 200) };
    })
    .on('GET', /^\/departments$/, organization ? { body: page(departments) } : forbidden)
    .on('GET', /^\/external-entities$/, organization ? { body: page(entities) } : forbidden)
    .on(
      'GET',
      /^\/configuration-resolutions\/GOVERNANCE_PROFILE$/,
      profiles
        ? {
            body: {
              versionId: 'v1',
              familyCode: 'GOVERNANCE_PROFILE',
              versionNo: 1,
              content: { governanceProfiles: PROFILE_SETTINGS },
            },
          }
        : forbidden,
    )
    .on('GET', /^\/users\/[^/]+$/, (request) => {
      const id = request.path.split('/').at(-1) ?? '';
      const displayName = PEOPLE[id];
      return !people
        ? forbidden
        : displayName === undefined
          ? problem(404, 'NOT_FOUND')
          : { body: { id, displayName, username: 'x', userType: 'INTERNAL' } };
    });
  return api;
}

/** GET /projects/{id} answering the project with its ETag. */
export function withProject(api: MockApi, project: ProjectDetail, etag = ETAG): MockApi {
  return api.on('GET', new RegExp(`^/projects/${project.id}$`), {
    body: project,
    headers: { ETag: etag },
  });
}
