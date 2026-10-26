import { describe, expect, test } from 'vitest';

import { todayUtc } from '@/features/tasks/taskRules.ts';
import {
  financialPosition,
  financialUpdate,
  measurement,
  measurements,
  monthBefore,
} from '@/test/financialKpiFixtures.ts';

import {
  checkFinancialUpdate,
  checkKpiValue,
  checkKpiValueForm,
  checkSar,
  currentMeasurement,
  figureOf,
  type FinancialUpdateValues,
  latestPublished,
  monthOf,
  targetSegments,
  toFinancialRequest,
  toMoney,
  trendPoints,
} from './financialKpiRules.ts';

// The WF-14 rules the screens apply (TASK-053): a figure is shown only when MEASURED, a withheld one as restricted;
// the trend draws each value against its own pinned target; the forms send no figure for an Unknown status.

const TODAY = '2026-10-05';

describe('acceptance criterion 1: a figure is shown only when it is known', () => {
  test('MISSING, STALE and NOT_APPLICABLE are their state, never a value, even if a figure came with them', () => {
    for (const status of ['MISSING', 'STALE', 'NOT_APPLICABLE'] as const) {
      const update = financialUpdate({ valueStatus: status, actualExpenditureToDateSar: '0.00' });
      expect(figureOf(update, 'actualExpenditureToDateSar', status)).toEqual({
        kind: 'unknown',
        status,
      });
    }
  });

  test('a measured figure is its value, "0.00" included when the API measured it so', () => {
    const update = financialUpdate({ valueStatus: 'MEASURED', actualExpenditureToDateSar: '0.00' });
    expect(figureOf(update, 'actualExpenditureToDateSar', 'MEASURED')).toEqual({
      kind: 'measured',
      value: '0.00',
    });
  });

  test('a withheld figure is restricted, not unknown and not absent (ADR-010)', () => {
    const position = financialPosition({
      valueStatus: 'MISSING',
      maskedFields: ['approvedBudgetSar'],
    });
    delete position.approvedBudgetSar;
    expect(figureOf(position, 'approvedBudgetSar', null)).toEqual({ kind: 'restricted' });
    expect(
      figureOf(
        { ...position, maskedFields: ['actualExpenditureToDateSar'] },
        'actualExpenditureToDateSar',
        'MISSING',
      ),
    ).toEqual({
      kind: 'restricted',
    });
  });

  test('no budget and no forecast beside a measured actual are absent', () => {
    const position = financialPosition({
      approvedBudgetSar: null,
      valueStatus: 'MEASURED',
      actualExpenditureToDateSar: '10.00',
    });
    expect(figureOf(position, 'approvedBudgetSar', null)).toEqual({ kind: 'absent' });
    expect(figureOf(position, 'forecastAtCompletionSar', 'MEASURED')).toEqual({ kind: 'absent' });
  });
});

describe('the current period and the trend', () => {
  test('a KPI with nothing recorded for the month today falls in has no current measurement', () => {
    expect(currentMeasurement(measurements(), todayUtc())).toBeNull();
    const thisMonth = monthBefore(0);
    const current = measurement({
      id: 'now',
      periodStart: thisMonth.start,
      periodEnd: thisMonth.end,
    });
    expect(currentMeasurement([...measurements(), current], thisMonth.end)?.id).toBe('now');
  });

  test('the latest published value is the latest period AHDA published, not a submitted one', () => {
    expect(latestPublished(measurements())?.periodStart).toBe(monthBefore(2).start);
  });

  test('acceptance criterion 2: each point keeps the target version it was pinned to, earliest period first', () => {
    const points = trendPoints(measurements());
    expect(points.map((point) => [point.targetVersionNo, point.targetValue, point.value])).toEqual([
      [1, 95, 92],
      [1, 95, null],
      [2, 80, 92],
    ]);
    expect(targetSegments(points)).toEqual([
      { targetVersionNo: 1, targetValue: 95, first: 0, last: 1 },
      { targetVersionNo: 2, targetValue: 80, first: 2, last: 2 },
    ]);
  });

  test('a value recorded late under a newer target makes its own step, between two of the older one', () => {
    const points = trendPoints([
      measurement({ id: 'a', periodStart: '2026-07-01', periodEnd: '2026-07-31' }),
      measurement({
        id: 'b',
        periodStart: '2026-08-01',
        periodEnd: '2026-08-31',
        targetVersionNo: 2,
        targetValue: 80,
      }),
      measurement({ id: 'c', periodStart: '2026-09-01', periodEnd: '2026-09-30' }),
    ]);
    expect(targetSegments(points).map((segment) => segment.targetVersionNo)).toEqual([1, 2, 1]);
  });

  test('a month is its first and last day, December and a leap February included', () => {
    expect(monthOf('2026-12-15')).toEqual({ start: '2026-12-01', end: '2026-12-31' });
    expect(monthOf('2028-02-10')).toEqual({ start: '2028-02-01', end: '2028-02-29' });
  });
});

describe('MOD-023 and MOD-024 send no figure for an Unknown status', () => {
  const values = (overrides: Partial<FinancialUpdateValues> = {}): FinancialUpdateValues => ({
    valueStatus: 'MEASURED',
    actual: '1250000',
    forecast: '',
    sourceReference: '',
    asOfDate: TODAY,
    narrative: '',
    ...overrides,
  });

  test('SAR amounts: digits with up to two places, no sign and no grouping; written with two places', () => {
    expect(checkSar('1250000', true)).toBeNull();
    expect(checkSar('1250000.5', true)).toBeNull();
    expect(checkSar('', true)).toBe('REQUIRED');
    expect(checkSar('', false)).toBeNull();
    for (const bad of ['1,250', '-5', '1.234', 'abc']) {
      expect(checkSar(bad, true)).toBe('MALFORMED');
    }
    expect(toMoney('1250000')).toBe('1250000.00');
    expect(toMoney('12.5')).toBe('12.50');
  });

  test('a measured update needs its actual; any other status ignores the amounts and sends null', () => {
    expect(checkFinancialUpdate(values({ actual: '' }), TODAY).actualExpenditureToDateSar).toBe(
      'REQUIRED',
    );
    const missing = values({ valueStatus: 'MISSING', actual: '0', forecast: '5' });
    expect(Object.values(checkFinancialUpdate(missing, TODAY)).filter(Boolean)).toEqual([]);
    const request = toFinancialRequest(missing, 'en', financialUpdate());
    expect(request.actualExpenditureToDateSar).toBeNull();
    expect(request.forecastAtCompletionSar).toBeNull();
    expect(request.valueStatus).toBe('MISSING');
  });

  test('an as-of date after today and no status chosen are refused before sending', () => {
    const codes = checkFinancialUpdate(values({ valueStatus: '', asOfDate: '2026-10-06' }), TODAY);
    expect(codes.valueStatus).toBe('REQUIRED');
    expect(codes.asOfDate).toBe('DATE_IN_FUTURE');
  });

  test('a KPI value: a number to four places; a new one names a period that does not end before it starts', () => {
    expect(checkKpiValue('92.1234')).toBeNull();
    expect(checkKpiValue('92.12345')).toBe('OUT_OF_RANGE');
    expect(checkKpiValue('ninety')).toBe('MALFORMED');
    const codes = checkKpiValueForm(
      {
        periodStart: '2026-10-31',
        periodEnd: '2026-10-01',
        valueStatus: 'NOT_APPLICABLE',
        value: '',
        asOfDate: TODAY,
        narrative: '',
      },
      TODAY,
      true,
    );
    expect(codes.periodEnd).toBe('BEFORE_START');
    expect(codes.measuredValue).toBeNull();
  });
});
