import { randomUUID } from 'node:crypto';

import { runFixture } from '../support/fixtures.ts';
import { activeProject } from '../support/projects.ts';
import { expect, expectText, plain, test, unique, utcDate } from '../support/journey.ts';
import {
  csvRows,
  exportThroughTheDialog,
  pdfTexts,
  publishedFinancials,
  reportParameter,
  xlsxSheets,
} from '../support/projections.ts';
import { DATA_SET } from '../support/reference.ts';

// UJ-015 — Committed report exports (Step 18; FG-02; BPK-027; ICD-17).
// Participant: Authorized report consumer.
// Procedure: export an authorized governed report separately as PDF, XLSX and CSV; compare scope, filters, context and
// restricted-field exclusion.
// Expected: committed export formats are PDF/XLSX/CSV. Word export remains subject to unresolved scope applicability.
// Negative extension: denied scope and a stale/replayed request; confirm source-appropriate failure without unauthorized
// mutation.
//
// The consumer is the department manager (E2E-R03): authorised for the department's projects and their reports, with
// REPORT_EXPORT, and without FINANCIAL_VIEW — the classification financial amounts are restricted to (ADR-010). AHDA's
// portfolio manager (E2E-R02) holds it and exports the same report for comparison. The project's financial figures are entered
// and published through WF-14; WF-14 reports on WF-02's periods, which WF-02 cannot generate in any environment
// (progress-update.md F-1), so the period and its publication are the named fixture uj-009-published-progress-period.

const ACTUAL = '3250000.00';
const FORECAST = '12400000.00';

/** The columns the consumer keeps (MOD-061), in the report's order: identity, a published health, two restricted amounts. */
const COLUMNS = [
  'Project ID',
  'Project',
  'Department',
  'Published Overall Project Health',
  'Actual expenditure to date (published)',
  'Forecast at completion (published)',
];

/** The value of a column in a row, by the header row's label. */
function at(header: string[], row: string[], label: string): string {
  const index = header.indexOf(label);
  if (index < 0) throw new Error(`No column "${label}" in ${JSON.stringify(header)}`);
  return row[index] ?? '';
}

test('UJ-015 PDF, XLSX and CSV exports keep scope, filters and context, and leave restricted fields out', async ({
  actor,
}) => {
  const department = await actor('departmentManager');
  const portfolio = await actor('portfolioManager');
  const manager = await actor('projectManager');
  const outsider = await actor('otherDepartmentManager');
  const title = unique('UJ-015 Harbour bridge');
  const ours = await activeProject(
    {
      creator: department.api,
      reviewer: department.api,
      approver: portfolio.api,
      manager: manager.api,
    },
    { title },
  );
  const theirs = await activeProject(
    {
      creator: outsider.api,
      reviewer: outsider.api,
      approver: portfolio.api,
      manager: manager.api,
    },
    { title: unique('UJ-015 Other department'), departmentCode: DATA_SET.otherDepartment },
  );
  const formalId = String(ours.formalProjectId);
  const page = department.page;

  await test.step('the source figures: WF-02 publishes the period (fixture), WF-14 publishes its expenditure and forecast', async () => {
    await runFixture('uj-009-published-progress-period', {
      projectId: ours.id,
      periodStartDaysAgo: '7',
      publishedDaysAgo: '0',
      actualPercent: '35',
      plannedPercent: '36',
      overallHealth: 'GREEN',
      scheduleHealth: 'GREEN',
    });
    const snapshot = await publishedFinancials(manager.api, portfolio.api, ours.id, {
      actualSar: ACTUAL,
      forecastSar: FORECAST,
      asOfDate: utcDate(-1),
    });
    expect([snapshot.actualExpenditureToDateSar, snapshot.forecastAtCompletionSar]).toEqual([
      ACTUAL,
      FORECAST,
    ]);
  });

  await test.step('the consumer runs the Project Report on an authorised project, keeping six columns: the amounts read Restricted', async () => {
    await page.goto('/reports/project/PROJECT_REPORT');
    const project = reportParameter(page, 'Project');
    await expect(project.locator(`option[value="${theirs.id}"]`)).toHaveCount(0);
    await project.selectOption(ours.id);
    await page.getByRole('button', { name: 'Choose columns' }).click();
    const dialog = page.getByRole('dialog', { name: 'Select columns and filters' });
    const shown = dialog
      .getByRole('group', { name: 'Shown columns, in order' })
      .getByRole('checkbox');
    const labels = () =>
      shown.evaluateAll((boxes) => boxes.map((b) => b.parentElement?.textContent ?? ''));
    for (let names = await labels(); ; names = await labels()) {
      const drop = names.findIndex(
        (name) => !COLUMNS.some((column) => plain(name).startsWith(column)),
      );
      if (drop < 0) break;
      // Unticking a shown column takes it out of the list, so the next one moves into its place.
      await shown.nth(drop).click();
      await expect(shown).toHaveCount(names.length - 1);
    }
    await dialog.getByRole('button', { name: 'Apply' }).click();
    await page.getByRole('button', { name: 'Run report' }).click();
    await expectText(page.getByRole('region', { name: 'Active filters' }), `Project: ${formalId}`);
    await expectText(page.getByRole('region', { name: 'Active filters' }), '6 columns shown');

    const table = page.getByRole('table', { name: 'Project Report' });
    await expect(table.getByRole('row')).toHaveCount(2);
    const header = (await table.getByRole('columnheader').allTextContents()).map(plain);
    expect(header.map((h) => COLUMNS.find((column) => h.startsWith(column)))).toEqual(COLUMNS);
    const cells = (await table.getByRole('row').nth(1).getByRole('cell').allTextContents()).map(
      plain,
    );
    expect(cells).toEqual([
      formalId,
      title,
      'E2E Journeys Department',
      'Green',
      'Restricted',
      'Restricted',
    ]);
  });

  await test.step('the committed formats are offered, and only them: PDF, Excel (XLSX) and CSV, no Word', async () => {
    await page.getByRole('button', { name: 'Export', exact: true }).click();
    const dialog = page.getByRole('dialog', { name: 'Export report' });
    const formats = dialog.getByRole('group', { name: 'Format' }).getByRole('radio');
    expect(
      await formats.evaluateAll((radios) => radios.map((r) => r.getAttribute('value'))),
    ).toEqual(['PDF', 'XLSX', 'CSV']);
    expect(plain(await dialog.textContent())).not.toMatch(/Word|DOCX/i);
    await dialog.getByRole('button', { name: 'Cancel' }).click();
    const view = await department.api.get<{ exportFormats: string[] }>('/reports/PROJECT_REPORT');
    expect(view.exportFormats).toEqual(['PDF', 'XLSX', 'CSV']);
    const word = await department.api.send('POST', '/reports/PROJECT_REPORT/export', {
      parameters: [{ code: 'PROJECT', value: ours.id }],
      columns: null,
      sort: null,
      format: 'DOCX',
      language: 'en',
    });
    expect(word.status).toBe(400);
  });

  const csv =
    await test.step('CSV: the authorised row, the restricted fields as RESTRICTED, never the amounts', async () => {
      const file = await exportThroughTheDialog(page, 'CSV');
      expect(file.name).toMatch(/\.csv$/);
      const rows = csvRows(file.bytes);
      const [header = [], ...data] = rows;
      expect(data).toHaveLength(1);
      const row = data[0] ?? [];
      expect(at(header, row, 'Project ID')).toBe(formalId);
      expect(at(header, row, 'Project')).toBe(title);
      expect(at(header, row, 'Department')).toBe('E2E Journeys Department');
      expect(at(header, row, 'Actual expenditure to date (published)')).toBe('RESTRICTED');
      expect(at(header, row, 'Forecast at completion (published)')).toBe('RESTRICTED');
      expect(at(header, row, 'Published Overall Project Health')).toBe('GREEN');
      const text = file.bytes.toString('utf8');
      expect(text).not.toContain(ACTUAL);
      expect(text).not.toContain(String(theirs.formalProjectId));
      return { header, row };
    });

  await test.step('XLSX: the same row and columns, and a Context sheet stating the parameter, the sources and the classification', async () => {
    const file = await exportThroughTheDialog(page, 'XLSX');
    expect(file.name).toMatch(/\.xlsx$/);
    const { report, context } = xlsxSheets(file.bytes);
    const [header = [], ...data] = report;
    expect(header).toEqual(csv.header);
    expect(data).toHaveLength(1);
    const row = data[0] ?? [];
    expect(at(header, row, 'Project ID')).toBe(formalId);
    expect(at(header, row, 'Actual expenditure to date (published)')).toBe('Restricted');
    expect(at(header, row, 'Forecast at completion (published)')).toBe('Restricted');
    const lines = new Map(context.map(([label = '', value = '']) => [label, value]));
    expect(lines.get('Parameters: Project')).toBe(`${formalId} ${title}`);
    expect(lines.get('Classification')).toBe('Standard');
    expect(lines.get('Rows')).toBe('1');
    expect(lines.get('Generated at')).toMatch(/^\d{4}-\d{2}-\d{2} \d{2}:\d{2} UTC$/);
    const sources = context
      .filter(([label]) => label === 'Sources')
      .map(([, value]) => value ?? '');
    expect(sources).toContainEqual(
      expect.stringMatching(
        /^FINANCIAL_KPI\.PUBLISHED_FINANCIAL_SNAPSHOT \(WF-14\): Published, Restricted/,
      ),
    );
    expect(sources).toContainEqual(
      expect.stringMatching(
        /^PROGRESS\.PUBLISHED_PROGRESS_SNAPSHOT \(WF-02\): Published, fresh, as of .* coverage 1\/1$/,
      ),
    );
    expect(file.bytes.includes(Buffer.from(ACTUAL))).toBe(false);
  });

  await test.step('PDF: a complete document stating the title, the parameter, the sources, the classification and the same row', async () => {
    const file = await exportThroughTheDialog(page, 'PDF');
    expect(file.name).toMatch(/\.pdf$/);
    expect(file.bytes.subarray(0, 8).toString('latin1')).toBe('%PDF-1.7');
    const texts = pdfTexts(file.bytes);
    expect(texts[0]).toBe('Project Report');
    expect(texts).toContain(`Parameters: Project: ${formalId} ${title}`);
    expect(texts).toContain('Classification: Standard');
    expect(texts).toContain('Rows: 1');
    expect(texts).toContainEqual(
      expect.stringMatching(
        /^Sources: FINANCIAL_KPI\.PUBLISHED_FINANCIAL_SNAPSHOT \(WF-14\): Published, Restricted/,
      ),
    );
    // The table: the six headers, then the row, every value as on screen.
    const table = texts.slice(texts.indexOf('Rows: 1') + 1, texts.indexOf('Rows: 1') + 13);
    expect(table).toEqual([
      ...COLUMNS,
      formalId,
      expect.stringMatching(/^UJ-015 Harbour bridge/),
      'E2E Journeys Department',
      'Green',
      'Restricted',
      'Restricted',
    ]);
    expect(texts.at(-1)).toMatch(/^Page 1 of 1 · .* · Standard$/);
    expect(texts.join(' ')).not.toMatch(/3,250,000|3250000/);
    expect(texts.join(' ')).not.toContain(String(theirs.formalProjectId));
  });

  await test.step("scope across the department: the register export holds the department's projects only", async () => {
    await page.goto('/reports/department/PROJECT_REGISTER');
    await reportParameter(page, 'Department').selectOption({ label: 'E2E Journeys Department' });
    await page.getByRole('button', { name: 'Run report' }).click();
    await expectText(
      page.getByRole('region', { name: 'Active filters' }),
      'Department: E2E Journeys Department',
    );
    const [header = [], ...rows] = csvRows((await exportThroughTheDialog(page, 'CSV')).bytes);
    expect(rows.map((row) => at(header, row, 'Project ID'))).toContain(formalId);
    expect(rows.map((row) => at(header, row, 'Project ID'))).not.toContain(
      String(theirs.formalProjectId),
    );
    expect(new Set(rows.map((row) => at(header, row, 'Department')))).toEqual(
      new Set(['E2E Journeys Department']),
    );
  });

  await test.step("the classified consumer's file holds the amounts, and is classified Sensitive", async () => {
    const authorised = portfolio.page;
    await authorised.goto('/reports/project/PROJECT_REPORT');
    await reportParameter(authorised, 'Project').selectOption(ours.id);
    await authorised.getByRole('button', { name: 'Run report' }).click();
    const [header = [], row = []] = csvRows(
      (await exportThroughTheDialog(authorised, 'CSV')).bytes,
    );
    expect(at(header, row, 'Actual expenditure to date (published)')).toBe(ACTUAL);
    expect(at(header, row, 'Forecast at completion (published)')).toBe(FORECAST);
    const latest =
      (await portfolio.api.get<{ items: { id: string }[] }>('/report-jobs?pageSize=1')).items[0]
        ?.id ?? '';
    const job = await portfolio.api.get<{ output: { sensitivity: string } | null }>(
      `/report-jobs/${latest}`,
    );
    expect(job.output?.sensitivity).toBe('SENSITIVE');
  });

  await test.step("denied scope: another department's project cannot be exported, and the consumer's files are theirs alone", async () => {
    const before = (await department.api.get<{ totalCount: number }>('/report-jobs')).totalCount;
    const refused = await department.api.send('POST', '/reports/PROJECT_REPORT/export', {
      parameters: [{ code: 'PROJECT', value: theirs.id }],
      columns: null,
      sort: null,
      format: 'CSV',
      language: 'en',
    });
    expect([refused.status, refused.code]).toEqual([422, 'REPORT_PARAMETER_UNAUTHORIZED']);
    expect((await department.api.get<{ totalCount: number }>('/report-jobs')).totalCount).toBe(
      before,
    );

    const latest =
      (await department.api.get<{ items: { id: string }[] }>('/report-jobs?pageSize=1')).items[0]
        ?.id ?? '';
    for (const path of [`/report-jobs/${latest}`, `/report-jobs/${latest}/content`]) {
      const other = await outsider.api.send('GET', path);
      expect([path, other.status, other.code]).toEqual([path, 404, 'NOT_FOUND']);
    }
  });

  await test.step('a replayed export answers with its job and makes no second file; the key reused for other choices is refused', async () => {
    const request = {
      parameters: [{ code: 'PROJECT', value: ours.id }],
      columns: null,
      sort: null,
      format: 'CSV',
      language: 'en',
    };
    const key = randomUUID();
    const first = await department.api.send<{ id: string }>(
      'POST',
      '/reports/PROJECT_REPORT/export',
      request,
      {
        idempotencyKey: key,
      },
    );
    expect(first.status).toBe(202);
    const before = (await department.api.get<{ totalCount: number }>('/report-jobs')).totalCount;
    const replay = await department.api.send<{ id: string }>(
      'POST',
      '/reports/PROJECT_REPORT/export',
      request,
      {
        idempotencyKey: key,
      },
    );
    expect([replay.status, replay.body.id]).toEqual([202, first.body.id]);
    const changed = await department.api.send(
      'POST',
      '/reports/PROJECT_REPORT/export',
      { ...request, format: 'PDF' },
      {
        idempotencyKey: key,
      },
    );
    expect([changed.status, changed.code]).toEqual([422, 'REPORT_DUPLICATE_REQUEST']);
    expect((await department.api.get<{ totalCount: number }>('/report-jobs')).totalCount).toBe(
      before,
    );
  });
});
