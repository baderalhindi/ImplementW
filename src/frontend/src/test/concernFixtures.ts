import { type Session } from '@/features/identity-access/session/sessionApi.ts';
import {
  type ConcernDetail,
  type ConcernEscalationDetail,
} from '@/features/issues-challenges/api/types.ts';
import { type ProjectDetail, type ProjectSummary } from '@/features/projects/api/types.ts';
import { type RiskMatrixResolution } from '@/features/risks/api/types.ts';
import { type MasterDataItemSummary } from '@/shared/api/masterData.ts';

import { sessionFor } from './identityAccessFixtures.ts';
import { type MockApi, page, problem } from './mockApi.ts';
import {
  CATALOGUES,
  DEPARTMENT_ID,
  ENTITY_ID,
  ENTITY_USER_ID,
  ITEMS,
  OTHER_PERSON_ID,
  projectDetail,
  projectSummary,
  PROJECT_ID,
  REVIEWER_ID,
} from './projectFixtures.ts';
import { COST_DIMENSION_ID, SCHEDULE_DIMENSION_ID, standardMatrix } from './riskFixtures.ts';
import { day, OTHER_PROJECT_ID } from './taskFixtures.ts';

// Test data only: fixed ids, each starting differently so a shortened id is recognisable. Dates are relative to today.
//
// The Coastal road upgrade (PROJECT_ID), ACTIVE, managed by Nora (internal, R04); its delivering entity's user is Huda
// (R08); Faisal is the department's manager (R03), the role escalations are addressed to.
//   Issue "Collapsed culvert" — ASSIGNED to Huda, cost impact 4 → MAJOR, priority Low, target two days ago (overdue);
//     escalation 1 RESOLVED by Faisal, escalation 2 OPEN (raised by Nora).
//   Issue "Missing excavation permit" — OPEN, unassessed, priority High.
//   Issue "Damaged fence" — CLOSED, cost impact 2 → MINOR.
//   Challenge "Utility approval pending" — IN_PROGRESS, schedule impact 5 → CRITICAL; escalation 1 WITHDRAWN.
// The Harbour bridge repair (OTHER_PROJECT_ID), ACTIVE: issue "Crane breakdown" — OPEN, unassessed.

export const CULVERT_ID = 'c1000000-0000-4000-8000-000000000a01';
export const PERMIT_ID = 'c1000000-0000-4000-8000-000000000a02';
export const FENCE_ID = 'c1000000-0000-4000-8000-000000000a03';
export const UTILITY_CHALLENGE_ID = 'c1000000-0000-4000-8000-000000000a04';
export const CRANE_ID = 'c2000000-0000-4000-8000-000000000a05';
export const CULVERT_OPEN_ESCALATION_ID = 'e1000000-0000-4000-8000-000000000b01';
export const CULVERT_RESOLVED_ESCALATION_ID = 'e1000000-0000-4000-8000-000000000b02';
export const UTILITY_WITHDRAWN_ESCALATION_ID = 'e1000000-0000-4000-8000-000000000b03';
export const DM_ROLE_ID = 'd3000000-0000-4000-8000-000000000c03';
export const CONCERN_ETAG = '"81"';

export const CONCERN_CATALOGUE_ID = 'c0000000-0000-4000-8000-000000000071';
export const PRIORITY_CATALOGUE_ID = 'c0000000-0000-4000-8000-000000000072';
export const SEVERITY_CATALOGUE_ID = 'c0000000-0000-4000-8000-000000000073';
export const DIMENSION_CATALOGUE_ID = 'c0000000-0000-4000-8000-000000000074';
export const SITE_CATEGORY_ID = 'a7000000-0000-4000-8000-000000000f01';
export const STAKEHOLDER_CATEGORY_ID = 'a7000000-0000-4000-8000-000000000f02';
export const HIGH_PRIORITY_ID = 'a8000000-0000-4000-8000-000000000f03';
export const LOW_PRIORITY_ID = 'a8000000-0000-4000-8000-000000000f04';
export const MINOR_SEVERITY_ID = 'a9000000-0000-4000-8000-000000000f05';
export const MAJOR_SEVERITY_ID = 'a9000000-0000-4000-8000-000000000f06';
export const CRITICAL_SEVERITY_ID = 'a9000000-0000-4000-8000-000000000f07';

/** The project's internal Project Manager (R04 at OWN): raises, manages and escalates (TASK-057 D-8). */
export function managerSession(): Session {
  const base = sessionFor([]);
  return {
    ...base,
    user: {
      ...base.user,
      id: OTHER_PERSON_ID,
      userType: 'INTERNAL',
      displayName: 'Nora Manager',
      roleAssignments: [
        {
          roleCode: 'R04',
          permissionProfileVersionId: 'version-1',
          departmentId: null,
          externalEntityId: null,
          projectId: PROJECT_ID,
        },
      ],
    },
  };
}

/** The ACTIVE Coastal road upgrade, managed by Nora, delivered by Huda's entity. */
export function concernProject(overrides: Partial<ProjectDetail> = {}): ProjectDetail {
  return projectDetail({
    status: 'ACTIVE',
    projectManagerUserId: OTHER_PERSON_ID,
    departmentId: DEPARTMENT_ID,
    externalEntityId: ENTITY_ID,
    activatedAt: '2026-10-01T10:00:00Z',
    ...overrides,
  });
}

function at(days: number): string {
  return `${day(days)}T09:00:00Z`;
}

export function escalation(
  overrides: Partial<ConcernEscalationDetail> = {},
): ConcernEscalationDetail {
  return {
    id: CULVERT_OPEN_ESCALATION_ID,
    managementConcernId: CULVERT_ID,
    escalationNo: 2,
    escalatedByUserId: OTHER_PERSON_ID,
    escalatedAt: at(-3),
    escalatedToRoleId: DM_ROLE_ID,
    reason: {
      text: 'The culvert blocks the haul road; we need a budget decision.',
      language: 'EN',
    },
    status: 'OPEN',
    resolvedAt: null,
    resolvedByUserId: null,
    resolution: null,
    updatedAt: at(-3),
    ...overrides,
  };
}

export function concern(overrides: Partial<ConcernDetail> = {}): ConcernDetail {
  return {
    id: CULVERT_ID,
    projectId: PROJECT_ID,
    concernType: 'ISSUE',
    title: { text: 'Collapsed culvert', language: 'EN' },
    description: { text: 'The culvert under chainage 4+200 collapsed after rain.', language: 'EN' },
    categoryItemId: SITE_CATEGORY_ID,
    priorityItemId: LOW_PRIORITY_ID,
    overallImpactLevel: 4,
    severityItemId: MAJOR_SEVERITY_ID,
    severityConfigurationVersionId: standardMatrix().versionId,
    impacts: [{ impactDimensionItemId: COST_DIMENSION_ID, impactLevel: 4, rationale: null }],
    status: 'ASSIGNED',
    revisionNo: 1,
    raisedByUserId: OTHER_PERSON_ID,
    raisedAt: at(-10),
    assigneeUserId: ENTITY_USER_ID,
    originatingRiskId: null,
    targetResolutionDate: day(-2),
    nextReviewDate: day(5),
    lastReviewedAt: null,
    resolution: null,
    resolvedAt: null,
    closedAt: null,
    openEscalation: escalation(),
    createdAt: at(-10),
    createdBy: OTHER_PERSON_ID,
    updatedAt: at(-3),
    updatedBy: OTHER_PERSON_ID,
    ...overrides,
  };
}

export function coastalConcerns(): ConcernDetail[] {
  return [
    concern({
      id: PERMIT_ID,
      title: { text: 'Missing excavation permit', language: 'EN' },
      categoryItemId: STAKEHOLDER_CATEGORY_ID,
      priorityItemId: HIGH_PRIORITY_ID,
      overallImpactLevel: null,
      severityItemId: null,
      severityConfigurationVersionId: null,
      impacts: [],
      status: 'OPEN',
      assigneeUserId: null,
      targetResolutionDate: day(20),
      openEscalation: null,
    }),
    concern(),
    concern({
      id: FENCE_ID,
      title: { text: 'Damaged fence', language: 'EN' },
      overallImpactLevel: 2,
      severityItemId: MINOR_SEVERITY_ID,
      impacts: [{ impactDimensionItemId: COST_DIMENSION_ID, impactLevel: 2, rationale: null }],
      status: 'CLOSED',
      targetResolutionDate: day(-20),
      resolution: { text: 'Fence replaced.', language: 'EN' },
      resolvedAt: at(-6),
      closedAt: at(-5),
      openEscalation: null,
    }),
    concern({
      id: UTILITY_CHALLENGE_ID,
      concernType: 'CHALLENGE',
      title: { text: 'Utility approval pending', language: 'EN' },
      categoryItemId: STAKEHOLDER_CATEGORY_ID,
      priorityItemId: HIGH_PRIORITY_ID,
      overallImpactLevel: 5,
      severityItemId: CRITICAL_SEVERITY_ID,
      impacts: [{ impactDimensionItemId: SCHEDULE_DIMENSION_ID, impactLevel: 5, rationale: null }],
      status: 'IN_PROGRESS',
      targetResolutionDate: day(30),
      openEscalation: null,
    }),
  ];
}

export function harbourConcerns(): ConcernDetail[] {
  return [
    concern({
      id: CRANE_ID,
      projectId: OTHER_PROJECT_ID,
      title: { text: 'Crane breakdown', language: 'EN' },
      overallImpactLevel: null,
      severityItemId: null,
      severityConfigurationVersionId: null,
      impacts: [],
      status: 'OPEN',
      assigneeUserId: null,
      targetResolutionDate: null,
      openEscalation: null,
    }),
  ];
}

export function concernEscalations(): ConcernEscalationDetail[] {
  return [
    escalation(),
    escalation({
      id: CULVERT_RESOLVED_ESCALATION_ID,
      escalationNo: 1,
      escalatedAt: at(-9),
      reason: { text: 'Contractor disputes who pays for the culvert.', language: 'EN' },
      status: 'RESOLVED',
      resolvedAt: at(-7),
      resolvedByUserId: REVIEWER_ID,
      resolution: { text: 'The contractor pays; AHDA covers the survey.', language: 'EN' },
      updatedAt: at(-7),
    }),
    escalation({
      id: UTILITY_WITHDRAWN_ESCALATION_ID,
      managementConcernId: UTILITY_CHALLENGE_ID,
      escalationNo: 1,
      escalatedAt: at(-8),
      reason: { text: 'The utility has not answered in a month.', language: 'EN' },
      status: 'WITHDRAWN',
      resolvedAt: at(-6),
      resolvedByUserId: OTHER_PERSON_ID,
      updatedAt: at(-6),
    }),
  ];
}

export function concernProjects(): ProjectSummary[] {
  return [
    projectSummary({ status: 'ACTIVE', projectManagerUserId: OTHER_PERSON_ID }),
    projectSummary({
      id: OTHER_PROJECT_ID,
      title: { text: 'Harbour bridge repair', language: 'EN' },
      status: 'ACTIVE',
      projectManagerUserId: REVIEWER_ID,
    }),
  ];
}

function catalogueItem(id: string, catalogueId: string, code: string, en: string, ar: string) {
  return {
    id,
    catalogueId,
    code,
    label: { en, ar },
    parentItemId: null,
    sortOrder: 1,
    lifecycleState: 'PUBLISHED',
    isSystem: false,
  } satisfies MasterDataItemSummary;
}

const CONCERN_CATALOGUES = [
  { id: CONCERN_CATALOGUE_ID, code: 'CONCERN_CATEGORY' },
  { id: PRIORITY_CATALOGUE_ID, code: 'PRIORITY' },
  { id: SEVERITY_CATALOGUE_ID, code: 'CONCERN_SEVERITY' },
  { id: DIMENSION_CATALOGUE_ID, code: 'IMPACT_DIMENSION' },
];

const CONCERN_ITEMS: Record<string, MasterDataItemSummary[]> = {
  [CONCERN_CATALOGUE_ID]: [
    catalogueItem(SITE_CATEGORY_ID, CONCERN_CATALOGUE_ID, 'SITE', 'Site conditions', 'ظروف الموقع'),
    catalogueItem(
      STAKEHOLDER_CATEGORY_ID,
      CONCERN_CATALOGUE_ID,
      'STAKEHOLDER',
      'Stakeholder',
      'أصحاب المصلحة',
    ),
  ],
  [PRIORITY_CATALOGUE_ID]: [
    catalogueItem(HIGH_PRIORITY_ID, PRIORITY_CATALOGUE_ID, 'HIGH', 'High', 'عالية'),
    catalogueItem(LOW_PRIORITY_ID, PRIORITY_CATALOGUE_ID, 'LOW', 'Low', 'منخفضة'),
  ],
  [SEVERITY_CATALOGUE_ID]: [
    catalogueItem(MINOR_SEVERITY_ID, SEVERITY_CATALOGUE_ID, 'MINOR', 'Minor', 'طفيفة'),
    catalogueItem(MAJOR_SEVERITY_ID, SEVERITY_CATALOGUE_ID, 'MAJOR', 'Major', 'كبيرة'),
    catalogueItem(CRITICAL_SEVERITY_ID, SEVERITY_CATALOGUE_ID, 'CRITICAL', 'Critical', 'حرجة'),
  ],
  [DIMENSION_CATALOGUE_ID]: [
    catalogueItem(COST_DIMENSION_ID, DIMENSION_CATALOGUE_ID, 'COST', 'Cost', 'التكلفة'),
    catalogueItem(
      SCHEDULE_DIMENSION_ID,
      DIMENSION_CATALOGUE_ID,
      'SCHEDULE',
      'Schedule',
      'الجدول الزمني',
    ),
  ],
};

export interface ConcernState {
  projects?: ProjectSummary[];
  concerns?: ConcernDetail[];
  escalations?: ConcernEscalationDetail[];
  /** The RISK_MATRIX resolution, or how it is refused: 403 or 422. */
  matrix?: RiskMatrixResolution | 'forbidden' | 'missing';
  /** MASTER_DATA_VIEW; false is 403. */
  catalogues?: boolean;
  /** ROLE_VIEW; false is 403, as for everyone but R01. */
  roles?: boolean;
  /** PROJECT_VIEW on a single project read outside the workspace (SCR-088); false is 403. */
  projectReadable?: boolean;
}

/**
 * The WF-07 reads, each filtered as the API does. Layer over withProjectLookups (people's names) and withProject (the
 * workspace's project).
 */
export function withConcerns(
  api: MockApi,
  {
    projects = concernProjects(),
    concerns = [...coastalConcerns(), ...harbourConcerns()],
    escalations = concernEscalations(),
    matrix = standardMatrix(),
    catalogues = true,
    roles = true,
    projectReadable = true,
  }: ConcernState = {},
): MockApi {
  const forbidden = problem(403, 'PERMISSION_DENIED');
  api
    .on('GET', /^\/projects$/, { body: page(projects, 200) })
    .on('GET', /^\/management-concerns$/, (request) => {
      const type = request.query.get('concernType');
      return {
        body: page(
          concerns.filter(
            (item) =>
              item.projectId === request.query.get('projectId') &&
              (type === null || item.concernType === type),
          ),
          200,
        ),
      };
    })
    .on('GET', /^\/management-concerns\/[^/]+$/, (request) => {
      const found = concerns.find((item) => request.path.endsWith(item.id));
      return found === undefined
        ? problem(404, 'NOT_FOUND')
        : { body: found, headers: { ETag: CONCERN_ETAG } };
    })
    .on('GET', /^\/concern-escalations$/, (request) => ({
      body: page(
        escalations.filter(
          (item) => item.managementConcernId === request.query.get('managementConcernId'),
        ),
        200,
      ),
    }))
    .on('GET', /^\/concern-escalations\/[^/]+$/, (request) => {
      const found = escalations.find((item) => request.path.endsWith(item.id));
      return found === undefined ? problem(404, 'NOT_FOUND') : { body: found };
    })
    .on(
      'GET',
      /^\/configuration-resolutions\/RISK_MATRIX$/,
      matrix === 'forbidden'
        ? forbidden
        : matrix === 'missing'
          ? problem(422, 'CONFIGURATION_MISSING')
          : { body: matrix },
    )
    .on(
      'GET',
      /^\/master-data-catalogues$/,
      catalogues ? { body: [...CATALOGUES, ...CONCERN_CATALOGUES] } : forbidden,
    )
    .on('GET', /^\/master-data-items$/, (request) => {
      const id = request.query.get('catalogueId') ?? '';
      return { body: page(CONCERN_ITEMS[id] ?? ITEMS[id] ?? [], 200) };
    })
    .on(
      'GET',
      /^\/roles$/,
      roles
        ? {
            body: [
              {
                id: DM_ROLE_ID,
                code: 'R03',
                name: { en: 'Department Manager', ar: 'مدير الإدارة' },
                isSystem: true,
                isExternalEligible: false,
              },
            ],
          }
        : forbidden,
    )
    .on('GET', /^\/users$/, forbidden);
  if (!projectReadable) {
    api.on('GET', /^\/projects\/[^/]+$/, forbidden);
  }
  return api;
}
