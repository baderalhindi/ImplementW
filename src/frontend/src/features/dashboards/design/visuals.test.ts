import { describe, expect, test } from 'vitest';

import { designFieldsFixture, FIELD } from '@/test/portfolioInsightFixtures.ts';

import { DEFAULT_AXES, sentAxes, valueScale } from './axes.ts';
import { type Visual } from './designApi.ts';
import { dateKeyText, formatDayNumber } from './fields.ts';
import {
  autoTitle,
  drillPath,
  fits,
  freePlace,
  gridPlace,
  newVisual,
  overlaps,
  packed,
  retitled,
  rowsOf,
  settled,
  shiftedTogether,
  tenth,
  widenedIntoHidden,
  withoutEmptyRows,
  withType,
} from './visuals.ts';

// The 17 Nov 2026 dashboard designer: the rules the designer applies before the API checks the same ones.

const card: Visual = {
  title: { ar: 'عدد المشاريع', en: 'Projects' },
  visualType: 'CARD',
  measures: ['PROJECTS', 'IN_PROGRESS'],
  groupBy: 'NONE',
  layoutColumn: 0,
  layoutRow: 0,
  layoutWidth: 4,
  layoutHeight: 2,
};

describe('the grid', () => {
  test('a visual fits beside another, not on it, and not past the twelfth column', () => {
    expect(fits({ ...card, layoutColumn: 4 }, [card], null)).toBe(true);
    expect(fits({ ...card, layoutColumn: 3 }, [card], null)).toBe(false);
    expect(fits({ ...card, layoutColumn: 9 }, [], null)).toBe(false);
    // A visual never covers itself while it moves.
    expect(fits({ ...card, layoutColumn: 1 }, [card], 0)).toBe(true);
  });

  test('a new visual takes the first free place, row by row', () => {
    expect(freePlace([card], 4, 2)).toEqual({ column: 4, row: 0 });
    expect(
      freePlace([card, { ...card, layoutColumn: 4 }, { ...card, layoutColumn: 8 }], 6, 5),
    ).toEqual({
      column: 0,
      row: 2,
    });
  });
});

describe('a visual', () => {
  test('changing its type keeps what the new type takes', () => {
    const pie = withType({ ...card, measures: ['AVERAGE_PLANNED', 'PROJECTS'] }, 'PIE');
    expect(pie.measures).toEqual(['PROJECTS']);
    expect(pie.groupBy).toBe('PORTFOLIO');
    expect(withType(pie, 'CARD').groupBy).toBe('NONE');
    expect(withType(card, 'GAUGE').measures).toEqual(['AVERAGE_PLANNED']);
  });

  test('a new one is named in both languages, without display marks', () => {
    const bar = newVisual('BAR', []);
    expect(bar?.title).toEqual({ ar: 'عدد المشاريع حسب المحفظة', en: 'Projects by Portfolio' });
    expect(autoTitle({ measures: ['VALUE_SAR'], groupBy: 'NONE' }, 'en')).toBe('Total value');
  });

  test('its automatic title follows its grouping; a title someone wrote stays', () => {
    const bar = newVisual('BAR', []);
    if (bar === null) {
      throw new Error('The empty grid has room');
    }
    expect(retitled(bar, { ...bar, groupBy: 'SECTOR' }).title.en).toBe('Projects by Sector');
    const named = { ...bar, title: { ...bar.title, en: 'Our portfolios' } };
    const regrouped = retitled(named, { ...named, groupBy: 'SECTOR' });
    expect(regrouped.title.en).toBe('Our portfolios');
    expect(regrouped.title.ar).toBe('عدد المشاريع حسب القطاع');
  });
});

describe('round 8, point 01: a figure opens its records', () => {
  test('with the filters, the group and what the measure counts', () => {
    const filters = { period: '2026' };
    expect(drillPath('PROJECTS', 'SECTOR', 's1', filters)).toBe(
      '/projects?period=2026&registered=true&classificationItemId=s1',
    );
    expect(drillPath('IN_PROGRESS', 'NONE', null, filters)).toBe(
      '/projects?period=2026&registered=true&status=ACTIVE',
    );
    expect(drillPath('NEEDING_FOLLOW_UP', 'PORTFOLIO', 'p1', filters)).toBe(
      '/projects/follow-up?period=2026&strategicPortfolioItemId=p1',
    );
    expect(drillPath('PROJECTS', 'STAGE', null, {})).toBe('/projects?registered=true&stage=NONE');
  });

  test('none where no list can filter by it', () => {
    expect(drillPath('PROJECTS', 'PORTFOLIO', null, {})).toBeUndefined();
    expect(drillPath('OVERDUE_ACTIONS', 'NONE', null, {})).toBeUndefined();
  });
});

describe('no white space (18 Nov 2026)', () => {
  const box = (width: number, height: number) => ({
    ...card,
    layoutWidth: width,
    layoutHeight: height,
  });

  test('the visuals pack in their order, each in the first place it fits, a gap filled by what fits', () => {
    const laidOut = packed([box(6, 4), box(4, 2), box(4, 2), box(2, 2), box(12, 3)]);
    expect(laidOut.map((v) => [v.layoutColumn, v.layoutRow])).toEqual([
      [0, 0],
      [6, 0],
      [6, 2],
      [10, 0],
      [0, 4],
    ]);
  });

  test('a visual made smaller keeps its size when another is sized (19 Nov 2026)', () => {
    // Two side by side; the left one made shorter, then the right one: the left one stays as it was set, never stretched back.
    const shrunk = packed([box(6, 6), box(6, 10)]).map((v, index) =>
      index === 0 ? { ...v, layoutHeight: 4 } : v,
    );
    const both = packed(shrunk.map((v, index) => (index === 1 ? { ...v, layoutHeight: 5 } : v)));
    expect(both.map((v) => [v.layoutWidth, v.layoutHeight])).toEqual([
      [6, 4],
      [6, 5],
    ]);
  });

  test('a visual removed leaves no hole', () => {
    const three = packed([box(12, 2), box(12, 2), box(12, 2)]);
    expect(packed(three.filter((_, index) => index !== 1)).map((v) => v.layoutRow)).toEqual([0, 2]);
  });
});

describe('lines and figures over time (more fields contract)', () => {
  const fields = designFieldsFixture().fields;

  test('a line needs the catalogue: none without it, the published history by period with it', () => {
    expect(newVisual('LINE', [])).toBeNull();
    expect(newVisual('LINE', [], fields)).toMatchObject({
      visualType: 'LINE',
      measures: [],
      groupBy: 'NONE',
      values: [{ field: FIELD.HISTORY_ACTUAL_PERCENT, aggregation: 'AVERAGE' }],
      groupField: FIELD.HISTORY_PERIOD_START,
      groupDateUnit: 'MONTH',
    });
  });

  test('a line over time becomes a column chart with its period, and a card falls back to the Portfolio figures', () => {
    const line = newVisual('LINE', [], fields);
    if (line === null) {
      throw new Error('no line');
    }
    expect(withType(line, 'COLUMN', fields)).toMatchObject({
      visualType: 'COLUMN',
      groupField: FIELD.HISTORY_PERIOD_START,
      groupDateUnit: 'MONTH',
    });
    expect(withType(line, 'CARD', fields)).toMatchObject({
      visualType: 'CARD',
      measures: ['PROJECTS'],
      values: null,
      groupField: null,
      groupDateUnit: null,
    });
  });

  test('day numbers and month keys read as Gregorian dates in both languages', () => {
    expect(formatDayNumber(20515, 'en')).toBe('3 Mar 2026');
    expect(formatDayNumber(20515, 'ar')).toMatch(/2026/);
    expect(dateKeyText('2026-03', 'MONTH', 'en')).toBe('Mar 2026');
    expect(dateKeyText('2026', 'YEAR', 'en')).toBe('2026');
  });
});

describe('axes (19 Nov 2026, axes contract)', () => {
  const column: Visual = { ...card, visualType: 'COLUMN', groupBy: 'PORTFOLIO' };

  test('the value axis runs in round steps: from zero, 0 to 100 for percentages, from the earliest for dates', () => {
    expect(
      valueScale([3, 7], { min: null, max: null }, { percent: false, zero: true }).ticks,
    ).toEqual([0, 2, 4, 6, 8]);
    expect(
      valueScale([55, 30], { min: null, max: null }, { percent: true, zero: true }).ticks,
    ).toEqual([0, 20, 40, 60, 80, 100]);
    const dates = valueScale(
      [20515, 20600],
      { min: null, max: null },
      { percent: false, zero: false },
    );
    expect(dates.low).toBeGreaterThan(20000);
    expect(dates.base).toBe(dates.low);
    // Below zero, the scale reaches the lowest value and a bar starts from zero.
    const signed = valueScale([-3, 9], { min: null, max: null }, { percent: false, zero: true });
    expect(signed.low).toBeLessThan(0);
    expect(signed.base).toBe(0);
  });

  test('a given start and end are kept, and a figure past them sits on the edge', () => {
    const scale = valueScale([150, 20], { min: 10, max: 100 }, { percent: false, zero: true });
    expect([scale.low, scale.high]).toEqual([10, 100]);
    expect(scale.at(150)).toBe(1);
    expect(scale.at(0)).toBe(0);
    expect(scale.base).toBe(10);
  });

  test('null is sent for the defaults and for a type without axes; a half title or a reversed range is not sent', () => {
    expect(sentAxes({ ...column, axes: DEFAULT_AXES })).toBeNull();
    const titled = {
      ...DEFAULT_AXES,
      category: { show: true, title: { ar: ' المحفظة ', en: 'Portfolio ' } },
    };
    expect(sentAxes({ ...column, axes: titled })?.category.title).toEqual({
      ar: 'المحفظة',
      en: 'Portfolio',
    });
    expect(sentAxes({ ...column, visualType: 'PIE', axes: titled })).toBeNull();
    expect(
      sentAxes({
        ...column,
        axes: { ...DEFAULT_AXES, category: { show: true, title: { ar: '', en: 'x' } } },
      }),
    ).toBeNull();
    expect(
      sentAxes({
        ...column,
        axes: { ...DEFAULT_AXES, value: { ...DEFAULT_AXES.value, min: 5, max: 5 } },
      }),
    ).toBeNull();
  });
});

describe('free placement (19 Nov 2026)', () => {
  const at = (column: number, row: number, width = 4, height = 2) => ({
    ...card,
    layoutColumn: column,
    layoutRow: row,
    layoutWidth: width,
    layoutHeight: height,
  });

  test('a visual stays where it is put, and empty room stays', () => {
    const laidOut = settled([at(0, 0), at(8, 6)], 1);
    expect(laidOut.map((v) => [v.layoutColumn, v.layoutRow])).toEqual([
      [0, 0],
      [8, 6],
    ]);
  });

  test('what a dropped visual lands on moves down below it, then what that covers', () => {
    // The third is dropped on the first; the first moves under it, and the second, under the first, moves down after it.
    const laidOut = settled([at(0, 0), at(0, 2), at(0, 1)], 2);
    expect(laidOut.map((v) => v.layoutRow)).toEqual([1, 3, 5]);
  });

  test('the dashboard takes out the rows nothing covers, not the room beside a visual', () => {
    const laidOut = withoutEmptyRows([at(0, 0), at(6, 5)]);
    expect(laidOut.map((v) => [v.layoutColumn, v.layoutRow])).toEqual([
      [0, 0],
      [6, 2],
    ]);
  });
});

describe('several visuals moved together (21 Nov 2026)', () => {
  const at = (title: string, column: number, row: number, width = 4, height = 2) => ({
    ...card,
    title: { ar: title, en: title },
    layoutColumn: column,
    layoutRow: row,
    layoutWidth: width,
    layoutHeight: height,
  });
  const places = (visuals: Visual[]) =>
    Object.fromEntries(visuals.map((v) => [v.title.en, [v.layoutColumn, v.layoutRow]]));

  test('the selected visuals move by the same offset and keep their places to each other; the others stay', () => {
    const design = [at('A', 0, 0), at('B', 6, 0), at('C', 0, 8)];
    expect(places(shiftedTogether(design, [0, 1], 0, 3))).toEqual({
      A: [0, 3],
      B: [6, 3],
      C: [0, 8],
    });
  });

  test('what the group lands on moves down below it', () => {
    // A and B (rows 0–2) are moved down onto C (row 3): C goes under them.
    const design = [at('A', 0, 0), at('B', 6, 0), at('C', 0, 3)];
    expect(places(shiftedTogether(design, [0, 1], 0, 2))).toEqual({
      A: [0, 2],
      B: [6, 2],
      C: [0, 4],
    });
  });

  test('the group stops at the grid edges as one: none of them leaves the grid, and they stay apart', () => {
    const design = [at('A', 1, 1), at('B', 6, 2, 4)];
    expect(places(shiftedTogether(design, [0, 1], 9, -5))).toEqual({ A: [3, 0], B: [8, 1] });
    expect(places(shiftedTogether(design, [0, 1], -9, 0))).toEqual({ A: [0, 1], B: [5, 2] });
  });
});

describe('decimal places and sizes (20 Nov 2026, decimal layout contract)', () => {
  const at = (column: number, row: number, width: number, height: number) => ({
    ...card,
    layoutColumn: column,
    layoutRow: row,
    layoutWidth: width,
    layoutHeight: height,
  });
  const places = (visuals: Visual[]) =>
    visuals.map((v) => [v.layoutColumn, v.layoutRow, v.layoutWidth, v.layoutHeight]);

  test('two visuals 4.5 wide sit side by side without overlapping; touching edges is no overlap', () => {
    const first = at(0, 0, 4.5, 2);
    const second = at(4.5, 0, 4.5, 2);
    expect(overlaps(first, second)).toBe(false);
    expect(fits(second, [first], null)).toBe(true);
    expect(fits(at(4.4, 0, 4.5, 2), [first], null)).toBe(false);
    // Floating point would make 0.7 + 0.1 a hair short of 0.8: compared in tenths, they only touch.
    expect(overlaps(at(0.1, 0, 0.7, 1), at(0.8, 0, 1, 1))).toBe(false);
    expect(fits(at(7.5, 0, 4.5, 2), [], null)).toBe(true);
    expect(fits(at(7.6, 0, 4.5, 2), [], null)).toBe(false);
  });

  test('a new visual goes right beside one of a decimal size, and under one of a decimal height', () => {
    expect(freePlace([at(0, 0, 4.5, 2)], 4.5, 2)).toEqual({ column: 4.5, row: 0 });
    expect(freePlace([at(0, 0, 12, 2.7)], 4, 2)).toEqual({ column: 0, row: 2.7 });
    expect(rowsOf([at(0, 0, 12, 2.7), at(0, 2.7, 4, 1.6)])).toBe(4.3);
  });

  test('what a dropped visual lands on moves to just below its decimal bottom, rounded to a tenth', () => {
    // The anchor's bottom, 0.1 + 0.7, is 0.7999999999999999 in floating point: what it covers moves to 0.8.
    const laidOut = settled([at(0, 0, 4, 2), at(0, 0.1, 4.7, 0.7)], 1);
    expect(places(laidOut)).toEqual([
      [0, 0.1, 4.7, 0.7],
      [0, 0.8, 4, 2],
    ]);
  });

  test('every computed value is rounded to one decimal place', () => {
    expect(tenth(0.1 + 0.2)).toBe(0.3);
    expect(tenth(4.699999)).toBe(4.7);
    expect(tenth(4.75)).toBe(4.8);
    expect(packed([at(0.33, 0, 4.66, 2.04)]).map((v) => [v.layoutWidth, v.layoutHeight])).toEqual([
      [4.7, 2],
    ]);
    expect(gridPlace(at(4.5, 0.2 + 0.1, 4.7, 2.7))).toEqual({
      '--visual-column': '5.5',
      '--visual-row': '1.3',
      '--visual-width': '4.7',
      '--visual-height': '2.7',
    });
  });

  test('the dashboard takes out an empty band of any height, and keeps the decimal places beside it', () => {
    // Rows 2.5 to 4.2 are empty: 1.7 rows; the band between the third and the fourth is 0.3.
    const laidOut = withoutEmptyRows([
      at(0, 0, 4.5, 2.5),
      at(4.5, 0.5, 4, 1.5),
      at(0, 4.2, 6, 2),
      at(0, 6.5, 4, 1),
    ]);
    expect(places(laidOut)).toEqual([
      [0, 0, 4.5, 2.5],
      [4.5, 0.5, 4, 1.5],
      [0, 2.5, 6, 2],
      [0, 4.5, 4, 1],
    ]);
  });
});

describe('a visual the caller may not see leaves no room in its row (28 Nov 2026)', () => {
  const at = (column: number, row: number, width = 4, height = 2) => ({
    ...card,
    layoutColumn: column,
    layoutRow: row,
    layoutWidth: width,
    layoutHeight: height,
  });
  const places = (visuals: Visual[]) =>
    visuals.map((v) => [v.layoutColumn, v.layoutRow, v.layoutWidth, v.layoutHeight]);

  test('the two cards left of three share the row', () => {
    // The Viewer may not see the Total value card between Registered projects and Average planned.
    expect(places(widenedIntoHidden([at(0, 0), at(8, 0)], [at(4, 0)]))).toEqual([
      [0, 0, 6, 2],
      [6, 0, 6, 2],
    ]);
  });

  test('a chart left alone takes the whole row, and the next row keeps its place', () => {
    expect(
      places(
        widenedIntoHidden([at(0, 9, 6, 5), at(0, 14, 6, 5), at(6, 14, 6, 5)], [at(6, 9, 6, 5)]),
      ),
    ).toEqual([
      [0, 9, 12, 5],
      [0, 14, 6, 5],
      [6, 14, 6, 5],
    ]);
  });

  test('the visuals left keep their proportions, to a tenth, across the span the row had', () => {
    expect(places(widenedIntoHidden([at(0, 0, 2), at(5, 0, 4)], [at(2, 0, 3)]))).toEqual([
      [0, 0, 3, 2],
      [3, 0, 6, 2],
    ]);
  });

  test('a row with nothing left out keeps the room the designer left', () => {
    expect(places(widenedIntoHidden([at(0, 0), at(8, 0)], [at(0, 4)]))).toEqual([
      [0, 0, 4, 2],
      [8, 0, 4, 2],
    ]);
  });

  test('a row a taller visual crosses stays as placed', () => {
    expect(places(widenedIntoHidden([at(0, 0), at(8, 0, 4, 4)], [at(4, 0)]))).toEqual([
      [0, 0, 4, 2],
      [8, 0, 4, 4],
    ]);
  });
});
