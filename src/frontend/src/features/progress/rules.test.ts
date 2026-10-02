import { describe, expect, test } from 'vitest';

import { sessionFor } from '@/test/identityAccessFixtures.ts';
import { submission, submitted } from '@/test/progressFixtures.ts';
import {
  ENTITY_USER_ID,
  entitySession,
  projectDetail,
  REVIEWER_ID,
  reviewerSession,
} from '@/test/projectFixtures.ts';

import { canReport, canReview } from './access.ts';
import { formatPercent, formatVariance, variance } from './presentation.ts';
import {
  checkPercent,
  checkProgressUpdate,
  differsFrom,
  OVERRIDE_PERCENT_FIELD,
  OVERRIDE_REASON_FIELD,
  toRequest,
  valuesOf,
} from './progressUpdate.ts';

// The client-side rules of SCR-048 and MOD-020 to MOD-022, against TASK-044's API rules.

describe('the override percentage is checked as the API checks it (ProgressOverrideRequest.Validate)', () => {
  test.each(['0', '100', '42.5', '33.3333', ' 7 ', '100.0000'])('%s is accepted', (value) => {
    expect(checkPercent(value)).toBeNull();
  });

  test.each([
    ['-0.0001', 'OUT_OF_RANGE'],
    ['-5', 'OUT_OF_RANGE'],
    ['100.0001', 'OUT_OF_RANGE'],
    ['101', 'OUT_OF_RANGE'],
    ['250', 'OUT_OF_RANGE'],
    ['42.12345', 'OUT_OF_RANGE'],
    ['', 'REQUIRED'],
    ['  ', 'REQUIRED'],
    ['abc', 'MALFORMED'],
    ['42%', 'MALFORMED'],
    ['4,5', 'MALFORMED'],
    ['1e2', 'MALFORMED'],
  ])('%s is refused with %s', (value, code) => {
    expect(checkPercent(value)).toBe(code);
  });

  test('an override needs its reason; the calculated figure needs neither', () => {
    const draft = valuesOf(submission());
    expect(checkProgressUpdate(draft)[OVERRIDE_PERCENT_FIELD]).toBeNull();
    const overriding = { ...draft, figureSource: 'override' as const, overridePercent: '45' };
    expect(checkProgressUpdate(overriding)[OVERRIDE_REASON_FIELD]).toBe('REQUIRED');
    expect(
      checkProgressUpdate({ ...overriding, overrideReason: 'Site survey' })[OVERRIDE_REASON_FIELD],
    ).toBeNull();
  });
});

describe('the request carries no derived figure (ADR-009)', () => {
  test('an override is sent as a number with its reason in the language it was typed in', () => {
    const draft = submission();
    const request = toRequest(
      {
        ...valuesOf(draft),
        figureSource: 'override',
        overridePercent: ' 45.25 ',
        overrideReason: ' Survey of the northern section ',
      },
      'ar',
      draft,
    );
    expect(request).toEqual({
      narrative: { text: 'Earthworks on the northern section.', language: 'en' },
      override: {
        actualPercent: 45.25,
        reason: { text: 'Survey of the northern section', language: 'ar' },
      },
    });
    expect(Object.keys(request).sort()).toEqual(['narrative', 'override']);
  });

  test('choosing the calculated figure clears the override; a blank narrative is null', () => {
    const draft = submission({
      isOverridden: true,
      actualPercentOverride: 45,
      overrideReason: { text: 'Survey', language: 'EN' },
    });
    expect(
      toRequest({ ...valuesOf(draft), figureSource: 'calculated', narrative: ' ' }, 'en', draft),
    ).toEqual({ narrative: null, override: null });
  });

  test('a pre-filled draft confirmed as it is is not a change (ADR-017)', () => {
    const draft = submission({
      isOverridden: true,
      actualPercent: '45.0000',
      actualPercentOverride: '45.0000',
      overrideReason: { text: 'Survey', language: 'EN' },
    });
    const values = valuesOf(draft);
    expect(differsFrom(values, draft)).toBe(false);
    expect(differsFrom({ ...values, overridePercent: '45' }, draft)).toBe(false);
    expect(differsFrom({ ...values, overridePercent: '46' }, draft)).toBe(true);
    expect(differsFrom({ ...values, figureSource: 'calculated' }, draft)).toBe(true);
  });
});

describe('figures', () => {
  test('variance is actual minus planned in points, to four places; none without a plan', () => {
    expect(variance(40, 50)).toBe(-10);
    expect(variance('30.0000', '35.0000')).toBe(-5);
    expect(variance(33.3333, 33.3332)).toBe(0.0001);
    expect(variance(40, null)).toBeNull();
    expect(formatVariance(-10)).toBe('−10');
    expect(formatVariance(2.5)).toBe('+2.5');
    expect(formatVariance(0)).toBe('0');
  });

  test('a percentage keeps the API’s four places and drops trailing zeros', () => {
    expect(formatPercent('30.0000')).toBe('30%');
    expect(formatPercent(33.3333)).toBe('33.3333%');
    expect(formatPercent(null)).toBeNull();
  });
});

describe('who is offered what (navigation only; the API decides)', () => {
  const manager = entitySession().user;
  const active = projectDetail({ status: 'ACTIVE', projectManagerUserId: ENTITY_USER_ID });

  test('the project’s own Project Manager reports, on an ACTIVE project only (TASK-044 D-5, D-10)', () => {
    expect(canReport(manager, active)).toBe(true);
    expect(canReport(manager, { ...active, status: 'APPROVED_PLANNED' })).toBe(false);
    expect(canReport(reviewerSession().user, active)).toBe(false);
  });

  test('review is AHDA’s and never the submitter’s (ADR-013, TASK-044 D-9)', () => {
    const reviewer = reviewerSession().user;
    expect(canReview(reviewer, active, submitted())).toBe(true);
    expect(canReview(reviewer, active, submitted({ status: 'UNDER_REVIEW' }))).toBe(true);
    expect(canReview(reviewer, active, submission())).toBe(false);
    expect(canReview(reviewer, active, submitted({ submittedByUserId: REVIEWER_ID }))).toBe(false);
    expect(canReview(manager, active, submitted({ submittedByUserId: REVIEWER_ID }))).toBe(false);
    // An internal user whose assignments do not reach the project.
    expect(canReview(sessionFor([]).user, active, submitted())).toBe(false);
  });
});
