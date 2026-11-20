import { describe, expect, test } from 'vitest';

import { sarCompact } from '@/shared/format/money.ts';
import { translate } from '@/shared/i18n/i18n.ts';
import { designFieldsFixture, FIELD } from '@/test/portfolioInsightFixtures.ts';

import { type TableCell, type TableColumn, type TableRow, type Visual } from './designApi.ts';
import { NO_HIDDEN_COLUMNS } from './sectionColumns.ts';
import {
  addedColumn,
  cellExact,
  cellText,
  type CellNames,
  columnAggregations,
  columnHeading,
  columnName,
  editableColumns,
  freeAggregations,
  isEditableSection,
  madeEditable,
  MAX_COLUMNS,
  movedColumn,
  NEW_TABLE_COLUMNS,
  sortedRows,
  VALUE_FIELD,
  withoutColumns,
} from './tableColumns.ts';

// The Power BI-style table's pure rules (docs/architecture/dashboard-designer-power-table.md).

const FIELDS = designFieldsFixture().fields;
const en = (key: string, params?: Record<string, string | number>) =>
  translate('en', key, params).replace(/[⁨⁩]/g, '');
const t = (key: string, params?: Record<string, string | number>) => translate('en', key, params);

const dimension = (key: string, aggregation: TableColumn['aggregation'] = 'NONE'): TableColumn => ({
  kind: 'DIMENSION',
  key,
  aggregation,
});
const measure = (key: string): TableColumn => ({ kind: 'MEASURE', key, aggregation: null });
const field = (key: string, aggregation: TableColumn['aggregation']): TableColumn => ({
  kind: 'FIELD',
  key,
  aggregation,
});

const NAMES: CellNames = {
  itemLabel: () => null,
  departmentName: () => null,
  entityName: () => null,
};

describe('a column’s calculations', () => {
  test('a figure takes none, a dimension Don’t summarize, Count and Count distinct', () => {
    expect(columnAggregations(measure('PROJECTS'), FIELDS)).toEqual([null]);
    expect(columnAggregations(dimension('SECTOR'), FIELDS)).toEqual([
      'NONE',
      'COUNT',
      'COUNT_DISTINCT',
    ]);
  });

  test('a field takes Don’t summarize, then its type’s own with Count distinct after Count', () => {
    const of = (key: string) => columnAggregations(field(key, 'NONE'), FIELDS);
    expect(of(FIELD.REGISTRATION_BUDGET)).toEqual([
      'NONE',
      'COUNT',
      'COUNT_DISTINCT',
      'SUM',
      'AVERAGE',
      'MIN',
      'MAX',
    ]);
    // A percentage has no sum.
    expect(of(FIELD.ACTUAL_PERCENT)).toEqual([
      'NONE',
      'COUNT',
      'COUNT_DISTINCT',
      'AVERAGE',
      'MIN',
      'MAX',
    ]);
    expect(of(FIELD.IS_STRATEGIC)).toEqual([
      'NONE',
      'COUNT',
      'COUNT_DISTINCT',
      'COUNT_TRUE',
      'SHARE_TRUE',
    ]);
    expect(of(FIELD.FUNDING_SOURCE)).toEqual(['NONE', 'COUNT', 'COUNT_DISTINCT']);
    expect(of(FIELD.PLANNED_END_DATE)).toEqual(['NONE', 'COUNT', 'COUNT_DISTINCT', 'MIN', 'MAX']);
  });

  test('a field not in the catalogue keeps its own calculation', () => {
    expect(columnAggregations(field('X.Y.Z', 'SUM'), FIELDS)).toEqual(['SUM']);
  });

  test('a column may not change to a calculation another column of its key has', () => {
    const columns = [
      field(FIELD.REGISTRATION_BUDGET, 'SUM'),
      field(FIELD.REGISTRATION_BUDGET, 'AVERAGE'),
    ];
    expect(freeAggregations(columns, 0, FIELDS)).toEqual([
      'NONE',
      'COUNT',
      'COUNT_DISTINCT',
      'SUM',
      'MIN',
      'MAX',
    ]);
    expect(freeAggregations(columns, 5, FIELDS)).toEqual([]);
  });
});

describe('a column’s words', () => {
  test('its name, and a Power BI heading', () => {
    const budget = en('dashboards.designer.field.PROJECT_PROJECT_PROFILE.REGISTRATION_BUDGET');
    expect(columnName(dimension('SECTOR'), 'en')).toBe('Sector');
    expect(columnName(dimension('TITLE'), 'en')).toBe('Project title');
    expect(columnName(measure('PROJECT_SHARE'), 'en')).toBe('Share of projects');
    expect(columnHeading(dimension('SECTOR'), 'en')).toBe('Sector');
    expect(columnHeading(dimension('SECTOR', 'COUNT'), 'en')).toBe('Count of Sector');
    expect(columnHeading(field(FIELD.REGISTRATION_BUDGET, 'SUM'), 'en')).toBe(`Sum of ${budget}`);
    expect(columnHeading(field(FIELD.REGISTRATION_BUDGET, 'COUNT_DISTINCT'), 'en')).toBe(
      `Count distinct of ${budget}`,
    );
    expect(columnHeading(measure('PROJECTS'), 'en')).toBe('Projects');
    // No bidi isolates in a heading.
    expect(columnHeading(dimension('SECTOR', 'COUNT'), 'ar')).not.toMatch(/[⁨⁩]/);
  });
});

describe('adding and moving columns', () => {
  test('a pick adds Don’t summarize, then the next calculation free; a figure once; none past twelve', () => {
    expect(addedColumn(NEW_TABLE_COLUMNS, 'DIMENSION', 'STATUS', FIELDS)).toEqual(
      dimension('STATUS'),
    );
    expect(addedColumn(NEW_TABLE_COLUMNS, 'DIMENSION', 'SECTOR', FIELDS)).toEqual(
      dimension('SECTOR', 'COUNT'),
    );
    expect(addedColumn(NEW_TABLE_COLUMNS, 'MEASURE', 'PROJECTS', FIELDS)).toBeNull();
    expect(addedColumn(NEW_TABLE_COLUMNS, 'MEASURE', 'VALUE_SAR', FIELDS)).toEqual(
      measure('VALUE_SAR'),
    );
    const full = Array.from({ length: MAX_COLUMNS }, (_, at) => field(`F.G.H${String(at)}`, 'SUM'));
    expect(addedColumn(full, 'DIMENSION', 'STATUS', FIELDS)).toBeNull();
  });

  test('a column moves to its new place; an impossible move leaves the order', () => {
    const columns = [dimension('SECTOR'), measure('PROJECTS'), measure('VALUE_SAR')];
    expect(movedColumn(columns, 2, 0)).toEqual([
      measure('VALUE_SAR'),
      dimension('SECTOR'),
      measure('PROJECTS'),
    ]);
    expect(movedColumn(columns, 0, 1)).toEqual([
      measure('PROJECTS'),
      dimension('SECTOR'),
      measure('VALUE_SAR'),
    ]);
    expect(movedColumn(columns, 0, -1)).toEqual(columns);
    expect(movedColumn(columns, 0, 3)).toEqual(columns);
  });
});

describe('Make editable', () => {
  const BREAKDOWN = [
    measure('PROJECTS'),
    field(VALUE_FIELD, 'SUM'),
    measure('MEASURED'),
    measure('AVERAGE_PLANNED'),
    measure('AVERAGE_ACTUAL'),
    measure('GAP_POINTS'),
    measure('ON_PLAN'),
    measure('NEEDING_FOLLOW_UP'),
  ];

  test('each table section’s columns, less the hidden ones', () => {
    expect(VALUE_FIELD).toBe(FIELD.REGISTRATION_BUDGET);
    expect(editableColumns('PORTFOLIO_TABLE', NO_HIDDEN_COLUMNS)).toEqual([
      dimension('PORTFOLIO'),
      ...BREAKDOWN,
    ]);
    expect(editableColumns('INITIATIVE_TABLE', NO_HIDDEN_COLUMNS)).toEqual([
      dimension('INITIATIVE'),
      ...BREAKDOWN,
    ]);
    expect(editableColumns('SECTOR_TABLE', new Set(['VALUE', 'GAP', 'SPLIT']))).toEqual([
      dimension('SECTOR'),
      ...BREAKDOWN.filter((column) => column.key !== VALUE_FIELD && column.key !== 'GAP_POINTS'),
    ]);
    expect(editableColumns('STATUS_TABLE', NO_HIDDEN_COLUMNS)).toEqual([
      dimension('STATUS'),
      measure('PROJECTS'),
      measure('PROJECT_SHARE'),
    ]);
    expect(editableColumns('CURRENT_STAGE_TABLE', new Set(['PROJECTS']))).toEqual([
      dimension('STAGE'),
      field(VALUE_FIELD, 'SUM'),
    ]);
  });

  test('is offered on the five table sections only', () => {
    for (const section of [
      'PORTFOLIO_TABLE',
      'INITIATIVE_TABLE',
      'SECTOR_TABLE',
      'STATUS_TABLE',
      'CURRENT_STAGE_TABLE',
    ] as const) {
      expect(isEditableSection(section), section).toBe(true);
    }
    for (const section of [
      'TOP_FOLLOW_UP',
      'ENTITY_COMPLETION_TABLE',
      'ON_PLAN_COUNTS',
      null,
    ] as const) {
      expect(isEditableSection(section), String(section)).toBe(false);
    }
  });

  test('keeps the section’s place, size and titles, and is a plain table', () => {
    const section: Visual = {
      title: { ar: 'حسب القطاع', en: 'By sector' },
      visualType: 'SECTION',
      measures: [],
      groupBy: 'NONE',
      section: 'SECTOR_TABLE',
      hiddenColumns: ['GAP'],
      layoutColumn: 0,
      layoutRow: 31,
      layoutWidth: 12,
      layoutHeight: 8,
    };
    const table = madeEditable(section, new Set(['GAP']));
    expect(table).toMatchObject({
      title: section.title,
      visualType: 'TABLE',
      measures: [],
      groupBy: 'NONE',
      section: null,
      hiddenColumns: null,
      layoutColumn: 0,
      layoutRow: 31,
      layoutWidth: 12,
      layoutHeight: 8,
      showTotals: true,
    });
    expect(table.columns?.map((column) => column.key)).not.toContain('GAP_POINTS');
    // Any other visual as it is.
    const top: Visual = { ...section, section: 'TOP_FOLLOW_UP' };
    expect(madeEditable(top, NO_HIDDEN_COLUMNS)).toBe(top);
  });

  test('a column table changed to a chart keeps its grouping and figures', () => {
    const table: Visual = {
      title: { ar: 'ج', en: 'T' },
      visualType: 'TABLE',
      measures: [],
      groupBy: 'NONE',
      layoutColumn: 0,
      layoutRow: 0,
      layoutWidth: 6,
      layoutHeight: 4,
      columns: [dimension('SECTOR'), measure('PROJECTS'), measure('MEASURED')],
      showTotals: false,
    };
    expect(withoutColumns(table)).toMatchObject({
      columns: null,
      showTotals: null,
      measures: ['PROJECTS'],
      groupBy: 'SECTOR',
      values: null,
    });
  });
});

describe('a cell in words', () => {
  test('an amount compact with its full figure on hover, a percentage, points, a missing figure', () => {
    const cell = (value: number | null, extra: Partial<TableCell> = {}): TableCell => ({
      key: null,
      label: null,
      value,
      ...extra,
    });
    const text = (column: TableColumn, type: string, value: TableCell) =>
      cellText(column, type, value, NAMES, t, 'en').replace(/[⁨⁩]/g, '');
    expect(text(field(VALUE_FIELD, 'SUM'), 'SAR', cell(5718150197))).toBe(
      sarCompact('5718150197', t).replace(/[⁨⁩]/g, ''),
    );
    expect(cellExact('SAR', cell(5718150197), t)?.replace(/[⁨⁩]/g, '')).toContain('5,718,150,197');
    expect(cellExact('COUNT', cell(3), t)).toBeUndefined();
    expect(text(measure('AVERAGE_ACTUAL'), 'PERCENT', cell(55.55))).toBe('55.6%');
    expect(text(measure('GAP_POINTS'), 'POINTS', cell(2))).toBe(
      en('projects.followUp.gapValue', { points: '+2' }),
    );
    expect(text(measure('PROJECTS'), 'COUNT', cell(0))).toBe('0');
    expect(text(measure('AVERAGE_ACTUAL'), 'PERCENT', cell(null))).toBe(
      en('dashboards.insights.notAvailable'),
    );
    // A withheld amount is never a zero.
    expect(text(field(VALUE_FIELD, 'SUM'), 'SAR', cell(null, { masked: true }))).toBe(
      en('dashboards.insights.notAvailable'),
    );
    expect(
      text(dimension('SECTOR'), 'ITEM', cell(null, { key: 'x', label: { ar: 'ص', en: 'Health' } })),
    ).toBe('Health');
    expect(text(dimension('SECTOR'), 'ITEM', cell(null))).toBe(
      en('dashboards.insights.unclassified'),
    );
    expect(text(dimension('STATUS'), 'CODE', cell(null, { key: 'ACTIVE' }))).toBe(
      en('projects.status.ACTIVE'),
    );
  });

  test('an On plan cell reads its status from its key; none is Not measured', () => {
    const onPlan = measure('ON_PLAN');
    const text = (cell: TableCell) => cellText(onPlan, 'ON_PLAN', cell, NAMES, t, 'en');
    expect(text({ key: 'LATE', label: null, value: null })).toBe(en('projects.onPlan.LATE'));
    expect(text({ key: null, label: null, value: null })).toBe(
      en('dashboards.insights.notMeasured'),
    );
  });
});

describe('the viewer’s sort', () => {
  const row = (name: string | null, projects: number | null): TableRow => ({
    projects: projects ?? 0,
    projectId: null,
    cells: [
      { key: name, label: name === null ? null : { ar: name, en: name }, value: null },
      { key: null, label: null, value: projects },
    ],
  });
  const rows = [row('Health', 30), row(null, 5), row('Education', null), row('Water', 30)];
  const TYPES = ['ITEM', 'COUNT'];
  const textOf = (cell: TableCell) => cell.label?.en ?? '';
  const names = (sorted: TableRow[]) => sorted.map((one) => one.cells[0]?.label?.en ?? '-');

  test('a figure by its value, the missing last both ways, ties in the API’s order', () => {
    const by = (direction: 'ascending' | 'descending') =>
      names(sortedRows(rows, { column: 1, direction }, TYPES, textOf, 'en'));
    expect(by('ascending')).toEqual(['-', 'Health', 'Water', 'Education']);
    expect(by('descending')).toEqual(['Health', 'Water', '-', 'Education']);
  });

  test('words by the language’s order, the unnamed last both ways', () => {
    const by = (direction: 'ascending' | 'descending') =>
      names(sortedRows(rows, { column: 0, direction }, TYPES, textOf, 'en'));
    expect(by('ascending')).toEqual(['Education', 'Health', 'Water', '-']);
    expect(by('descending')).toEqual(['Water', 'Health', 'Education', '-']);
  });

  test('no sort keeps the API’s order, and the rows given are not changed', () => {
    const copy = [...rows];
    expect(sortedRows(rows, null, TYPES, textOf, 'en')).toEqual(rows);
    sortedRows(rows, { column: 1, direction: 'descending' }, TYPES, textOf, 'en');
    expect(rows).toEqual(copy);
  });
});
