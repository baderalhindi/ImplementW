import { describe, expect, test } from 'vitest';

import { designFieldsFixture } from '@/test/portfolioInsightFixtures.ts';

import { type Visual, type VisualType } from './designApi.ts';
import { filterDimensionsFor } from './fields.ts';
import {
  groupBysOf,
  RECORD_SOURCES,
  sourceOf,
  takesSource,
  typesFor,
  withSource,
} from './recordSources.ts';
import { RISK_COUNT, RISK_PROJECT_DIMENSIONS, RISK_TABLE_COLUMNS } from './riskFields.ts';
import {
  firstColumnsFor,
  NEW_TABLE_COLUMNS,
  tableDimensionsFor,
  tableMeasuresFor,
} from './tableColumns.ts';
import { newVisual, VISUAL_TYPES } from './visuals.ts';

// What a visual counts (20 Nov 2026, docs/architecture/dashboard-designer-record-sources.md): the rules of the Source select.

const FIELDS = designFieldsFixture().fields;

function placed(type: VisualType): Visual {
  const visual = newVisual(type, [], FIELDS);
  if (visual === null) {
    throw new Error(`No ${type} could be placed`);
  }
  return visual;
}

const ALLOWED: VisualType[] = ['CARD', 'BAR', 'COLUMN', 'PIE', 'DONUT', 'LINE', 'TABLE'];
const PROJECTS_ONLY: VisualType[] = ['GAUGE', 'MAP', 'TEXT', 'CONTAINER', 'SECTION'];

describe('the record sources', () => {
  test('Projects, then Risks; a visual with none counts projects', () => {
    expect(RECORD_SOURCES).toEqual(['PROJECTS', 'RISKS']);
    expect(sourceOf({})).toBe('PROJECTS');
    expect(sourceOf({ source: null })).toBe('PROJECTS');
    expect(sourceOf({ source: 'RISKS' })).toBe('RISKS');
  });

  test.each(ALLOWED)('a %s takes a source', (type) => {
    expect(takesSource(type)).toBe(true);
  });

  test.each(PROJECTS_ONLY)('a %s counts projects only', (type) => {
    expect(takesSource(type)).toBe(false);
  });

  test('on Risks a visual may change to the types that take a source only', () => {
    expect(typesFor('PROJECTS')).toEqual(VISUAL_TYPES);
    expect([...typesFor('RISKS')].sort()).toEqual([...ALLOWED].sort());
  });

  test('on Risks a chart groups by a project attribute but not by stage or on plan', () => {
    const projects = groupBysOf('BAR', 'PROJECTS');
    const risks = groupBysOf('BAR', 'RISKS');
    expect(projects).toContain('STAGE');
    expect(projects).toContain('ON_PLAN');
    expect(risks).not.toContain('STAGE');
    expect(risks).not.toContain('ON_PLAN');
    expect(risks).toContain('SECTOR');
    expect(risks).toContain('STATUS');
    expect(risks.every((groupBy) => projects.includes(groupBy))).toBe(true);
  });
});

describe('a visual switched to Risks', () => {
  test('a card counts the risks, grouped by nothing', () => {
    expect(withSource(placed('CARD'), 'RISKS')).toMatchObject({
      source: 'RISKS',
      measures: [],
      values: [RISK_COUNT],
      groupBy: 'NONE',
      groupField: null,
      filters: null,
    });
  });

  test.each(['BAR', 'COLUMN', 'PIE', 'DONUT'] as const)(
    'a %s counts the risks by rating',
    (type) => {
      expect(withSource(placed(type), 'RISKS')).toMatchObject({
        source: 'RISKS',
        measures: [],
        values: [RISK_COUNT],
        groupBy: 'NONE',
        groupField: 'RISK.RATING',
        groupDateUnit: null,
      });
    },
  );

  test('a line counts the risks by the month they were identified', () => {
    expect(withSource(placed('LINE'), 'RISKS')).toMatchObject({
      source: 'RISKS',
      values: [RISK_COUNT],
      groupField: 'RISK.IDENTIFIED_DATE',
      groupDateUnit: 'MONTH',
    });
  });

  test('a table lists the risks, with its totals', () => {
    const table = withSource(placed('TABLE'), 'RISKS');
    expect(table).toMatchObject({
      source: 'RISKS',
      measures: [],
      groupBy: 'NONE',
      showTotals: true,
    });
    expect(table.columns).toEqual(RISK_TABLE_COLUMNS);
    expect(table.values ?? null).toBeNull();
  });

  test('keeps its place, size and titles; drops the filters that tested projects', () => {
    const card: Visual = {
      ...placed('BAR'),
      title: { ar: 'عنوان', en: 'My title' },
      layoutColumn: 3,
      layoutRow: 7,
      layoutWidth: 5,
      layoutHeight: 4,
      filters: [{ field: 'PROJECT.STATUS', values: ['ACTIVE'] }],
    };
    const risks = withSource(card, 'RISKS');
    expect(risks).toMatchObject({
      title: card.title,
      layoutColumn: 3,
      layoutRow: 7,
      layoutWidth: 5,
      layoutHeight: 4,
      filters: null,
    });
  });

  test('a visual already on the source, or a type that takes none, is returned unchanged', () => {
    const risks = withSource(placed('CARD'), 'RISKS');
    expect(withSource(risks, 'RISKS')).toBe(risks);
    const gauge = placed('GAUGE');
    expect(withSource(gauge, 'RISKS')).toBe(gauge);
    const card = placed('CARD');
    expect(withSource(card, 'PROJECTS')).toBe(card);
  });
});

describe('a visual switched back to Projects', () => {
  test.each(['CARD', 'BAR', 'COLUMN', 'PIE', 'DONUT', 'TABLE'] as const)(
    'a %s takes the gallery’s figures again, with no source',
    (type) => {
      const fresh = placed(type);
      const back = withSource(withSource(fresh, 'RISKS'), 'PROJECTS', FIELDS);
      expect(back.source ?? null).toBeNull();
      expect(back.measures).toEqual(fresh.measures);
      expect(back.groupBy).toBe(fresh.groupBy);
      expect(back.values ?? null).toEqual(fresh.values ?? null);
      expect(back.groupField ?? null).toEqual(fresh.groupField ?? null);
      if (type === 'TABLE') {
        expect(back.columns).toEqual(NEW_TABLE_COLUMNS);
      }
    },
  );

  test('a line takes its first value from the projects’ catalogue again', () => {
    const fresh = placed('LINE');
    const back = withSource(withSource(fresh, 'RISKS'), 'PROJECTS', FIELDS);
    expect(back.source ?? null).toBeNull();
    expect(back.values).toEqual(fresh.values);
    expect(back.groupField).toBe(fresh.groupField);
  });
});

describe('a table and a filter on each source', () => {
  test('a table on Risks starts with the risk list, offers the risk’s project details and no Portfolio figures', () => {
    expect(firstColumnsFor('PROJECTS')).toEqual(NEW_TABLE_COLUMNS);
    expect(firstColumnsFor('RISKS')).toEqual(RISK_TABLE_COLUMNS);
    expect(tableMeasuresFor('PROJECTS').length).toBeGreaterThan(0);
    expect(tableMeasuresFor('RISKS')).toEqual([]);
    expect(tableDimensionsFor('RISKS')).toEqual(RISK_PROJECT_DIMENSIONS);
    expect(tableDimensionsFor('PROJECTS')).not.toEqual(RISK_PROJECT_DIMENSIONS);
  });

  test('a filter on Risks tests the project attributes a risk has through its project only', () => {
    const projects = filterDimensionsFor('PROJECTS');
    const risks = filterDimensionsFor('RISKS');
    expect(risks.length).toBeGreaterThan(0);
    expect(risks.every((dimension) => projects.includes(dimension))).toBe(true);
    expect(
      risks.every((dimension) =>
        (RISK_PROJECT_DIMENSIONS as readonly string[]).includes(dimension),
      ),
    ).toBe(true);
    expect(risks).not.toContain('ON_PLAN');
  });
});
