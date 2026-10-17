import { randomUUID } from 'node:crypto';

import { type PlatformApi } from '../support/api.ts';
import { runFixture } from '../support/fixtures.ts';
import { expect, expectText, test } from '../support/journey.ts';
import { activeProject, project } from '../support/projects.ts';

// UJ-013 — Technical recovery (Step 18; FG-05; BPK-009; ICD-14).
// Participants: integration operator and source owner.
// Procedure: replay a failed technical integration using an approved recovery fixture; compare business lifecycle before and
// after. Expected: technical recovery does not itself change business lifecycle or duplicate source application. Negative
// extension: denied scope and stale/replayed request; confirm source-appropriate failure without unauthorized mutation.
//
// The failed integration is the directory reconciliation, the one operation FG-05 executes and an operator may replay
// (integration-monitoring.md D-2, D-6, D-8): e-mail and sign-in attempts are made and retried by their own modules, and FG-05
// refuses to replay them. The recovery fixture `uj-013-reconciliation-dead-letter` leaves the lineage a directory outage
// leaves — three technical failures, the last a dead letter (IntegrationMonitoring/IntegrationFailureSimulationTests). The
// integration operator is the administrator (R01 holds INTEGRATION_*, D-16) on ADM-047 and ADM-048. The business records are
// the people the reconciliation compares (IdentityAccess's, never written by FG-05, D-8) and an ACTIVE project of the
// journey's own, whose owner — the department manager — sees it before and after.

interface Invocation {
  id: string;
  attemptNo: number;
  status: 'IN_PROGRESS' | 'SUCCEEDED' | 'FAILED' | 'RETRY_SCHEDULED' | 'DEAD_LETTER';
  syncRunId: string | null;
  correlationId: string;
  idempotencyKey: string | null;
  sourceModule: string | null;
  operationCode: string;
}

interface InvocationDetail {
  invocation: Invocation;
  lineage: Invocation[];
  retryPermitted: boolean;
}

interface SyncRun {
  id: string;
  status: string;
  recordsRead: number;
  recordsApplied: number;
}

interface DirectoryPerson {
  id: string;
  username: string;
  jobTitle: string | null;
  departmentId: string | null;
  managerUserId: string | null;
  status: string;
}

test('UJ-013 replaying a failed integration recovers it without changing business lifecycle or applying twice', async ({
  actor,
}) => {
  const department = await actor('departmentManager');
  const portfolio = await actor('portfolioManager');
  const manager = await actor('projectManager');
  const operator = await actor('administrator');

  const active = await activeProject({
    creator: department.api,
    reviewer: department.api,
    approver: portfolio.api,
    manager: manager.api,
  });
  const correlationId = randomUUID();
  const [deadLetterId = ''] = await runFixture('uj-013-reconciliation-dead-letter', {
    lineageKey: randomUUID(),
    correlationId,
  });
  expect(deadLetterId).toMatch(/^[0-9a-f-]{36}$/);

  const attempt = (id: string) =>
    operator.api.get<InvocationDetail>(`/integration-invocations/${id}`);
  const runs = async () =>
    (
      await operator.api.get<{ items: SyncRun[] }>(
        `/sync-runs?correlationId=${correlationId}&pageSize=50`,
      )
    ).items;

  let peopleBefore: DirectoryPerson[] = [];
  let projectBefore = active;

  await test.step('before: the dead letter is the latest of three technical failures, and the business records stand', async () => {
    const before = await attempt(deadLetterId);
    expect(before.lineage.map((a) => [a.attemptNo, a.status])).toEqual([
      [1, 'FAILED'],
      [2, 'FAILED'],
      [3, 'DEAD_LETTER'],
    ]);
    expect(before.retryPermitted).toBe(true);
    expect((await runs()).map((r) => [r.status, r.recordsApplied])).toEqual([
      ['FAILED', 0],
      ['FAILED', 0],
      ['FAILED', 0],
    ]);
    peopleBefore = await directoryPeople(operator.api);
    expect(peopleBefore.length).toBeGreaterThan(0);
    projectBefore = await project(department.api, active.id);
    expect(projectBefore.status).toBe('ACTIVE');
    await department.page.goto(`/projects/${active.id}`);
    await expect(department.page.locator('.workspace__status')).toContainText('Active');
  });

  await test.step('the integration operator finds the dead letter on Integration monitoring and opens it', async () => {
    await operator.page.goto('/admin/integration-monitoring');
    const deadLetters = operator.page.getByRole('table', { name: 'Dead letters' });
    await deadLetters.locator(`a[href="/admin/integration-logs/${deadLetterId}"]`).click();
    await expect(
      operator.page.getByRole('heading', { level: 1, name: 'Integration attempt' }),
    ).toBeVisible();
    await expectText(operator.page.getByRole('main'), correlationId);
    const lineage = operator.page.getByRole('table', { name: 'Every attempt of this operation' });
    await expect(lineage.getByRole('row')).toHaveCount(4); // the header and three attempts
    await expect(lineage.getByRole('row').last()).toContainText('Dead letter');
  });

  await test.step('stale: a retry of a superseded attempt of the lineage is refused, and nothing runs', async () => {
    const superseded = (await attempt(deadLetterId)).lineage.find((a) => a.attemptNo === 2);
    const stale = await operator.api.send(
      'POST',
      `/integration-invocations/${superseded?.id ?? ''}/retry`,
      {},
    );
    expect([stale.status, stale.code]).toEqual([409, 'INVOCATION_NOT_RETRYABLE']);
    expect((await attempt(deadLetterId)).lineage).toHaveLength(3);
  });

  await test.step('denied: a person without the integration permissions reaches neither the screen nor the replay', async () => {
    await portfolio.page.goto(`/admin/integration-logs/${deadLetterId}`);
    await expect(
      portfolio.page.getByRole('heading', { level: 1, name: 'Access denied' }),
    ).toBeVisible();
    const denied = await portfolio.api.send(
      'POST',
      `/integration-invocations/${deadLetterId}/retry`,
      {},
    );
    expect([denied.status, denied.code]).toEqual([403, 'PERMISSION_DENIED']);
    expect((await attempt(deadLetterId)).lineage).toHaveLength(3);
  });

  let recoveredId = '';

  await test.step('the operator replays it: one new attempt, under the same lineage key and correlation ID, succeeds', async () => {
    await operator.page.getByRole('button', { name: 'Retry now' }).click();
    const dialog = operator.page.getByRole('dialog', { name: 'Retry this operation now?' });
    await dialog.getByRole('button', { name: 'Retry now' }).click();
    await expect(operator.page).not.toHaveURL(new RegExp(deadLetterId));
    recoveredId = /\/admin\/integration-logs\/([0-9a-f-]{36})/.exec(operator.page.url())?.[1] ?? '';
    const state = operator.page
      .getByRole('main')
      .locator('dt', { hasText: /^State$/ })
      .locator('xpath=following-sibling::dd[1]');
    await expect(state).toHaveText('Succeeded');
    await expect(operator.page.getByRole('button', { name: 'Retry now' })).toHaveCount(0);
    const lineage = operator.page.getByRole('table', { name: 'Every attempt of this operation' });
    await expect(lineage.getByRole('row')).toHaveCount(5);

    const recovered = await attempt(recoveredId);
    const first = recovered.lineage[0];
    expect([
      recovered.invocation.attemptNo,
      recovered.invocation.status,
      recovered.invocation.correlationId,
    ]).toEqual([4, 'SUCCEEDED', correlationId]);
    expect(recovered.invocation.idempotencyKey).toBe(first?.idempotencyKey);
    expect(recovered.lineage.map((a) => a.status)).toEqual([
      'FAILED',
      'FAILED',
      'FAILED',
      'SUCCEEDED',
    ]);
  });

  await test.step('no source application: the recovered run compared the directory and applied nothing', async () => {
    const recovered = (await runs()).find((r) => r.status === 'SUCCEEDED');
    expect(recovered?.recordsRead).toBeGreaterThan(0);
    expect(recovered?.recordsApplied).toBe(0);
  });

  await test.step('after: the people the run compared and the project are exactly as before, and the owner still sees it Active', async () => {
    expect(await directoryPeople(operator.api)).toEqual(peopleBefore);
    expect(await project(department.api, active.id)).toEqual(projectBefore);
    await department.page.reload();
    await expect(department.page.locator('.workspace__status')).toContainText('Active');
  });

  await test.step('replayed: retrying the recovered dead letter again is refused, with no second attempt and no second run', async () => {
    const replay = await operator.api.send(
      'POST',
      `/integration-invocations/${deadLetterId}/retry`,
      {},
    );
    expect([replay.status, replay.code]).toEqual([409, 'INVOCATION_ALREADY_SUCCEEDED']);
    expect((await attempt(recoveredId)).lineage).toHaveLength(4);
    expect((await runs()).filter((r) => r.status === 'SUCCEEDED')).toHaveLength(1);
    expect(await directoryPeople(operator.api)).toEqual(peopleBefore);
  });

  await test.step("source-appropriate: an attempt another module made is that module's to retry, and FG-05 refuses it", async () => {
    const observed = await operator.api.get<{ items: Invocation[] }>(
      '/integration-invocations?operationCode=AUTHENTICATE&pageSize=1',
    );
    const signIn = observed.items[0];
    expect(signIn?.sourceModule).toBe('IdentityAccess');
    const refused = await operator.api.send(
      'POST',
      `/integration-invocations/${signIn?.id ?? ''}/retry`,
      {},
    );
    expect([refused.status, refused.code]).toEqual([422, 'RETRY_NOT_PERMITTED']);
  });
});

/** The internal people the directory reconciliation compares, with the attributes it compares (D-8) and their status. */
async function directoryPeople(api: PlatformApi): Promise<DirectoryPerson[]> {
  const page = await api.get<{ items: { id: string; userType: string }[] }>(
    '/users?q=e2e.&pageSize=100',
  );
  const people: DirectoryPerson[] = [];
  for (const user of page.items.filter((u) => u.userType === 'INTERNAL')) {
    const person = await api.get<DirectoryPerson>(`/users/${user.id}`);
    people.push({
      id: person.id,
      username: person.username,
      jobTitle: person.jobTitle,
      departmentId: person.departmentId,
      managerUserId: person.managerUserId,
      status: person.status,
    });
  }
  return people.sort((a, b) => a.username.localeCompare(b.username));
}
