import { describe, expect, it } from 'vitest';

import { checkStagePlan, rowsOf, toEntries } from './stagePlan.ts';
import { type StagePlanStage } from './stagePlanApi.ts';
import { currentStage, oneDecimal, plannedPercent, weightedOverall } from './stageProgress.ts';

const WEIGHTS: [string, number][] = [
  ['BUSINESS_CASE', 5],
  ['DESIGN', 15],
  ['TENDERING', 10],
  ['EXECUTION', 55],
  ['TESTING', 10],
  ['HANDOVER', 5],
];

function stages(): StagePlanStage[] {
  return WEIGHTS.map(([code, weight], index) => ({
    stageCode: code,
    name: { en: code, ar: code },
    sortOrder: index + 1,
    weightPercent: weight,
    applies: true,
    plannedStartDate: null,
    plannedEndDate: null,
    startingPercent: null,
  }));
}

describe('stage-weighted progress (review round 7, item 1)', () => {
  it('weighs 100/100/100/40/0/0 to 52.0%, and to 43.5% when design does not apply', () => {
    const percents = [100, 100, 100, 40, 0, 0];
    const all = WEIGHTS.map(([, weight], i) => ({ weight, percent: percents[i] ?? 0 }));
    expect(oneDecimal(weightedOverall(all) ?? NaN)).toBe('52.0');
    expect(oneDecimal(weightedOverall(all.filter((_, i) => i !== 1)) ?? NaN)).toBe('43.5');
  });

  it('plans a stage at 0 before its start, 100 from its end, and linearly between', () => {
    expect(plannedPercent('2026-01-11', '2026-01-21', '2026-01-10')).toBe(0);
    expect(plannedPercent('2026-01-11', '2026-01-21', '2026-01-16')).toBe(50);
    expect(plannedPercent('2026-01-11', '2026-01-21', '2026-01-21')).toBe(100);
  });

  it('takes the first applicable stage below 100% as current, the last when all are done', () => {
    expect(currentStage([100, 100, 40, 0], (p) => p)).toBe(40);
    expect(currentStage([100, 100], (p) => p)).toBe(100);
    expect(currentStage([], (p: number) => p)).toBeNull();
  });
});

describe('the stage plan as the form checks it', () => {
  const bounds = {
    projectStart: '2027-01-01',
    projectEnd: '2027-12-31',
    requireDates: true,
    acceptsStartingPercent: false,
  };

  it('needs dates for each stage that applies, within the project, and one stage applying', () => {
    const rows = rowsOf(stages()).map((row, i) =>
      i === 0
        ? { ...row, plannedStartDate: '2026-12-31', plannedEndDate: '2027-02-01' }
        : i === 1
          ? { ...row, plannedStartDate: '2027-03-01', plannedEndDate: '2028-01-01' }
          : row,
    );
    const codes = checkStagePlan(rows, bounds);
    expect(codes['stages[0].plannedStartDate']).toBe('DATE_BEFORE_START');
    expect(codes['stages[1].plannedEndDate']).toBe('OUTSIDE_RANGE');
    expect(codes['stages[2].plannedStartDate']).toBe('REQUIRED');
    expect(
      checkStagePlan(
        rows.map((row) => ({ ...row, applies: false })),
        bounds,
      ).stages,
    ).toBe('NOT_ALLOWED');
  });

  it('lets a draft leave dates for later, and sends no dates for a stage that does not apply', () => {
    const rows = rowsOf(stages()).map((row, i) =>
      i === 1 ? { ...row, applies: false, plannedStartDate: '2027-01-01' } : row,
    );
    expect(checkStagePlan(rows, { ...bounds, requireDates: false })).toEqual({});
    expect(toEntries(rows, false)[1]).toEqual({
      stageCode: 'DESIGN',
      applies: false,
      plannedStartDate: null,
      plannedEndDate: null,
      startingPercent: null,
    });
  });
});
