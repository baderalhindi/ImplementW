import { describe, expect, test } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import { catalogueEntry, catalogueFor, widget } from '@/test/dashboardFixtures.ts';
import { sessionFor } from '@/test/identityAccessFixtures.ts';

import { bucketColors, chartForm } from './charts.ts';
import { drillPath } from './drill.ts';
import { landingEntry, landingRole, mayPersonalize, selectedEntry } from './landing.ts';
import { arrangedWidgets, drawnSpan, filledSpans, visibleWidgets } from './layout.ts';
import { formatFigure, isStatusScale, labelOfValue } from './presentation.ts';

const t = (key: string, params?: Record<string, string | number>) => translate('en', key, params);
const user = (roles: string[]) => sessionFor(roles).user;

describe('the landing (Blueprint §20.2, TBC-DSH-003)', () => {
  test('the landing role is the first role in code order, as the API chooses the landing', () => {
    expect(landingRole(user(['R07', 'R04', 'R03']))).toBe('R03');
    expect(landingRole(user(['R08']))).toBe('R08');
    expect(landingRole(user(['X99']))).toBeNull();
    expect(landingRole(user([]))).toBeNull();
  });

  test('the Home shows what the address asks for only among the dashboards it hosts', () => {
    const entries = catalogueFor(['R02']);
    expect(selectedEntry(entries, null)?.code).toBe('PORTFOLIO');
    expect(selectedEntry(entries, 'GOVERNANCE')?.code).toBe('GOVERNANCE');
    expect(selectedEntry(entries, 'PROJECT')?.code).toBe('PORTFOLIO');
    expect(selectedEntry(entries, 'ANYTHING')?.code).toBe('PORTFOLIO');
    expect(selectedEntry([], null)).toBeNull();
  });

  test('with no landing marked, the first hosted dashboard is shown; a Project landing is kept', () => {
    expect(landingEntry([catalogueEntry('PROJECT'), catalogueEntry('GOVERNANCE')])?.code).toBe(
      'GOVERNANCE',
    );
    expect(landingEntry(catalogueFor(['R08'], true))?.code).toBe('PROJECT');
    expect(landingEntry([catalogueEntry('PROJECT')])).toBeNull();
  });

  test('personalisation is offered on a personalisable dashboard to R02, R03 and R07, never on PROJECT', () => {
    const portfolio = catalogueEntry('PORTFOLIO');
    expect(mayPersonalize(user(['R03']), portfolio)).toBe(true);
    expect(mayPersonalize(user(['R06']), portfolio)).toBe(false);
    expect(mayPersonalize(user(['R02']), catalogueEntry('GOVERNANCE'))).toBe(false);
    expect(
      mayPersonalize(user(['R02']), catalogueEntry('PROJECT', { allowsPersonalization: true })),
    ).toBe(false);
  });
});

describe('the layout (ADR-019 presentation only)', () => {
  const governed = widget({ code: 'G', layoutRow: 1, layoutColumn: 1 });
  const first = widget({ code: 'A', layoutRow: 2, layoutColumn: 1, isOptionalVisibility: true });
  const second = widget({ code: 'B', layoutRow: 2, layoutColumn: 7, isOptionalVisibility: true });

  test('widgets follow the governed grid, row by row', () => {
    expect(arrangedWidgets([second, governed, first]).map((w) => w.code)).toEqual(['G', 'A', 'B']);
  });

  test('a personal order moves optional widgets among their own places; a governed one never moves', () => {
    const reordered = [
      { ...first, personalSortOrder: 2 },
      { ...second, personalSortOrder: 1 },
      { ...governed, layoutRow: 3 },
    ];
    expect(arrangedWidgets(reordered).map((w) => w.code)).toEqual(['B', 'A', 'G']);
  });

  test('a hidden widget is left out of the dashboard', () => {
    expect(
      visibleWidgets([governed, { ...first, isHidden: true }, second]).map((w) => w.code),
    ).toEqual(['G', 'B']);
  });

  test('the figure cards lead and the charts follow, each in the arranged order', () => {
    const chart = (code: string, layoutRow: number) =>
      widget({ code, layoutRow, layoutColumn: 1, widgetType: 'STATUS_DISTRIBUTION' });
    const card = (code: string, layoutRow: number) =>
      widget({ code, layoutRow, layoutColumn: 1, widgetType: 'METRIC_CARD' });
    expect(
      visibleWidgets([chart('C1', 1), card('M1', 2), chart('C2', 3), card('M2', 4)]).map(
        (w) => w.code,
      ),
    ).toEqual(['M1', 'M2', 'C1', 'C2']);
  });

  test('after the figure cards come bar and column charts, then donuts, then trends, each kind side by side', () => {
    const distribution = (count: number) => ({
      state: null,
      figures: [],
      series: [],
      distribution: Array.from({ length: count }, (_, index) => ({
        key: `K${String(index)}`,
        label: null,
        count: 1,
      })),
    });
    const drawn = visibleWidgets([
      widget({ code: 'TREND', layoutRow: 1, widgetType: 'LINE_TREND' }),
      widget({
        code: 'DONUT',
        layoutRow: 2,
        widgetType: 'STATUS_DISTRIBUTION',
        data: distribution(4),
      }),
      widget({
        code: 'LONG',
        layoutRow: 3,
        widgetType: 'STATUS_DISTRIBUTION',
        data: distribution(7),
      }),
      widget({ code: 'RISK', layoutRow: 4, widgetType: 'BAR_COLUMN', data: distribution(4) }),
      widget({ code: 'CARD', layoutRow: 5, widgetType: 'METRIC_CARD', layoutSpan: 6 }),
    ]);
    expect(drawn.map((w) => w.code)).toEqual(['CARD', 'LONG', 'RISK', 'DONUT', 'TREND']);
    expect(drawn.map(drawnSpan)).toEqual([6, 6, 6, 4, 12]);
  });

  test('a row the next widget does not fit shares its spare columns, so the grid has no holes', () => {
    // The Project dashboard's figure cards, then its charts: every row already full.
    expect(filledSpans([3, 3, 3, 3, 4, 4, 4, 6, 6, 12, 6, 6])).toEqual([
      3, 3, 3, 3, 4, 4, 4, 6, 6, 12, 6, 6,
    ]);
    expect(filledSpans([4, 6, 6, 4, 12])).toEqual([5, 7, 7, 5, 12]);
    expect(filledSpans([4, 4, 4, 4, 4])).toEqual([4, 4, 4, 6, 6]);
    expect(filledSpans([3, 3])).toEqual([6, 6]);
    expect(filledSpans([])).toEqual([]);
  });
});

describe('values in words and units', () => {
  test('each unit is formatted from the exact string; a masked or absent figure has no value', () => {
    const figure = (unit: string, value: string | null, isMasked = false) => ({
      measure: 'M',
      unit,
      value,
      isMasked,
    });
    expect(formatFigure(figure('COUNT', '12345'), t)).toBe('12,345');
    expect(formatFigure(figure('PERCENT', '40.25'), t)).toBe('40.25%');
    expect(formatFigure(figure('DAYS', '-3'), t)).toBe(t('dashboards.units.days', { value: '-3' }));
    expect(formatFigure(figure('DAYS', '4'), t)).toBe(t('dashboards.units.days', { value: '+4' }));
    expect(formatFigure(figure('SAR', '1234567.80'), t)).toBe(
      t('dashboards.units.sar', { amount: '1,234,567.80' }),
    );
    expect(formatFigure(figure('SAR', null, true), t)).toBeNull();
    expect(formatFigure(figure('COUNT', null), t)).toBeNull();
    expect(formatFigure(figure('COUNT', '0'), t)).toBe('0');
  });

  test("a value reads in the source's vocabulary; green only for a rating the source gave", () => {
    const label = (projection: string, key: string) => labelOfValue(projection, key, null, 'en', t);
    expect(label('PROGRESS.PROJECT_HEALTH_STATUS', 'GREEN')).toEqual({
      text: t('progress.health.GREEN'),
      tone: 'positive',
    });
    expect(label('PROGRESS.PROJECT_HEALTH_STATUS', 'UNKNOWN').tone).toBe('neutral');
    expect(label('PROGRESS.REPORTING_COMPLETENESS', 'UP_TO_DATE').tone).toBe('info');
    expect(label('PROJECT.LIFECYCLE_STATE', 'ACTIVE').text).toBe(t('projects.status.ACTIVE'));
    expect(label('FINANCIAL_KPI.KPI_CONDITION', 'NOT_PUBLISHED').text).toBe(
      t('dashboards.values.NOT_PUBLISHED'),
    );
    expect(label('DASHBOARDS.DEFINITION_BACKLOG', 'VALIDATED').text).toBe(
      t('dashboards.values.definition.VALIDATED'),
    );
  });

  test("a source's own label wins, and a value with no word keeps its code, uncoloured", () => {
    expect(
      labelOfValue('RISK.RISK_EXPOSURE', 'HIGH', { en: 'High', ar: 'مرتفع' }, 'ar', t),
    ).toEqual({
      text: 'مرتفع',
      tone: 'neutral',
    });
    expect(labelOfValue('RISK.RISK_EXPOSURE', 'EXTREME', null, 'en', t).text).toBe('EXTREME');
    expect(labelOfValue('NEW.PROJECTION', 'X', null, 'en', t)).toEqual({
      text: 'X',
      tone: 'neutral',
    });
  });
});

describe('drill-through (BR-DSH-011)', () => {
  test("a project's widget opens its tab; a portfolio widget its register; anything else no link", () => {
    expect(drillPath('SCR-080', 'p1')).toBe('/projects/p1/risks');
    expect(drillPath('SCR-049', 'p1')).toBe('/projects/p1/financials');
    expect(drillPath('SCR-050', 'p1')).toBe('/projects/p1/kpis');
    expect(drillPath('SCR-040', 'p1')).toBeNull();
    expect(drillPath('SCR-025', null)).toBe('/projects');
    expect(drillPath('SCR-080', null)).toBe('/risks');
    expect(drillPath('SCR-027', null)).toBeNull();
    expect(drillPath(null, null)).toBeNull();
  });
});

describe('the charts a distribution is drawn as', () => {
  test('a few values are a donut, or columns for BAR_COLUMN; more than six are a bar list', () => {
    expect(chartForm('STATUS_DISTRIBUTION', 4)).toBe('donut');
    expect(chartForm('DONUT_PIE', 6)).toBe('donut');
    expect(chartForm('BAR_COLUMN', 5)).toBe('columns');
    expect(chartForm('STATUS_DISTRIBUTION', 7)).toBe('bars');
    expect(chartForm('BAR_COLUMN', 9)).toBe('bars');
  });

  test('a status scale keeps its RAG colours with greys for no data; any other takes categorical fills', () => {
    expect(isStatusScale('PROGRESS.PROJECT_HEALTH_STATUS')).toBe(true);
    expect(isStatusScale('FINANCIAL_KPI.KPI_CONDITION')).toBe(true);
    expect(isStatusScale('PROJECT.LIFECYCLE_STATE')).toBe(false);
    expect(isStatusScale('RISK.RISK_EXPOSURE')).toBe(false);
    expect(isStatusScale('UNREGISTERED.PROJECTION')).toBe(false);

    expect(bucketColors(['positive', 'warning', 'negative', 'neutral', 'neutral'], true)).toEqual([
      'var(--chart-positive)',
      'var(--chart-warning)',
      'var(--chart-negative)',
      'var(--chart-neutral-1)',
      'var(--chart-neutral-2)',
    ]);
    // A lifecycle's states share tones: told apart in order, never by the tone they share.
    expect(bucketColors(['info', 'neutral', 'neutral'], false)).toEqual([
      'var(--chart-category-1)',
      'var(--chart-category-2)',
      'var(--chart-category-3)',
    ]);
    // Past six values one fill: a seventh hue is never generated.
    expect(new Set(bucketColors(Array<'neutral'>(7).fill('neutral'), false))).toEqual(
      new Set(['var(--chart-single)']),
    );
  });
});
