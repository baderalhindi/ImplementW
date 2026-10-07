import { describe, expect, test } from 'vitest';

import { changeRequest, pavingAt } from '@/test/changeRequestFixtures.ts';

import { type ChangeRequestStatus } from './api/types.ts';
import {
  approvalStateOf,
  CHANGE_REQUEST_STATUSES,
  changeRequestFormValuesOf,
  checkDraft,
  checkForSubmission,
  emptyChangeRequestForm,
  implementationStateOf,
  toChangeRequestRequest,
} from './changeRequestRules.ts';
import { approvalStyle, implementationStyle, statusStyle } from './presentation.ts';

// The WF-08 rules the screens apply (changeRequestRules.ts, presentation.ts): the two state dimensions SCR-107 shows
// apart, the checks before saving and classifying, and the request body, which never carries a materiality.

describe('approval and implementation are separate dimensions', () => {
  test.each<[ChangeRequestStatus, string, string]>([
    ['DRAFT', 'NOT_SUBMITTED', 'NOT_APPLICABLE'],
    ['SUBMITTED', 'AWAITING_REVIEW', 'NOT_APPLICABLE'],
    ['UNDER_REVIEW', 'IN_REVIEW', 'NOT_APPLICABLE'],
    ['RETURNED', 'RETURNED', 'NOT_APPLICABLE'],
    ['APPROVED', 'APPROVED', 'NOT_STARTED'],
    ['IMPLEMENTATION', 'APPROVED', 'IN_PROGRESS'],
    ['IMPLEMENTED', 'APPROVED', 'IMPLEMENTED'],
    ['CLOSED', 'APPROVED', 'IMPLEMENTED'],
    ['REJECTED', 'REJECTED', 'NOT_APPLICABLE'],
    ['WITHDRAWN', 'WITHDRAWN', 'NOT_APPLICABLE'],
  ])('%s: approval %s, implementation %s', (status, approval, implementation) => {
    expect(approvalStateOf(status)).toBe(approval);
    expect(implementationStateOf(status)).toBe(implementation);
  });

  test('only Approved is drawn in the approved style and only Implemented in the implemented style', () => {
    const drawn = CHANGE_REQUEST_STATUSES.map((status) => [
      status,
      statusStyle(status),
      approvalStyle(approvalStateOf(status)),
      implementationStyle(implementationStateOf(status)),
    ]);
    for (const [status, lifecycle, approval, implementation] of drawn) {
      expect(lifecycle === 'approved').toBe(status === 'APPROVED');
      expect(lifecycle === 'implemented').toBe(status === 'IMPLEMENTED');
      expect(approval).not.toBe('implemented');
      expect(implementation).not.toBe('approved');
    }
    expect(approvalStyle('APPROVED')).not.toBe(implementationStyle('IMPLEMENTED'));
  });
});

describe('the form checks', () => {
  const filled = { ...emptyChangeRequestForm(), title: 'T', justification: 'J' };

  test('a draft needs a type, a title and a justification; impacts may be left out', () => {
    expect(checkDraft(emptyChangeRequestForm())).toMatchObject({
      changeType: 'REQUIRED',
      title: 'REQUIRED',
      justification: 'REQUIRED',
    });
    const codes = checkDraft({ ...filled, changeType: 'SCHEDULE' });
    expect(Object.values(codes).every((code) => code === null)).toBe(true);
  });

  test.each([
    ['0', 'OUT_OF_RANGE'],
    ['-0.00', 'OUT_OF_RANGE'],
    ['12.345', 'MALFORMED'],
    ['1,000', 'MALFORMED'],
    ['-1500.5', null],
    ['60000', null],
  ])('cost %s → %s', (cost, code) => {
    expect(checkDraft({ ...filled, changeType: 'COST', costImpactSar: cost }).costImpactSar).toBe(
      code,
    );
  });

  test.each([
    ['0', 'OUT_OF_RANGE'],
    ['3651', 'OUT_OF_RANGE'],
    ['-3650', null],
    ['1.5', 'MALFORMED'],
    ['12', null],
  ])('schedule days %s → %s', (days, code) => {
    expect(
      checkDraft({ ...filled, changeType: 'SCHEDULE', scheduleImpactDays: days })
        .scheduleImpactDays,
    ).toBe(code);
  });

  test.each([
    ['SCHEDULE', 'scheduleImpactDays'],
    ['COST', 'costImpactSar'],
    ['SCOPE', 'scopeImpact'],
    ['CONTRACTUAL_OBLIGATION', 'isContractualObligation'],
    ['GOVERNANCE_PROFILE', 'requestedGovernanceProfileItemId'],
  ] as const)('classifying a %s change needs its %s, as submission does', (changeType, field) => {
    expect(checkForSubmission({ ...filled, changeType })[field]).toBe('REQUIRED');
    expect(checkDraft({ ...filled, changeType })[field] ?? null).toBeNull();
  });
});

describe('the request body', () => {
  test('carries the fields as the API takes them and never a band or materiality', () => {
    const body = toChangeRequestRequest(
      {
        ...emptyChangeRequestForm(),
        changeType: 'SCHEDULE',
        title: '  Extend  ',
        justification: 'Late plant.',
        costImpactSar: '-1500.5',
        scheduleImpactDays: '12',
        requestedGovernanceProfileItemId: 'ignored-for-a-schedule-change',
      },
      'ar',
      null,
    );
    expect(body).toEqual({
      title: { text: 'Extend', language: 'ar' },
      justification: { text: 'Late plant.', language: 'ar' },
      costImpactSar: '-1500.50',
      scheduleImpactDays: 12,
      scopeImpact: null,
      isContractualObligation: false,
      requestedGovernanceProfileItemId: null,
    });
    expect(Object.keys(body).some((key) => /band|materiality|cumulative/i.test(key))).toBe(false);
  });

  test('an edit keeps the language an unchanged text was entered in', () => {
    const before = changeRequest();
    const body = toChangeRequestRequest(changeRequestFormValuesOf(before), 'ar', before);
    expect(body.title).toEqual({ text: before.title.text, language: 'en' });
  });

  test('a recorded classification is never part of what an edit sends', () => {
    const before = pavingAt('RETURNED');
    const body = toChangeRequestRequest(changeRequestFormValuesOf(before), 'en', before);
    expect(JSON.stringify(body)).not.toContain(String(before.materiality?.evaluationId));
  });
});
