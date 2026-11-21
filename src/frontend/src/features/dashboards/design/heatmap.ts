import { type Language, translate } from '@/shared/i18n/i18n.ts';

import {
  type FieldDescriptor,
  type FieldValue,
  type Visual,
  type VisualGroup,
} from './designApi.ts';
import { fieldName, fieldValueText, isDateType, valuesFor } from './fields.ts';
import { RISK_COUNT } from './riskFields.ts';

// The heatmap (22 Nov 2026, "a heatmap matrix visual for cases like risks"): one figure per pair of two fields' values, rows by `groupField`,
// columns by `columnField`, each cell coloured by its figure. The API sends one group per cell, every pair of the two fields' values, in their
// order (PortfolioDesignService.Cells); the designer only lays them out. Its rules are the API's: one field value, two different status, yes/no or
// item fields, no project grouping.

/** A new heatmap counts risks by probability and impact: the risk matrix. */
export const RISK_HEATMAP_ROWS = 'RISK.PROBABILITY';
export const RISK_HEATMAP_COLUMNS = 'RISK.IMPACT';

/** Whether a field may be a heatmap's rows or columns: a status, yes/no or item field of a project or a risk, never a date or a history. */
export function heatmapAxisFits(field: FieldDescriptor): boolean {
  return (
    field.grain === 'PROJECT' &&
    !isDateType(field.type) &&
    (field.type === 'CODE' || field.type === 'BOOLEAN' || field.type === 'ITEM')
  );
}

/** The fields a heatmap's rows or columns may take, in the catalogue's order. */
export function heatmapAxes(fields: readonly FieldDescriptor[]): FieldDescriptor[] {
  return fields.filter(heatmapAxisFits);
}

/** "Number of risks by Probability and Impact": the title a heatmap takes from its value and its two fields until someone writes another. */
export function heatmapTitle(
  visual: Pick<Visual, 'values' | 'groupField' | 'columnField'>,
  language: Language,
): string {
  const [value] = visual.values ?? [];
  const measure =
    value === undefined
      ? translate(language, 'dashboards.designer.measure.PROJECTS')
      : fieldValueText(value, language);
  return translate(language, 'dashboards.designer.titleByTwo', {
    measure,
    rows: visual.groupField ? fieldName(visual.groupField, language) : '…',
    columns: visual.columnField ? fieldName(visual.columnField, language) : '…',
  }).replace(/[⁨⁩]/g, '');
}

/** A new heatmap at `place`: the number of risks by probability (rows) and impact (columns). */
export function heatmapVisual(
  place: { column: number; row: number },
  width: number,
  height: number,
): Visual {
  const visual: Visual = {
    title: { ar: '', en: '' },
    visualType: 'HEATMAP',
    source: 'RISKS',
    measures: [],
    values: [RISK_COUNT],
    groupBy: 'NONE',
    groupField: RISK_HEATMAP_ROWS,
    columnField: RISK_HEATMAP_COLUMNS,
    groupDateUnit: null,
    layoutColumn: place.column,
    layoutRow: place.row,
    layoutWidth: width,
    layoutHeight: height,
  };
  return { ...visual, title: { ar: heatmapTitle(visual, 'ar'), en: heatmapTitle(visual, 'en') } };
}

/**
 * A visual changed to a heatmap: its first field value that a heatmap draws (else a count of the first axis field), its rows kept when they fit,
 * else the first axis field, and the next one as its columns. Null when the catalogue offers fewer than two axis fields.
 */
export function asHeatmap(visual: Visual, fields: readonly FieldDescriptor[]): Visual | null {
  const axes = heatmapAxes(fields);
  const keptRows = axes.find((field) => field.key === visual.groupField);
  const rows = keptRows ?? axes[0];
  const columns =
    axes.find((field) => field.key === visual.columnField && field.key !== rows?.key) ??
    axes.find((field) => field.key !== rows?.key);
  if (rows === undefined || columns === undefined) {
    return null;
  }
  const kept = valuesFor(visual.values ?? [], 'HEATMAP', fields).filter(
    (value) => fields.find((field) => field.key === value.field)?.grain !== 'SNAPSHOT',
  );
  const counted: FieldValue = { field: rows.key, aggregation: 'COUNT' };
  return {
    ...visual,
    visualType: 'HEATMAP',
    measures: [],
    values: kept.length > 0 ? kept.slice(0, 1) : [visual.source === 'RISKS' ? RISK_COUNT : counted],
    groupBy: 'NONE',
    groupField: rows.key,
    columnField: columns.key,
    groupDateUnit: null,
    axes: null,
  };
}

/** The matrix the API's cells make: the rows and the columns in the order they come, and each cell by its row and column. */
export interface HeatmapMatrix {
  rows: VisualGroup[];
  columns: VisualGroup[];
  cell: (rowKey: string | null, columnKey: string | null) => VisualGroup | undefined;
  /** The largest figure, for the colours; 0 when every figure is 0 or not available. */
  max: number;
}

const keyOf = (rowKey: string | null, columnKey: string | null) =>
  `${rowKey ?? '\u0000'}|${columnKey ?? '\u0000'}`;

export function heatmapMatrix(groups: readonly VisualGroup[]): HeatmapMatrix {
  const rows = new Map<string | null, VisualGroup>();
  const columns = new Map<string | null, VisualGroup>();
  const cells = new Map<string, VisualGroup>();
  let max = 0;
  for (const group of groups) {
    const columnKey = group.columnKey ?? null;
    if (!rows.has(group.key)) {
      rows.set(group.key, group);
    }
    if (!columns.has(columnKey)) {
      // A column's words are the cell's column words, read as a group's are.
      columns.set(columnKey, { ...group, key: columnKey, label: group.columnLabel ?? null });
    }
    cells.set(keyOf(group.key, columnKey), group);
    const value = group.values[0];
    if (typeof value === 'number' && value > max) {
      max = value;
    }
  }
  return {
    rows: [...rows.values()],
    columns: [...columns.values()],
    cell: (rowKey, columnKey) => cells.get(keyOf(rowKey, columnKey)),
    max,
  };
}

/** A cell's colour strength, 0 to 1: its figure over the largest (an empty or unavailable cell 0). */
export function heatStrength(value: number | null | undefined, max: number): number {
  return typeof value === 'number' && max > 0 ? Math.max(0, Math.min(1, value / max)) : 0;
}
