import {
  type ApprovalInboxItem,
  type ApprovalInstanceDetail,
  type ApprovalInstanceSummary,
} from '@/features/approvals/api/types.ts';
import {
  type BaselineActivityDetail,
  type BaselineDependencyDetail,
  type ProjectBaselineDetail,
  type ProjectScheduleDetail,
  type ScheduleActivityDetail,
  type ScheduleDependencyDetail,
  type ScheduleHealthStatusDetail,
} from '@/features/schedule/api/types.ts';

import { approvalRun, inboxItem, task } from './approvalFixtures.ts';
import { type MockApi, page } from './mockApi.ts';
import { ENTITY_USER_ID, PROJECT_ID } from './projectFixtures.ts';

// Test data only: fixed ids, each starting differently so a shortened id is recognisable. The schedule is a summary
// "1 Earthworks" over "1.1 Survey" and "1.2 Excavation", then "2 Paving": 1.1 → 1.2 → 2, finish to start, no lag. With
// the ACTIVE baseline, 1.2 and 2 are forecast two days late.

export const SCHEDULE_ID = 'aa000000-0000-4000-8000-000000000a01';
export const SUMMARY_ID = 'ab000000-0000-4000-8000-000000000b01';
export const SURVEY_ID = 'ab000000-0000-4000-8000-000000000b02';
export const EXCAVATION_ID = 'ab000000-0000-4000-8000-000000000b03';
export const PAVING_ID = 'ab000000-0000-4000-8000-000000000b04';
export const BASELINE_ID = 'ac000000-0000-4000-8000-000000000c01';
export const CANDIDATE_ID = 'ac000000-0000-4000-8000-000000000c02';
export const SCHEDULE_RUN_ID = 'ad000000-0000-4000-8000-000000000d01';
export const ACTIVITY_ETAG = '"31"';
export const CHANGE_AUTHORIZATION_ID = 'ae000000-0000-4000-8000-000000000e01';

const AUDIT = {
  createdAt: '2026-10-01T08:00:00Z',
  createdBy: ENTITY_USER_ID,
  updatedAt: '2026-10-01T08:00:00Z',
  updatedBy: ENTITY_USER_ID,
};

export function projectSchedule(
  overrides: Partial<ProjectScheduleDetail> = {},
): ProjectScheduleDetail {
  return {
    id: SCHEDULE_ID,
    projectId: PROJECT_ID,
    calendarItemId: null,
    activeBaselineId: BASELINE_ID,
    ...AUDIT,
    ...overrides,
  };
}

export function activity(overrides: Partial<ScheduleActivityDetail> = {}): ScheduleActivityDetail {
  return {
    id: SURVEY_ID,
    projectId: PROJECT_ID,
    projectScheduleId: SCHEDULE_ID,
    parentActivityId: SUMMARY_ID,
    wbsCode: '1.1',
    name: { text: 'Survey', language: 'EN' },
    activityKind: 'ACTIVITY',
    status: 'PLANNED',
    sortOrder: 1,
    requestedStartDate: '2026-11-01',
    plannedStartDate: '2026-11-01',
    plannedFinishDate: '2026-11-05',
    plannedDurationDays: 5,
    forecastStartDate: '2026-11-01',
    forecastFinishDate: '2026-11-05',
    actualStartDate: null,
    actualFinishDate: null,
    baselineId: BASELINE_ID,
    baselineStartDate: '2026-11-01',
    baselineFinishDate: '2026-11-05',
    startVarianceDays: 0,
    finishVarianceDays: 0,
    ...AUDIT,
    ...overrides,
  };
}

/** The schedule under its ACTIVE baseline: 1.2 and 2 forecast two days after the baseline. */
export function baselinedActivities(): ScheduleActivityDetail[] {
  return [
    activity({
      id: SUMMARY_ID,
      parentActivityId: null,
      wbsCode: '1',
      name: { text: 'Earthworks', language: 'EN' },
      activityKind: 'SUMMARY',
      sortOrder: 0,
      plannedFinishDate: '2026-11-15',
      plannedDurationDays: 15,
      forecastFinishDate: '2026-11-17',
      baselineFinishDate: '2026-11-15',
      finishVarianceDays: 2,
    }),
    activity(),
    activity({
      id: EXCAVATION_ID,
      wbsCode: '1.2',
      name: { text: 'Excavation', language: 'EN' },
      sortOrder: 2,
      requestedStartDate: '2026-11-01',
      plannedStartDate: '2026-11-06',
      plannedFinishDate: '2026-11-15',
      plannedDurationDays: 10,
      forecastStartDate: '2026-11-08',
      forecastFinishDate: '2026-11-17',
      baselineStartDate: '2026-11-06',
      baselineFinishDate: '2026-11-15',
      startVarianceDays: 2,
      finishVarianceDays: 2,
    }),
    activity({
      id: PAVING_ID,
      parentActivityId: null,
      wbsCode: '2',
      name: { text: 'Paving', language: 'EN' },
      sortOrder: 3,
      requestedStartDate: '2026-11-01',
      plannedStartDate: '2026-11-16',
      plannedFinishDate: '2026-11-25',
      plannedDurationDays: 10,
      forecastStartDate: '2026-11-18',
      forecastFinishDate: '2026-11-27',
      baselineStartDate: '2026-11-16',
      baselineFinishDate: '2026-11-25',
      startVarianceDays: 2,
      finishVarianceDays: 2,
    }),
  ];
}

/** The same schedule before any baseline: the forecast follows the plan and nothing is measured. */
export function plannedActivities(): ScheduleActivityDetail[] {
  return baselinedActivities().map((item) => ({
    ...item,
    forecastStartDate: item.plannedStartDate,
    forecastFinishDate: item.plannedFinishDate,
    baselineId: null,
    baselineStartDate: null,
    baselineFinishDate: null,
    startVarianceDays: null,
    finishVarianceDays: null,
  }));
}

export function dependency(
  overrides: Partial<ScheduleDependencyDetail> = {},
): ScheduleDependencyDetail {
  return {
    id: 'af000000-0000-4000-8000-000000000f01',
    projectId: PROJECT_ID,
    predecessorActivityId: SURVEY_ID,
    successorActivityId: EXCAVATION_ID,
    dependencyType: 'FS',
    lagDays: 0,
    createdAt: AUDIT.createdAt,
    createdBy: AUDIT.createdBy,
    ...overrides,
  };
}

/** 1.1 → 1.2 → 2. */
export function chain(): ScheduleDependencyDetail[] {
  return [
    dependency(),
    dependency({
      id: 'af000000-0000-4000-8000-000000000f02',
      predecessorActivityId: EXCAVATION_ID,
      successorActivityId: PAVING_ID,
    }),
  ];
}

export function baseline(overrides: Partial<ProjectBaselineDetail> = {}): ProjectBaselineDetail {
  return {
    id: BASELINE_ID,
    projectId: PROJECT_ID,
    baselineType: 'APPROVED',
    versionNo: 1,
    revisionNo: 1,
    status: 'ACTIVE',
    baselineFinishDate: '2026-11-25',
    activatedAt: '2026-10-02T10:00:00Z',
    supersededAt: null,
    supersededByBaselineId: null,
    changeAuthorizationId: null,
    projectIntakeId: null,
    declaredEndDate: null,
    declaredScope: null,
    ...AUDIT,
    ...overrides,
  };
}

/** The next candidate: version 2, a DRAFT unless overridden. */
export function candidate(overrides: Partial<ProjectBaselineDetail> = {}): ProjectBaselineDetail {
  return baseline({
    id: CANDIDATE_ID,
    versionNo: 2,
    status: 'DRAFT',
    baselineFinishDate: '2026-11-27',
    activatedAt: null,
    ...overrides,
  });
}

export function health(
  overrides: Partial<ScheduleHealthStatusDetail> = {},
): ScheduleHealthStatusDetail {
  return {
    id: 'a0000000-0000-4000-8000-000000000001',
    projectId: PROJECT_ID,
    projectBaselineId: BASELINE_ID,
    scheduleHealth: 'AMBER',
    finishVarianceDays: 2,
    computedAt: '2026-10-02T10:00:00Z',
    healthRuleConfigurationVersionId: null,
    ...overrides,
  };
}

export function baselineCopy(): BaselineActivityDetail[] {
  return baselinedActivities().map((item) => ({
    scheduleActivityId: item.id,
    parentActivityId: item.parentActivityId,
    activityKind: item.activityKind,
    plannedStartDate: item.baselineStartDate ?? item.plannedStartDate,
    plannedFinishDate: item.baselineFinishDate ?? item.plannedFinishDate,
    plannedDurationDays: item.plannedDurationDays,
  }));
}

export function baselineCopyDependencies(): BaselineDependencyDetail[] {
  return chain().map(({ predecessorActivityId, successorActivityId, dependencyType, lagDays }) => ({
    predecessorActivityId,
    successorActivityId,
    dependencyType,
    lagDays,
  }));
}

/** The WF-11 run reviewing the submitted candidate, requested by the project's Project Manager. */
export function scheduleRun(
  overrides: Partial<ApprovalInstanceDetail> = {},
): ApprovalInstanceDetail {
  return approvalRun({
    id: SCHEDULE_RUN_ID,
    subject: { module: 'Schedule', type: 'ProjectBaseline', id: CANDIDATE_ID, revisionNo: 1 },
    routingKey: 'SCHEDULE_BASELINE',
    scopeProjectId: PROJECT_ID,
    requestedByUserId: ENTITY_USER_ID,
    requestedAt: '2026-10-02T12:00:00Z',
    tasks: [task()],
    ...overrides,
  });
}

export function runSummary(run: ApprovalInstanceDetail): ApprovalInstanceSummary {
  return {
    id: run.id,
    subject: run.subject,
    routingKey: run.routingKey,
    requestedByUserId: run.requestedByUserId,
    requestedAt: run.requestedAt,
    status: run.status,
    completedAt: run.completedAt,
  };
}

/** The reviewer's inbox task deciding the submitted candidate. */
export function scheduleTask(overrides: Partial<ApprovalInboxItem> = {}): ApprovalInboxItem {
  return inboxItem({ instance: runSummary(scheduleRun()), ...overrides });
}

export interface ScheduleState {
  schedule?: ProjectScheduleDetail | null;
  activities?: ScheduleActivityDetail[];
  dependencies?: ScheduleDependencyDetail[];
  baselines?: ProjectBaselineDetail[];
  health?: ScheduleHealthStatusDetail[];
  /** The runs a reader of the candidate's review history is shown; none by default. */
  runs?: ApprovalInstanceDetail[];
  /** The caller's approval inbox; empty by default. */
  inbox?: ApprovalInboxItem[];
}

/**
 * The WF-03 reads of one project, as the API answers them. Each activity's detail answers with ACTIVITY_ETAG, and the
 * baseline's frozen copy is the schedule's own dates.
 */
export function withSchedule(
  api: MockApi,
  {
    schedule = projectSchedule(),
    activities = baselinedActivities(),
    dependencies = chain(),
    baselines = [baseline()],
    health: healthItems = [health()],
    runs = [],
    inbox = [],
  }: ScheduleState = {},
): MockApi {
  api
    .on('GET', /^\/project-schedules$/, { body: page(schedule === null ? [] : [schedule], 200) })
    .on('GET', /^\/schedule-activities$/, { body: page(activities, 200) })
    .on('GET', /^\/schedule-dependencies$/, { body: page(dependencies, 200) })
    .on('GET', /^\/project-baselines$/, { body: page(baselines, 200) })
    .on('GET', /^\/schedule-health-statuses$/, { body: page(healthItems) })
    .on('GET', /^\/schedule-activities\/[^/]+$/, (request) => {
      const found = activities.find((item) => request.path.endsWith(item.id));
      return found === undefined
        ? { status: 404, body: { code: 'NOT_FOUND' } }
        : { body: found, headers: { ETag: ACTIVITY_ETAG } };
    })
    .on('GET', /^\/project-baselines\/[^/]+\/baseline-activities$/, {
      body: page(baselineCopy(), 200),
    })
    .on('GET', /^\/project-baselines\/[^/]+\/baseline-dependencies$/, {
      body: page(baselineCopyDependencies(), 200),
    })
    .on('GET', /^\/approval-instances$/, { body: page(runs.map(runSummary)) })
    .on('GET', /^\/approval-tasks$/, { body: page(inbox, 200) });
  return api;
}
