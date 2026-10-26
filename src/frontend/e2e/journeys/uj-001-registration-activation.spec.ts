import { type Page } from '@playwright/test';

import {
  expect,
  expectText,
  signInThroughTheForm,
  test,
  unique,
  utcDate,
} from '../support/journey.ts';
import { activeBaseline, project, untilStatus } from '../support/projects.ts';

// UJ-001 — Registration and activation (Step 18; WF-01; ICD-02).
// Procedure: create and approve a registration; inspect Project lifecycle before and after the governed Project-master
// transition. Expected: formal registration and Planned→Active follow Project-master governance; no standalone Project
// Activation workflow. Negative extension: denied scope and a stale/replayed request fail without unauthorized mutation.

test('UJ-001 registration and activation follow Project-master governance', async ({
  actor,
  visitor,
}) => {
  const title = unique('UJ-001 Coastal road');
  const department = await actor('departmentManager');
  const portfolio = await actor('portfolioManager');
  const manager = await actor('projectManager');
  // The draft creator works in a tab signed in through the form, the way a person arrives.
  const page = visitor;

  await test.step('the department manager signs in and registers a draft', async () => {
    await signInThroughTheForm(page, 'departmentManager', '/projects/new');
    await page.getByLabel('Project title (required)').fill(title);
    await page.getByLabel('Classification (required)').selectOption({ label: 'Infrastructure' });
    await page.getByLabel('Governance profile (required)').selectOption({ label: 'Standard' });
    await page.getByRole('radio', { name: 'Managed by AHDA' }).check();
    await page
      .getByLabel('Owning sector (required)')
      .selectOption({ label: 'E2E Journeys Department' });
    await page.getByLabel('Registration budget (SAR)').fill('8500000');
    await page.getByLabel('Planned start date').fill(utcDate(7));
    await page.getByLabel('Planned end date').fill(utcDate(372));
    await page.getByLabel('Region').selectOption({ label: 'Northern region' });
    await page.getByLabel('City').selectOption({ label: 'North city' });
    await page.getByRole('button', { name: 'Create draft' }).click();

    await expect(page.getByRole('heading', { level: 1, name: title })).toBeVisible();
    await expectText(page.getByRole('main'), 'No Formal Project ID yet');
    await expect(page.locator('.workspace__status')).toContainText('Draft');
  });

  const projectId = /\/projects\/([0-9a-f-]{36})/.exec(page.url())?.[1] ?? '';
  expect(projectId).not.toBe('');
  const draftVersion = (await department.api.ok('GET', `/projects/${projectId}`)).etag;

  await test.step('the draft is submitted naming its Project Manager', async () => {
    await page.getByRole('button', { name: 'Submit for review' }).click();
    const dialog = page.getByRole('dialog', { name: 'Submit for review' });
    await dialog.getByRole('radio', { name: 'Someone else' }).check();
    await dialog
      .getByRole('searchbox', { name: 'Search by name, username or email' })
      .fill('e2e.pm');
    await dialog.getByRole('button', { name: 'Search' }).click();
    await dialog.getByRole('button', { name: 'E2E Project Manager (e2e.pm)' }).click();
    await dialog.getByRole('button', { name: 'Submit for review' }).click();
    await expect(page.locator('.workspace__status')).toContainText('Submitted');
    expect((await project(department.api, projectId)).projectManagerUserId).toBe(
      manager.api.userId,
    );
  });

  await test.step('a command sent against the draft it no longer is (stale If-Match) is refused and changes nothing', async () => {
    const stale = await department.api.send(
      'POST',
      `/projects/${projectId}/start-review`,
      {},
      { ifMatch: draftVersion },
    );
    expect([stale.status, stale.code]).toEqual([412, 'PRECONDITION_FAILED']);
    expect((await project(department.api, projectId)).status).toBe('SUBMITTED');
  });

  await test.step("the department manager starts AHDA's review; a WF-11 run opens on the registration", async () => {
    await changeStatus(page, "Start AHDA's review");
    await expect(page.locator('.workspace__status')).toContainText('Under review');
  });

  await test.step('before the transition: under review, with no Formal Project ID and no activation', async () => {
    const before = await project(portfolio.api, projectId);
    expect([before.status, before.formalProjectId, before.activatedAt]).toEqual([
      'UNDER_REVIEW',
      null,
      null,
    ]);
  });

  const runs = () =>
    portfolio.api.get<{ items: { id: string; routingKey: string }[] }>(
      `/approval-instances?subjectModule=Project&subjectType=Project&subjectId=${projectId}&pageSize=50`,
    );

  await test.step('the portfolio manager approves the registration in the approval inbox', async () => {
    // The inbox names a request by its subject type and revision, not its title: the row is the one linking to this run.
    const runId = (await runs()).items[0]?.id ?? '';
    await portfolio.page.goto('/approvals/inbox');
    const row = portfolio.page
      .getByRole('row')
      .filter({ has: portfolio.page.locator(`a[href="/approvals/instances/${runId}"]`) });
    await row.getByRole('button', { name: 'Approve' }).click();
    await portfolio.page
      .getByRole('dialog', { name: 'Approve request' })
      .getByRole('button', { name: 'Approve' })
      .click();
    await expectText(portfolio.page.getByRole('main'), 'Approved. The request moves');
  });

  await test.step('AHDA approval issues the Formal Project ID and leaves the project Approved – planned', async () => {
    const approved = await untilStatus(portfolio.api, projectId, 'APPROVED_PLANNED');
    expect(approved.formalProjectId).toMatch(/^PRJ-[0-9]{6,}$/);
    expect(approved.activatedAt).toBeNull();
    await portfolio.page.goto(`/projects/${projectId}`);
    await expect(portfolio.page.locator('.workspace__status')).toContainText('Approved – planned');
    await expectText(
      portfolio.page.getByRole('main'),
      `Formal Project ID ${String(approved.formalProjectId)}`,
    );
  });

  await test.step('no standalone activation workflow: the only run on the project is its registration review', async () => {
    expect((await runs()).items.map((r) => r.routingKey)).toEqual(['PROJECT_REGISTRATION']);
  });

  await test.step('activation is the Project-master command, and it needs the WF-03 baseline first (ADR-009)', async () => {
    const early = await portfolio.api.send('POST', `/projects/${projectId}/activate`);
    expect([early.status, early.code]).toEqual([422, 'SCHEDULE_ACTIVE_BASELINE_REQUIRED']);
    expect((await project(portfolio.api, projectId)).status).toBe('APPROVED_PLANNED');

    await activeBaseline(manager.api, portfolio.api, projectId);
    await portfolio.page.reload();
    await changeStatus(portfolio.page, 'Activate the project');
    await expectText(portfolio.page.getByRole('main'), 'The project is now active.');
    await expect(portfolio.page.locator('.workspace__status')).toContainText('Active');
    const active = await project(portfolio.api, projectId);
    expect(active.status).toBe('ACTIVE');
    expect(active.activatedAt).not.toBeNull();
    // Activation started no approval of its own.
    expect((await runs()).items.map((r) => r.routingKey)).toEqual(['PROJECT_REGISTRATION']);
  });

  await test.step('denied scope: another department reaches neither the record nor its commands', async () => {
    const outsider = await actor('otherDepartmentManager');
    await outsider.page.goto(`/projects/${projectId}`);
    await expectText(
      outsider.page.getByRole('main'),
      'The project was not found, or you do not have access to it.',
    );
    const read = await outsider.api.send('GET', `/projects/${projectId}`);
    expect(read.status).toBe(404);
    const move = await outsider.api.send('POST', `/projects/${projectId}/withdraw`);
    expect(move.status).toBe(404);
    expect((await project(portfolio.api, projectId)).status).toBe('ACTIVE');
  });

  await test.step('a replayed activation is refused and changes nothing', async () => {
    const before = await project(portfolio.api, projectId);
    const replay = await portfolio.api.send('POST', `/projects/${projectId}/activate`);
    expect([replay.status, replay.code]).toEqual([409, 'INVALID_TRANSITION']);
    const after = await project(portfolio.api, projectId);
    expect([after.status, after.activatedAt]).toEqual(['ACTIVE', before.activatedAt]);
  });
});

async function changeStatus(page: Page, command: string): Promise<void> {
  await page.getByRole('button', { name: 'Change status' }).click();
  const dialog = page.getByRole('dialog', { name: 'Change status' });
  // One radio per command the state and the person allow; a single command is already chosen.
  const option = dialog.getByRole('radio', { name: command });
  if ((await option.count()) > 0) await option.check();
  else await expect(dialog).toContainText(command);
  await dialog.getByRole('button', { name: 'Apply' }).click();
}
