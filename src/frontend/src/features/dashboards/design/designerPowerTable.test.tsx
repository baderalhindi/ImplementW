import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { formatPoints } from '@/features/projects/presentation.ts';
import { sarCompact } from '@/shared/format/money.ts';
import { translate } from '@/shared/i18n/i18n.ts';
import { dashboardView, withDashboards } from '@/test/dashboardFixtures.ts';
import { sessionFor } from '@/test/identityAccessFixtures.ts';
import { type MockApi, mockApi } from '@/test/mockApi.ts';
import {
  designFieldsFixture,
  FIELD,
  fullInsights,
  SECTOR_A,
  standardDesignOf,
} from '@/test/portfolioInsightFixtures.ts';
import { withProjectLookups } from '@/test/projectFixtures.ts';
import { renderApp } from '@/test/renderApp.tsx';

import {
  type DesignView,
  type TableColumn,
  type TableCell,
  type Visual,
  type VisualResult,
  type VisualType,
} from './designApi.ts';

// The Power BI-style table (20 Nov 2026, docs/architecture/dashboard-designer-power-table.md): a TABLE visual takes ordered columns, each a
// dimension, a field or a portfolio figure with its calculation; the columns left as "Don't summarize" group the rows. "Make editable" turns a
// table section into such a visual. The controls are found by role and by the contract's names, not by the markup.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);
const plain = (text: string | null | undefined) =>
  (text ?? '')
    .replace(/[⁨⁩\u00a0]/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();

const DESIGNER = '/dashboards/design';
const DASHBOARD = '/dashboards?dashboard=PORTFOLIO';

const SECTOR_B = 'ac000000-0000-4000-8000-000000000002';

function present<T>(value: T | null | undefined, what = 'The element'): T {
  if (value === null || value === undefined) {
    throw new Error(`${what} is not there`);
  }
  return value;
}

function visualsSent(body: unknown): Visual[] {
  return (body as { visuals: Visual[] }).visuals;
}

type TableResult = NonNullable<VisualResult['table']>;

/** What the mocked API answers for a column table: `table` when the test gives one, else one empty row per call. */
function answer(visual: Visual, table: TableResult | null): VisualResult {
  const { children: _children, ...rest } = visual;
  const columns = visual.columns ?? null;
  if (columns === null || columns.length === 0) {
    return { ...rest, groups: visual.visualType === 'SECTION' ? [] : [] };
  }
  const blank: TableCell = { key: null, label: null, value: null };
  return {
    ...rest,
    groups: [],
    table: table ?? {
      columnTypes: columns.map(() => 'COUNT'),
      rows: [],
      totals: visual.showTotals === false ? null : columns.map(() => blank),
      truncated: false,
    },
  };
}

/**
 * The Portfolio dashboard's API: every insight answered, this design read (the standard one unless given), the fields, and a preview and a
 * save that echo the visuals sent; a column table is answered with `table` (or an empty one).
 */
function api(
  design: DesignView = standardDesignOf(fullInsights()),
  table: TableResult | null = null,
): MockApi {
  const mock = mockApi();
  withProjectLookups(mock);
  withDashboards(mock, { views: { PORTFOLIO: dashboardView('PORTFOLIO') } });
  const echo = (visuals: Visual[]) => visuals.map((visual) => answer(visual, table));
  return mock
    .on('GET', /^\/portfolio-insights$/, { body: fullInsights() })
    .on('GET', /^\/portfolio-insights\/design$/, { body: design })
    .on('GET', /^\/portfolio-insights\/design\/fields$/, { body: designFieldsFixture() })
    .on('POST', /^\/portfolio-insights\/design\/preview$/, (request) => ({
      body: { isStandard: false, updatedAt: null, visuals: echo(visualsSent(request.body)) },
    }))
    .on('PUT', /^\/portfolio-insights\/design$/, (request) => ({
      body: {
        isStandard: false,
        updatedAt: '2026-11-20T09:00:00Z',
        visuals: echo(visualsSent(request.body)),
      },
    }));
}

const NOTHING_SAVED: DesignView = { isStandard: true, updatedAt: null, visuals: [] };

const session = () => sessionFor(['R02']);

const settings = () => screen.getByRole('region', { name: en('dashboards.designer.properties') });

const columnsList = () =>
  within(settings()).getByRole('list', { name: en('dashboards.designer.columnsLabel') });

/** The Columns list's entries, as their words read (the column's name, then its calculation's selected words). */
const listedColumns = () =>
  within(columnsList())
    .getAllByRole('listitem')
    .map((item) => item);

/** The calculation select of a column, labelled "Calculation for {column}". */
function calculationFor(column: string): HTMLSelectElement {
  const select = within(settings()).getByRole('combobox', {
    name: (name) =>
      plain(name) === plain(en('dashboards.designer.columnAggregationLabel', { column })),
  });
  if (!(select instanceof HTMLSelectElement)) {
    throw new Error(`The calculation of ${column} is not a select`);
  }
  return select;
}

const optionValues = (select: HTMLSelectElement) =>
  Array.from(select.options).map((option) => option.value);

async function addVisual(user: ReturnType<typeof userEvent.setup>, type: VisualType) {
  await screen.findByRole('heading', { level: 1, name: en('dashboards.designer.title') });
  const tile = await waitFor(() => {
    const button = screen.getByRole('button', { name: en(`dashboards.designer.type.${type}`) });
    expect(button.hasAttribute('disabled')).toBe(false);
    return button;
  });
  await user.click(tile);
}

async function edit(user: ReturnType<typeof userEvent.setup>, title: string) {
  await user.click(
    await screen.findByRole('button', {
      name: (name) => plain(name) === plain(en('dashboards.designer.editVisual', { title })),
    }),
  );
}

/** Opens Add column and picks the entry whose name is `name` (a dimension, a figure or a field). */
async function addColumn(user: ReturnType<typeof userEvent.setup>, name: string) {
  await user.click(
    within(settings()).getByRole('button', { name: en('dashboards.designer.addColumn') }),
  );
  const exact = (candidate: string) => plain(candidate) === name;
  const pick = await waitFor(
    () =>
      within(settings()).queryByRole('button', { name: exact }) ??
      within(settings()).queryByRole('option', { name: exact }) ??
      within(settings()).queryByRole('menuitem', { name: exact }) ??
      within(settings()).getByRole('checkbox', { name: exact }),
  );
  await user.click(pick);
}

function lastPreviewed(mock: MockApi): Visual[] {
  const previews = mock.requestsTo('POST', /^\/portfolio-insights\/design\/preview$/);
  return visualsSent(previews.at(-1)?.body ?? { visuals: [] });
}

async function previewedLast(mock: MockApi, expected: Partial<Visual>) {
  await waitFor(() => {
    expect(lastPreviewed(mock).at(-1)).toMatchObject(expected);
  });
}

async function saved(mock: MockApi, user: ReturnType<typeof userEvent.setup>): Promise<Visual[]> {
  await user.click(screen.getByRole('button', { name: en('dashboards.designer.save') }));
  await waitFor(() => {
    expect(mock.requestsTo('PUT', /^\/portfolio-insights\/design$/)).toHaveLength(1);
  });
  return visualsSent(mock.requestsTo('PUT', /^\/portfolio-insights\/design$/)[0]?.body);
}

const dimension = (key: string, aggregation: TableColumn['aggregation'] = 'NONE'): TableColumn => ({
  kind: 'DIMENSION',
  key,
  aggregation,
});
const measure = (key: string): TableColumn => ({ kind: 'MEASURE', key, aggregation: null });
const fieldColumn = (key: string, aggregation: TableColumn['aggregation']): TableColumn => ({
  kind: 'FIELD',
  key,
  aggregation,
});

/** A field's English name, as the Data panel names it. */
function fieldName(key: string): string {
  const descriptor = present(
    designFieldsFixture().fields.find((field) => field.key === key),
    key,
  );
  return en(
    `dashboards.designer.field.${descriptor.projectionCode.replace('.', '_')}.${descriptor.fieldCode}`,
  );
}

/** A cell's words without its sort arrow or other hidden marks. */
function wordsOf(cell: Element): string {
  const copy = cell.cloneNode(true) as Element;
  copy.querySelectorAll('[aria-hidden="true"]').forEach((hidden) => {
    hidden.remove();
  });
  return plain(copy.textContent);
}

/** Whether a cell is the row selection's (its checkbox), which every section table has before its columns. */
const isSelection = (cell: Element) =>
  cell.classList.contains('cell--select') || cell.querySelector('input[type="checkbox"]') !== null;

/** The heading words and body/total rows of a drawn table, each cell as its words, the selection column left out. */
function readTable(table: HTMLElement) {
  const cellsOf = (row: Element) =>
    [...row.querySelectorAll('th, td')].filter((cell) => !isSelection(cell)).map(wordsOf);
  const heads = [...table.querySelectorAll('thead tr')].flatMap(cellsOf);
  const rowsOf = (selector: string) => [...table.querySelectorAll(selector)].map(cellsOf);
  return { heads, rows: rowsOf('tbody tr'), totals: rowsOf('tfoot tr') };
}

describe('a new table from the gallery', () => {
  test('starts as Sector (Don’t summarize) and Projects, with no measures or grouping of its own', async () => {
    const mock = api(NOTHING_SAVED);
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await addVisual(user, 'TABLE');

    await previewedLast(mock, {
      visualType: 'TABLE',
      columns: [dimension('SECTOR'), measure('PROJECTS')],
      measures: [],
      groupBy: 'NONE',
    });
    const sent = present(lastPreviewed(mock).at(-1));
    expect(sent.values ?? null).toBeNull();
    expect(sent.groupField ?? null).toBeNull();
    expect(sent.showTotals).toBe(true);
    expect(plain(sent.title.en)).toBe('Projects by Sector');
    // The Columns list names them in order; a figure has no calculation to choose.
    const items = listedColumns().map((item) => plain(item.textContent));
    expect(items).toHaveLength(2);
    expect(present(items[0])).toContain(en('dashboards.designer.groupBy.SECTOR'));
    expect(present(items[1])).toContain(en('dashboards.designer.measure.PROJECTS'));
    expect(optionValues(calculationFor(en('dashboards.designer.groupBy.SECTOR')))).toContain(
      'NONE',
    );
    expect(
      within(present(listedColumns()[1])).queryByRole('combobox'),
      'a figure takes no calculation',
    ).toBeNull();
  });
});

describe('the Columns of a table', () => {
  test('a dimension offers Don’t summarize, Count and Count distinct; an amount field adds Sum, Average, Minimum and Maximum', async () => {
    const mock = api(NOTHING_SAVED);
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await addVisual(user, 'TABLE');
    await previewedLast(mock, { columns: [dimension('SECTOR'), measure('PROJECTS')] });

    expect(optionValues(calculationFor(en('dashboards.designer.groupBy.SECTOR')))).toEqual([
      'NONE',
      'COUNT',
      'COUNT_DISTINCT',
    ]);

    const budget = fieldName(FIELD.REGISTRATION_BUDGET);
    await addColumn(user, budget);
    const calculation = await waitFor(() => calculationFor(budget));
    expect(optionValues(calculation)).toEqual([
      'NONE',
      'COUNT',
      'COUNT_DISTINCT',
      'SUM',
      'AVERAGE',
      'MIN',
      'MAX',
    ]);
    await user.selectOptions(calculation, 'SUM');
    await previewedLast(mock, {
      columns: [
        dimension('SECTOR'),
        measure('PROJECTS'),
        fieldColumn(FIELD.REGISTRATION_BUDGET, 'SUM'),
      ],
    });

    // The dimension's calculation is sent too.
    await user.selectOptions(
      calculationFor(en('dashboards.designer.groupBy.SECTOR')),
      'COUNT_DISTINCT',
    );
    await previewedLast(mock, {
      columns: [
        dimension('SECTOR', 'COUNT_DISTINCT'),
        measure('PROJECTS'),
        fieldColumn(FIELD.REGISTRATION_BUDGET, 'SUM'),
      ],
    });
  });

  test('a dimension added from Project details groups the rows by it as well', async () => {
    const mock = api(NOTHING_SAVED);
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await addVisual(user, 'TABLE');
    await addColumn(user, en('dashboards.designer.groupBy.STATUS'));
    await previewedLast(mock, {
      columns: [dimension('SECTOR'), measure('PROJECTS'), dimension('STATUS')],
    });
  });

  test('removing and reordering a column change the columns sent', async () => {
    const mock = api(NOTHING_SAVED);
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await addVisual(user, 'TABLE');
    await addColumn(user, en('dashboards.designer.measure.VALUE_SAR'));
    await previewedLast(mock, {
      columns: [dimension('SECTOR'), measure('PROJECTS'), measure('VALUE_SAR')],
    });

    // × on Projects.
    const projectsItem = present(
      listedColumns().find((item) =>
        plain(item.textContent).includes(en('dashboards.designer.measure.PROJECTS')),
      ),
      'The Projects column',
    );
    await user.click(
      within(projectsItem).getByRole('button', {
        name: (name) =>
          plain(name) ===
          plain(
            en('dashboards.designer.removeColumn', {
              column: en('dashboards.designer.measure.PROJECTS'),
            }),
          ),
      }),
    );
    await previewedLast(mock, { columns: [dimension('SECTOR'), measure('VALUE_SAR')] });

    // Moving Total value up puts it first (the keyboard's way of dragging).
    const valueItem = present(
      listedColumns().find((item) =>
        plain(item.textContent).includes(en('dashboards.designer.measure.VALUE_SAR')),
      ),
      'The Total value column',
    );
    await user.click(
      within(valueItem).getByRole('button', {
        name: (name) =>
          plain(name) ===
          plain(
            en('dashboards.designer.moveColumnUp', {
              column: en('dashboards.designer.measure.VALUE_SAR'),
            }),
          ),
      }),
    );
    await previewedLast(mock, { columns: [measure('VALUE_SAR'), dimension('SECTOR')] });
  });

  test('Show totals off sends showTotals false', async () => {
    const mock = api(NOTHING_SAVED);
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await addVisual(user, 'TABLE');
    const totals = within(settings()).getByRole<HTMLInputElement>('checkbox', {
      name: en('dashboards.designer.showTotals'),
    });
    expect(totals.checked).toBe(true);
    await user.click(totals);
    await previewedLast(mock, { showTotals: false });

    const sent = await saved(mock, user);
    expect(sent.at(-1)).toMatchObject({
      visualType: 'TABLE',
      showTotals: false,
      columns: [dimension('SECTOR'), measure('PROJECTS')],
    });
  });
});

// A drawn column table: Sector, Projects, Total value, Average actual, Gap, On plan and the summed registration budget.
const TABLE_TITLE = 'Sectors at a glance';
const TABLE_COLUMNS: TableColumn[] = [
  dimension('SECTOR'),
  measure('PROJECTS'),
  measure('VALUE_SAR'),
  measure('AVERAGE_ACTUAL'),
  measure('GAP_POINTS'),
  measure('ON_PLAN'),
  fieldColumn(FIELD.REGISTRATION_BUDGET, 'SUM'),
];
const cell = (value: number | null, extra: Partial<TableCell> = {}): TableCell => ({
  key: null,
  label: null,
  value,
  ...extra,
});
const TABLE: TableResult = {
  columnTypes: ['ITEM', 'COUNT', 'SAR', 'PERCENT', 'POINTS', 'ON_PLAN', 'SAR'],
  rows: [
    {
      projects: 30,
      projectId: null,
      cells: [
        cell(null, { key: SECTOR_A, label: { ar: 'الصحة', en: 'Health' } }),
        cell(30),
        cell(5718150197, { masked: false }),
        cell(55.5),
        cell(-7.25),
        cell(null, { key: 'LATE' }),
        // A field cell the caller may not see.
        cell(null, { masked: true }),
      ],
    },
    {
      projects: 12,
      projectId: null,
      cells: [
        cell(null, { key: SECTOR_B, label: { ar: 'التعليم', en: 'Education' } }),
        cell(12),
        cell(1500000, { masked: false }),
        // Nothing measured: not available, never 0.
        cell(null),
        cell(2),
        cell(null, { key: 'ON_TRACK' }),
        cell(2400000),
      ],
    },
  ],
  totals: [
    cell(null),
    cell(42),
    cell(7219650197),
    cell(55.5),
    cell(-4.5),
    cell(null, { key: 'LATE' }),
    cell(null, { masked: true }),
  ],
  truncated: true,
};

function savedTable(table: TableResult): DesignView {
  return {
    isStandard: false,
    updatedAt: '2026-11-20T09:00:00Z',
    visuals: [
      {
        title: { ar: TABLE_TITLE, en: TABLE_TITLE },
        visualType: 'TABLE',
        measures: [],
        groupBy: 'NONE',
        layoutColumn: 0,
        layoutRow: 0,
        layoutWidth: 12,
        layoutHeight: 6,
        values: null,
        columns: TABLE_COLUMNS,
        showTotals: true,
        groups: [],
        table,
      },
    ],
  };
}

const drawnTable = async () => screen.findByRole('table', { name: TABLE_TITLE });

describe('a column table drawn', () => {
  test.each([
    ['on the dashboard', DASHBOARD],
    ['in the designer', DESIGNER],
  ])(
    'reads its headings, formatted cells, totals and the truncated note %s',
    async (_where, path) => {
      api(savedTable(TABLE), TABLE);
      renderApp({ path, session: session() });

      const table = await drawnTable();
      const { heads, rows, totals } = readTable(table);
      expect(heads.filter((head) => head !== '')).toHaveLength(TABLE_COLUMNS.length);
      expect(present(heads[0])).toBe(en('dashboards.designer.groupBy.SECTOR'));
      expect(present(heads[1])).toBe(en('dashboards.designer.measure.PROJECTS'));
      expect(present(heads[2])).toBe(en('dashboards.designer.measure.VALUE_SAR'));
      // Power BI style: "Sum of Registration budget".
      expect(present(heads[6])).toBe(
        plain(
          en('dashboards.designer.aggregationOf.SUM', {
            field: fieldName(FIELD.REGISTRATION_BUDGET),
          }),
        ),
      );

      const [health, education] = rows;
      expect(present(health).slice(0, 5)).toEqual([
        'Health',
        '30',
        plain(sarCompact('5718150197', en)),
        '55.5%',
        plain(en('projects.followUp.gapValue', { points: formatPoints(-7.25, true) })),
      ]);
      expect(present(health)[5]).toBe(en('projects.onPlan.LATE'));
      // The masked cell is withheld, not a zero.
      expect([en('financialKpi.restricted'), en('dashboards.insights.notAvailable')]).toContain(
        present(health)[6],
      );
      expect(present(education)[3]).toBe(en('dashboards.insights.notAvailable'));
      expect(present(education)[4]).toBe(
        plain(en('projects.followUp.gapValue', { points: formatPoints(2, true) })),
      );
      expect(present(education)[5]).toBe(en('projects.onPlan.ON_TRACK'));

      // The full figure on hover.
      const value = present(
        [...([...table.querySelectorAll('tbody tr')][0]?.querySelectorAll('th, td') ?? [])].filter(
          (one) => !isSelection(one),
        )[2],
        'The value cell',
      );
      expect(
        value.querySelector('[title]')?.getAttribute('title') ?? value.getAttribute('title'),
      ).toMatch(/5,718,150,197/);

      expect(totals).toHaveLength(1);
      expect(present(totals[0])[0]).toBe(en('dashboards.designer.total'));
      expect(present(totals[0])[1]).toBe('42');
      expect(screen.getByText(en('dashboards.designer.tableTruncated'))).toBeTruthy();
    },
  );

  // In the designer a visual's own controls are inert; the viewer sorts on the dashboard.
  test('on the dashboard, a heading sorts the drawn rows, and nothing is sent', async () => {
    const mock = api(savedTable(TABLE), TABLE);
    const user = userEvent.setup();
    renderApp({ path: DASHBOARD, session: session() });

    const table = await drawnTable();
    const firstColumn = () => readTable(table).rows.map((row) => row[0]);
    expect(firstColumn()).toEqual(['Health', 'Education']);
    const heading = (name: string) =>
      within(table).getByRole('columnheader', { name: (accessible) => plain(accessible) === name });

    // Words go A to Z first, then back; a figure largest first.
    const sector = heading(en('dashboards.designer.groupBy.SECTOR'));
    await user.click(within(sector).getByRole('button'));
    expect(firstColumn()).toEqual(['Education', 'Health']);
    expect(sector.getAttribute('aria-sort')).toBe('ascending');
    await user.click(within(sector).getByRole('button'));
    expect(firstColumn()).toEqual(['Health', 'Education']);
    expect(sector.getAttribute('aria-sort')).toBe('descending');

    const projects = heading(en('dashboards.designer.measure.PROJECTS'));
    await user.click(within(projects).getByRole('button'));
    expect(projects.getAttribute('aria-sort')).toBe('descending');
    expect(firstColumn()).toEqual(['Health', 'Education']);
    await user.click(within(projects).getByRole('button'));
    expect(firstColumn()).toEqual(['Education', 'Health']);
    // The totals row stays at the foot.
    expect(readTable(table).totals).toHaveLength(1);

    expect(mock.requestsTo('POST', /^\/portfolio-insights\/design\/preview$/)).toHaveLength(0);
    expect(mock.requestsTo('PUT', /^\/portfolio-insights\/design$/)).toHaveLength(0);
  });

  test('with totals off, no totals row is drawn', async () => {
    const withoutTotals = { ...TABLE, totals: null, truncated: false };
    const design = savedTable(withoutTotals);
    api(
      { ...design, visuals: design.visuals.map((visual) => ({ ...visual, showTotals: false })) },
      withoutTotals,
    );
    renderApp({ path: DASHBOARD, session: session() });
    const table = await drawnTable();
    expect(readTable(table).totals).toHaveLength(0);
    expect(screen.queryByText(en('dashboards.designer.tableTruncated'))).toBeNull();
  });
});

describe('Make editable', () => {
  // Value becomes the summed registration budget, a field whose calculation can then be changed (lead, 20 Nov 2026).
  const VALUE_COLUMN = fieldColumn(FIELD.REGISTRATION_BUDGET, 'SUM');
  const BREAKDOWN_COLUMNS: readonly (readonly [string, TableColumn])[] = [
    ['PROJECTS', measure('PROJECTS')],
    ['VALUE', VALUE_COLUMN],
    ['MEASURED', measure('MEASURED')],
    ['AVERAGE_PLANNED', measure('AVERAGE_PLANNED')],
    ['AVERAGE_ACTUAL', measure('AVERAGE_ACTUAL')],
    ['GAP', measure('GAP_POINTS')],
    ['ON_PLAN', measure('ON_PLAN')],
    ['NEEDING_FOLLOW_UP', measure('NEEDING_FOLLOW_UP')],
  ];
  const makeEditable = () =>
    within(settings()).getByRole('button', { name: en('dashboards.designer.makeEditable') });

  test('turns By sector into a table of the same place, size and titles, with its columns', async () => {
    const mock = api();
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await edit(user, 'By sector');
    await user.click(makeEditable());

    await waitFor(() => {
      expect(document.querySelector('[data-section="sector"]')).toBeNull();
    });
    expect(screen.getByText(en('dashboards.designer.unsaved'))).toBeTruthy();
    const sent = await saved(mock, user);
    const table = present(
      sent.find((visual) => visual.title.en === 'By sector'),
      'The By sector visual',
    );
    expect(table).toMatchObject({
      visualType: 'TABLE',
      measures: [],
      groupBy: 'NONE',
      layoutColumn: 0,
      layoutRow: 31,
      layoutWidth: 12,
      layoutHeight: 8,
      title: { ar: 'By sector', en: 'By sector' },
      columns: [dimension('SECTOR'), ...BREAKDOWN_COLUMNS.map(([, column]) => column)],
    });
    expect(table.section ?? null).toBeNull();
    expect(table.showTotals).toBe(true);
    expect(sent.some((visual) => visual.section === 'SECTOR_TABLE')).toBe(false);
    // Only a column table carries columns and showTotals.
    for (const visual of sent.filter((one) => one !== table)) {
      expect('columns' in visual, visual.title.en).toBe(false);
      expect('showTotals' in visual, visual.title.en).toBe(false);
    }
  });

  test('leaves out the section’s hidden columns', async () => {
    const mock = api();
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await edit(user, 'By sector');
    const columns = within(settings()).getByRole('group', {
      name: en('dashboards.designer.columns'),
    });
    await user.click(
      within(columns).getByRole('checkbox', { name: en('dashboards.insights.value') }),
    );
    await user.click(
      within(columns).getByRole('checkbox', { name: en('dashboards.insights.gap') }),
    );
    await user.click(makeEditable());

    const sent = await saved(mock, user);
    expect(sent.find((visual) => visual.title.en === 'By sector')?.columns).toEqual([
      dimension('SECTOR'),
      ...BREAKDOWN_COLUMNS.filter(([code]) => code !== 'VALUE' && code !== 'GAP').map(
        ([, column]) => column,
      ),
    ]);
  });

  // Two visuals each are titled "Projects by status" and "Projects by stage": the section is the one whose settings offer Make editable.
  test.each([
    [
      'STATUS_TABLE',
      'Projects by status',
      [0, 39, 6, 5],
      [dimension('STATUS'), measure('PROJECTS'), measure('PROJECT_SHARE')],
    ],
    [
      'CURRENT_STAGE_TABLE',
      'Projects by stage',
      [6, 39, 6, 5],
      [dimension('STAGE'), measure('PROJECTS'), VALUE_COLUMN],
    ],
  ] as const)('turns %s into a table of its columns', async (section, title, place, columns) => {
    const mock = api();
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await waitFor(() => {
      expect(document.querySelector('[data-section]')).not.toBeNull();
    });
    const editButtons = await screen.findAllByRole('button', {
      name: (name) => plain(name) === plain(en('dashboards.designer.editVisual', { title })),
    });
    let found = false;
    for (const button of editButtons) {
      await user.click(button);
      if (
        within(settings()).queryByRole('button', { name: en('dashboards.designer.makeEditable') })
      ) {
        found = true;
        break;
      }
    }
    expect(found).toBe(true);
    await user.click(makeEditable());

    const sent = await saved(mock, user);
    const table = present(
      sent.find((visual) => visual.visualType === 'TABLE' && visual.title.en === title),
      `The ${section} table`,
    );
    const [layoutColumn, layoutRow, layoutWidth, layoutHeight] = place;
    expect(table).toMatchObject({
      layoutColumn,
      layoutRow,
      layoutWidth,
      layoutHeight,
      measures: [],
      groupBy: 'NONE',
      columns: [...columns],
    });
    expect(sent.some((visual) => visual.section === section)).toBe(false);
  });

  test('is not offered on Top 10 to follow up, nor on a section that is not a table', async () => {
    api();
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await edit(user, 'Top 10 to follow up');
    expect(
      within(settings()).queryByRole('button', { name: en('dashboards.designer.makeEditable') }),
    ).toBeNull();
    await edit(user, 'Update completion by entity (table)');
    expect(
      within(settings()).queryByRole('button', { name: en('dashboards.designer.makeEditable') }),
    ).toBeNull();
  });

  test('Discard brings the section back', async () => {
    const mock = api();
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await edit(user, 'By sector');
    await user.click(makeEditable());
    await waitFor(() => {
      expect(document.querySelector('[data-section="sector"]')).toBeNull();
    });

    await user.click(screen.getByRole('button', { name: en('dashboards.designer.discard') }));
    await waitFor(() => {
      expect(document.querySelector('[data-section="sector"]')).not.toBeNull();
    });
    // The standard design reads as unsaved already (its widgets split into one visual each), so the baseline is the section and no save.
    expect(mock.requestsTo('PUT', /^\/portfolio-insights\/design$/)).toHaveLength(0);
  });
});
