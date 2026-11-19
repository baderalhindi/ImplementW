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
  moved,
  newVisual,
  packed,
  retitled,
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

  test('moving a visual to another place in the order moves the others along at once', () => {
    const laidOut = packed(moved([box(4, 2), box(4, 2), box(4, 2)], 0, 2));
    expect(laidOut.map((v) => v.layoutColumn)).toEqual([0, 4, 8]);
    expect(moved(['a', 'b', 'c'], 0, 2)).toEqual(['b', 'c', 'a']);
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
