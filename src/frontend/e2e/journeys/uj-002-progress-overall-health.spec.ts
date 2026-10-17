import { type Locator, type Page } from '@playwright/test';

import { activeProject } from '../support/projects.ts';
import { expect, expectText, test, unique, utcDate } from '../support/journey.ts';

// UJ-002 — Progress and overall health (Step 18; WF-02; BPK-022; ICD-03).
// Participants: the Project Manager (reports WF-04 task progress, WF-03's forecast and WF-02's update) and the progress
// reviewer (AHDA's portfolio manager, who reviews and publishes WF-02's update).
// Procedure: submit permitted progress inputs; publish the applicable source snapshot; compare overall health and domain
// indicators.
// Expected: WF-02 owns Overall Project Health; domain indicators do not overwrite it. Missing or stale inputs do not silently
// become zero or Green.
// Negative extension: denied scope and a stale/replayed request fail without unauthorized mutation.

interface Snapshot {
  overallHealth: string;
  actualPercent: string | number | null;
  scheduleHealth: string | null;
  financialStatus: string | null;
}

interface Submission {
  id: string;
  status: string;
  actualPercentCalculated: string | number | null;
}

const page25 = 'page=1&pageSize=25';

test('UJ-002 WF-02 owns Overall Project Health; missing inputs never become zero or Green', async ({
  actor,
}) => {
  const department = await actor('departmentManager');
  const reviewer = await actor('portfolioManager');
  const manager = await actor('projectManager');
  const crew = {
    creator: department.api,
    reviewer: department.api,
    approver: reviewer.api,
    manager: manager.api,
  };
  const project = await activeProject(crew, { title: unique('UJ-002 Desalination works') });
  const taskTitle = unique('Pour foundations');
  const activity = (
    await manager.api.get<{ items: { id: string }[] }>(
      `/schedule-activities?projectId=${project.id}&pageSize=50`,
    )
  ).items[0];
  const task = await manager.api.post<{ id: string }>('/project-tasks', {
    projectId: project.id,
    parentTaskId: null,
    scheduleActivityId: activity?.id ?? null,
    title: { text: taskTitle, language: 'en' },
    description: null,
    assigneeUserId: manager.api.userId,
    priorityItemId: null,
    plannedStartDate: utcDate(0),
    plannedFinishDate: utcDate(30),
  });
  await manager.api.post(`/project-tasks/${task.id}/start`, {});

  const snapshots = () =>
    reviewer.api.get<{ items: Snapshot[] }>(
      `/published-progress-snapshots?projectId=${project.id}&${page25}`,
    );
  const liveHealth = () =>
    reviewer.api.get<{ items: { overallHealth: string }[] }>(
      `/project-health-statuses?projectId=${project.id}&${page25}`,
    );
  const submissions = () =>
    manager.api.get<{ items: Submission[] }>(
      `/progress-submissions?projectId=${project.id}&${page25}`,
    );
  const dashboard = reviewer.page;
  const widget = (title: string): Locator => dashboard.getByRole('region', { name: title });

  await test.step('the Project Manager reports 40% on the task that carries out the schedule activity (WF-04)', async () => {
    const page = manager.page;
    await page.goto(`/projects/${project.id}/tasks`);
    await page.getByRole('button', { name: taskTitle }).click();
    await page.getByRole('button', { name: 'Report progress' }).click();
    await page.getByLabel('Percentage complete').fill('40');
    await page.getByRole('button', { name: 'Save progress' }).click();
    await expectText(page.getByRole('main'), 'Progress saved.');
    // WF-04 rolls the figure up to the activity in the same transaction (project-task.md).
    const rolledUp = await manager.api.get<{ items: { actualPercentComplete: number }[] }>(
      `/activity-execution-progresses?projectId=${project.id}&${page25}`,
    );
    expect(rolledUp.items.map((i) => i.actualPercentComplete)).toEqual([40]);
  });

  await test.step('before any publication: WF-03 rates the schedule Green, and Overall Health is no data — not zero, not Green', async () => {
    await dashboard.goto(`/projects/${project.id}`);
    await expect(widget('Current schedule health')).toContainText('Green');
    for (const title of ['Current Overall Project Health', 'Published Overall Project Health']) {
      await expect(widget(title)).toContainText('No data');
      await expectText(widget(title), 'It is not zero.');
      await expect(widget(title)).not.toContainText('Green');
    }
    expect((await snapshots()).items).toEqual([]);
    expect((await liveHealth()).items).toEqual([]);
  });

  await test.step("the Project Manager moves the activity's forecast 30 days late; WF-03 rates the schedule Delayed", async () => {
    const page = manager.page;
    await page.goto(`/projects/${project.id}/schedule`);
    await page
      .getByRole('row')
      .filter({ hasText: 'Works' })
      .getByRole('button', { name: 'Update forecast' })
      .click();
    const dialog = page.getByRole('dialog', { name: 'Update forecast' });
    await dialog.getByLabel('Forecast start').fill(utcDate(37));
    await dialog.getByLabel('Forecast finish').fill(utcDate(126));
    await dialog.getByRole('button', { name: 'Save forecast' }).click();
    await expectText(page.getByRole('main'), 'The forecast was saved.');
    await expect(page.getByRole('region', { name: 'Schedule status' })).toContainText('Delayed');
  });

  await test.step('a domain indicator does not overwrite Overall Health: schedule Red beside Overall still no data', async () => {
    await dashboard.goto(`/projects/${project.id}`);
    await expect(widget('Current schedule health')).toContainText('Red');
    for (const title of ['Current Overall Project Health', 'Published Overall Project Health']) {
      await expect(widget(title)).toContainText('No data');
      await expect(widget(title)).not.toContainText('Red');
    }
    expect((await liveHealth()).items).toEqual([]);
  });

  await test.step('denied scope: another department neither reaches the progress nor starts an update', async () => {
    const outsider = await actor('otherDepartmentManager');
    await outsider.page.goto(`/projects/${project.id}/progress`);
    await expectText(
      outsider.page.getByRole('main'),
      'The project was not found, or you do not have access to it.',
    );
    const start = await outsider.api.send('POST', '/progress-submissions', {
      projectId: project.id,
    });
    expect([start.status, start.code]).toEqual([403, 'PERMISSION_DENIED']);
    // progress-update.md D-14: a collection of a project the caller cannot see is an empty page, not a refusal.
    const listed = await outsider.api.send<{ items: unknown[] }>(
      'GET',
      `/progress-submissions?projectId=${project.id}&${page25}`,
    );
    expect([listed.status, listed.body.items]).toEqual([200, []]);
    expect((await submissions()).items).toEqual([]);
  });

  // From here the journey needs WF-02 to derive the update from the inputs above. progress-update.md F-1: Progress has no
  // ADR-003 §8.2 edge to WF-03, WF-04 or WF-14 yet, so NoProgressInputs answers every start 422 PROGRESS_ROLLUP_UNAVAILABLE
  // and the screen says the project "has no work breakdown with planned durations yet" — though it has one, with 40% reported.
  await test.step('the Project Manager starts the progress update; WF-02 derives it from the reported work', async () => {
    const page = manager.page;
    await page.goto(`/projects/${project.id}/progress`);
    await page.getByRole('button', { name: 'Start progress update' }).click();
    const dialog = page.getByRole('dialog', { name: 'Submit progress update' });
    const refusal = page.getByText(
      'Progress cannot be calculated: the project has no work breakdown with planned durations yet.',
    );
    await expect(dialog.or(refusal)).toBeVisible();
    await expect(refusal).toHaveCount(0);
    await expect(dialog).toBeVisible();
    await expectText(dialog, 'Actual');
    await expect(dialog.getByRole('radio', { name: 'Use the calculated figure' })).toBeChecked();
  });

  const draft = (await submissions()).items[0];
  expect(draft?.status).toBe('DRAFT');
  // Derived from the 40% reported on the activity's only task, never typed in and never zero (ADR-009).
  expect(Number(draft?.actualPercentCalculated)).toBe(40);
  const draftId = draft?.id ?? '';
  const draftVersion = (await manager.api.ok('GET', `/progress-submissions/${draftId}`)).etag;

  await test.step('the Project Manager submits the update to AHDA', async () => {
    const dialog = manager.page.getByRole('dialog', { name: 'Submit progress update' });
    await dialog
      .getByLabel('Narrative')
      .fill('Foundations 40% poured; the works forecast slipped 30 days.');
    await dialog.getByRole('button', { name: 'Submit to AHDA' }).click();
    await expect
      .poll(
        async () => (await manager.api.get<Submission>(`/progress-submissions/${draftId}`)).status,
      )
      .toBe('SUBMITTED');
  });

  await test.step('a review started against the draft it no longer is (stale If-Match) is refused and changes nothing', async () => {
    const stale = await reviewer.api.send(
      'POST',
      `/progress-submissions/${draftId}/start-review`,
      {},
      { ifMatch: draftVersion },
    );
    expect([stale.status, stale.code]).toEqual([412, 'PRECONDITION_FAILED']);
    expect((await manager.api.get<Submission>(`/progress-submissions/${draftId}`)).status).toBe(
      'SUBMITTED',
    );
  });

  await test.step('the progress reviewer takes the update into review and publishes it', async () => {
    const status = async () =>
      (await reviewer.api.get<Submission>(`/progress-submissions/${draftId}`)).status;
    await reviewProgress(reviewer.page, project.id, 'Start review');
    await expect.poll(status).toBe('UNDER_REVIEW');
    await reviewProgress(reviewer.page, project.id, 'Publish');
    await expect.poll(status).toBe('PUBLISHED');
  });

  await test.step('a replayed publication is refused and writes no second snapshot', async () => {
    const replay = await reviewer.api.send('POST', `/progress-submissions/${draftId}/publish`, {});
    expect([replay.status, replay.code]).toEqual([409, 'INVALID_TRANSITION']);
    expect((await snapshots()).items).toHaveLength(1);
  });

  await test.step('compare: WF-02 published its own Overall Health from the inputs; a missing input is Unknown, never zero or Green', async () => {
    const [snapshot] = (await snapshots()).items;
    // The reported work is published as 40%, not as zero.
    expect(Number(snapshot?.actualPercent)).toBe(40);
    // No financial status is published (WF-14 reported nothing): a dimension without its input makes Overall UNKNOWN
    // (progress-update.md D-4), never a colour.
    expect(snapshot?.financialStatus ?? null).toBeNull();
    expect(snapshot?.overallHealth).toBe('UNKNOWN');
    await reviewer.page.goto(`/projects/${project.id}/progress`);
    const official = reviewer.page.getByRole('region', { name: /Official progress/ });
    await expect(official).toContainText('Unknown');
    await expect(official).not.toContainText('Green');

    // The dashboard shows WF-02's published value beside WF-03's indicator: Red schedule health has not become Overall Health.
    await dashboard.goto(`/projects/${project.id}`);
    await expect(widget('Current schedule health')).toContainText('Red');
    await expect(widget('Published Overall Project Health')).not.toContainText('Green');
    await expect(widget('Published Overall Project Health')).not.toContainText('Red');
  });
});

/** MOD-022 from the Progress tab: the reviewer opens the update and takes the decision named. */
async function reviewProgress(page: Page, projectId: string, decision: 'Start review' | 'Publish') {
  await page.goto(`/projects/${projectId}/progress`);
  await page.getByRole('button', { name: 'Review update' }).click();
  const dialog = page.getByRole('dialog', { name: 'Review progress update' });
  if (decision === 'Start review') {
    await dialog.getByRole('button', { name: 'Start review' }).click();
    return;
  }
  await dialog.getByRole('radio', { name: 'Publish' }).check();
  await dialog.getByRole('button', { name: 'Confirm decision' }).click();
}
