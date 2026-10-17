import { type Locator, type Page } from '@playwright/test';

import { expect, expectText, test, unique, utcDate } from '../support/journey.ts';
import { type ProjectTask, startedTask } from '../support/participation.ts';
import { activeProject } from '../support/projects.ts';

// UJ-005 — External contribution staging (Step 18; WF-13; BPK-023; ICD-07).
// Participants: "External entity contributor and internal reviewer".
// Procedure: "Submit an assigned external contribution; accept it internally; observe staged and source-applied outcomes
// separately." Expected: "Acceptance alone does not mutate authoritative source facts; source application is explicit and
// scoped." Negative extension: "Denied scope and stale/replayed request; confirm source-appropriate failure without
// unauthorized mutation."
//
// The request is AHDA's (external-participation.md D-9): the project's own Project Manager (E2E-R04, OWN) drafts, issues,
// reviews and applies it; entity A's contributor (shipped R08: respond at ASSIGNED) answers it. The source is a WF-04 task's
// actual percentage, the one typed adapter (D-8, D-10).

interface Revision {
  id: string;
  status: string;
}

interface Attempt {
  id: string;
  status: string;
  attemptNo: number;
}

test('UJ-005 an accepted external contribution is staged, and only an explicit application changes its source', async ({
  actor,
}) => {
  const department = await actor('departmentManager');
  const portfolio = await actor('portfolioManager');
  const manager = await actor('projectManager');
  const contributor = await actor('entityContributor');
  const taskTitle = unique('UJ-005 Excavation');

  const project = await activeProject(
    {
      creator: department.api,
      reviewer: department.api,
      approver: portfolio.api,
      manager: manager.api,
    },
    { participationMode: 'ENTITY_MANAGED' },
  );
  const task = await startedTask(manager.api, project.id, taskTitle);
  const readTask = () => manager.api.get<ProjectTask>(`/project-tasks/${task.id}`);
  const before = await readTask();
  expect([before.status, before.actualPercentComplete]).toEqual(['IN_PROGRESS', null]);

  await test.step('the Project Manager asks entity A for the task progress, naming the contributor', async () => {
    const page = manager.page;
    await page.goto(`/projects/${project.id}/external-requests`);
    await page.getByRole('link', { name: 'Create request' }).click();
    await expect(
      page.getByRole('heading', { level: 1, name: 'Create update request' }),
    ).toBeVisible();
    await expect(page.getByLabel('External entity (required)')).toHaveValue(
      String(project.externalEntityId),
    );
    await page
      .getByLabel('Contribution type (required)')
      .selectOption({ label: 'Task progress (TASK_PROGRESS)' });
    await page.getByLabel('Source record (required)').selectOption({ label: taskTitle });
    await page
      .getByLabel('Instructions (required)')
      .fill('Report the excavation percentage your site team measured.');
    await page.getByLabel('Response due').fill(utcDate(14));
    const responder = page.getByRole('group', { name: 'Responder (entity user)' });
    await responder.getByRole('radio', { name: 'Someone else' }).check();
    await responder.getByLabel('User ID').fill(contributor.api.userId);
    await page.getByRole('button', { name: 'Save and issue' }).click();
    await expectText(page.getByRole('main'), 'Request issued.');
  });

  const requestId = /\/external-requests\/([0-9a-f-]{36})/.exec(manager.page.url())?.[1] ?? '';
  expect(requestId).not.toBe('');
  const readRequest = () =>
    manager.api.get<{ status: string }>(`/external-update-requests/${requestId}`);
  expect((await readRequest()).status).toBe('ISSUED');

  await test.step('the entity contributor finds the request assigned to them and submits 40 %', async () => {
    const page = contributor.page;
    await page.goto('/my-external-requests');
    await page.locator(`a[href="/external-requests/${requestId}"]`).first().click();
    await expectText(page.getByRole('main'), taskTitle);
    await page.getByLabel(/Actual percent complete/).fill('40');
    await page.getByLabel(/Progress note/).fill('Two of three excavators on site.');
    await page.getByRole('button', { name: 'Submit response' }).click();
    await page
      .getByRole('dialog', { name: 'Submit your response' })
      .getByRole('button', { name: 'Submit response' })
      .click();
    await expectText(page.getByRole('main'), 'Response submitted to AHDA for review.');
  });

  const revisions = () =>
    manager.api.get<{ items: Revision[] }>(
      `/external-contributions?externalUpdateRequestId=${requestId}`,
    );
  const contributionId = (await revisions()).items[0]?.id ?? '';
  const submitted = await manager.api.ok<Revision>(
    'GET',
    `/external-contributions/${contributionId}`,
  );
  expect(submitted.body.status).toBe('SUBMITTED');

  await test.step('the Project Manager reviews and accepts it: staged, the task unchanged', async () => {
    const page = manager.page;
    await page.goto(`/external-contributions/${contributionId}`);
    await page.getByRole('button', { name: 'Start review' }).click();
    await page
      .getByRole('dialog', { name: 'Start the review' })
      .getByRole('button', { name: 'Start review' })
      .click();
    await expectText(page.getByRole('main'), 'Review started.');

    // A decision sent against the revision as submitted, before its review started, is stale and decides nothing.
    const stale = await manager.api.send(
      'POST',
      `/external-contributions/${contributionId}/accept`,
      { internalNote: null },
      { ifMatch: submitted.etag },
    );
    expect([stale.status, stale.code]).toEqual([412, 'PRECONDITION_FAILED']);
    expect(
      (await manager.api.get<Revision>(`/external-contributions/${contributionId}`)).status,
    ).toBe('UNDER_REVIEW');

    await page.getByRole('button', { name: 'Accept', exact: true }).click();
    const dialog = page.getByRole('dialog', { name: 'Accept the response' });
    await dialog.getByLabel(/Internal note/).fill('Matches the site visit.');
    await dialog.getByRole('button', { name: 'Accept' }).click();
    await expectText(page.getByRole('main'), 'Response accepted.');
    await expectText(page.getByRole('main'), 'Accepted, pending application');

    const staged = await manager.api.get<Revision>(`/external-contributions/${contributionId}`);
    expect(staged.status).toBe('ACCEPTED_PENDING_APPLICATION');
    expect((await readRequest()).status).toBe('RESPONDED');
    const untouched = await readTask();
    expect([untouched.status, untouched.actualPercentComplete]).toEqual(['IN_PROGRESS', null]);
    await expect(await taskProgressCell(page, project.id, taskTitle)).toHaveText('0%');
  });

  await test.step('the entity reads its answer as accepted, never AHDA’s application state', async () => {
    await contributor.page.goto(`/external-requests/${requestId}`);
    await expectText(contributor.page.getByRole('main'), 'Accepted');
    await expect(contributor.page.getByRole('main')).not.toContainText('pending application');
  });

  let applyKey = '';
  await test.step('source application monitoring shows it pending; the Project Manager applies it explicitly', async () => {
    const page = manager.page;
    await page.goto('/external-requests/applications');
    const card = page.getByRole('listitem').filter({ hasText: taskTitle });
    await expect(card).toContainText('Pending application');
    page.on('request', (request) => {
      if (request.method() === 'POST' && request.url().endsWith('/api/v1/source-applications'))
        applyKey = request.headers()['idempotency-key'] ?? '';
    });
    await card.getByRole('button', { name: 'Apply to source' }).click();
    await page
      .getByRole('dialog', { name: 'Apply to the source record' })
      .getByRole('button', { name: 'Apply to source' })
      .click();
    await expectText(page.getByRole('main'), 'Applied to the source record.');
    await expect(card).toContainText('Applied');
  });

  const attempts = () =>
    manager.api.get<{ items: Attempt[] }>(
      `/source-applications?externalContributionId=${contributionId}`,
    );

  await test.step('the application changed the task’s percentage and nothing else, and closed the request', async () => {
    await expect(await taskProgressCell(manager.page, project.id, taskTitle)).toHaveText('40%');
    const applied = await readTask();
    expect([applied.status, applied.actualPercentComplete]).toEqual(['IN_PROGRESS', 40]);
    expect(
      (await manager.api.get<Revision>(`/external-contributions/${contributionId}`)).status,
    ).toBe('APPLIED');
    expect((await readRequest()).status).toBe('CLOSED');
    expect((await attempts()).items.map((a) => [a.attemptNo, a.status])).toEqual([[1, 'APPLIED']]);
  });

  await test.step('replayed: the same application again, and a second decision, change nothing', async () => {
    expect(applyKey).not.toBe('');
    const firstAttempt = (await attempts()).items[0]?.id;
    const replay = await manager.api.send<Attempt>(
      'POST',
      '/source-applications',
      { externalContributionId: contributionId },
      { idempotencyKey: applyKey },
    );
    expect([replay.status, replay.body.id]).toEqual([201, firstAttempt]);
    const again = await manager.api.send('POST', '/source-applications', {
      externalContributionId: contributionId,
    });
    expect([again.status, again.code]).toEqual([409, 'SOURCE_APPLICATION_ALREADY_COMPLETED']);
    const decided = await manager.api.send(
      'POST',
      `/external-contributions/${contributionId}/accept`,
      { internalNote: null },
    );
    expect([decided.status, decided.code]).toEqual([409, 'CONTRIBUTION_ALREADY_DECIDED']);
    expect((await attempts()).items).toHaveLength(1);
    expect((await readTask()).actualPercentComplete).toBe(40);
  });

  await test.step('denied scope: entity B reaches neither the request nor its answer; the entity applies nothing', async () => {
    const outsider = await actor('otherEntityProjectManager');
    await outsider.page.goto(`/external-requests/${requestId}`);
    await expectText(
      outsider.page.getByRole('main'),
      'This update request was not found, or you do not have access to it.',
    );
    for (const path of [
      `/external-update-requests/${requestId}`,
      `/external-contributions/${contributionId}`,
    ]) {
      const read = await outsider.api.send('GET', path);
      expect([path, read.status, read.code]).toEqual([path, 404, 'NOT_FOUND']);
    }
    const listed = await outsider.api.get<{ items: unknown[]; totalCount: number }>(
      `/external-update-requests?projectId=${project.id}`,
    );
    expect([listed.items.length, listed.totalCount]).toEqual([0, 0]);
    const answer = await outsider.api.send('POST', '/external-contributions', {
      externalUpdateRequestId: requestId,
      fields: [{ fieldCode: 'actualPercentComplete', value: '90' }],
    });
    expect([answer.status, answer.code]).toEqual([404, 'NOT_FOUND']);

    // Application is AHDA's: the contributor whose answer it is can neither apply it nor see the attempts.
    const byEntity = await contributor.api.send('POST', '/source-applications', {
      externalContributionId: contributionId,
    });
    expect([byEntity.status, byEntity.code]).toEqual([403, 'PERMISSION_DENIED']);
    const attemptRead = await contributor.api.send(
      'GET',
      `/source-applications/${String((await attempts()).items[0]?.id)}`,
    );
    expect(attemptRead.status).toBe(404);

    expect((await attempts()).items).toHaveLength(1);
    expect((await readTask()).actualPercentComplete).toBe(40);
    expect((await revisions()).items.map((r) => r.status)).toEqual(['APPLIED']);
  });
});

/** The Progress cell of a task's row on the project's Tasks tab (SCR-047), as the Project Manager sees it. */
async function taskProgressCell(page: Page, projectId: string, title: string): Promise<Locator> {
  await page.goto(`/projects/${projectId}/tasks`);
  return page.getByRole('row').filter({ hasText: title }).getByRole('cell').last();
}
