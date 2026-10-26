import { type BusinessLinkDetail, type DocumentSummary } from '@/features/documents/api/types.ts';
import {
  type MilestoneAchievementDetail,
  type MilestoneEvidenceDetail,
  type ProjectMilestoneDetail,
} from '@/features/milestones/api/types.ts';
import { type ProjectSummary } from '@/features/projects/api/types.ts';
import { type MasterDataItemSummary } from '@/shared/api/masterData.ts';

import { documentDetail, documentSummary, version } from './documentFixtures.ts';
import { type MockApi, page, problem } from './mockApi.ts';
import {
  CATALOGUES,
  ENTITY_USER_ID,
  ITEMS,
  projectSummary,
  PROJECT_ID,
  REVIEWER_ID,
} from './projectFixtures.ts';
import { activity, EXCAVATION_ID, SCHEDULE_ID } from './scheduleFixtures.ts';
import { day, OTHER_PROJECT_ID } from './taskFixtures.ts';

// Test data only: fixed ids, each starting differently so a shortened id is recognisable. Dates are relative to today,
// so "overdue" and "not after today" hold whenever the suite runs.
//
// The Coastal road upgrade (PROJECT_ID), ACTIVE, is managed by the entity's user, Huda:
//   "Site handover" — ACHIEVED; revision 1 accepted by Faisal, achieved 21 days ago.
//   "Design approval" — PLANNED, forecast 2 days ago (overdue); revision 1 RETURNED by Faisal: "Attach the signed design
//   approval letter."
//   "Foundations complete" — PLANNED, forecast in 10 days; revision 1 DRAFT, claimed yesterday, no evidence.
//   "Temporary access road" — CANCELLED.
// The Harbour bridge repair (OTHER_PROJECT_ID), ACTIVE, is managed by Faisal: "Bridge deck poured" — revision 1
// SUBMITTED.

export const HANDOVER_ID = 'da000000-0000-4000-8000-000000000a01';
export const DESIGN_ID = 'da000000-0000-4000-8000-000000000a02';
export const FOUNDATIONS_ID = 'da000000-0000-4000-8000-000000000a03';
export const ACCESS_ROAD_ID = 'da000000-0000-4000-8000-000000000a04';
export const DECK_ID = 'db000000-0000-4000-8000-000000000a05';
export const HANDOVER_REVISION_ID = 'dc000000-0000-4000-8000-000000000b01';
export const DESIGN_REVISION_ID = 'dc000000-0000-4000-8000-000000000b02';
export const FOUNDATIONS_REVISION_ID = 'dc000000-0000-4000-8000-000000000b03';
export const DECK_REVISION_ID = 'dd000000-0000-4000-8000-000000000b04';
export const MILESTONE_CATEGORY_CATALOGUE_ID = 'c0000000-0000-4000-8000-000000000051';
export const EVIDENCE_TYPE_CATALOGUE_ID = 'c0000000-0000-4000-8000-000000000052';
export const KEY_DELIVERY_ID = 'e1000000-0000-4000-8000-000000000c01';
export const CERTIFICATE_TYPE_ID = 'e2000000-0000-4000-8000-000000000c02';
export const PHOTO_TYPE_ID = 'e2000000-0000-4000-8000-000000000c03';
export const EVIDENCE_DOCUMENT_ID = 'e3000000-0000-4000-8000-000000000c04';
export const EVIDENCE_VERSION_ID = 'e4000000-0000-4000-8000-000000000c05';
export const POLICY_VERSION_ID = 'e5000000-0000-4000-8000-000000000c06';
export const MILESTONE_ETAG = '"51"';
export const REVISION_ETAG = '"61"';

function at(days: number): string {
  return `${day(days)}T09:00:00Z`;
}

export function milestone(overrides: Partial<ProjectMilestoneDetail> = {}): ProjectMilestoneDetail {
  return {
    id: FOUNDATIONS_ID,
    projectId: PROJECT_ID,
    projectScheduleId: SCHEDULE_ID,
    scheduleActivityId: EXCAVATION_ID,
    title: { text: 'Foundations complete', language: 'EN' },
    milestoneCategoryItemId: KEY_DELIVERY_ID,
    forecastDate: day(10),
    status: 'PLANNED',
    sortOrder: 0,
    baselineId: null,
    baselinePlannedDate: null,
    forecastVarianceDays: null,
    createdAt: at(-30),
    createdBy: ENTITY_USER_ID,
    updatedAt: at(-30),
    updatedBy: ENTITY_USER_ID,
    ...overrides,
  };
}

export function revision(
  overrides: Partial<MilestoneAchievementDetail> = {},
): MilestoneAchievementDetail {
  return {
    id: FOUNDATIONS_REVISION_ID,
    projectMilestoneId: FOUNDATIONS_ID,
    projectId: PROJECT_ID,
    revisionNo: 1,
    status: 'DRAFT',
    isCurrent: false,
    claimedAchievementDate: day(-1),
    acceptedActualAchievementDate: null,
    narrative: null,
    submittedByUserId: null,
    submittedAt: null,
    reviewedByUserId: null,
    reviewedAt: null,
    returnReason: null,
    supersededByAchievementId: null,
    projectIntakeId: null,
    createdAt: at(-1),
    createdBy: ENTITY_USER_ID,
    updatedAt: at(-1),
    updatedBy: ENTITY_USER_ID,
    ...overrides,
  };
}

export function coastalMilestones(): ProjectMilestoneDetail[] {
  return [
    milestone({
      id: HANDOVER_ID,
      title: { text: 'Site handover', language: 'EN' },
      forecastDate: day(-20),
      status: 'ACHIEVED',
      baselineId: 'ac000000-0000-4000-8000-000000000c01',
      baselinePlannedDate: day(-22),
      forecastVarianceDays: 2,
    }),
    milestone({
      id: DESIGN_ID,
      title: { text: 'Design approval', language: 'EN' },
      forecastDate: day(-2),
    }),
    milestone(),
    milestone({
      id: ACCESS_ROAD_ID,
      title: { text: 'Temporary access road', language: 'EN' },
      forecastDate: day(20),
      status: 'CANCELLED',
    }),
  ];
}

export function coastalRevisions(): MilestoneAchievementDetail[] {
  return [
    revision(),
    revision({
      id: DESIGN_REVISION_ID,
      projectMilestoneId: DESIGN_ID,
      status: 'RETURNED',
      claimedAchievementDate: day(-4),
      submittedByUserId: ENTITY_USER_ID,
      submittedAt: at(-4),
      reviewedByUserId: REVIEWER_ID,
      reviewedAt: at(-3),
      returnReason: { text: 'Attach the signed design approval letter.', language: 'EN' },
    }),
    revision({
      id: HANDOVER_REVISION_ID,
      projectMilestoneId: HANDOVER_ID,
      status: 'ACCEPTED',
      isCurrent: true,
      claimedAchievementDate: day(-21),
      acceptedActualAchievementDate: day(-21),
      submittedByUserId: ENTITY_USER_ID,
      submittedAt: at(-21),
      reviewedByUserId: REVIEWER_ID,
      reviewedAt: at(-19),
    }),
  ];
}

function harbourMilestones(): ProjectMilestoneDetail[] {
  return [
    milestone({
      id: DECK_ID,
      projectId: OTHER_PROJECT_ID,
      scheduleActivityId: null,
      title: { text: 'Bridge deck poured', language: 'EN' },
      forecastDate: day(-6),
    }),
  ];
}

function harbourRevisions(): MilestoneAchievementDetail[] {
  return [
    revision({
      id: DECK_REVISION_ID,
      projectMilestoneId: DECK_ID,
      projectId: OTHER_PROJECT_ID,
      status: 'SUBMITTED',
      claimedAchievementDate: day(-6),
      submittedByUserId: REVIEWER_ID,
      submittedAt: at(-5),
    }),
  ];
}

/** The two projects SCR-062 reads: Huda manages the first, Faisal the second. */
export function milestoneProjects(): ProjectSummary[] {
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

/** A revision's evidence while no EVIDENCE_POLICY is published: nothing mandatory (PTBC-006). */
export function pendingEvidence(
  overrides: Partial<MilestoneEvidenceDetail> = {},
): MilestoneEvidenceDetail {
  return {
    milestoneAchievementId: FOUNDATIONS_REVISION_ID,
    evidencePolicyVersionId: null,
    mandatoryEvidenceTypeItemIds: [],
    satisfiedEvidenceTypeItemIds: [],
    links: [],
    ...overrides,
  };
}

/** A policy in force that makes the completion certificate mandatory for the category. */
export function requiredEvidence(
  overrides: Partial<MilestoneEvidenceDetail> = {},
): MilestoneEvidenceDetail {
  return pendingEvidence({
    evidencePolicyVersionId: POLICY_VERSION_ID,
    mandatoryEvidenceTypeItemIds: [CERTIFICATE_TYPE_ID],
    ...overrides,
  });
}

/** The site photos attached as photo evidence, clean, on an active link. */
export function photoLink(overrides: Partial<BusinessLinkDetail> = {}): BusinessLinkDetail {
  return {
    id: 'e6000000-0000-4000-8000-000000000c07',
    documentId: EVIDENCE_DOCUMENT_ID,
    linkRole: 'ATTACHMENT',
    target: { module: 'Milestone', type: 'MilestoneAchievement', id: FOUNDATIONS_REVISION_ID },
    linkedByUserId: ENTITY_USER_ID,
    linkedAt: at(-1),
    unlinkedAt: null,
    unlinkedByUserId: null,
    evidence: [
      {
        id: 'e7000000-0000-4000-8000-000000000c08',
        businessLinkId: 'e6000000-0000-4000-8000-000000000c07',
        documentId: EVIDENCE_DOCUMENT_ID,
        documentVersionId: EVIDENCE_VERSION_ID,
        versionNo: 1,
        evidenceTypeItemId: PHOTO_TYPE_ID,
        designatedByUserId: ENTITY_USER_ID,
        designatedAt: at(-1),
        status: 'VALID',
        scanState: 'CLEAN',
        satisfies: true,
      },
    ],
    ...overrides,
  };
}

/** The project's documents MOD-054 offers: a clean completion certificate. */
export function certificateDocument(): DocumentSummary {
  return documentSummary({
    id: EVIDENCE_DOCUMENT_ID,
    title: { text: 'Foundations completion certificate', language: 'EN' },
    projectId: PROJECT_ID,
  });
}

function catalogueItem(
  id: string,
  catalogueId: string,
  code: string,
  en: string,
  ar: string,
): MasterDataItemSummary {
  return {
    id,
    catalogueId,
    code,
    label: { en, ar },
    parentItemId: null,
    sortOrder: 1,
    lifecycleState: 'PUBLISHED',
    isSystem: false,
  };
}

const MILESTONE_CATALOGUES = [
  { id: MILESTONE_CATEGORY_CATALOGUE_ID, code: 'MILESTONE_CATEGORY' },
  { id: EVIDENCE_TYPE_CATALOGUE_ID, code: 'EVIDENCE_TYPE' },
];

const MILESTONE_ITEMS: Record<string, MasterDataItemSummary[]> = {
  [MILESTONE_CATEGORY_CATALOGUE_ID]: [
    catalogueItem(
      KEY_DELIVERY_ID,
      MILESTONE_CATEGORY_CATALOGUE_ID,
      'KEY_DELIVERY',
      'Key delivery',
      'تسليم رئيسي',
    ),
  ],
  [EVIDENCE_TYPE_CATALOGUE_ID]: [
    catalogueItem(
      CERTIFICATE_TYPE_ID,
      EVIDENCE_TYPE_CATALOGUE_ID,
      'COMPLETION_CERTIFICATE',
      'Completion certificate',
      'شهادة إنجاز',
    ),
    catalogueItem(
      PHOTO_TYPE_ID,
      EVIDENCE_TYPE_CATALOGUE_ID,
      'SITE_PHOTOS',
      'Site photos',
      'صور الموقع',
    ),
  ],
};

export interface MilestoneState {
  projects?: ProjectSummary[];
  milestones?: ProjectMilestoneDetail[];
  revisions?: MilestoneAchievementDetail[];
  /** The evidence of each revision by id; a revision not listed has none, under no policy. */
  evidence?: Record<string, MilestoneEvidenceDetail>;
  /** MILESTONE_VIEW; false is 403 on the claims. */
  claims?: boolean;
  /** MASTER_DATA_VIEW for the two catalogues; false is 403, as today for everyone but R01 (TASK-038 F-1). */
  catalogues?: boolean;
}

/**
 * The milestone reads, each project's milestones and claims filtered as the API does. Layer over withProjectLookups:
 * the catalogue routes answer the project's catalogues as well as the milestone ones.
 */
export function withMilestones(
  api: MockApi,
  {
    projects = milestoneProjects(),
    milestones = [...coastalMilestones(), ...harbourMilestones()],
    revisions = [...coastalRevisions(), ...harbourRevisions()],
    evidence = {},
    claims = true,
    catalogues = true,
  }: MilestoneState = {},
): MockApi {
  const forbidden = problem(403, 'PERMISSION_DENIED');
  const newestFirst = (items: MilestoneAchievementDetail[]) =>
    [...items].sort((a, b) => b.revisionNo - a.revisionNo);
  api
    .on('GET', /^\/projects$/, { body: page(projects, 200) })
    .on('GET', /^\/project-milestones$/, (request) => ({
      body: page(
        milestones.filter((item) => item.projectId === request.query.get('projectId')),
        200,
      ),
    }))
    .on('GET', /^\/project-milestones\/[^/]+$/, (request) => {
      const found = milestones.find((item) => request.path.endsWith(item.id));
      return found === undefined
        ? problem(404, 'NOT_FOUND')
        : { body: found, headers: { ETag: MILESTONE_ETAG } };
    })
    .on('GET', /^\/milestone-achievements$/, (request) => {
      if (!claims) {
        return forbidden;
      }
      const projectId = request.query.get('projectId');
      const milestoneId = request.query.get('projectMilestoneId');
      return {
        body: page(
          newestFirst(
            revisions.filter((item) =>
              milestoneId === null
                ? item.projectId === projectId
                : item.projectMilestoneId === milestoneId,
            ),
          ),
          200,
        ),
      };
    })
    .on('GET', /^\/milestone-achievements\/[^/]+$/, (request) => {
      const found = revisions.find((item) => request.path.endsWith(item.id));
      return found === undefined
        ? problem(404, 'NOT_FOUND')
        : { body: found, headers: { ETag: REVISION_ETAG } };
    })
    .on('GET', /^\/milestone-achievements\/[^/]+\/evidence$/, (request) => {
      const id = request.path.split('/')[2] ?? '';
      return { body: evidence[id] ?? pendingEvidence({ milestoneAchievementId: id }) };
    })
    .on('GET', /^\/schedule-activities$/, {
      body: page(
        [
          activity({
            id: EXCAVATION_ID,
            wbsCode: '1.2',
            name: { text: 'Excavation', language: 'EN' },
          }),
        ],
        200,
      ),
    })
    .on(
      'GET',
      /^\/master-data-catalogues$/,
      catalogues ? { body: [...CATALOGUES, ...MILESTONE_CATALOGUES] } : forbidden,
    )
    .on('GET', /^\/master-data-items$/, (request) => {
      const id = request.query.get('catalogueId') ?? '';
      return { body: page(MILESTONE_ITEMS[id] ?? ITEMS[id] ?? [], 200) };
    })
    .on('GET', /^\/documents$/, { body: page([certificateDocument()]) })
    .on('GET', /^\/documents\/[^/]+$/, {
      body: documentDetail({
        id: EVIDENCE_DOCUMENT_ID,
        title: { text: 'Foundations completion certificate', language: 'EN' },
        projectId: PROJECT_ID,
        latestVersion: version({ id: EVIDENCE_VERSION_ID, documentId: EVIDENCE_DOCUMENT_ID }),
      }),
    });
  return api;
}
