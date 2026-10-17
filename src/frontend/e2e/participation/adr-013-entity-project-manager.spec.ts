import { type Page } from '@playwright/test';

import { type PlatformApi } from '../support/api.ts';
import { expect, expectText, test, unique, utcDate } from '../support/journey.ts';
import {
  approveTaskOn,
  project,
  type ProjectStatus,
  registration,
  untilStatus,
} from '../support/projects.ts';
import { itemId } from '../support/reference.ts';

// ADR-013 participation amendment (TASK-085): "the entity Project Manager journey".
// ADR-013 (docs/architecture/adrs/ADR-register.md): R04 Project Manager is employer-neutral — a person employed by the
// delivering entity may hold the Project Manager role on their own entity's project, scoped to that project; entities may
// create a project draft that AHDA approves; entities see progress, schedule status and health on their own projects; AHDA
// retains every approval and lifecycle gate.
//
// The entity Project Manager is entityProjectManager: an external user of entity A holding the SHIPPED R04 and R08 profiles
// (stack/provision/20-authority.sql), so the journey tests the shipped matrix as it is. AHDA's people are the e2e Department
// Manager (review) and Portfolio Manager (approval, activation).

test('ADR-013 entity Project Manager journey: the entity drafts and manages its own project, AHDA keeps every gate', async ({
  actor,
}) => {
  const entityPm = await actor('entityProjectManager');
  const department = await actor('departmentManager');
  const portfolio = await actor('portfolioManager');
  const title = unique('ADR-013 Entity depot');
  const page = entityPm.page;

  // Finding (report, TASK-085): the registration form cannot be used by an entity user on the shipped matrix. Shipped R04 and
  // R08 hold no MASTER_DATA_VIEW or ORGANIZATION_VIEW, so SCR-033 says "Your account cannot read some of the lists this form
  // needs" and keeps Create draft disabled. The entity's own PROJECT_REGISTER (ENTITY) is real, so the draft is created
  // through the API by the entity Project Manager, with the reference ids an AHDA session reads.
  const draft = await entityPm.api.post<{ id: string; status: ProjectStatus }>(
    '/projects',
    await registration(portfolio.api, { title, participationMode: 'ENTITY_MANAGED' }),
  );
  const projectId = draft.id;
  const draftVersion = (await entityPm.api.ok('GET', `/projects/${projectId}`)).etag;
  const read = () => project(portfolio.api, projectId);
  const expectStatus = async (status: ProjectStatus) => {
    expect((await read()).status).toBe(status);
  };

  await test.step('the entity Project Manager opens their draft and submits it, naming themselves Project Manager', async () => {
    await page.goto(`/projects/${projectId}`);
    await expect(page.getByRole('heading', { level: 1, name: title })).toBeVisible();
    await expect(page.locator('.workspace__status')).toContainText('Draft');
    await page.getByRole('button', { name: 'Submit for review' }).click();
    const dialog = page.getByRole('dialog', { name: 'Submit for review' });
    await expect(dialog.getByRole('radio', { name: /^Me \(/ })).toBeChecked();
    await dialog.getByRole('button', { name: 'Submit for review' }).click();
    await expect(page.locator('.workspace__status')).toContainText('Submitted');
    const submitted = await read();
    expect([submitted.status, submitted.projectManagerUserId, submitted.externalEntityId]).toEqual([
      'SUBMITTED',
      entityPm.api.userId,
      entityPm.api.session.user.roleAssignments.find((r) => r.externalEntityId !== null)
        ?.externalEntityId,
    ]);
  });

  await test.step('a replayed submission and a command against the draft it no longer is are refused', async () => {
    const replayed = await entityPm.api.send('POST', `/projects/${projectId}/submit`, {
      projectManagerUserId: entityPm.api.userId,
    });
    expect([replayed.status, replayed.code]).toEqual([409, 'INVALID_TRANSITION']);
    const stale = await department.api.send(
      'POST',
      `/projects/${projectId}/start-review`,
      {},
      { ifMatch: draftVersion },
    );
    expect([stale.status, stale.code]).toEqual([412, 'PRECONDITION_FAILED']);
    await expectStatus('SUBMITTED');
  });

  await test.step("AHDA's review is AHDA's: the entity Project Manager can neither start nor be offered it", async () => {
    await expectRefused(entityPm.api, 'POST', `/projects/${projectId}/start-review`);
    await expectStatus('SUBMITTED');
    await page.reload();
    await expect(page.locator('.workspace__status')).toContainText('Submitted');
    await expectNotOffered(page, "Start AHDA's review");
    await department.api.post(`/projects/${projectId}/start-review`);
    await expectStatus('UNDER_REVIEW');
  });

  await test.step('the registration decision is AHDA’s: the entity cannot decide its own run, the Portfolio Manager approves', async () => {
    const taskId = await openTaskOn(portfolio.api, projectId, 'PROJECT_REGISTRATION');
    await expectRefused(entityPm.api, 'POST', `/approval-tasks/${taskId}/approve`, {
      reason: null,
    });
    await expectStatus('UNDER_REVIEW');
    await page.goto('/approvals/inbox');
    await expectText(page.getByRole('main'), 'You do not have approval authority for this.');
    await approveTaskOn(portfolio.api, projectId, 'PROJECT_REGISTRATION');
    const approved = await untilStatus(portfolio.api, projectId, 'APPROVED_PLANNED');
    expect(approved.formalProjectId).toMatch(/^PRJ-[0-9]{6,}$/);
  });

  await test.step('the entity Project Manager plans the schedule and submits its baseline; AHDA alone approves it', async () => {
    await entityPm.api.post('/project-schedules', { projectId });
    await entityPm.api.post('/schedule-activities', {
      projectId,
      parentActivityId: null,
      wbsCode: '1',
      name: { text: 'Works', language: 'en' },
      requestedStartDate: utcDate(7),
      plannedDurationDays: 60,
      sortOrder: 0,
    });
    const baseline = await entityPm.api.post<{ id: string }>('/project-baselines', { projectId });
    await entityPm.api.post(`/project-baselines/${baseline.id}/submit`, {
      changeAuthorizationId: null,
    });
    const taskId = await openTaskOn(portfolio.api, baseline.id, 'SCHEDULE_BASELINE');
    await expectRefused(entityPm.api, 'POST', `/approval-tasks/${taskId}/approve`, {
      reason: null,
    });
    expect(
      (await entityPm.api.get<{ status: string }>(`/project-baselines/${baseline.id}`)).status,
    ).not.toBe('ACTIVE');
    await approveTaskOn(portfolio.api, baseline.id, 'SCHEDULE_BASELINE');
    await expect
      .poll(
        async () =>
          (await entityPm.api.get<{ status: string }>(`/project-baselines/${baseline.id}`)).status,
      )
      .toBe('ACTIVE');
  });

  await test.step('activation is AHDA’s: refused to the entity Project Manager, done by the Portfolio Manager', async () => {
    await expectRefused(entityPm.api, 'POST', `/projects/${projectId}/activate`);
    await expectStatus('APPROVED_PLANNED');
    await page.goto(`/projects/${projectId}`);
    await expect(page.locator('.workspace__status')).toContainText('Approved – planned');
    await expectNotOffered(page, 'Activate the project');
    await portfolio.api.post(`/projects/${projectId}/activate`);
    await untilStatus(portfolio.api, projectId, 'ACTIVE');
  });

  await test.step('the entity Project Manager adds a schedule activity to their active project', async () => {
    await page.goto(`/projects/${projectId}/schedule`);
    await page.getByRole('button', { name: 'Add activity' }).click();
    const dialog = page.getByRole('dialog', { name: 'Add activity' });
    await dialog.getByLabel('WBS code (required)').fill('2');
    await dialog.getByLabel('Name (required)').fill('Site mobilisation');
    await dialog.getByLabel('Duration (working days) (required)').fill('10');
    await dialog.getByRole('button', { name: 'Save' }).click();
    await expect(dialog).toBeHidden();
    await expect(page.getByRole('row').filter({ hasText: 'Site mobilisation' })).toBeVisible();
  });

  await test.step('the entity Project Manager adds a task they own', async () => {
    await page.goto(`/projects/${projectId}/tasks`);
    await page.getByRole('button', { name: 'Add task' }).click();
    const dialog = page.getByRole('dialog', { name: 'Add task' });
    await dialog.getByLabel('Title (required)').fill('Fence the depot site');
    await dialog.getByLabel('Planned start (required)').fill(utcDate(0));
    await dialog.getByLabel('Planned finish (required)').fill(utcDate(5));
    await dialog.getByRole('radio', { name: /^Me \(/ }).check();
    await dialog.getByRole('button', { name: 'Save' }).click();
    await expect(dialog).toBeHidden();
    await expect(page.getByRole('row').filter({ hasText: 'Fence the depot site' })).toBeVisible();
  });

  await test.step('the entity Project Manager registers a risk and raises an issue on their project', async () => {
    // Finding (report, TASK-085): Register risk and Raise issue cannot be used by the entity Project Manager on the shipped
    // matrix — the risk categories, concern categories and priorities are master data the shipped R04/R08 cannot read
    // (no MASTER_DATA_VIEW), so both forms keep Save disabled. Their RISK_MANAGE and CONCERN_RAISE (OWN) are real: the
    // records are created through the API, with the item ids an AHDA session reads, and read back in the browser.
    await entityPm.api.post('/risks', {
      projectId,
      title: { text: 'Late steel delivery', language: 'en' },
      description: {
        text: 'The steel supplier may deliver late, delaying the frame.',
        language: 'en',
      },
      riskCategoryItemId: await itemId(portfolio.api, 'RISK_CATEGORY', 'E2E_COMMERCIAL'),
      ownerUserId: entityPm.api.userId,
      identifiedDate: utcDate(0),
      nextReviewDate: null,
    });
    await entityPm.api.post('/management-concerns', {
      projectId,
      concernType: 'ISSUE',
      title: { text: 'Access road closed', language: 'en' },
      description: { text: 'The access road is closed for repairs.', language: 'en' },
      categoryItemId: await itemId(portfolio.api, 'CONCERN_CATEGORY', 'E2E_SITE_ACCESS'),
      priorityItemId: await itemId(portfolio.api, 'PRIORITY', 'E2E_HIGH'),
      impacts: [],
      targetResolutionDate: null,
    });
    await page.goto(`/projects/${projectId}/risks`);
    await expect(page.getByRole('main')).toContainText('Late steel delivery');
    await page.goto(`/projects/${projectId}/issues-challenges`);
    await expect(page.getByRole('main')).toContainText('Access road closed');
  });

  await test.step('the entity Project Manager sees progress, schedule status and health of their own project', async () => {
    await page.goto(`/projects/${projectId}`);
    const dashboard = page.getByRole('region', { name: 'Project Dashboard' });
    for (const panel of [
      'Lifecycle state',
      'Current Overall Project Health',
      'Published progress',
      'Current schedule health',
      'Open risks by rating',
    ]) {
      const region = dashboard.getByRole('region', { name: panel, exact: true });
      await expect(region).toBeVisible();
      await expect(region).not.toContainText('You may not see this data here.');
    }
    await expect(dashboard.getByRole('region', { name: 'Lifecycle state' })).toContainText(
      'Active',
    );
    await expect(dashboard.getByRole('region', { name: 'Current schedule health' })).toContainText(
      'Green',
    );
    await page.goto(`/projects/${projectId}/schedule`);
    await expect(page.getByRole('region', { name: 'Schedule status' })).toContainText('On track');
    await page.goto(`/projects/${projectId}/progress`);
    await expect(page.getByRole('heading', { name: /Current progress/ })).toBeVisible();
  });

  await test.step('AHDA-only commands stay refused on the active project and change nothing', async () => {
    const before = await read();
    await expectRefused(entityPm.api, 'POST', '/external-update-requests', {
      projectId,
      externalEntityId: before.externalEntityId,
      contributionTypeItemId: await itemId(
        portfolio.api,
        'CONTRIBUTION_TYPE',
        'PROJECT_INFORMATION',
      ),
      targetId: null,
      instructions: { text: 'Report on the depot.', language: 'en' },
      responsibleUserId: null,
      reviewerUserId: null,
      dueDate: null,
    });
    const after = await read();
    expect([after.status, after.revisionNo, after.activatedAt]).toEqual([
      'ACTIVE',
      before.revisionNo,
      before.activatedAt,
    ]);
    const requests = await portfolio.api.get<{ totalCount: number }>(
      `/external-update-requests?projectId=${projectId}`,
    );
    expect(requests.totalCount).toBe(0);
  });
});

/** A refusal of an AHDA-only command: the person may see the record, so 403 PERMISSION_DENIED (R-47), not 404. */
async function expectRefused(
  api: PlatformApi,
  method: 'POST',
  path: string,
  body?: unknown,
): Promise<void> {
  const refused = await api.send(method, path, body ?? {});
  expect([path, refused.status, refused.code]).toEqual([path, 403, 'PERMISSION_DENIED']);
}

/** The status dialog offers only the commands the state and the person allow; this one is not among them. */
async function expectNotOffered(page: Page, command: string): Promise<void> {
  const change = page.getByRole('button', { name: 'Change status' });
  if ((await change.count()) === 0) return;
  await change.click();
  const dialog = page.getByRole('dialog', { name: 'Change status' });
  await expect(dialog).toBeVisible();
  await expect(dialog).not.toContainText(command);
  await dialog.getByRole('button', { name: 'Cancel' }).click();
}

/** The open WF-11 task on a subject, as its approver's inbox lists it. */
async function openTaskOn(
  approver: PlatformApi,
  subjectId: string,
  routingKey: string,
): Promise<string> {
  let taskId: string | undefined;
  await expect
    .poll(
      async () => {
        const tasks = await approver.get<{
          items: {
            taskId: string;
            instance: { routingKey: string; subject: { id: string } };
          }[];
        }>('/approval-tasks?pageSize=100');
        taskId = tasks.items.find(
          (i) => i.instance.subject.id === subjectId && i.instance.routingKey === routingKey,
        )?.taskId;
        return taskId;
      },
      { timeout: 60_000 },
    )
    .toBeDefined();
  return String(taskId);
}
