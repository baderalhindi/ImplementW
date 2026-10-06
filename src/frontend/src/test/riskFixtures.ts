import { type ProjectSummary } from '@/features/projects/api/types.ts';
import {
  type RiskAcceptanceDetail,
  type RiskAssessmentDetail,
  type RiskDetail,
  type RiskMatrixResolution,
  type RiskTreatmentActionDetail,
} from '@/features/risks/api/types.ts';
import { type MasterDataItemSummary } from '@/shared/api/masterData.ts';

import { type MockApi, page, problem } from './mockApi.ts';
import {
  CATALOGUES,
  ENTITY_USER_ID,
  ITEMS,
  projectSummary,
  PROJECT_ID,
  REVIEWER_ID,
} from './projectFixtures.ts';
import { day, OTHER_PROJECT_ID } from './taskFixtures.ts';

// Test data only: fixed ids, each starting differently so a shortened id is recognisable. Dates are relative to today.
//
// The Coastal road upgrade (PROJECT_ID), ACTIVE, is managed by the entity's user, Huda:
//   "Flooding of the works" — ASSESSED, probability 4 × impact 5 → CRITICAL (version 1), review due yesterday.
//   "Asphalt price rise" — TREATMENT, probability 2 × impact 3 → MEDIUM, one PLANNED action, review in 10 days.
//   "Utility relocation delay" — IDENTIFIED, never assessed.
//   "Survey access refused" — CLOSED, rationale "Access agreed with the landowner."
// The Harbour bridge repair (OTHER_PROJECT_ID), ACTIVE: "Deck corrosion" — MONITORING, 5 × 4 → CRITICAL.
//
// RISK_MATRIX version 1 (standardMatrix) has five probability levels, two impact dimensions (cost, schedule) of five
// levels each, and four ratings LOW < MEDIUM < HIGH < CRITICAL. compactMatrix is another version: three levels, one
// dimension, ratings MINOR < MAJOR — what a re-publication could look like.

export const FLOODING_ID = 'f1000000-0000-4000-8000-000000000d01';
export const ASPHALT_ID = 'f1000000-0000-4000-8000-000000000d02';
export const UTILITY_ID = 'f1000000-0000-4000-8000-000000000d03';
export const SURVEY_RISK_ID = 'f1000000-0000-4000-8000-000000000d04';
export const CORROSION_ID = 'f2000000-0000-4000-8000-000000000d05';
export const BARRIER_ACTION_ID = 'f3000000-0000-4000-8000-000000000e01';
export const HEDGE_ACTION_ID = 'f3000000-0000-4000-8000-000000000e02';
export const RISK_CATALOGUE_ID = 'c0000000-0000-4000-8000-000000000061';
export const DIMENSION_CATALOGUE_ID = 'c0000000-0000-4000-8000-000000000062';
export const WEATHER_CATEGORY_ID = 'e6000000-0000-4000-8000-000000000f01';
export const COMMERCIAL_CATEGORY_ID = 'e6000000-0000-4000-8000-000000000f02';
export const COST_DIMENSION_ID = 'e7000000-0000-4000-8000-000000000f03';
export const SCHEDULE_DIMENSION_ID = 'e7000000-0000-4000-8000-000000000f04';
export const MATRIX_VERSION_ID = 'e8000000-0000-4000-8000-000000000f05';
export const COMPACT_VERSION_ID = 'e8000000-0000-4000-8000-000000000f06';
export const RISK_ETAG = '"71"';
export const ACTION_ETAG = '"72"';

function label(en: string, ar: string) {
  return { en, ar };
}

const RATINGS = {
  LOW: label('Low', 'منخفض'),
  MEDIUM: label('Medium', 'متوسط'),
  HIGH: label('High', 'مرتفع'),
  CRITICAL: label('Critical', 'حرج'),
} as const;

type StandardRating = keyof typeof RATINGS;

/** Test data: version 1's mapping, by probability × impact (the real one is OQ-006's). */
function standardRating(probability: number, impact: number): StandardRating {
  const score = probability * impact;
  return score >= 15 ? 'CRITICAL' : score >= 10 ? 'HIGH' : score >= 5 ? 'MEDIUM' : 'LOW';
}

const LEVELS = [1, 2, 3, 4, 5];

export function standardMatrix(): RiskMatrixResolution {
  return {
    versionId: MATRIX_VERSION_ID,
    familyCode: 'RISK_MATRIX',
    versionNo: 1,
    effectiveFrom: '2026-09-01T00:00:00Z',
    effectiveTo: null,
    content: {
      probabilityLevels: LEVELS.map((level) => ({
        level,
        label: label(
          ['Rare', 'Unlikely', 'Possible', 'Likely', 'Almost certain'][level - 1] ?? '',
          `احتمال ${String(level)}`,
        ),
        lowerPct: null,
        upperPct: null,
      })),
      impactLevels: [COST_DIMENSION_ID, SCHEDULE_DIMENSION_ID].flatMap((impactDimensionItemId) =>
        LEVELS.map((level) => ({
          impactDimensionItemId,
          level,
          label: label(`Impact level ${String(level)}`, `أثر ${String(level)}`),
          description: null,
          lowerBound: null,
          upperBound: null,
        })),
      ),
      riskRatings: (Object.keys(RATINGS) as StandardRating[]).map((code, index) => ({
        code,
        label: RATINGS[code],
        sortOrder: index + 1,
      })),
      riskMatrixCells: LEVELS.flatMap((probabilityLevel) =>
        LEVELS.map((impactLevel) => ({
          probabilityLevel,
          impactLevel,
          ratingCode: standardRating(probabilityLevel, impactLevel),
        })),
      ),
    },
  };
}

/** Another published version: 3 × 3, one dimension, two ratings. */
export function compactMatrix(): RiskMatrixResolution {
  const levels = [1, 2, 3];
  return {
    versionId: COMPACT_VERSION_ID,
    familyCode: 'RISK_MATRIX',
    versionNo: 2,
    effectiveFrom: '2026-10-01T00:00:00Z',
    effectiveTo: null,
    content: {
      probabilityLevels: levels.map((level) => ({
        level,
        label: label(`Chance ${String(level)}`, `فرصة ${String(level)}`),
        lowerPct: null,
        upperPct: null,
      })),
      impactLevels: levels.map((level) => ({
        impactDimensionItemId: COST_DIMENSION_ID,
        level,
        label: label(`Cost ${String(level)}`, `تكلفة ${String(level)}`),
        description: null,
        lowerBound: null,
        upperBound: null,
      })),
      riskRatings: [
        { code: 'MAJOR', label: label('Major', 'كبير'), sortOrder: 2 },
        { code: 'MINOR', label: label('Minor', 'صغير'), sortOrder: 1 },
      ],
      riskMatrixCells: levels.flatMap((probabilityLevel) =>
        levels.map((impactLevel) => ({
          probabilityLevel,
          impactLevel,
          ratingCode: probabilityLevel + impactLevel >= 5 ? 'MAJOR' : 'MINOR',
        })),
      ),
    },
  };
}

function rating(code: StandardRating) {
  return { id: `rating-${code}`, code, label: RATINGS[code] };
}

function at(days: number): string {
  return `${day(days)}T09:00:00Z`;
}

export function risk(overrides: Partial<RiskDetail> = {}): RiskDetail {
  return {
    id: FLOODING_ID,
    projectId: PROJECT_ID,
    title: { text: 'Flooding of the works', language: 'EN' },
    description: { text: 'Heavy rain floods the excavation and halts work.', language: 'EN' },
    riskCategoryItemId: WEATHER_CATEGORY_ID,
    ownerUserId: ENTITY_USER_ID,
    status: 'ASSESSED',
    identifiedDate: day(-30),
    nextReviewDate: day(-1),
    currentAssessment: {
      id: 'a1000000-0000-4000-8000-000000000001',
      versionNo: 1,
      assessedAt: at(-20),
      matrixConfigurationVersionId: MATRIX_VERSION_ID,
      probabilityLevel: 4,
      overallImpactLevel: 5,
      rating: rating('CRITICAL'),
    },
    acceptedUntil: null,
    materialisedAt: null,
    materialisedIssueIds: [],
    closureRationale: null,
    closedAt: null,
    closedByUserId: null,
    reopenedCount: 0,
    createdAt: at(-30),
    createdBy: ENTITY_USER_ID,
    updatedAt: at(-20),
    updatedBy: ENTITY_USER_ID,
    ...overrides,
  };
}

function assessmentSummary(id: string, probability: number, impact: number, version = 1) {
  return {
    id,
    versionNo: version,
    assessedAt: at(-10),
    matrixConfigurationVersionId: MATRIX_VERSION_ID,
    probabilityLevel: probability,
    overallImpactLevel: impact,
    rating: rating(standardRating(probability, impact)),
  };
}

export function coastalRisks(): RiskDetail[] {
  return [
    risk({
      id: ASPHALT_ID,
      title: { text: 'Asphalt price rise', language: 'EN' },
      riskCategoryItemId: COMMERCIAL_CATEGORY_ID,
      status: 'TREATMENT',
      nextReviewDate: day(10),
      currentAssessment: assessmentSummary('a1000000-0000-4000-8000-000000000002', 2, 3),
    }),
    risk(),
    risk({
      id: UTILITY_ID,
      title: { text: 'Utility relocation delay', language: 'EN' },
      status: 'IDENTIFIED',
      ownerUserId: null,
      nextReviewDate: null,
      currentAssessment: null,
    }),
    risk({
      id: SURVEY_RISK_ID,
      title: { text: 'Survey access refused', language: 'EN' },
      status: 'CLOSED',
      nextReviewDate: day(-5),
      currentAssessment: assessmentSummary('a1000000-0000-4000-8000-000000000003', 1, 2),
      closureRationale: { text: 'Access agreed with the landowner.', language: 'EN' },
      closedAt: at(-3),
      closedByUserId: ENTITY_USER_ID,
    }),
  ];
}

export function harbourRisks(): RiskDetail[] {
  return [
    risk({
      id: CORROSION_ID,
      projectId: OTHER_PROJECT_ID,
      title: { text: 'Deck corrosion', language: 'EN' },
      status: 'MONITORING',
      ownerUserId: REVIEWER_ID,
      nextReviewDate: day(20),
      currentAssessment: assessmentSummary('a2000000-0000-4000-8000-000000000004', 5, 4),
    }),
  ];
}

export function assessment(overrides: Partial<RiskAssessmentDetail> = {}): RiskAssessmentDetail {
  return {
    id: 'a1000000-0000-4000-8000-000000000001',
    riskId: FLOODING_ID,
    versionNo: 1,
    assessedAt: at(-20),
    assessedByUserId: REVIEWER_ID,
    matrixConfigurationVersionId: MATRIX_VERSION_ID,
    probabilityLevel: 4,
    overallImpactLevel: 5,
    rating: rating('CRITICAL'),
    impacts: [
      { impactDimensionItemId: COST_DIMENSION_ID, impactLevel: 5, rationale: null },
      { impactDimensionItemId: SCHEDULE_DIMENSION_ID, impactLevel: 3, rationale: null },
    ],
    rationale: { text: 'Rainy season starts next month.', language: 'EN' },
    ...overrides,
  };
}

export function treatmentAction(
  overrides: Partial<RiskTreatmentActionDetail> = {},
): RiskTreatmentActionDetail {
  return {
    id: BARRIER_ACTION_ID,
    riskId: FLOODING_ID,
    title: { text: 'Install flood barriers', language: 'EN' },
    description: null,
    actionType: 'MITIGATE',
    ownerUserId: ENTITY_USER_ID,
    dueDate: day(14),
    status: 'PLANNED',
    completedAt: null,
    createdAt: at(-15),
    createdBy: ENTITY_USER_ID,
    updatedAt: at(-15),
    updatedBy: ENTITY_USER_ID,
    ...overrides,
  };
}

export function acceptance(overrides: Partial<RiskAcceptanceDetail> = {}): RiskAcceptanceDetail {
  return {
    id: 'ac000000-0000-4000-8000-000000000001',
    riskId: FLOODING_ID,
    acceptedByUserId: REVIEWER_ID,
    acceptedAt: at(-40),
    expiresOn: day(-25),
    rationale: { text: 'Within tolerance for the dry season.', language: 'EN' },
    status: 'EXPIRED',
    revokedAt: null,
    updatedAt: at(-25),
    ...overrides,
  };
}

export function riskProjects(): ProjectSummary[] {
  return [
    projectSummary({ status: 'ACTIVE', projectManagerUserId: ENTITY_USER_ID }),
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

const RISK_CATALOGUES = [
  { id: RISK_CATALOGUE_ID, code: 'RISK_CATEGORY' },
  { id: DIMENSION_CATALOGUE_ID, code: 'IMPACT_DIMENSION' },
];

const RISK_ITEMS: Record<string, MasterDataItemSummary[]> = {
  [RISK_CATALOGUE_ID]: [
    catalogueItem(WEATHER_CATEGORY_ID, RISK_CATALOGUE_ID, 'WEATHER', 'Weather', 'الطقس'),
    catalogueItem(COMMERCIAL_CATEGORY_ID, RISK_CATALOGUE_ID, 'COMMERCIAL', 'Commercial', 'تجاري'),
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

export interface RiskState {
  projects?: ProjectSummary[];
  risks?: RiskDetail[];
  assessments?: RiskAssessmentDetail[];
  actions?: RiskTreatmentActionDetail[];
  acceptances?: RiskAcceptanceDetail[];
  /** The RISK_MATRIX resolution, or how it is refused: 403 (no CONFIGURATION_VIEW) or 422 (none published). */
  matrix?: RiskMatrixResolution | 'forbidden' | 'missing';
  /** MASTER_DATA_VIEW; false is 403. */
  catalogues?: boolean;
}

/**
 * The risk reads, each risk's histories filtered as the API does. Layer over withProjectLookups: the catalogue routes
 * answer the project's catalogues as well as the risk ones.
 */
export function withRisks(
  api: MockApi,
  {
    projects = riskProjects(),
    risks = [...coastalRisks(), ...harbourRisks()],
    assessments = [assessment()],
    actions = [
      treatmentAction({
        id: HEDGE_ACTION_ID,
        riskId: ASPHALT_ID,
        title: { text: 'Hedge the asphalt price', language: 'EN' },
        actionType: 'TRANSFER',
      }),
    ],
    acceptances = [],
    matrix = standardMatrix(),
    catalogues = true,
  }: RiskState = {},
): MockApi {
  const forbidden = problem(403, 'PERMISSION_DENIED');
  const ofRisk = <T extends { riskId: string }>(items: T[], riskId: string | null) =>
    page(
      items.filter((item) => item.riskId === riskId),
      200,
    );
  api
    .on('GET', /^\/projects$/, { body: page(projects, 200) })
    .on('GET', /^\/risks$/, (request) => ({
      body: page(
        risks.filter((item) => item.projectId === request.query.get('projectId')),
        200,
      ),
    }))
    .on('GET', /^\/risks\/[^/]+$/, (request) => {
      const found = risks.find((item) => request.path.endsWith(item.id));
      return found === undefined
        ? problem(404, 'NOT_FOUND')
        : { body: found, headers: { ETag: RISK_ETAG } };
    })
    .on('GET', /^\/risk-assessments$/, (request) => ({
      body: ofRisk(assessments, request.query.get('riskId')),
    }))
    .on('GET', /^\/risk-acceptances$/, (request) => ({
      body: ofRisk(acceptances, request.query.get('riskId')),
    }))
    .on('GET', /^\/risk-treatment-actions$/, (request) => ({
      body: ofRisk(actions, request.query.get('riskId')),
    }))
    .on('GET', /^\/risk-treatment-actions\/[^/]+$/, (request) => {
      const found = actions.find((item) => request.path.endsWith(item.id));
      return found === undefined
        ? problem(404, 'NOT_FOUND')
        : { body: found, headers: { ETag: ACTION_ETAG } };
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
      catalogues ? { body: [...CATALOGUES, ...RISK_CATALOGUES] } : forbidden,
    )
    .on('GET', /^\/master-data-items$/, (request) => {
      const id = request.query.get('catalogueId') ?? '';
      return { body: page(RISK_ITEMS[id] ?? ITEMS[id] ?? [], 200) };
    });
  return api;
}
