import { describe, expect, it } from 'vitest';

import { type MyUpdateRequest } from '../api/monthlyUpdateTypes.ts';

import { monthlyFields, monthlyStageFigures } from './monthlyForm.ts';

const STAGES = [
  ['BUSINESS_CASE', 5, 100],
  ['DESIGN', 15, 100],
  ['TENDERING', 10, 100],
  ['EXECUTION', 55, 50],
  ['TESTING', 10, 0],
  ['HANDOVER', 5, 0],
] as const;

function request(withStages: boolean): MyUpdateRequest {
  return {
    requestId: 'r',
    requestVersion: 1,
    projectId: 'p',
    formalProjectId: null,
    legacyReference: null,
    projectTitle: { text: 'Project', language: 'EN' },
    dueDate: null,
    overdue: false,
    status: 'SENT',
    instructions: { text: '', language: 'EN' },
    contributionId: null,
    contributionVersion: null,
    contributionStatus: null,
    returnReason: null,
    previouslyReturned: false,
    plannedProgressPercent: null,
    prefill: {},
    prefillLanguages: {},
    stages: withStages
      ? STAGES.map(([code, weight, planned]) => ({
          stageCode: code,
          name: { en: code, ar: code },
          weightPercent: weight,
          plannedPercent: planned,
        }))
      : [],
  };
}

describe('the monthly update of a project with a stage plan (review round 7, item 1)', () => {
  it('asks for the current stage and its % instead of the overall figure', () => {
    expect(
      monthlyFields(request(true))
        .map((field) => field.fieldCode)
        .slice(0, 2),
    ).toEqual(['currentStageCode', 'currentStagePercent']);
    expect(monthlyFields(request(false))[0]?.fieldCode).toBe('actualProgressPercent');
  });

  it('weighs execution at 40% to 52.0% overall, beside the plan', () => {
    const figures = monthlyStageFigures(request(true), {
      currentStageCode: 'EXECUTION',
      currentStagePercent: '40',
    });
    expect(figures.actual).toBeCloseTo(52, 6);
    expect(figures.planned).toBeCloseTo(57.5, 6);
  });
});
