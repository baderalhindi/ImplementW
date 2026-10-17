import { type Page, request } from '@playwright/test';

import { eventually, type PlatformApi } from './api.ts';
import { environment } from './environment.ts';
import { expectText, utcDate } from './journey.ts';
import {
  approveTaskOn,
  registration,
  untilStatus,
  type ProjectCrew,
  type ProjectDetail,
} from './projects.ts';

// The execution journeys' own steps (UJ-002, UJ-003, UJ-006): WF-01 and WF-03 taken apart where a journey needs to act between
// them, and the WF-11 runs and inbox decisions WF-05 claims go through. projects.ts's activeProject() is these steps in one go.

/** A project of the journey's own, through WF-01 to Approved – planned: its schedule can be planned, not yet baselined. */
export async function plannedProject(crew: ProjectCrew, title: string): Promise<ProjectDetail> {
  const draft = await crew.creator.post<ProjectDetail>(
    '/projects',
    await registration(crew.approver, { title }),
  );
  await crew.creator.post(`/projects/${draft.id}/submit`, {
    projectManagerUserId: crew.manager.userId,
  });
  await crew.reviewer.post(`/projects/${draft.id}/start-review`);
  await approveTaskOn(crew.approver, draft.id, 'PROJECT_REGISTRATION');
  return untilStatus(crew.approver, draft.id, 'APPROVED_PLANNED');
}

/** WF-03: the project's working schedule with one activity, "1 · Works", 60 days from a week ahead; answers its id. */
export async function scheduleWithOneActivity(
  manager: PlatformApi,
  projectId: string,
): Promise<string> {
  await manager.post('/project-schedules', { projectId });
  const activity = await manager.post<{ id: string }>('/schedule-activities', {
    projectId,
    parentActivityId: null,
    wbsCode: '1',
    name: { text: 'Works', language: 'en' },
    requestedStartDate: utcDate(7),
    plannedDurationDays: 60,
    sortOrder: 0,
  });
  return activity.id;
}

/** WF-03's baseline of the schedule as it stands, submitted by the manager and approved through WF-11, then the project activated. */
export async function baselineAndActivate(crew: ProjectCrew, projectId: string): Promise<string> {
  const baseline = await crew.manager.post<{ id: string }>('/project-baselines', { projectId });
  await crew.manager.post(`/project-baselines/${baseline.id}/submit`, {
    changeAuthorizationId: null,
  });
  await approveTaskOn(crew.approver, baseline.id, 'SCHEDULE_BASELINE');
  await eventually(
    () => crew.manager.get<{ status: string }>(`/project-baselines/${baseline.id}`),
    (b) => b.status === 'ACTIVE',
    `baseline ${baseline.id} to be ACTIVE`,
  );
  await crew.approver.post(`/projects/${projectId}/activate`);
  await untilStatus(crew.approver, projectId, 'ACTIVE');
  return baseline.id;
}

/** The WF-11 run opened on a subject, once its source has started it. */
export async function runOn(
  api: PlatformApi,
  subjectModule: string,
  subjectType: string,
  subjectId: string,
): Promise<string> {
  const page = await eventually(
    () =>
      api.get<{ items: { id: string }[] }>(
        `/approval-instances?subjectModule=${subjectModule}&subjectType=${subjectType}&subjectId=${subjectId}&pageSize=50`,
      ),
    (p) => p.items.length > 0,
    `a WF-11 run on ${subjectType} ${subjectId}`,
  );
  return page.items[0]?.id ?? '';
}

/**
 * Approves a run from the approval inbox, as UJ-001 does: the inbox names a request by subject type and revision, so the row
 * is the one linking to the run. WF-11 assigns the task first; the approver's task list says when it has.
 */
export async function approveInTheInbox(
  approver: { page: Page; api: PlatformApi },
  runId: string,
): Promise<void> {
  await eventually(
    () =>
      approver.api.get<{ items: { instance: { id: string } }[] }>('/approval-tasks?pageSize=100'),
    (tasks) => tasks.items.some((t) => t.instance.id === runId),
    `the task of run ${runId} in ${approver.api.persona}'s inbox`,
  );
  const { page } = approver;
  await page.goto('/approvals/inbox');
  await page
    .getByRole('row')
    .filter({ has: page.locator(`a[href="/approvals/instances/${runId}"]`) })
    .getByRole('button', { name: 'Approve' })
    .click();
  await page
    .getByRole('dialog', { name: 'Approve request' })
    .getByRole('button', { name: 'Approve' })
    .click();
  await expectText(page.getByRole('main'), 'Approved. The request moves');
}

/**
 * A file's bytes as the API serves them to `api`'s session, with the status: PlatformApi reads every answer as JSON, and
 * document content is not JSON.
 */
export async function download(
  api: PlatformApi,
  path: string,
): Promise<{ status: number; body: Buffer }> {
  const http = await request.newContext({
    baseURL: environment.baseUrl(),
    ignoreHTTPSErrors: environment.target === 'compose', // the e2e stack's internal CA (playwright.config.ts)
  });
  try {
    const response = await http.get(`/api/v1${path}`, {
      headers: { Authorization: `Bearer ${api.session.accessToken}` },
    });
    return { status: response.status(), body: await response.body() };
  } finally {
    await http.dispose();
  }
}
