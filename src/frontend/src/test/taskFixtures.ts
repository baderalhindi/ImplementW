import { type ProjectSummary } from '@/features/projects/api/types.ts';
import { type ScheduleActivityDetail } from '@/features/schedule/api/types.ts';
import { addDays } from '@/features/schedule/dependencyRules.ts';
import { type ProjectTaskDetail, type TaskDependencyDetail } from '@/features/tasks/api/types.ts';
import { todayUtc } from '@/features/tasks/taskRules.ts';
import { type MasterDataItemSummary } from '@/shared/api/masterData.ts';

import { type MockApi, page, problem } from './mockApi.ts';
import {
  ENTITY_USER_ID,
  OTHER_PERSON_ID,
  projectSummary,
  PROJECT_ID,
  REVIEWER_ID,
} from './projectFixtures.ts';
import { activity, EXCAVATION_ID, PAVING_ID, SURVEY_ID } from './scheduleFixtures.ts';

// Test data only: fixed ids, each starting differently so a shortened id is recognisable. Dates are relative to today,
// so "overdue" and "updates required" hold whenever the suite runs.
//
// The Coastal road upgrade (PROJECT_ID), ACTIVE, is managed by the entity's user, Huda:
//   "Site preparation" (IN_PROGRESS, Huda, 50 %) with subtasks "Clear the site" (COMPLETED, Huda) and "Fence the site"
//   (IN_PROGRESS, Nora, 20 %, finish 3 days ago, unchanged for 10 days);
//   "Lay drainage" (BLOCKED, Huda, started, no figure), which "Survey the plot" (NOT_STARTED, Huda, planned to start 2
//   days ago) waits for, finish to start.
// The Harbour bridge repair (OTHER_PROJECT_ID), ACTIVE, is managed by Faisal: "Inspect the bridge" (IN_PROGRESS, Huda,
// 30 %, finish yesterday) and "Paint the railings" (NOT_STARTED, Nora).

export const OTHER_PROJECT_ID = '4e4e4e4e-0000-4000-8000-000000000105';
export const PARENT_ID = 'ba000000-0000-4000-8000-000000000f01';
export const CLEARING_ID = 'ba000000-0000-4000-8000-000000000f02';
export const FENCING_ID = 'ba000000-0000-4000-8000-000000000f03';
export const DRAINAGE_ID = 'ba000000-0000-4000-8000-000000000f04';
export const PLOT_SURVEY_ID = 'ba000000-0000-4000-8000-000000000f05';
export const BRIDGE_ID = 'bb000000-0000-4000-8000-000000000f06';
export const RAILINGS_ID = 'bb000000-0000-4000-8000-000000000f07';
export const DEPENDENCY_ID = 'bc000000-0000-4000-8000-000000000f08';
export const SUBTASK_DEPENDENCY_ID = 'bc000000-0000-4000-8000-000000000f09';
export const UNRELATED_USER_ID = '9f9f9f9f-0000-4000-8000-000000000199';
export const PRIORITY_CATALOGUE_ID = 'c0000000-0000-4000-8000-000000000009';
export const HIGH_PRIORITY_ID = 'c9000000-0000-4000-8000-000000000901';
export const TASK_ETAG = '"41"';

/** Today's date moved by `days`, as the API dates a task (UTC). */
export function day(days: number): string {
  return addDays(todayUtc(), days);
}

function at(days: number): string {
  return `${day(days)}T08:00:00Z`;
}

export function task(overrides: Partial<ProjectTaskDetail> = {}): ProjectTaskDetail {
  return {
    id: PLOT_SURVEY_ID,
    projectId: PROJECT_ID,
    scheduleActivityId: SURVEY_ID,
    parentTaskId: null,
    title: { text: 'Survey the plot', language: 'EN' },
    description: null,
    assigneeUserId: ENTITY_USER_ID,
    priorityItemId: null,
    status: 'NOT_STARTED',
    plannedStartDate: day(-2),
    plannedFinishDate: day(4),
    plannedDurationDays: 7,
    actualStartDate: null,
    actualFinishDate: null,
    actualPercentComplete: null,
    percentComplete: 0,
    subtaskCount: 0,
    blockedReason: null,
    completedAt: null,
    reopenedCount: 0,
    createdAt: at(-20),
    createdBy: ENTITY_USER_ID,
    updatedAt: at(-1),
    updatedBy: ENTITY_USER_ID,
    ...overrides,
  };
}

/** The Coastal road upgrade's tasks, as the API lists them: each parent followed by its subtasks. */
export function coastalTasks(): ProjectTaskDetail[] {
  return [
    task({
      id: PARENT_ID,
      title: { text: 'Site preparation', language: 'EN' },
      scheduleActivityId: EXCAVATION_ID,
      status: 'IN_PROGRESS',
      plannedStartDate: day(-10),
      plannedFinishDate: day(5),
      plannedDurationDays: 16,
      actualStartDate: day(-10),
      percentComplete: 50,
      subtaskCount: 2,
    }),
    task({
      id: CLEARING_ID,
      parentTaskId: PARENT_ID,
      title: { text: 'Clear the site', language: 'EN' },
      scheduleActivityId: EXCAVATION_ID,
      status: 'COMPLETED',
      plannedStartDate: day(-10),
      plannedFinishDate: day(-6),
      plannedDurationDays: 5,
      actualStartDate: day(-10),
      actualFinishDate: day(-6),
      actualPercentComplete: 100,
      percentComplete: 100,
      completedAt: at(-6),
    }),
    task({
      id: FENCING_ID,
      parentTaskId: PARENT_ID,
      title: { text: 'Fence the site', language: 'EN' },
      scheduleActivityId: EXCAVATION_ID,
      assigneeUserId: OTHER_PERSON_ID,
      status: 'IN_PROGRESS',
      plannedStartDate: day(-8),
      plannedFinishDate: day(-3),
      plannedDurationDays: 6,
      actualStartDate: day(-8),
      actualPercentComplete: 20,
      percentComplete: 20,
      updatedAt: at(-10),
    }),
    task({
      id: DRAINAGE_ID,
      title: { text: 'Lay drainage', language: 'EN' },
      scheduleActivityId: PAVING_ID,
      status: 'BLOCKED',
      plannedStartDate: day(-1),
      plannedFinishDate: day(10),
      plannedDurationDays: 12,
      actualStartDate: day(-1),
      blockedReason: { text: 'Waiting for the permit', language: 'EN' },
    }),
    task(),
  ];
}

export function harbourTasks(): ProjectTaskDetail[] {
  return [
    task({
      id: BRIDGE_ID,
      projectId: OTHER_PROJECT_ID,
      scheduleActivityId: null,
      title: { text: 'Inspect the bridge', language: 'EN' },
      status: 'IN_PROGRESS',
      plannedStartDate: day(-6),
      plannedFinishDate: day(-1),
      plannedDurationDays: 6,
      actualStartDate: day(-6),
      actualPercentComplete: 30,
      percentComplete: 30,
      updatedAt: at(0),
    }),
    task({
      id: RAILINGS_ID,
      projectId: OTHER_PROJECT_ID,
      scheduleActivityId: null,
      title: { text: 'Paint the railings', language: 'EN' },
      assigneeUserId: OTHER_PERSON_ID,
      plannedStartDate: day(-3),
      plannedFinishDate: day(9),
    }),
  ];
}

export function dependency(overrides: Partial<TaskDependencyDetail> = {}): TaskDependencyDetail {
  return {
    id: DEPENDENCY_ID,
    projectId: PROJECT_ID,
    predecessorTaskId: DRAINAGE_ID,
    successorTaskId: PLOT_SURVEY_ID,
    dependencyType: 'FS',
    createdAt: at(-15),
    createdBy: ENTITY_USER_ID,
    ...overrides,
  };
}

export function coastalDependencies(): TaskDependencyDetail[] {
  return [
    dependency(),
    dependency({
      id: SUBTASK_DEPENDENCY_ID,
      predecessorTaskId: CLEARING_ID,
      successorTaskId: FENCING_ID,
    }),
  ];
}

/** The two projects SCR-063–066 read: Huda manages the first, Faisal the second. */
export function taskProjects(): ProjectSummary[] {
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

function scheduleActivities(): ScheduleActivityDetail[] {
  return [
    activity(),
    activity({ id: EXCAVATION_ID, wbsCode: '1.2', name: { text: 'Excavation', language: 'EN' } }),
    activity({ id: PAVING_ID, wbsCode: '2', name: { text: 'Paving', language: 'EN' } }),
  ];
}

const PRIORITIES: MasterDataItemSummary[] = [
  {
    id: HIGH_PRIORITY_ID,
    catalogueId: PRIORITY_CATALOGUE_ID,
    code: 'HIGH',
    label: { en: 'High', ar: 'عالية' },
    parentItemId: null,
    sortOrder: 1,
    lifecycleState: 'PUBLISHED',
    isSystem: true,
  },
];

export interface TaskState {
  projects?: ProjectSummary[];
  tasks?: ProjectTaskDetail[];
  dependencies?: TaskDependencyDetail[];
  /** SCHEDULE_VIEW; false is 403. */
  schedule?: boolean;
  /** MASTER_DATA_VIEW for the PRIORITY catalogue; false is 403, as today for everyone but R01. */
  priorities?: boolean;
  /** USER_VIEW for the user search; false is 403, as today for everyone but R01. */
  userSearch?: boolean;
}

/** The WF-04 reads, each project's tasks filtered by `projectId` as the API does. Layer over withProjectLookups. */
export function withTasks(
  api: MockApi,
  {
    projects = taskProjects(),
    tasks = [...coastalTasks(), ...harbourTasks()],
    dependencies = coastalDependencies(),
    schedule = true,
    priorities = true,
    userSearch = false,
  }: TaskState = {},
): MockApi {
  const forbidden = problem(403, 'PERMISSION_DENIED');
  const ofProject = <T extends { projectId: string }>(items: T[], query: URLSearchParams) =>
    items.filter((item) => item.projectId === query.get('projectId'));
  api
    .on('GET', /^\/projects$/, { body: page(projects, 200) })
    .on('GET', /^\/project-tasks$/, (request) => ({
      body: page(ofProject(tasks, request.query), 200),
    }))
    .on('GET', /^\/project-tasks\/[^/]+$/, (request) => {
      const found = tasks.find((item) => request.path.endsWith(item.id));
      return found === undefined
        ? problem(404, 'NOT_FOUND')
        : { body: found, headers: { ETag: TASK_ETAG } };
    })
    .on('GET', /^\/task-dependencies$/, (request) => ({
      body: page(ofProject(dependencies, request.query), 200),
    }))
    .on(
      'GET',
      /^\/schedule-activities$/,
      schedule ? { body: page(scheduleActivities(), 200) } : forbidden,
    )
    .on(
      'GET',
      /^\/master-data-catalogues$/,
      priorities ? { body: [{ id: PRIORITY_CATALOGUE_ID, code: 'PRIORITY' }] } : forbidden,
    )
    .on('GET', /^\/master-data-items$/, (request) => ({
      body: page(request.query.get('catalogueId') === PRIORITY_CATALOGUE_ID ? PRIORITIES : [], 200),
    }))
    .on(
      'GET',
      /^\/users$/,
      userSearch
        ? {
            body: page([
              {
                id: UNRELATED_USER_ID,
                userType: 'INTERNAL',
                username: 'sami.unrelated',
                displayName: 'Sami Unrelated',
                email: 'sami@example.test',
                status: 'ACTIVE',
                departmentId: null,
                externalEntityId: null,
              },
            ]),
          }
        : forbidden,
    );
  return api;
}
