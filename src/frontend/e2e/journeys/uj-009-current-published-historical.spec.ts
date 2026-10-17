import { type Locator, type Page } from '@playwright/test';

import { runFixture } from '../support/fixtures.ts';
import { activeProject } from '../support/projects.ts';
import { expect, expectText, plain, test, unique, utcDate } from '../support/journey.ts';
import {
  assignedKpi,
  type Measurement,
  recordedMeasurement,
  reportParameter,
} from '../support/projections.ts';

// UJ-009 — Current Published Historical (Step 18; FG-01, FG-02).
// Participants: Report consumer and authorized publisher.
// Procedure: view current values, publish a snapshot, change current values and reopen the historical snapshot.
// Expected: Current, Published and Historical remain distinguishable; historical context is retained.
// Negative extension: denied scope and a stale/replayed request; confirm source-appropriate failure without unauthorized
// mutation.
//
// The consumer is the department manager (E2E-R03); the publisher is AHDA (E2E-R02), who publishes the Project Manager's KPI
// value in the browser (WF-14, PUBLISHED/OFFICIAL). The current value that moves is WF-03's live Schedule Health, re-forecast
// by the Project Manager. The historical snapshots are WF-02's published periods: WF-02 cannot publish in any environment
// (progress-update.md F-1), so its two publications are the named fixture uj-009-published-progress-period, written as WF-02
// stores them.

interface ProgressSnapshot {
  id: string;
  publishedAt: string;
  overallHealth: string;
  actualPercent: number | string;
  plannedPercent: number | string | null;
}

interface Activity {
  id: string;
  forecastStartDate: string | null;
  forecastFinishDate: string | null;
}

function widget(page: Page, title: string): Locator {
  return page
    .getByRole('region', { name: 'Project Dashboard' })
    .getByRole('region', { name: title, exact: true });
}

function dayOf(iso: string): string {
  return new Date(iso).toLocaleDateString('en-GB', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    timeZone: 'UTC',
  });
}

test('UJ-009 current, published and historical values stay distinguishable and history is retained', async ({
  actor,
}) => {
  const department = await actor('departmentManager');
  const portfolio = await actor('portfolioManager');
  const manager = await actor('projectManager');
  const crew = {
    creator: department.api,
    reviewer: department.api,
    approver: portfolio.api,
    manager: manager.api,
  };
  const title = unique('UJ-009 Ring road');
  const project = await activeProject(crew, { title });
  const assignment = await assignedKpi(manager.api, portfolio.api, project.id);
  const measuredOn = utcDate(-3);
  const consumer = department.page;
  const overview = `/projects/${project.id}`;

  await test.step('the consumer views the current values: live and dated, with nothing published yet', async () => {
    await consumer.goto(overview);
    const schedule = widget(consumer, 'Current schedule health');
    await expectText(schedule, 'Current · live');
    await expect(schedule.getByText('Green', { exact: true })).toBeVisible();
    await expect(schedule.getByRole('term')).toHaveText('Finish variance');
    await expectText(schedule.getByRole('definition'), '0 days');
    for (const published of [
      'KPI condition',
      'Published Overall Project Health',
      'Published progress trend',
    ]) {
      await expectText(widget(consumer, published), 'No data');
    }
    await expectText(widget(consumer, 'Published progress trend'), 'Historical · snapshot');
  });

  let measurement = '';
  await test.step("the publisher publishes the Project Manager's KPI value for last month", async () => {
    measurement = (
      await recordedMeasurement(manager.api, assignment, {
        periodStartsDaysAgo: 33,
        valueStatus: 'MEASURED',
        value: '97',
        asOfDate: measuredOn,
      })
    ).id;
    await manager.api.post(`/kpi-measurements/${measurement}/submit`);

    const publisher = portfolio.page;
    await publisher.goto(`${overview}/kpis`);
    await publisher.getByRole('button', { name: 'Publish value' }).click();
    const dialog = publisher.getByRole('dialog', { name: /^Publish KPI value/ });
    await expectText(dialog, 'It cannot be changed afterwards.');
    await dialog.getByRole('button', { name: 'Publish' }).click();
    await expect(dialog).toBeHidden();
    const published = await portfolio.api.get<Measurement>(`/kpi-measurements/${measurement}`);
    expect([published.status, published.ragStatus]).toEqual(['PUBLISHED', 'GREEN']);
  });

  await test.step('WF-02 publishes two periods (fixture): Amber a week ago, Green today', async () => {
    await runFixture('uj-009-published-progress-period', {
      projectId: project.id,
      periodStartDaysAgo: '14',
      publishedDaysAgo: '7',
      actualPercent: '20',
      plannedPercent: '28',
      overallHealth: 'AMBER',
      scheduleHealth: 'GREEN',
    });
    await runFixture('uj-009-published-progress-period', {
      projectId: project.id,
      periodStartDaysAgo: '7',
      publishedDaysAgo: '0',
      actualPercent: '45',
      plannedPercent: '46',
      overallHealth: 'GREEN',
      scheduleHealth: 'GREEN',
    });
  });

  const snapshots = () =>
    department.api.get<{ items: ProgressSnapshot[] }>(
      `/published-progress-snapshots?projectId=${project.id}`,
    );
  const publishedBefore = await snapshots();
  expect(publishedBefore.items.map((s) => s.overallHealth)).toEqual(['GREEN', 'AMBER']);

  await test.step('the consumer sees the published values as Published, and the periods as Historical', async () => {
    await consumer.goto(overview);
    const kpi = widget(consumer, 'KPI condition');
    await expectText(kpi, 'Published · official');
    await expectText(kpi, `As of ${dayOf(measuredOn)}`);
    await expect(kpi.getByRole('row', { name: 'Green 1' })).toBeVisible();

    const health = widget(consumer, 'Published Overall Project Health');
    await expectText(health, 'Published · official');
    await expect(health.getByText('Green', { exact: true })).toBeVisible();

    const trend = widget(consumer, 'Published progress trend');
    await expectText(trend, 'Historical · snapshot');
    // The history is WF-02's own snapshots, oldest first: Amber at 20%, then Green at 45%.
    const rows = trend.getByRole('table').getByRole('row');
    await expect(rows).toHaveCount(3);
    const cells = async (row: Locator) =>
      (await row.locator('th, td').allTextContents()).map(plain);
    expect(await cells(rows.nth(1))).toEqual([
      `${utcDate(-14)} to ${utcDate(-8)}`,
      'Amber',
      '20%',
      '28%',
      expect.stringContaining(dayOf(utcDate(-7))),
    ]);
    expect(await cells(rows.nth(2))).toEqual([
      `${utcDate(-7)} to ${utcDate(-1)}`,
      'Green',
      '45%',
      '46%',
      expect.stringContaining(dayOf(utcDate(0))),
    ]);
  });

  let activity: Activity = { id: '', forecastStartDate: null, forecastFinishDate: null };
  let replacedVersion = '';
  await test.step('current values change: the Project Manager re-forecasts the works 30 days later, and records a lower KPI value', async () => {
    const activities = await manager.api.get<{ items: Activity[] }>(
      `/schedule-activities?projectId=${project.id}`,
    );
    activity = activities.items[0] ?? activity;
    const read = await manager.api.ok<Activity>('GET', `/schedule-activities/${activity.id}`);
    replacedVersion = read.etag ?? '';
    const finish = new Date(`${String(read.body.forecastFinishDate)}T00:00:00Z`);
    finish.setUTCDate(finish.getUTCDate() + 30);
    await manager.api.ok(
      'POST',
      `/schedule-activities/${activity.id}/reforecast`,
      {
        forecastStartDate: read.body.forecastStartDate,
        forecastFinishDate: finish.toISOString().slice(0, 10),
      },
      { ifMatch: read.etag },
    );
    // This month's value is recorded and submitted, not yet published.
    const next = await recordedMeasurement(manager.api, assignment, {
      periodStartsDaysAgo: 3,
      valueStatus: 'MEASURED',
      value: '85',
      asOfDate: utcDate(0),
    });
    await manager.api.post(`/kpi-measurements/${next.id}/submit`);
  });

  await test.step('the consumer sees the current value move while the published and historical ones stand', async () => {
    await consumer.goto(overview);
    const schedule = widget(consumer, 'Current schedule health');
    await expectText(schedule, 'Current · live');
    await expect(schedule.getByText('Red', { exact: true })).toBeVisible();
    // Thirty days late against the frozen baseline (schedule-baseline.md D-9), RED from 20 in the e2e WORKFLOW_POLICY.
    await expectText(schedule.getByRole('definition'), '+30 days');

    const publishedSchedule = widget(consumer, 'Published schedule health');
    await expectText(publishedSchedule, 'Published · official');
    await expect(publishedSchedule.getByText('Green', { exact: true })).toBeVisible();

    // A submitted value is not published: the official condition is still last month's Green.
    const kpi = widget(consumer, 'KPI condition');
    await expect(kpi.getByRole('row', { name: 'Green 1' })).toBeVisible();
    await expectText(kpi, `As of ${dayOf(measuredOn)}`);
  });

  await test.step('the consumer reopens the historical snapshots in Published Progress History: unchanged, oldest first', async () => {
    await consumer.goto('/reports/progress/PROGRESS_HISTORY');
    await reportParameter(consumer, 'Project').selectOption(project.id);
    await consumer.getByRole('button', { name: 'Run report' }).click();
    const table = consumer.getByRole('table', { name: 'Published Progress History' });
    await expect(
      table.getByRole('columnheader', { name: /^Published Overall Project Health/ }),
    ).toContainText('Historical · snapshot');
    const rows = table.getByRole('row');
    await expect(rows).toHaveCount(3);
    const first = plain(await rows.nth(1).textContent());
    const second = plain(await rows.nth(2).textContent());
    expect(first).toMatch(/Amber/);
    expect(first).toMatch(/20(\.0+)?\s*%/);
    expect(second).toMatch(/Green/);
    expect(second).toMatch(/45(\.0+)?\s*%/);
    await expectText(
      consumer.getByRole('region', { name: 'Coverage and freshness' }),
      'Historical · snapshot',
    );

    // The project's own report puts the three side by side, each column named for what it is.
    await consumer.goto('/reports/project/PROJECT_REPORT');
    await reportParameter(consumer, 'Project').selectOption(project.id);
    await consumer.getByRole('button', { name: 'Run report' }).click();
    const report = consumer.getByRole('table', { name: 'Project Report' });
    await expect(
      report.getByRole('columnheader', { name: /^Current schedule health/ }),
    ).toContainText('Current · live');
    await expect(
      report.getByRole('columnheader', { name: /^Published schedule health/ }),
    ).toContainText('Published · official');
    const header = (await report.getByRole('columnheader').allTextContents()).map(plain);
    const cells = (await report.getByRole('row').nth(1).getByRole('cell').allTextContents()).map(
      plain,
    );
    const at = (label: string) => cells[header.findIndex((h) => h.startsWith(label))];
    expect([
      at('Current schedule health'),
      at('Published schedule health'),
      at('KPIs green'),
    ]).toEqual(['Red', 'Green', '1']);

    // Through the API: the snapshots are exactly as published before the current values moved.
    expect(await snapshots()).toEqual(publishedBefore);
  });

  await test.step('denied scope: another department cannot open the history, the dashboard or the publication', async () => {
    const outsider = await actor('otherDepartmentManager');
    await outsider.page.goto('/reports/progress/PROGRESS_HISTORY');
    await expect(
      reportParameter(outsider.page, 'Project').locator(`option[value="${project.id}"]`),
    ).toHaveCount(0);
    const history = await outsider.api.send('POST', '/reports/PROGRESS_HISTORY/run', {
      parameters: [{ code: 'PROJECT', value: project.id }],
      columns: null,
      sort: null,
    });
    expect([history.status, history.code]).toEqual([422, 'REPORT_PARAMETER_UNAUTHORIZED']);
    const dashboard = await outsider.api.send('GET', `/dashboards/PROJECT?projectId=${project.id}`);
    expect([dashboard.status, dashboard.code]).toEqual([404, 'NOT_FOUND']);
    // Holding no KPI_REVIEW in any scope, the outsider is refused before any record is looked up: the same answer as for an id
    // that does not exist, so nothing is disclosed (R-47).
    const publish = await outsider.api.send('POST', `/kpi-measurements/${measurement}/publish`);
    const nothing = await outsider.api.send(
      'POST',
      `/kpi-measurements/${crypto.randomUUID()}/publish`,
    );
    expect([publish.status, publish.code]).toEqual([403, 'PERMISSION_DENIED']);
    expect([nothing.status, nothing.code]).toEqual([403, 'PERMISSION_DENIED']);
  });

  await test.step('a replayed publication and a re-forecast sent against the replaced version are refused and change nothing', async () => {
    const replay = await portfolio.api.send('POST', `/kpi-measurements/${measurement}/publish`);
    expect([replay.status, replay.code]).toEqual([409, 'INVALID_TRANSITION']);

    const before = await manager.api.ok<Activity>('GET', `/schedule-activities/${activity.id}`);
    const stale = await manager.api.send(
      'POST',
      `/schedule-activities/${activity.id}/reforecast`,
      {
        forecastStartDate: before.body.forecastStartDate,
        forecastFinishDate: before.body.forecastStartDate,
      },
      { ifMatch: replacedVersion },
    );
    expect([stale.status, stale.code]).toEqual([412, 'PRECONDITION_FAILED']);
    expect(
      (await manager.api.ok<Activity>('GET', `/schedule-activities/${activity.id}`)).etag,
    ).toBe(before.etag);
    expect(await snapshots()).toEqual(publishedBefore);
  });
});
