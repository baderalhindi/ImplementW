import { type Locator, type Page } from '@playwright/test';

import { runFixture } from '../support/fixtures.ts';
import { activeProject, type ProjectDetail } from '../support/projects.ts';
import { expect, expectText, plain, test, unique, utcDate } from '../support/journey.ts';
import {
  assignedKpi,
  type Measurement,
  publishedMeasurement,
  recordedMeasurement,
  reportParameter,
} from '../support/projections.ts';

// UJ-010 — Missing and stale information (Step 18; FG-01, FG-02; ICD-11).
// Participants: Business consumer; agreed source fixtures.
// Procedure: compare fixtures with missing, stale, Unknown and N/A source values against a genuine zero or Green record.
// Expected: each meaning remains distinguishable; absence of evidence cannot be shown as zero or Green.
// Negative extension: denied scope and a stale/replayed request; confirm source-appropriate failure without unauthorized
// mutation.
//
// The fixtures are built through the product: five ACTIVE projects of the department (WF-01, WF-03), each with the data
// set's KPI and an approved target (WF-11), and a measurement the Project Manager records and AHDA publishes (WF-14) — MEASURED
// and GREEN, none at all, MISSING (Unknown), NOT_APPLICABLE and STALE. WF-02 cannot publish a period in any environment
// (progress-update.md F-1), so its publications and the passage of time to the next due date are named fixtures
// (uj-009-published-progress-period, uj-010-overdue-progress-period), written as WF-02 stores them.

type Meaning = 'green' | 'missing' | 'unknown' | 'notApplicable' | 'stale';

const MEANINGS: Meaning[] = ['green', 'missing', 'unknown', 'notApplicable', 'stale'];

/** One widget of the Project Dashboard composed into the project's Overview (SCR-040, DSH-009). */
function widget(page: Page, title: string): Locator {
  return page
    .getByRole('region', { name: 'Project Dashboard' })
    .getByRole('region', { name: title, exact: true });
}

/** "17 Oct 2026": how the SPA writes a date in English (en-GB), for a source's as-of. */
function dayOf(iso: string): string {
  return new Date(iso).toLocaleDateString('en-GB', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    timeZone: 'UTC',
  });
}

test('UJ-010 missing, stale, Unknown and N/A stay distinguishable from a genuine zero or Green', async ({
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
  const run = unique('UJ-010');
  const projects = {} as Record<Meaning, ProjectDetail>;
  const measurements = {} as Partial<Record<Meaning, Measurement>>;
  const measuredOn = utcDate(-2);

  await test.step('the agreed fixtures are built through the product: five active projects of the department, each with an approved KPI target', async () => {
    const built = await Promise.all(
      MEANINGS.map(async (meaning) => {
        const project = await activeProject(crew, { title: `${run} ${meaning}` });
        return {
          meaning,
          project,
          assignment: await assignedKpi(manager.api, portfolio.api, project.id),
        };
      }),
    );
    const assignment = {} as Record<Meaning, string>;
    for (const { meaning, project, assignment: id } of built) {
      projects[meaning] = project;
      assignment[meaning] = id;
    }

    // The Project Manager records each project's last month and AHDA publishes it; the "missing" project records nothing.
    const values: Partial<
      Record<Meaning, { valueStatus: Measurement['valueStatus']; value?: string }>
    > = {
      green: { valueStatus: 'MEASURED', value: '97' },
      unknown: { valueStatus: 'MISSING' },
      notApplicable: { valueStatus: 'NOT_APPLICABLE' },
      stale: { valueStatus: 'STALE' },
    };
    for (const [meaning, value] of Object.entries(values) as [
      Meaning,
      (typeof values)[Meaning],
    ][]) {
      if (value === undefined) continue;
      measurements[meaning] = await publishedMeasurement(
        manager.api,
        portfolio.api,
        assignment[meaning],
        {
          periodStartsDaysAgo: 32,
          asOfDate: measuredOn,
          ...value,
        },
      );
    }
    expect(
      MEANINGS.flatMap((m) => {
        const recorded = measurements[m];
        return recorded === undefined
          ? []
          : [[m, recorded.valueStatus, recorded.measuredValue, recorded.ragStatus]];
      }),
    ).toEqual([
      ['green', 'MEASURED', 97, 'GREEN'],
      ['unknown', 'MISSING', null, 'UNKNOWN'],
      ['notApplicable', 'NOT_APPLICABLE', null, 'NOT_APPLICABLE'],
      ['stale', 'STALE', null, 'UNKNOWN'],
    ]);
  });

  await test.step('WF-02 fixtures: the green project published last week; the stale one 20 days ago, its next period now overdue', async () => {
    await runFixture('uj-009-published-progress-period', {
      projectId: projects.green.id,
      periodStartDaysAgo: '7',
      publishedDaysAgo: '0',
      actualPercent: '30',
      plannedPercent: '30',
      overallHealth: 'GREEN',
      scheduleHealth: 'GREEN',
    });
    await runFixture('uj-009-published-progress-period', {
      projectId: projects.stale.id,
      periodStartDaysAgo: '28',
      publishedDaysAgo: '20',
      actualPercent: '30',
      plannedPercent: '30',
      overallHealth: 'GREEN',
      scheduleHealth: 'GREEN',
    });
    await runFixture('uj-010-overdue-progress-period', {
      projectId: projects.stale.id,
      periodStartDaysAgo: '10',
    });
  });

  const page = department.page;

  await test.step('the genuine record: Green with its own as-of, and a zero the source counted', async () => {
    await page.goto(`/projects/${projects.green.id}`);
    const kpi = widget(page, 'KPI condition');
    await expectText(kpi, 'Published · official');
    await expectText(kpi, `As of ${dayOf(measuredOn)}`);
    await expect(kpi.getByRole('row', { name: 'Green 1' })).toBeVisible();
    await expect(kpi.getByText('Stale', { exact: true })).toHaveCount(0);

    const health = widget(page, 'Published Overall Project Health');
    await expectText(health, 'Published · official');
    await expect(health.getByText('Green', { exact: true })).toBeVisible();
    await expect(health.getByText('Stale', { exact: true })).toHaveCount(0);

    // No open risk is a count of 0 the risk register keeps, FRESH and dated: a value, not an absence (dashboards.md F-13).
    const risks = widget(page, 'Open risks by rating');
    await expectText(risks, 'Current · live');
    await expect(risks.getByRole('definition').first()).toHaveText('0');
    await expect(risks.getByText('No data', { exact: true })).toHaveCount(0);
    await expectText(widget(page, 'Reporting completeness'), 'Up to date');
  });

  await test.step('missing: nothing published is "No data", never a zero and never Green', async () => {
    await page.goto(`/projects/${projects.missing.id}`);
    for (const title of [
      'KPI condition',
      'Published Overall Project Health',
      'Published progress trend',
    ]) {
      const missing = widget(page, title);
      await expectText(missing, 'No data');
      await expectText(missing, 'It is not zero.');
      await expectText(missing, 'No as-of date: there is no value to date');
      expect(plain(await missing.textContent())).not.toMatch(/\bGreen\b|\b0\b/);
    }
  });

  await test.step('Unknown: a published measurement with no value is counted "Not rated", apart from Green', async () => {
    await page.goto(`/projects/${projects.unknown.id}`);
    const kpi = widget(page, 'KPI condition');
    await expect(kpi.getByRole('row', { name: 'Not rated 1' })).toBeVisible();
    await expect(kpi.getByRole('row', { name: /^Green/ })).toHaveCount(0);
  });

  await test.step('N/A: a period that does not apply is counted "Not applicable", apart from Green and from missing', async () => {
    await page.goto(`/projects/${projects.notApplicable.id}`);
    const kpi = widget(page, 'KPI condition');
    await expect(kpi.getByRole('row', { name: 'Not applicable 1' })).toBeVisible();
    await expect(kpi.getByRole('row', { name: /^Green/ })).toHaveCount(0);
    await expect(kpi.getByText('No data', { exact: true })).toHaveCount(0);
  });

  await test.step('stale: the last published Green keeps its value and its own as-of, flagged Stale; the obligation reads Overdue', async () => {
    await page.goto(`/projects/${projects.stale.id}`);
    const health = widget(page, 'Published Overall Project Health');
    await expect(health.getByText('Green', { exact: true })).toBeVisible();
    await expect(health.getByText('Stale', { exact: true })).toBeVisible();
    await expectText(health, `As of ${dayOf(utcDate(-20))}`);
    await expectText(widget(page, 'Reporting completeness'), 'Overdue');

    // WF-14's own word: a measurement recorded STALE makes the published KPI condition stale, rated nothing.
    const kpi = widget(page, 'KPI condition');
    await expect(kpi.getByText('Stale', { exact: true })).toBeVisible();
    await expect(kpi.getByRole('row', { name: /^Green/ })).toHaveCount(0);
  });

  await test.step('FG-02 keeps the same meanings apart: the KPI Performance report, row by row', async () => {
    await page.goto('/reports/financial/KPI_PERFORMANCE');
    await reportParameter(page, 'Department').selectOption({ label: 'E2E Journeys Department' });
    // The newest projects first, so this run's five are on the first page whoever else is reporting.
    await page.getByRole('button', { name: 'Choose columns' }).click();
    const columns = page.getByRole('dialog', { name: 'Select columns and filters' });
    await columns.getByRole('checkbox', { name: /^KPIs unknown/ }).check();
    await columns.getByLabel('Sort by').selectOption({ label: 'Project ID' });
    await columns.getByRole('radio', { name: 'descending' }).check();
    await columns.getByRole('button', { name: 'Apply' }).click();
    await page.getByRole('button', { name: 'Run report' }).click();

    const table = page.getByRole('table', { name: 'KPI Performance' });
    await expect(table.getByRole('columnheader', { name: /^KPIs unknown/ })).toBeVisible();
    const header = (await table.getByRole('columnheader').allTextContents()).map(plain);
    const cell = async (meaning: Meaning, label: string): Promise<string> => {
      const row = table.getByRole('row').filter({ hasText: `${run} ${meaning}` });
      await expect(row).toHaveCount(1);
      const index = header.findIndex((h) => h.startsWith(label));
      expect(index, `column ${label}`).toBeGreaterThanOrEqual(0);
      return plain(await row.getByRole('cell').nth(index).textContent());
    };

    expect(await cell('green', 'KPIs green')).toBe('1');
    expect(await cell('unknown', 'KPIs unknown')).toBe('1');
    expect(await cell('unknown', 'KPIs green')).toBe('0');
    // Nothing published is not a count of zero green KPIs: it is no data.
    expect(await cell('missing', 'KPIs green')).toBe('No data');
    expect(await cell('missing', 'KPIs unknown')).toBe('No data');
    // The stale count keeps its value and the source's own as-of, flagged.
    expect(await cell('stale', 'KPIs unknown')).toMatch(
      new RegExp(`^1Stale · as of ${dayOf(measuredOn)}`),
    );
    expect(await cell('notApplicable', 'KPIs green')).toBe('0');
    expect(await cell('green', 'Progress reporting status')).toBe('Up to date');
    expect(await cell('stale', 'Progress reporting status')).toBe('Overdue');
    expect(await cell('missing', 'Progress reporting status')).toBe('No data');
  });

  await test.step('denied scope: another department sees none of these projects, on its screens or by asking', async () => {
    const outsider = await actor('otherDepartmentManager');
    await outsider.page.goto(`/projects/${projects.green.id}`);
    await expectText(
      outsider.page.getByRole('main'),
      'The project was not found, or you do not have access to it.',
    );
    const dashboard = await outsider.api.send(
      'GET',
      `/dashboards/PROJECT?projectId=${projects.green.id}`,
    );
    expect([dashboard.status, dashboard.code]).toEqual([404, 'NOT_FOUND']);
    const report = await outsider.api.send('POST', '/reports/PROJECT_REPORT/run', {
      parameters: [{ code: 'PROJECT', value: projects.stale.id }],
      columns: null,
      sort: null,
    });
    expect([report.status, report.code]).toEqual([422, 'REPORT_PARAMETER_UNAUTHORIZED']);
    const kpis = await outsider.api.send<{ items: unknown[] }>(
      'GET',
      `/kpi-assignments?projectId=${projects.green.id}`,
    );
    expect([kpis.status, kpis.body.items.length]).toEqual([200, 0]);
  });

  await test.step('a replayed publication and a stale edit are refused, and the missing project still reads No data', async () => {
    const green = measurements.green;
    if (green === undefined) throw new Error('unreachable');
    const replay = await portfolio.api.send('POST', `/kpi-measurements/${green.id}/publish`);
    expect([replay.status, replay.code]).toEqual([409, 'INVALID_TRANSITION']);
    expect((await portfolio.api.get<Measurement>(`/kpi-measurements/${green.id}`)).ragStatus).toBe(
      'GREEN',
    );

    // A value recorded on the missing project, changed once; the edit sent against the first version is refused.
    const assignments = await manager.api.get<{ items: { id: string }[] }>(
      `/kpi-assignments?projectId=${projects.missing.id}`,
    );
    const draft = await recordedMeasurement(manager.api, assignments.items[0]?.id ?? '', {
      periodStartsDaysAgo: 32,
      valueStatus: 'MEASURED',
      value: '91',
      asOfDate: measuredOn,
    });
    const body = {
      measuredValue: '92',
      valueStatus: 'MEASURED',
      asOfDate: measuredOn,
      narrative: null,
    };
    await manager.api.ok('PUT', `/kpi-measurements/${draft.id}`, body, { ifMatch: draft.etag });
    const stale = await manager.api.send(
      'PUT',
      `/kpi-measurements/${draft.id}`,
      { ...body, measuredValue: '99' },
      { ifMatch: draft.etag },
    );
    expect([stale.status, stale.code]).toEqual([412, 'PRECONDITION_FAILED']);
    expect(
      (await manager.api.get<Measurement>(`/kpi-measurements/${draft.id}`)).measuredValue,
    ).toBe(92);

    // Recorded is not published: the consumer still sees no data, not the draft's value.
    await page.goto(`/projects/${projects.missing.id}`);
    await expectText(widget(page, 'KPI condition'), 'No data');
  });
});
