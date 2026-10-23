import { type Locator, type Page } from '@playwright/test';

import { type PlatformApi } from '../support/api.ts';
import { expect, expectText, plain, test, unique, utcDate } from '../support/journey.ts';
import { activeProject } from '../support/projects.ts';
import {
  assignedKpi,
  exportThroughTheDialog,
  csvRows,
  PROJECTION_WRITES,
  publishedMeasurement,
  recordWrites,
  reportParameter,
} from '../support/projections.ts';
import { DATA_SET, departmentId, itemId } from '../support/reference.ts';

// UJ-008 — Governed read projections (Step 18; FG-01, FG-02; BPK-026, BPK-027; ICD-10).
// Participant: Business report consumer.
// Procedure: compare an authorized source record with its dashboard/report projection using the same context and freshness
// point.
// Expected: read projections preserve source ownership, scope and freshness; reporting does not become a source write path.
// Negative extension: denied scope and a stale/replayed request; confirm source-appropriate failure without unauthorized
// mutation.
//
// Gap G-2 (test-strategy.md §7): "reporting does not become a source write path" was held only by the architecture tests. Here
// it is held by behaviour: the consumer (the department manager, E2E-R03) uses every control the dashboard and report screens
// give them — the department filter, refresh, the dashboard switch, layout personalisation, a report's parameters, columns and
// sort, a run, an export and its download, a saved view, the controlled explorer, the export history — while the journey
// records every request the page sends; then the source records are read again. Every write must be one of FG-01's or FG-02's
// own (PROJECTION_WRITES) and every source record must be byte-for-byte the version it was.

interface Versioned {
  etag: string | null;
  body: unknown;
}

/** The source records the projections read, each with its version: the project (WF-01), its risk (WF-06), its KPI value (WF-14). */
async function sources(
  api: PlatformApi,
  ids: { project: string; risk: string; measurement: string },
) {
  const read = async (path: string): Promise<Versioned> => {
    const response = await api.ok('GET', path);
    return { etag: response.etag, body: response.body };
  };
  return {
    project: await read(`/projects/${ids.project}`),
    risk: await read(`/risks/${ids.risk}`),
    measurement: await read(`/kpi-measurements/${ids.measurement}`),
  };
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

test('UJ-008 read projections keep source ownership, scope and freshness, and write no source', async ({
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
  const title = unique('UJ-008 Metro depot');
  const project = await activeProject(crew, { title });
  const formalId = String(project.formalProjectId);
  const measuredOn = utcDate(-2);

  // The source records: an open risk the Project Manager raised, and a KPI value AHDA published.
  const risk = await manager.api.post<{ id: string }>('/risks', {
    projectId: project.id,
    title: { text: 'Flooding of the depot pit', language: 'en' },
    description: { text: 'Heavy rain may flood the excavation.', language: 'en' },
    riskCategoryItemId: await itemId(manager.api, 'RISK_CATEGORY', 'E2E_WEATHER'),
    ownerUserId: manager.api.userId,
    identifiedDate: utcDate(0),
    nextReviewDate: utcDate(14),
  });
  const measurement = await publishedMeasurement(
    manager.api,
    portfolio.api,
    await assignedKpi(manager.api, portfolio.api, project.id),
    { periodStartsDaysAgo: 32, valueStatus: 'MEASURED', value: '97', asOfDate: measuredOn },
  );
  const ids = { project: project.id, risk: risk.id, measurement: measurement.id };
  const before = await sources(department.api, ids);

  const page = department.page;
  const writes = recordWrites(page);
  let owningDepartment = '';

  await test.step('the consumer reads the source record: its owner, its lifecycle, its Formal Project ID', async () => {
    await page.goto(`/projects/${project.id}`);
    await expect(page.getByRole('heading', { level: 1, name: title })).toBeVisible();
    owningDepartment = plain(
      await page
        .getByRole('term')
        .filter({ hasText: /^Owning sector$/ })
        .locator('xpath=following-sibling::dd[1]')
        .textContent(),
    );
    expect(owningDepartment).toBe('E2E Journeys Department');
  });

  await test.step("the Project Dashboard projects the same record, naming each value's source and its own freshness point", async () => {
    const lifecycle = widget(page, 'Lifecycle state');
    await expectText(lifecycle, 'Owned by WF-01');
    await expect(lifecycle.getByText('Active', { exact: true })).toBeVisible();

    const risks = widget(page, 'Open risks by rating');
    await expectText(risks, 'Current · live');
    await expectText(risks, 'Owned by WF-06');
    await expect(risks.getByRole('definition').first()).toHaveText('1');

    // The KPI condition is dated by the measurement's own as-of, not by when the dashboard was assembled.
    const kpi = widget(page, 'KPI condition');
    await expectText(kpi, 'Published · official');
    await expectText(kpi, 'Owned by WF-14');
    await expectText(kpi, `As of ${dayOf(measuredOn)}`);
    await expect(kpi.getByRole('row', { name: 'Green 1' })).toBeVisible();

    await page
      .getByRole('region', { name: 'Project Dashboard' })
      .getByRole('button', { name: 'Refresh' })
      .click();
    await expectText(kpi, `As of ${dayOf(measuredOn)}`);
  });

  await test.step("the Home: the department filter offers only the consumer's scope; refresh, switch and layout change nothing at source", async () => {
    await page.goto('/');
    const filter = page.getByRole('combobox', { name: 'Sector' });
    await expect(filter.getByRole('option')).toHaveText([
      'All your sectors',
      'E2E Journeys Department',
    ]);
    await filter.selectOption({ label: 'E2E Journeys Department' });
    await expect(page).toHaveURL(/department=/);
    await page.getByRole('button', { name: 'Refresh' }).click();
    await expect(page.getByRole('region', { name: 'Projects by lifecycle state' })).toBeVisible();

    await page.getByRole('button', { name: 'Customise layout' }).click();
    const layout = page.getByRole('dialog', { name: 'Customise your dashboard' });
    await layout.getByRole('button', { name: 'Save layout' }).click();
    await expectText(page.getByRole('main'), 'Your layout was saved.');
    await page.getByRole('button', { name: 'Customise layout' }).click();
    await page
      .getByRole('dialog', { name: 'Customise your dashboard' })
      .getByRole('button', { name: 'Restore the standard layout' })
      .click();
    await expectText(page.getByRole('main'), 'The standard layout was restored.');

    await page.getByRole('link', { name: 'Governance Dashboard' }).click();
    await expect(
      page.getByRole('heading', { level: 1, name: 'Governance Dashboard' }),
    ).toBeVisible();
  });

  await test.step('the Project Report projects the record with its owner, identity and counts, under the same context', async () => {
    await page.goto('/reports/project/PROJECT_REPORT');
    await reportParameter(page, 'Project').selectOption(project.id);
    await page.getByRole('button', { name: 'Choose columns' }).click();
    const columns = page.getByRole('dialog', { name: 'Select columns and filters' });
    await columns.getByLabel('Sort by').selectOption({ label: 'Project ID' });
    await columns.getByRole('button', { name: 'Apply' }).click();
    await page.getByRole('button', { name: 'Run report' }).click();
    const table = page.getByRole('table', { name: 'Project Report' });
    await expect(table.getByRole('row')).toHaveCount(2);
    const header = (await table.getByRole('columnheader').allTextContents()).map(plain);
    const cells = (await table.getByRole('row').nth(1).getByRole('cell').allTextContents()).map(
      plain,
    );
    // A header is the column's label, then its semantic state when it has one.
    const cell = (label: string) =>
      cells[
        header.findIndex(
          (h) =>
            h === label ||
            (/^(Current|Published|Historical) ·/.test(h.slice(label.length)) &&
              h.startsWith(label)),
        )
      ];
    expect([
      cell('Project ID'),
      cell('Project'),
      cell('Sector'),
      cell('Lifecycle state'),
      cell('Open risks'),
      cell('KPIs green'),
    ]).toEqual([formalId, title, owningDepartment, 'Active', '1', '1']);
    const banner = page.getByRole('region', { name: 'Coverage and freshness' });
    const kpiSection = banner.locator('[data-projection="FINANCIAL_KPI.KPI_CONDITION"]');
    await expectText(kpiSection, 'Source WF-14');
    await expectText(kpiSection, `As of ${dayOf(measuredOn)}`);
    await expectText(kpiSection, '1 of 1 projects shown');

    // An export and its download, then a saved view of the same choices.
    const [csvHeader = [], csvRow = []] = csvRows(
      (await exportThroughTheDialog(page, 'CSV')).bytes,
    );
    expect(csvRow[csvHeader.indexOf('Sector')]).toBe(owningDepartment);
    expect(csvRow[csvHeader.indexOf('Open risks')]).toBe('1');
    await page.getByRole('button', { name: 'Save view' }).click();
    const save = page.getByRole('dialog', { name: 'Save report view' });
    await save.getByLabel(/^Name/).fill(`${title} view`);
    await save.getByRole('button', { name: 'Save view' }).click();
    await expectText(page.getByRole('main'), 'was saved.');
  });

  await test.step('the controlled explorer and the export history: the same counts, still no write to a source', async () => {
    await page.goto('/reports/explorer');
    await page.getByRole('button', { name: 'Choose columns and filters' }).first().click();
    const compose = page.getByRole('dialog', { name: 'Select columns and filters' });
    const shown = compose
      .getByRole('group', { name: 'Shown columns, in order' })
      .getByRole('checkbox');
    for (const [index, field] of ['Project ID', 'Project', 'Sector', 'Open risks'].entries()) {
      // Ticking a field moves it to the shown columns.
      await compose
        .getByRole('group', { name: 'Fields you can add' })
        .getByRole('checkbox', {
          name: new RegExp(`^${field}( (Current|Published|Historical) ·.*)?$`),
        })
        .click();
      await expect(shown).toHaveCount(index + 1);
    }
    await compose.getByRole('button', { name: 'Add a filter' }).click();
    await compose
      .getByRole('combobox', { name: 'Field', exact: true })
      .selectOption({ label: 'Project' });
    await compose
      .getByRole('combobox', { name: 'Condition', exact: true })
      .selectOption({ label: 'contains' });
    await compose.getByRole('textbox', { name: 'Value', exact: true }).fill(title);
    await compose.getByRole('button', { name: 'Apply' }).click();
    const table = page.getByRole('table');
    await expect(table.getByRole('row')).toHaveCount(2);
    expect(
      (await table.getByRole('row').nth(1).getByRole('cell').allTextContents()).map(plain),
    ).toEqual([formalId, title, owningDepartment, '1']);

    await page.goto('/reports/exports');
    await expectText(page.getByRole('main'), 'Project Report');
  });

  await test.step("G-2: every request the screens sent was a read or FG-01/FG-02's own, and every source record is the version it was", async () => {
    expect(writes.length).toBeGreaterThan(0);
    expect(writes.filter((write) => !PROJECTION_WRITES.test(write))).toEqual([]);
    for (const expected of [
      'POST /reports/PROJECT_REPORT/run',
      'POST /reports/PROJECT_REPORT/export',
      'POST /report-explorer/run',
      'POST /dashboards/PORTFOLIO/personalize',
      'POST /saved-views',
    ]) {
      expect(writes).toContain(expected);
    }
    expect(await sources(department.api, ids)).toEqual(before);
  });

  await test.step('denied scope: another department reaches neither the record nor its projections, and cannot widen its filter', async () => {
    const outsider = await actor('otherDepartmentManager');
    await outsider.page.goto(`/projects/${project.id}`);
    await expectText(
      outsider.page.getByRole('main'),
      'The project was not found, or you do not have access to it.',
    );
    const ours = await departmentId(portfolio.api, DATA_SET.department);
    await outsider.page.goto(`/?department=${ours}`);
    await expectText(outsider.page.getByRole('main'), 'That sector is not one you may filter by.');
    const filtered = await outsider.api.send('GET', `/dashboards/PORTFOLIO?departmentId=${ours}`);
    expect([filtered.status, filtered.code]).toEqual([422, 'DASHBOARD_FILTER_VALUE_UNAUTHORIZED']);
    const dashboard = await outsider.api.send('GET', `/dashboards/PROJECT?projectId=${project.id}`);
    expect([dashboard.status, dashboard.code]).toEqual([404, 'NOT_FOUND']);
    const report = await outsider.api.send('POST', '/reports/PROJECT_REPORT/run', {
      parameters: [{ code: 'PROJECT', value: project.id }],
      columns: null,
      sort: null,
    });
    expect([report.status, report.code]).toEqual([422, 'REPORT_PARAMETER_UNAUTHORIZED']);
    const views = await outsider.api.get<{ items: { name: { text: string } }[] }>('/saved-views');
    expect(views.items.map((v) => v.name.text)).not.toContain(`${title} view`);
  });

  await test.step('a stale change to the saved view is refused and leaves it as saved; the sources are still unchanged', async () => {
    const views = await department.api.get<{ items: { id: string; name: { text: string } }[] }>(
      '/saved-views',
    );
    const view = views.items.find((v) => v.name.text === `${title} view`);
    expect(view).toBeDefined();
    const read = await department.api.ok<Record<string, unknown>>(
      'GET',
      `/saved-views/${String(view?.id)}`,
    );
    const body = {
      name: { text: `${title} renamed`, language: 'en' },
      viewType: 'REPORT_PARAMETERS',
      reportCode: 'PROJECT_REPORT',
      parameters: [{ code: 'PROJECT', value: project.id }],
      columns: [],
      filters: [],
    };
    await department.api.ok('PUT', `/saved-views/${String(view?.id)}`, body, {
      ifMatch: read.etag,
    });
    const stale = await department.api.send(
      'PUT',
      `/saved-views/${String(view?.id)}`,
      { ...body, name: { text: `${title} again`, language: 'en' } },
      { ifMatch: read.etag },
    );
    expect([stale.status, stale.code]).toEqual([412, 'PRECONDITION_FAILED']);
    const after = await department.api.get<{ name: { text: string } }>(
      `/saved-views/${String(view?.id)}`,
    );
    expect(after.name.text).toBe(`${title} renamed`);
    await department.api.ok('DELETE', `/saved-views/${String(view?.id)}`);
    expect(await sources(department.api, ids)).toEqual(before);
  });
});
