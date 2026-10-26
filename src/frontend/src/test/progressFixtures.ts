import {
  type ProgressSubmissionDetail,
  type ProjectHealthStatusDetail,
  type PublishedProgressSnapshotDetail,
  type ReportingCycleSummary,
} from '@/features/progress/api/types.ts';

import { type MockApi, page } from './mockApi.ts';
import { ENTITY_USER_ID, PROJECT_ID } from './projectFixtures.ts';

// Test data only: fixed ids, each starting differently so a shortened id is recognisable.

export const CYCLE_ID = '9a9a9a9a-0000-4000-8000-000000000401';
export const EARLIER_CYCLE_ID = '9a9a9a9a-0000-4000-8000-000000000402';
export const SUBMISSION_ID = '9b9b9b9b-0000-4000-8000-000000000501';
export const RETURNED_SUBMISSION_ID = '9b9b9b9b-0000-4000-8000-000000000502';
export const PUBLISHED_SUBMISSION_ID = '9b9b9b9b-0000-4000-8000-000000000503';
export const INTAKE_ID = '9c9c9c9c-0000-4000-8000-000000000601';
export const RULE_VERSION_ID = '9d9d9d9d-0000-4000-8000-000000000701';
export const SUBMISSION_ETAG = '"21"';

export function cycle(overrides: Partial<ReportingCycleSummary> = {}): ReportingCycleSummary {
  return {
    id: CYCLE_ID,
    projectId: PROJECT_ID,
    periodStart: '2026-09-01',
    periodEnd: '2026-09-30',
    dueDate: '2026-09-30',
    status: 'OPEN',
    ...overrides,
  };
}

/** A DRAFT the project's Project Manager started: 40% calculated against 50% planned. */
export function submission(
  overrides: Partial<ProgressSubmissionDetail> = {},
): ProgressSubmissionDetail {
  return {
    id: SUBMISSION_ID,
    projectId: PROJECT_ID,
    reportingCycleId: CYCLE_ID,
    revisionNo: 1,
    status: 'DRAFT',
    actualPercent: 40,
    actualPercentCalculated: 40,
    actualPercentOverride: null,
    overrideReason: null,
    isOverridden: false,
    plannedPercent: 50,
    baselineId: null,
    narrative: { text: 'Earthworks on the northern section.', language: 'EN' },
    projectIntakeId: null,
    submittedByUserId: null,
    submittedAt: null,
    reviewedByUserId: null,
    reviewedAt: null,
    returnReason: null,
    createdAt: '2026-10-01T08:00:00Z',
    createdBy: ENTITY_USER_ID,
    updatedAt: '2026-10-01T08:00:00Z',
    updatedBy: ENTITY_USER_ID,
    ...overrides,
  };
}

/** The same revision once the Project Manager submitted it. */
export function submitted(overrides: Partial<ProgressSubmissionDetail> = {}) {
  return submission({
    status: 'SUBMITTED',
    submittedByUserId: ENTITY_USER_ID,
    submittedAt: '2026-10-01T09:00:00Z',
    ...overrides,
  });
}

/** The official record of the earlier period: an override of 30% (calculated 25%) against 35% planned, AMBER. */
export function snapshot(
  overrides: Partial<PublishedProgressSnapshotDetail> = {},
): PublishedProgressSnapshotDetail {
  return {
    id: '9e9e9e9e-0000-4000-8000-000000000801',
    projectId: PROJECT_ID,
    reportingCycleId: EARLIER_CYCLE_ID,
    progressSubmissionId: PUBLISHED_SUBMISSION_ID,
    publishedAt: '2026-09-02T10:00:00Z',
    publishedByUserId: '2b2b2b2b-0000-4000-8000-000000000102',
    actualPercent: '30.0000',
    isOverridden: true,
    plannedPercent: '35.0000',
    overallHealth: 'AMBER',
    scheduleHealth: null,
    financialStatus: null,
    healthRuleConfigurationVersionId: RULE_VERSION_ID,
    ...overrides,
  };
}

/** The live value: computed from the derived figures, never an unpublished override. */
export function liveHealth(
  overrides: Partial<ProjectHealthStatusDetail> = {},
): ProjectHealthStatusDetail {
  return {
    id: '9f9f9f9f-0000-4000-8000-000000000901',
    projectId: PROJECT_ID,
    overallHealth: 'RED',
    actualPercent: 40,
    plannedPercent: 55,
    computedAt: '2026-10-01T09:00:00Z',
    healthRuleConfigurationVersionId: RULE_VERSION_ID,
    ...overrides,
  };
}

interface ProgressState {
  submissions?: ProgressSubmissionDetail[];
  /** The detail GET answers; defaults to the first of `submissions`. */
  detail?: ProgressSubmissionDetail;
  snapshots?: PublishedProgressSnapshotDetail[];
  live?: ProjectHealthStatusDetail[];
  cycles?: ReportingCycleSummary[];
}

/** The four WF-02 reads of one project, as the API answers them. */
export function withProgress(
  api: MockApi,
  {
    submissions = [],
    detail,
    snapshots = [],
    live = [],
    cycles = [
      cycle(),
      cycle({
        id: EARLIER_CYCLE_ID,
        periodStart: '2026-08-01',
        periodEnd: '2026-08-31',
        status: 'CLOSED',
      }),
    ],
  }: ProgressState = {},
): MockApi {
  const shown = detail ?? submissions[0];
  api
    .on('GET', /^\/reporting-cycles$/, { body: page(cycles, 200) })
    .on('GET', /^\/progress-submissions$/, (request) => {
      const size = Number(request.query.get('pageSize') ?? '25');
      const number = Number(request.query.get('page') ?? '1');
      return {
        body: {
          items: submissions.slice((number - 1) * size, number * size),
          page: number,
          pageSize: size,
          totalCount: submissions.length,
        },
      };
    })
    .on('GET', /^\/published-progress-snapshots$/, { body: page(snapshots.slice(0, 1), 1) })
    .on('GET', /^\/project-health-statuses$/, { body: page(live) });
  if (shown !== undefined) {
    api.on('GET', new RegExp(`^/progress-submissions/${shown.id}$`), {
      body: shown,
      headers: { ETag: SUBMISSION_ETAG },
    });
  }
  return api;
}
