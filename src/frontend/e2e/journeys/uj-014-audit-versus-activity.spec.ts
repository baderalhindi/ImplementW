import { type Page, type Request } from '@playwright/test';

import { auditEvents, type AuditEvent } from '../support/access-audit.ts';
import { expect, expectText, plain, test, unique, utcDate } from '../support/journey.ts';
import { activeProject } from '../support/projects.ts';

// UJ-014 — Audit versus activity (Step 18; FG-06; BPK-008, BPK-028; ICD-15).
// Procedure: perform an auditable business action; inspect its permitted activity view, formal audit record and QA
// operational evidence. Expected: Formal Audit, Activity and operational logs remain distinct, correlated and appropriately
// restricted. Negative extension: denied scope and a stale/replayed request fail without unauthorized mutation.
//
// The business action is a task the Project Manager adds in the browser. Correlation is the request's X-Correlation-Id
// (api-conventions R-41, R-42): the SPA's response carries it, the formal audit event records it, and the Activity line
// names the audit event it was derived from (audit-activity.md D-8). Operational logs are not readable by the suite on any
// environment (no log access from the runner, the stand-ins or SIT's QA service), so the journey asserts what the platform
// exposes of its own operation — the correlation id on every answer, refusals included, and ADM-052's narrow technical
// view of the audit pipeline — and the log line itself stays a manual check (docs/testing/e2e-suite.md).

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;

interface ActivityEntry {
  id: string;
  auditEventId: string;
  eventType: string;
  summaryEn: string;
  actorDisplayName: string | null;
  subjectId: string | null;
}

interface AuditEventDetail {
  event: AuditEvent;
  attributes: { name: string }[];
  integrity: { eventHash: string };
}

test('UJ-014 formal audit, activity and operational evidence stay distinct, correlated and restricted', async ({
  actor,
}) => {
  const department = await actor('departmentManager');
  const reviewer = await actor('portfolioManager');
  const manager = await actor('projectManager');
  const administrator = await actor('administrator');
  const outsider = await actor('otherDepartmentManager');

  const project = await activeProject({
    creator: department.api,
    reviewer: department.api,
    approver: reviewer.api,
    manager: manager.api,
  });
  const taskTitle = unique('UJ-014 Survey the coastal corridor');
  const taskNote = unique('UJ-014 internal note on the land negotiation');
  const page = manager.page;

  let correlationId = '';
  let taskId = '';

  await test.step('the Project Manager adds a task to the project in the browser', async () => {
    await page.goto(`/projects/${project.id}/tasks`);
    await page.getByRole('button', { name: 'Add task' }).click();
    const dialog = page.getByRole('dialog', { name: 'Add task' });
    await dialog.getByLabel('Title').fill(taskTitle);
    await dialog.getByLabel('Description').fill(taskNote);
    await dialog.getByLabel('Planned start').fill(utcDate(7));
    await dialog.getByLabel('Planned finish').fill(utcDate(37));
    await dialog.getByRole('radio', { name: /^Me \(/ }).check();
    const created = page.waitForResponse(
      (r) =>
        r.request().method() === 'POST' && new URL(r.url()).pathname === '/api/v1/project-tasks',
    );
    await dialog.getByRole('button', { name: 'Save' }).click();
    const response = await created;
    expect(response.status()).toBe(201);
    correlationId = response.headers()['x-correlation-id'] ?? '';
    expect(correlationId).toMatch(UUID);
    // The id is the server's: the SPA sends none of its own (R-41 asks it to; reported as a finding of TASK-085).
    taskId = ((await response.json()) as { id: string }).id;
    await expectText(page.getByRole('main'), 'Task added.');
    await expect(page.getByRole('region', { name: 'Tasks', exact: true })).toContainText(taskTitle);
  });

  const created = async (): Promise<AuditEvent | undefined> =>
    (await auditEvents(reviewer.api, `correlationId=${correlationId}`)).find(
      (e) => e.eventType === 'ProjectTask.TaskCreated',
    );

  await test.step('the formal audit record carries the action’s correlation id', async () => {
    const event = await created();
    expect(event).toMatchObject({
      category: 'BUSINESS',
      eventType: 'ProjectTask.TaskCreated',
      outcome: 'SUCCESS',
      correlationId,
      subjectId: taskId,
      actorUserId: manager.api.userId,
    });
  });
  const auditEventId = (await created())?.id ?? '';

  await test.step('the Activity view shows the action in plain language, derived from that record, with no restricted field', async () => {
    const reads = watchApi(page);
    await page.goto(`/projects/${project.id}/activity-history`);
    const timeline = page.getByRole('list', { name: 'Activity history' });
    await expect(timeline).toContainText('Task created');
    const line = timeline.getByRole('listitem').filter({ hasText: 'Task created' }).first();
    await expectText(line, 'by E2E Project Manager');
    // A line carries no value, name, title or note of the record (audit-activity.md D-12).
    await expect(page.getByRole('region', { name: 'Activity history' })).not.toContainText(
      taskTitle,
    );
    await expect(page.getByRole('region', { name: 'Activity history' })).not.toContainText(
      taskNote,
    );
    // SCR-057 reads only the Activity projection, never the audit store (audit-activity-ui.md D-1).
    expect(reads.some((path) => path.startsWith('/api/v1/activity-entries'))).toBe(true);
    expect(reads.some((path) => path.startsWith('/api/v1/audit-events'))).toBe(false);

    const entries = await activityOf(manager.api, project.id, taskId);
    expect(entries.map((e) => [e.eventType, e.summaryEn, e.auditEventId])).toEqual([
      ['ProjectTask.TaskCreated', 'Task created', auditEventId],
    ]);
    const raw = JSON.stringify(entries);
    for (const restricted of [taskTitle, taskNote, 'client_address', 'attributes']) {
      expect(raw).not.toContain(restricted);
    }
  });

  await test.step('the audit reviewer reads the formal record in its own tab, correlation id and attributes included', async () => {
    const audit = reviewer.page;
    const reads = watchApi(audit);
    await audit.goto(`/projects/${project.id}/audit-history`);
    await expect(
      audit.getByRole('heading', { level: 2, name: 'Formal audit history' }),
    ).toBeVisible();
    const row = audit
      .getByRole('table', { name: "This project's audit events" })
      .getByRole('row')
      .filter({ hasText: 'ProjectTask.TaskCreated' });
    await expect(row).toHaveCount(1);
    await expectText(row, 'Business');
    await expectText(row, 'E2E Project Manager');
    await row.getByRole('link', { name: /^Open/ }).click();
    await expect(audit).toHaveURL(
      new RegExp(`/projects/${project.id}/audit-history/${auditEventId}$`),
    );
    await expectText(audit.getByRole('main'), correlationId);
    await expectText(
      audit.getByRole('main'),
      'Audit evidence is immutable: this event cannot be edited or deleted, here or anywhere else.',
    );
    // SCR-058 reads only the audit store, never the Activity projection.
    expect(reads.some((path) => path.startsWith('/api/v1/audit-events'))).toBe(true);
    expect(reads.some((path) => path.startsWith('/api/v1/activity-entries'))).toBe(false);

    const detail = await reviewer.api.get<AuditEventDetail>(`/audit-events/${auditEventId}`);
    expect(detail.event.correlationId).toBe(correlationId);
    expect(detail.attributes.length).toBeGreaterThan(0);
  });

  await test.step("in scope: the Project Manager reads its own project's formal audit beside Activity, in the page and at the API", async () => {
    // The agreed matrix gives AUDIT_BUSINESS_VIEW to R02–R07 in their scope (shipped-permission-matrix.md, review H4): a Project
    // Manager reads the business audit of the projects it manages. Another department and the administrator are refused below.
    const sections = page.getByRole('navigation', { name: 'Project sections' });
    await page.goto(`/projects/${project.id}/audit-history`);
    await expect(
      page.getByRole('heading', { level: 2, name: 'Formal audit history' }),
    ).toBeVisible();
    await expect(
      page
        .getByRole('table', { name: "This project's audit events" })
        .getByRole('row')
        .filter({ hasText: 'ProjectTask.TaskCreated' }),
    ).toHaveCount(1);
    await expect(sections.getByRole('link', { name: 'Activity history' })).toBeVisible();

    const list = await auditEvents(manager.api, `category=BUSINESS&projectId=${project.id}`);
    expect(list.map((e) => e.id)).toContain(auditEventId);
    const detail = await manager.api.get<AuditEventDetail>(`/audit-events/${auditEventId}`);
    expect(detail.event.correlationId).toBe(correlationId);
  });

  await test.step('restricted: the administrator reads administration audit only, never this business record', async () => {
    const access = await administrator.api.get<{ categories: string[]; activityVisible: boolean }>(
      '/audit-access',
    );
    expect([access.categories, access.activityVisible]).toEqual([['ADMINISTRATION'], false]);
    const business = await administrator.api.send(
      'GET',
      `/audit-events?category=BUSINESS&projectId=${project.id}`,
    );
    expect([business.status, business.code]).toEqual([403, 'PERMISSION_DENIED']);
    const event = await administrator.api.send('GET', `/audit-events/${auditEventId}`);
    expect([event.status, event.code]).toEqual([404, 'NOT_FOUND']);
    const byCorrelation = await auditEvents(administrator.api, `correlationId=${correlationId}`);
    expect(byCorrelation).toEqual([]);

    const trail = administrator.page;
    await trail.goto('/admin/audit-trail');
    await expectText(trail.getByRole('main'), 'You may read: Administration.');
    const rows = trail.getByRole('table', { name: 'Audit trail' }).getByRole('row');
    await expect(rows.nth(1)).toBeVisible();
    const categories = await rows.evaluateAll((all) =>
      all.slice(1).map((row) => row.querySelectorAll('td')[1]?.textContent ?? ''),
    );
    expect(new Set(categories)).toEqual(new Set(['Administration']));
    // The business record's own correlation id finds nothing for the administrator.
    await trail.getByLabel('Correlation ID').fill(correlationId);
    await trail.getByRole('button', { name: 'Apply filters' }).click();
    await expectText(trail.getByRole('main'), 'No audit events match these filters.');
  });

  await test.step('QA operational evidence: refusals carry their own correlation id, and ADM-052 shows the pipeline, apart from audit', async () => {
    const refused = await manager.api.send('GET', `/audit-events?category=BUSINESS`);
    expect((refused.body as { correlationId?: string }).correlationId).toMatch(UUID);

    const operations = administrator.page;
    await operations.goto('/admin/system-event-logs');
    const main = operations.getByRole('main');
    await expectText(main, 'Narrow technical view');
    await expectText(
      main,
      'It is not the formal audit trail, and it is not an observability or SIEM console',
    );
    await operations.getByRole('button', { name: 'Compare the Activity timeline' }).click();
    await expectText(main, 'Matches the audit store');

    await page.goto('/admin/system-event-logs');
    await expectText(
      page.getByRole('main'),
      'System event logs need the audit operations permission.',
    );
  });

  await test.step('denied scope: another department reaches neither the task, its Activity nor its audit', async () => {
    const record = await outsider.api.send('GET', `/projects/${project.id}`);
    expect([record.status, record.code]).toEqual([404, 'NOT_FOUND']);
    // E2E-R03 holds no task or Activity permission at any scope: refused at the gate, before any record is read.
    const task = await outsider.api.send('GET', `/project-tasks/${taskId}`);
    expect([task.status, task.code]).toEqual([403, 'PERMISSION_DENIED']);
    const activity = await outsider.api.send('GET', `/activity-entries?projectId=${project.id}`);
    expect([activity.status, activity.code]).toEqual([403, 'PERMISSION_DENIED']);
    expect(
      (
        await outsider.api.get<{ totalCount: number }>(
          `/audit-events?correlationId=${correlationId}`,
        )
      ).totalCount,
    ).toBe(0);
    await outsider.page.goto(`/projects/${project.id}/activity-history`);
    await expectText(
      outsider.page.getByRole('main'),
      'The project was not found, or you do not have access to it.',
    );
  });

  await test.step('a stale edit (old If-Match) is refused, recorded nowhere, and changes nothing', async () => {
    const first = await manager.api.ok<TaskDetail>('GET', `/project-tasks/${taskId}`);
    const renamed = `${taskTitle} (scope agreed)`;
    await manager.api.ok('PUT', `/project-tasks/${taskId}`, editOf(first.body, renamed), {
      ifMatch: first.etag,
    });
    const stale = await manager.api.send(
      'PUT',
      `/project-tasks/${taskId}`,
      editOf(first.body, `${taskTitle} (stale)`),
      { ifMatch: first.etag },
    );
    expect([stale.status, stale.code]).toEqual([412, 'PRECONDITION_FAILED']);
    expect((await manager.api.get<TaskDetail>(`/project-tasks/${taskId}`)).title.text).toBe(
      renamed,
    );
    expect(await eventTypesOf(reviewer.api, taskId, 'ProjectTask.TaskChanged')).toBe(1);
    expect(
      (await activityOf(manager.api, project.id, taskId)).filter(
        (e) => e.eventType === 'ProjectTask.TaskChanged',
      ).length,
    ).toBe(1);
  });

  await test.step('a replayed start is refused and is neither audited twice nor shown twice', async () => {
    await manager.api.post(`/project-tasks/${taskId}/start`);
    const replay = await manager.api.send('POST', `/project-tasks/${taskId}/start`, {});
    expect([replay.status, replay.code]).toEqual([409, 'INVALID_TRANSITION']);
    expect(await eventTypesOf(reviewer.api, taskId, 'ProjectTask.TaskStarted')).toBe(1);
    expect(
      (await activityOf(manager.api, project.id, taskId)).filter(
        (e) => e.eventType === 'ProjectTask.TaskStarted',
      ).length,
    ).toBe(1);
  });

  await test.step('the formal record cannot be changed through the API: no write operation exists', async () => {
    const before = await reviewer.api.get<AuditEventDetail>(`/audit-events/${auditEventId}`);
    for (const method of ['PUT', 'DELETE'] as const) {
      const write = await reviewer.api.send(method, `/audit-events/${auditEventId}`, {});
      expect(write.status).toBe(405);
    }
    const after = await reviewer.api.get<AuditEventDetail>(`/audit-events/${auditEventId}`);
    expect(after.integrity.eventHash).toBe(before.integrity.eventHash);
  });
});

interface TaskDetail {
  title: { text: string; language: string };
  description: { text: string; language: string } | null;
  scheduleActivityId: string | null;
  assigneeUserId: string | null;
  priorityItemId: string | null;
  plannedStartDate: string | null;
  plannedFinishDate: string | null;
}

function editOf(task: TaskDetail, title: string): Record<string, unknown> {
  return {
    scheduleActivityId: task.scheduleActivityId,
    title: { text: title, language: 'en' },
    // The API answers a narrative's language as EN and accepts only en.
    description:
      task.description === null
        ? null
        : { text: task.description.text, language: task.description.language.toLowerCase() },
    assigneeUserId: task.assigneeUserId,
    priorityItemId: task.priorityItemId,
    plannedStartDate: task.plannedStartDate,
    plannedFinishDate: task.plannedFinishDate,
  };
}

async function activityOf(
  api: Parameters<typeof auditEvents>[0],
  projectId: string,
  subjectId: string,
): Promise<ActivityEntry[]> {
  return (
    await api.get<{ items: ActivityEntry[] }>(
      `/activity-entries?projectId=${projectId}&subjectId=${subjectId}&pageSize=100`,
    )
  ).items;
}

async function eventTypesOf(
  reviewer: Parameters<typeof auditEvents>[0],
  subjectId: string,
  eventType: string,
): Promise<number> {
  return (await auditEvents(reviewer, `subjectId=${subjectId}&eventType=${eventType}`)).length;
}

/** The API paths a page requests from now on, without their query. */
function watchApi(page: Page): string[] {
  const paths: string[] = [];
  page.on('request', (request: Request) => {
    const path = new URL(request.url()).pathname;
    if (path.startsWith('/api/')) paths.push(plain(path));
  });
  return paths;
}
