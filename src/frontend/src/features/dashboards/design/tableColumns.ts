import { formatPoints } from '@/features/projects/presentation.ts';
import { sarCompact, sarExactLabel } from '@/shared/format/money.ts';
import { type Language, translate, type TranslationKey } from '@/shared/i18n/i18n.ts';
import { type SortDirection } from '@/shared/ui/SortHeader.tsx';

import {
  type FieldAggregation,
  type FieldDescriptor,
  FOLLOW_UP_MEASURES,
  type GroupBy,
  type Measure,
  ON_PLAN_MEASURES,
  type Section,
  type TableAggregation,
  type TableCell,
  type TableColumn,
  type TableColumnKind,
  type TableRow,
  type Visual,
} from './designApi.ts';
import { codeText, fieldName, filterFits, formatDayNumber, NO_FIELD_DATA } from './fields.ts';
import { type HiddenColumns } from './sectionColumns.ts';

// A Power BI-style table (20 Nov 2026, docs/architecture/dashboard-designer-power-table.md): columns, each a project attribute, a data field or a
// portfolio figure; the columns left as "Don't summarize" group the rows, the others are calculated within each row. The API calculates every
// cell; this module holds which calculations a column takes, the columns' words, a section made editable, and the viewer's own sorting.

type Translate = (key: TranslationKey, params?: Record<string, string | number>) => string;

/** The most columns a table takes (contract rule). */
export const MAX_COLUMNS = 12;

/** A project attribute a column shows: a grouping's, or the project's own title or formal ID (one row per project). */
export type TableDimension = Exclude<GroupBy, 'NONE'> | 'TITLE' | 'FORMAL_PROJECT_ID';

/** A portfolio figure a column shows: a visual's measures, and the breakdown tables' measured count, on-plan status and share. */
export type TableMeasure = Measure | 'MEASURED' | 'ON_PLAN' | 'PROJECT_SHARE';

/** The Project details the field list offers, in its order. */
export const TABLE_DIMENSIONS: readonly TableDimension[] = [
  'TITLE',
  'FORMAL_PROJECT_ID',
  'PORTFOLIO',
  'INITIATIVE',
  'SECTOR',
  'DEPARTMENT',
  'ENTITY',
  'STATUS',
  'STAGE',
  'ON_PLAN',
  'REGION',
  'CITY',
  'PRIORITY',
];

/** The Portfolio figures the field list offers, in the breakdown tables' order. */
export const TABLE_MEASURES: readonly TableMeasure[] = [
  'PROJECTS',
  'IN_PROGRESS',
  'VALUE_SAR',
  'MEASURED',
  'AVERAGE_PLANNED',
  'AVERAGE_ACTUAL',
  'GAP_POINTS',
  'ON_PLAN',
  ...ON_PLAN_MEASURES,
  'NEEDING_FOLLOW_UP',
  ...FOLLOW_UP_MEASURES,
  'OVERDUE_ACTIONS',
  'PROJECT_SHARE',
];

/** The columns a new table from the gallery starts with: the sector, then its projects. */
export const NEW_TABLE_COLUMNS: readonly TableColumn[] = [
  { kind: 'DIMENSION', key: 'SECTOR', aggregation: 'NONE' },
  { kind: 'MEASURE', key: 'PROJECTS', aggregation: null },
];

const DIMENSION_AGGREGATIONS: readonly TableAggregation[] = ['NONE', 'COUNT', 'COUNT_DISTINCT'];

/** The groupings by a project attribute (`GroupBy`), which a chart takes too. */
const GROUPINGS: readonly string[] = [
  'PORTFOLIO',
  'INITIATIVE',
  'SECTOR',
  'DEPARTMENT',
  'ENTITY',
  'STATUS',
  'STAGE',
  'ON_PLAN',
  'REGION',
  'CITY',
  'PRIORITY',
];

const VISUAL_MEASURES: readonly string[] = [
  'PROJECTS',
  'IN_PROGRESS',
  'VALUE_SAR',
  'AVERAGE_PLANNED',
  'AVERAGE_ACTUAL',
  'GAP_POINTS',
  'NEEDING_FOLLOW_UP',
  'OVERDUE_ACTIONS',
  ...ON_PLAN_MEASURES,
  ...FOLLOW_UP_MEASURES,
];

const FIELD_AGGREGATIONS: readonly string[] = [
  'COUNT',
  'SUM',
  'AVERAGE',
  'MIN',
  'MAX',
  'COUNT_TRUE',
  'SHARE_TRUE',
];

/**
 * The calculations a column takes, in the select's order: a dimension's Don't summarize, Count and Count distinct; a field's Don't summarize, then
 * its type's own (Count distinct after Count); a figure none (`[null]`). A field not in the catalogue keeps the calculation it has.
 */
export function columnAggregations(
  column: TableColumn,
  fields: readonly FieldDescriptor[],
): (TableAggregation | null)[] {
  switch (column.kind) {
    case 'MEASURE':
      return [null];
    case 'DIMENSION':
      return [...DIMENSION_AGGREGATIONS];
    case 'FIELD': {
      const field = fields.find((candidate) => candidate.key === column.key);
      if (field === undefined) {
        return [column.aggregation];
      }
      const own: TableAggregation[] = field.aggregations.flatMap((aggregation) =>
        aggregation === 'COUNT' ? ['COUNT', 'COUNT_DISTINCT'] : [aggregation],
      );
      return [
        'NONE',
        ...own,
        ...(own.includes('COUNT_DISTINCT') ? [] : ['COUNT_DISTINCT' as const]),
      ];
    }
  }
}

/** Whether a column groups the rows ("Don't summarize"). */
export function groupsRows(column: TableColumn): boolean {
  return column.aggregation === 'NONE';
}

export function sameColumn(a: TableColumn, b: TableColumn): boolean {
  return a.kind === b.kind && a.key === b.key && a.aggregation === b.aggregation;
}

/** A text from the resources as plain text: translate() isolates each parameter for display, a heading and a title are plain. */
function plain(language: Language, key: string, params?: Record<string, string>): string {
  return translate(language, key, params).replace(/[⁨⁩]/g, '');
}

/** What a column shows, without its calculation: "Sector", "Registration budget", "Projects". */
export function columnName(column: Pick<TableColumn, 'kind' | 'key'>, language: Language): string {
  switch (column.kind) {
    case 'DIMENSION':
      return plain(language, `dashboards.designer.groupBy.${column.key}`);
    case 'FIELD':
      return fieldName(column.key, language);
    case 'MEASURE':
      return plain(language, `dashboards.designer.measure.${column.key}`);
  }
}

/** A column's heading, Power BI's way: its name when it groups the rows or is a figure, else "Count of Sector", "Sum of Registration budget". */
export function columnHeading(column: TableColumn, language: Language): string {
  const name = columnName(column, language);
  return column.aggregation === null || column.aggregation === 'NONE'
    ? name
    : plain(language, `dashboards.designer.aggregationOf.${column.aggregation}`, { field: name });
}

/** A key for a column, which no other column of the table shares (no two columns are equal). */
export function columnId(column: TableColumn): string {
  return `${column.kind}:${column.key}:${column.aggregation ?? ''}`;
}

/**
 * The column a pick from the field list adds: Don't summarize (a figure: none), or the first calculation no column of the same key has yet;
 * null when every calculation is taken, or the table is full.
 */
export function addedColumn(
  columns: readonly TableColumn[],
  kind: TableColumnKind,
  key: string,
  fields: readonly FieldDescriptor[],
): TableColumn | null {
  if (columns.length >= MAX_COLUMNS) {
    return null;
  }
  const free = columnAggregations({ kind, key, aggregation: null }, fields).find(
    (aggregation) => !columns.some((column) => sameColumn(column, { kind, key, aggregation })),
  );
  return free === undefined ? null : { kind, key, aggregation: free };
}

/** The columns with one moved from `from` to `to`. */
export function movedColumn(
  columns: readonly TableColumn[],
  from: number,
  to: number,
): TableColumn[] {
  const moved = columns[from];
  if (moved === undefined || to < 0 || to >= columns.length || to === from) {
    return [...columns];
  }
  const rest = columns.filter((_, at) => at !== from);
  return [...rest.slice(0, to), moved, ...rest.slice(to)];
}

/** The calculations column `at` may change to: its own, and those no other column of its key has. */
export function freeAggregations(
  columns: readonly TableColumn[],
  at: number,
  fields: readonly FieldDescriptor[],
): (TableAggregation | null)[] {
  const column = columns[at];
  if (column === undefined) {
    return [];
  }
  return columnAggregations(column, fields).filter(
    (aggregation) =>
      aggregation === column.aggregation ||
      !columns.some(
        (other, index) => index !== at && sameColumn(other, { ...column, aggregation }),
      ),
  );
}

/** Whether a visual is a table drawn from columns. */
export function hasColumns(visual: Pick<Visual, 'visualType' | 'columns'>): boolean {
  return visual.visualType === 'TABLE' && (visual.columns?.length ?? 0) > 0;
}

/**
 * A visual's figures and grouping as table columns: the grouping (a project attribute, or a project field), then its figures or field values;
 * the values over time, which a column table does not take, are left out. A table with columns has them already.
 */
export function columnsOf(visual: Visual, fields: readonly FieldDescriptor[]): TableColumn[] {
  if (visual.columns !== undefined && visual.columns !== null && visual.columns.length > 0) {
    return visual.columns;
  }
  const projectGrain = (key: string) =>
    fields.find((field) => field.key === key)?.grain !== 'SNAPSHOT';
  const groupField = visual.groupField ?? null;
  const grouping: TableColumn[] =
    groupField !== null
      ? projectGrain(groupField)
        ? [{ kind: 'FIELD', key: groupField, aggregation: 'NONE' }]
        : []
      : visual.groupBy === 'NONE'
        ? []
        : [{ kind: 'DIMENSION', key: visual.groupBy, aggregation: 'NONE' }];
  const values = visual.values ?? [];
  const figures: TableColumn[] =
    values.length > 0
      ? values
          .filter((value) => projectGrain(value.field))
          .map((value) => ({ kind: 'FIELD', key: value.field, aggregation: value.aggregation }))
      : visual.measures.map((measure) => ({ kind: 'MEASURE', key: measure, aggregation: null }));
  const columns = [...grouping, ...figures].slice(0, MAX_COLUMNS);
  return figures.length === 0 ? [...NEW_TABLE_COLUMNS] : columns;
}

/** A visual as a column table: its columns, totals shown unless turned off, no measures, values or grouping, and the filters that still fit. */
export function asColumnTable(
  visual: Visual,
  columns: TableColumn[],
  fields: readonly FieldDescriptor[],
): Visual {
  const filters = (visual.filters ?? []).filter((filter) => filterFits(filter, 'PROJECT', fields));
  return {
    ...visual,
    ...NO_FIELD_DATA,
    visualType: 'TABLE',
    measures: [],
    groupBy: 'NONE',
    columns,
    showTotals: visual.showTotals ?? true,
    filters: filters.length === 0 ? null : filters,
  };
}

/**
 * A column table's columns as a visual's measures and grouping, for a change to another type: the first grouping column that a chart takes as
 * its grouping, and the figures, or the field values, whichever comes first. Any other visual as it is.
 */
export function withoutColumns(visual: Visual): Visual {
  if (visual.columns === undefined || visual.columns === null) {
    return visual;
  }
  const { columns, showTotals: _showTotals, ...rest } = visual;
  const grouping = columns.find(
    (column) =>
      column.aggregation === 'NONE' && (column.kind === 'FIELD' || GROUPINGS.includes(column.key)),
  );
  const measures = columns
    .filter((column) => column.kind === 'MEASURE' && VISUAL_MEASURES.includes(column.key))
    .map((column) => column.key as Measure);
  const values = columns
    .filter(
      (column) =>
        column.kind === 'FIELD' &&
        column.aggregation !== null &&
        FIELD_AGGREGATIONS.includes(column.aggregation),
    )
    .map((column) => ({ field: column.key, aggregation: column.aggregation as FieldAggregation }));
  const firstFigure = columns.find(
    (column) =>
      (column.kind === 'MEASURE' && VISUAL_MEASURES.includes(column.key)) ||
      (column.kind === 'FIELD' &&
        column.aggregation !== null &&
        FIELD_AGGREGATIONS.includes(column.aggregation)),
  );
  const onFields = values.length > 0 && (measures.length === 0 || firstFigure?.kind === 'FIELD');
  return {
    ...rest,
    columns: null,
    showTotals: null,
    measures: onFields ? [] : measures.length > 0 ? measures : ['PROJECTS'],
    values: onFields ? values : null,
    groupBy: grouping?.kind === 'DIMENSION' ? (grouping.key as GroupBy) : 'NONE',
    groupField: onFields && grouping?.kind === 'FIELD' ? grouping.key : null,
  };
}

/** The table sections Make editable turns into a column table, with the attribute their rows are. */
const EDITABLE_SECTIONS = {
  PORTFOLIO_TABLE: 'PORTFOLIO',
  INITIATIVE_TABLE: 'INITIATIVE',
  SECTOR_TABLE: 'SECTOR',
  STATUS_TABLE: 'STATUS',
  CURRENT_STAGE_TABLE: 'STAGE',
} as const satisfies Partial<Record<Section, TableDimension>>;

export type EditableSection = keyof typeof EDITABLE_SECTIONS;

export function isEditableSection(section: Section | null | undefined): section is EditableSection {
  return section !== null && section !== undefined && section in EDITABLE_SECTIONS;
}

/** The field a section's value is: the registration budget, summed, so its calculation can be changed (to an average, say). */
export const VALUE_FIELD = 'PROJECT.PROJECT_PROFILE.REGISTRATION_BUDGET';

const measureColumn = (key: TableMeasure): TableColumn => ({
  kind: 'MEASURE',
  key,
  aggregation: null,
});

/** Each section column (sectionColumns.ts) and the column that draws it in a column table. */
const SECTION_FIGURES: Readonly<Record<string, TableColumn>> = {
  PROJECTS: measureColumn('PROJECTS'),
  VALUE: { kind: 'FIELD', key: VALUE_FIELD, aggregation: 'SUM' },
  MEASURED: measureColumn('MEASURED'),
  AVERAGE_PLANNED: measureColumn('AVERAGE_PLANNED'),
  AVERAGE_ACTUAL: measureColumn('AVERAGE_ACTUAL'),
  GAP: measureColumn('GAP_POINTS'),
  ON_PLAN: measureColumn('ON_PLAN'),
  NEEDING_FOLLOW_UP: measureColumn('NEEDING_FOLLOW_UP'),
  PERCENT: measureColumn('PROJECT_SHARE'),
};

const BREAKDOWN_CODES = [
  'PROJECTS',
  'VALUE',
  'MEASURED',
  'AVERAGE_PLANNED',
  'AVERAGE_ACTUAL',
  'GAP',
  'ON_PLAN',
  'NEEDING_FOLLOW_UP',
];

const SECTION_CODES: Record<EditableSection, readonly string[]> = {
  PORTFOLIO_TABLE: BREAKDOWN_CODES,
  INITIATIVE_TABLE: BREAKDOWN_CODES,
  SECTOR_TABLE: BREAKDOWN_CODES,
  STATUS_TABLE: ['PROJECTS', 'PERCENT'],
  CURRENT_STAGE_TABLE: ['PROJECTS', 'VALUE'],
};

/**
 * The columns a table section becomes: what its rows are (Don't summarize), then the figures it shows, less those its design hides; its value
 * is the registration budget's sum (a field), the other figures are portfolio figures.
 */
export function editableColumns(section: EditableSection, hidden: HiddenColumns): TableColumn[] {
  const figures = SECTION_CODES[section]
    .filter((code) => !hidden.has(code))
    .flatMap((code) => {
      const column = SECTION_FIGURES[code];
      return column === undefined ? [] : [column];
    });
  return [
    { kind: 'DIMENSION', key: EDITABLE_SECTIONS[section], aggregation: 'NONE' },
    ...(figures.length === 0 ? [measureColumn('PROJECTS')] : figures),
  ];
}

/**
 * Make editable: the table section as a column table at the same place, size and titles, with the same columns; the visual unchanged for any
 * other. The sector's spend table is not carried over.
 */
export function madeEditable(visual: Visual, hidden: HiddenColumns): Visual {
  if (!isEditableSection(visual.section)) {
    return visual;
  }
  return {
    ...visual,
    ...NO_FIELD_DATA,
    visualType: 'TABLE',
    measures: [],
    groupBy: 'NONE',
    section: null,
    widgetCode: null,
    hiddenColumns: null,
    axes: null,
    columns: editableColumns(visual.section, hidden),
    showTotals: true,
  };
}

/** The column types whose cells are figures: sorted by their value, and written left to right. */
const FIGURE_TYPES: ReadonlySet<string> = new Set([
  'COUNT',
  'PERCENT',
  'DAYS',
  'SAR',
  'DATE',
  'POINTS',
]);

export function isFigureType(type: string | undefined): boolean {
  return type !== undefined && FIGURE_TYPES.has(type);
}

/** Names of master data items, departments and entities, for a cell the API gave no words. */
export interface CellNames {
  itemLabel: (id: string) => string | null;
  departmentName: (id: string) => string | null;
  entityName: (id: string) => string | null;
}

/** The words a cell with no figure reads: its group's, as the dashboard's tables name it, or a yes/no, a code, a status. */
function groupWords(
  column: TableColumn,
  cell: TableCell,
  names: CellNames,
  t: Translate,
  language: Language,
): string {
  const key = cell.key;
  const label = cell.label?.[language];
  if (key === null) {
    if (label !== undefined) {
      return label;
    }
    switch (column.kind === 'DIMENSION' ? column.key : null) {
      case 'PORTFOLIO':
      case 'INITIATIVE':
      case 'SECTOR':
        return t('dashboards.insights.unclassified');
      case 'STAGE':
        return t('dashboards.insights.noCurrentStage');
      case 'ON_PLAN':
        return t('dashboards.insights.notMeasured');
      default:
        return t('dashboards.designer.notSet');
    }
  }
  if (key === 'TRUE' || key === 'FALSE') {
    return t(key === 'TRUE' ? 'dashboards.designer.yes' : 'dashboards.designer.no');
  }
  if (column.kind === 'FIELD') {
    return label ?? codeText(column.key, key, language, t);
  }
  switch (column.key) {
    case 'STATUS':
      return t(`projects.status.${key}` as TranslationKey);
    case 'ON_PLAN':
      return t(`projects.onPlan.${key}` as TranslationKey);
    case 'DEPARTMENT':
      return label ?? names.departmentName(key) ?? key;
    case 'ENTITY':
      return label ?? names.entityName(key) ?? key;
    default:
      return label ?? names.itemLabel(key) ?? key;
  }
}

/**
 * A cell in words, as the dashboard's tables write it: an amount compact ("5.7 مليار ريال"), a percentage, ±points, days, a date, a count, or
 * its group's words; "not available" for a missing or withheld figure. An On plan cell's badge is drawn from its key (ColumnTable).
 */
export function cellText(
  column: TableColumn,
  type: string | undefined,
  cell: TableCell,
  names: CellNames,
  t: Translate,
  language: Language,
): string {
  if (type === 'ON_PLAN') {
    // A row's on-plan status, of the attribute or the figure alike: "Not measured" when it has none.
    return cell.key === null
      ? t('dashboards.insights.notMeasured')
      : t(`projects.onPlan.${cell.key}` as TranslationKey);
  }
  if (!isFigureType(type)) {
    return cell.value !== null && type === 'BOOLEAN'
      ? t(cell.value > 0 ? 'dashboards.designer.yes' : 'dashboards.designer.no')
      : groupWords(column, cell, names, t, language);
  }
  const value = cell.value;
  if (value === null) {
    return t('dashboards.insights.notAvailable');
  }
  switch (type) {
    case 'SAR':
      return sarCompact(String(value), t);
    case 'PERCENT':
      return `${formatPoints(value)}%`;
    case 'POINTS':
      return t('projects.followUp.gapValue', { points: formatPoints(value, true) });
    case 'DAYS':
      return t('dashboards.units.days', { value: formatPoints(value) });
    case 'DATE':
      return formatDayNumber(value, language);
    default:
      return String(value);
  }
}

/** An amount's full figure, for the hover over its compact form; undefined for any other cell. */
export function cellExact(
  type: string | undefined,
  cell: TableCell,
  t: Translate,
): string | undefined {
  return type === 'SAR' && cell.value !== null ? sarExactLabel(String(cell.value), t) : undefined;
}

/** The table's order the viewer chose by a header: a column and its direction; null for the API's own order. */
export interface TableSort {
  column: number;
  direction: SortDirection;
}

/**
 * The rows in the viewer's order (never saved): a figure column by its value, any other by its words in the interface's language; the cells
 * with nothing to sort by go last either way, and rows that tie keep the API's order.
 */
export function sortedRows(
  rows: readonly TableRow[],
  sort: TableSort | null,
  columnTypes: readonly string[],
  textOf: (cell: TableCell, column: number) => string,
  language: Language,
): TableRow[] {
  if (sort === null) {
    return [...rows];
  }
  const { column, direction } = sort;
  const figure = isFigureType(columnTypes[column]);
  const sign = direction === 'ascending' ? 1 : -1;
  const collator = new Intl.Collator(language);
  const missing = (cell: TableCell | undefined) =>
    cell === undefined || (figure ? cell.value === null : cell.key === null && cell.label === null);
  return rows
    .map((row, index) => ({ row, index }))
    .sort((a, b) => {
      const left = a.row.cells[column];
      const right = b.row.cells[column];
      if (missing(left) || missing(right) || left === undefined || right === undefined) {
        return Number(missing(left)) - Number(missing(right)) || a.index - b.index;
      }
      const order = figure
        ? (left.value ?? 0) - (right.value ?? 0)
        : collator.compare(textOf(left, column), textOf(right, column));
      return sign * order || a.index - b.index;
    })
    .map(({ row }) => row);
}
