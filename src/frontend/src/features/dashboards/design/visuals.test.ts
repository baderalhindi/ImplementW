import { describe, expect, test } from 'vitest';

import { type Visual } from './designApi.ts';
import { autoTitle, drillPath, fits, freePlace, newVisual, retitled, withType } from './visuals.ts';

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
