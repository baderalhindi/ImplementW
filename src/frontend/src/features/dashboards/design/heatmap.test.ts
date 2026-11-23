import { describe, expect, test } from 'vitest';

import { type FieldDescriptor, type Visual, type VisualGroup } from './designApi.ts';
import { normalized } from './fields.ts';
import {
  asHeatmap,
  heatmapLabel,
  heatmapMatrix,
  heatmapVisual,
  heatStrength,
  withHeatmapLabel,
} from './heatmap.ts';
import { newVisual, withType } from './visuals.ts';

// The 22 Nov 2026 heatmap: one figure per pair of two fields' values; the API sends a cell per pair, the designer lays them out.

const field = (
  key: string,
  type: FieldDescriptor['type'],
  grain: FieldDescriptor['grain'] = 'PROJECT',
): FieldDescriptor =>
  ({
    key,
    projectionCode: key.split('.')[0] ?? '',
    fieldCode: key.split('.')[1] ?? '',
    type,
    grain,
    aggregations: ['COUNT'],
    values: [],
  }) as unknown as FieldDescriptor;

const RISK_FIELDS = [
  field('RISK.RISK', 'COUNT'),
  field('RISK.IDENTIFIED_DATE', 'DATE'),
  field('RISK.PROBABILITY', 'CODE'),
  field('RISK.IMPACT', 'CODE'),
  field('RISK.RATING', 'CODE'),
];

const cell = (key: string | null, columnKey: string | null, value: number): VisualGroup => ({
  key,
  label: null,
  projects: value,
  measured: value,
  valueEntered: value,
  values: [value],
  onPlan: null,
  columnKey,
  columnLabel: null,
});

describe('the heatmap', () => {
  test('a new one is the risk matrix: the number of risks by probability and impact', () => {
    const visual = newVisual('HEATMAP', []);

    expect(visual).toMatchObject({
      visualType: 'HEATMAP',
      source: 'RISKS',
      values: [{ field: 'RISK.RISK', aggregation: 'COUNT' }],
      groupBy: 'NONE',
      groupField: 'RISK.PROBABILITY',
      columnField: 'RISK.IMPACT',
      layoutWidth: 6,
      layoutHeight: 6,
    });
    expect(visual?.title.en).toBe('Number of risks by Probability and Impact');
  });

  test('its cells make a matrix in the order they come, with the largest figure for the colours', () => {
    const matrix = heatmapMatrix([
      cell('1', '1', 0),
      cell('1', '2', 3),
      cell('2', '1', 1),
      cell('2', '2', 6),
    ]);

    expect(matrix.rows.map((row) => row.key)).toEqual(['1', '2']);
    expect(matrix.columns.map((column) => column.key)).toEqual(['1', '2']);
    expect(matrix.cell('2', '2')?.values[0]).toBe(6);
    expect(matrix.max).toBe(6);
    expect([
      heatStrength(6, 6),
      heatStrength(3, 6),
      heatStrength(0, 6),
      heatStrength(null, 6),
    ]).toEqual([1, 0.5, 0, 0]);
  });

  test('another visual becomes one on its two first status fields, never a date, and the columns go when it becomes another type again', () => {
    const bar: Visual = {
      title: { ar: '', en: '' },
      visualType: 'BAR',
      source: 'RISKS',
      measures: [],
      values: [{ field: 'RISK.RISK', aggregation: 'COUNT' }],
      groupBy: 'NONE',
      groupField: 'RISK.RATING',
      layoutColumn: 0,
      layoutRow: 0,
      layoutWidth: 6,
      layoutHeight: 5,
    };

    const heatmap = asHeatmap(bar, RISK_FIELDS);
    expect(heatmap).toMatchObject({
      groupField: 'RISK.RATING',
      columnField: 'RISK.PROBABILITY',
      groupBy: 'NONE',
    });
    expect(asHeatmap(bar, [field('RISK.RISK', 'COUNT'), field('RISK.RATING', 'CODE')])).toBeNull();

    const back = withType(heatmap ?? bar, 'BAR', RISK_FIELDS);
    expect(back.columnField).toBeNull();
    expect('columnField' in normalized(back)).toBe(false);
    expect(normalized(heatmapVisual({ column: 0, row: 0 }, 6, 6)).columnField).toBe('RISK.IMPACT');
  });

  test('the levels read as the guide names them until the visual names them itself; the names go only with a heatmap', () => {
    const visual = heatmapVisual({ column: 0, row: 0 }, 6, 6);

    expect(heatmapLabel(visual, 'rows', '5')).toEqual({ ar: 'مُرجّح', en: 'Almost certain' });
    expect(heatmapLabel(visual, 'columns', '1')).toEqual({ ar: 'ضئيل', en: 'Negligible' });
    expect(heatmapLabel(visual, 'rows', null)).toBeNull();

    const named = withHeatmapLabel(visual, 'rows', '5', { ar: 'شبه مؤكد', en: 'Near certain' });
    expect(heatmapLabel(named, 'rows', '5')).toEqual({ ar: 'شبه مؤكد', en: 'Near certain' });
    expect(heatmapLabel(named, 'rows', '4')).toEqual({ ar: 'مُحتمل', en: 'Likely' });
    expect(normalized(named).labels?.rows).toEqual([
      { key: '5', label: { ar: 'شبه مؤكد', en: 'Near certain' } },
    ]);
    expect('labels' in normalized({ ...named, visualType: 'BAR' })).toBe(false);
  });
});
