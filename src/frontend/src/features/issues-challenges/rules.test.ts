import { describe, expect, test } from 'vitest';

import { riskMatrixOf } from '@/features/risks/riskRules.ts';
import { concern, concernProject, escalation, managerSession } from '@/test/concernFixtures.ts';
import { entitySession, reviewerSession } from '@/test/projectFixtures.ts';
import { COST_DIMENSION_ID, SCHEDULE_DIMENSION_ID, standardMatrix } from '@/test/riskFixtures.ts';

import {
  canCloseConcern,
  canEditConcern,
  canEscalateConcern,
  canRaiseConcerns,
  canResolveEscalation,
  canStartConcern,
  canSubmitResolution,
  canWithdrawEscalation,
} from './access.ts';
import {
  byEscalation,
  bySeverity,
  checkConcernForm,
  checkImpacts,
  concernFormValuesOf,
  DEFAULT_ESCALATION_VIEW,
  escalationAgeDays,
  impactValuesOf,
  isOverdue,
  isReturned,
  matchesEscalationView,
  matchesView,
  needsHistory,
  openEscalationsOf,
  overallImpactOf,
  severityStep,
  toAssessCommand,
  toConcernRequest,
} from './concernRules.ts';

const TODAY = '2026-10-06';
const project = concernProject();
const summary = project;

describe('severity is the server’s (acceptance criterion 1)', () => {
  test('the edit request carries priority and never a severity, impact or level', () => {
    const before = concern();
    const request = toConcernRequest(
      { ...concernFormValuesOf(before), priorityItemId: 'new-priority' },
      'en',
      before,
    );
    expect(request.priorityItemId).toBe('new-priority');
    expect(Object.keys(request).sort()).toEqual([
      'categoryItemId',
      'description',
      'priorityItemId',
      'targetResolutionDate',
      'title',
    ]);
  });

  test('an assessment sends levels only; the overall impact previewed is the highest', () => {
    const values = { [COST_DIMENSION_ID]: '2', [SCHEDULE_DIMENSION_ID]: '' };
    expect(overallImpactOf(values)).toBe(2);
    expect(toAssessCommand(values)).toEqual({
      impacts: [{ impactDimensionItemId: COST_DIMENSION_ID, impactLevel: 2, rationale: null }],
    });
    expect(checkImpacts({ [COST_DIMENSION_ID]: '' })).toEqual({ impacts: 'REQUIRED' });
    expect(overallImpactOf({})).toBeNull();
  });

  test('a reassessment starts from the levels that still fit the version in force', () => {
    const matrix = riskMatrixOf(standardMatrix());
    expect(
      impactValuesOf(
        matrix,
        concern({
          impacts: [
            { impactDimensionItemId: COST_DIMENSION_ID, impactLevel: 4, rationale: null },
            { impactDimensionItemId: 'retired-dimension', impactLevel: 3, rationale: null },
          ],
        }),
      ),
    ).toEqual({ [COST_DIMENSION_ID]: '4', [SCHEDULE_DIMENSION_ID]: '' });
  });

  test('the colour step is the overall impact level, clamped to the five steps; unassessed has none', () => {
    expect(severityStep(null)).toBeNull();
    expect(severityStep(4)).toBe(4);
    expect(severityStep(9)).toBe(5);
  });
});

describe('the registers', () => {
  test('most severe first by overall impact, unassessed after, closed last', () => {
    const entries = [
      concern({ id: 'a', overallImpactLevel: null, title: { text: 'A', language: 'EN' } }),
      concern({ id: 'b', status: 'CLOSED', overallImpactLevel: 5 }),
      concern({ id: 'c', overallImpactLevel: 2 }),
      concern({ id: 'd', overallImpactLevel: 5 }),
    ].map((item) => ({ concern: item, project: summary }));
    expect(entries.sort(bySeverity).map((entry) => entry.concern.id)).toEqual(['d', 'c', 'a', 'b']);
  });

  test('views: open, mine, escalated, overdue, pending validation, closed', () => {
    const userId = managerSession().user.id;
    const mine = concern({ assigneeUserId: userId, openEscalation: null });
    expect(matchesView(mine, 'mine', TODAY, userId)).toBe(true);
    expect(matchesView(concern(), 'escalated', TODAY, userId)).toBe(true);
    expect(matchesView(mine, 'escalated', TODAY, userId)).toBe(false);
    expect(matchesView(concern({ status: 'CLOSED' }), 'open', TODAY, userId)).toBe(false);
    expect(
      matchesView(concern({ status: 'PENDING_VALIDATION' }), 'pendingValidation', TODAY, userId),
    ).toBe(true);
  });

  test('overdue is unresolved past its target date; a resolved one is not overdue', () => {
    expect(isOverdue(concern({ targetResolutionDate: '2026-10-05' }), TODAY)).toBe(true);
    expect(isOverdue(concern({ targetResolutionDate: TODAY }), TODAY)).toBe(false);
    expect(
      isOverdue(concern({ targetResolutionDate: '2026-10-05', status: 'RESOLVED' }), TODAY),
    ).toBe(false);
  });

  test('a returned resolution is IN_PROGRESS at a later revision with its resolution kept', () => {
    const resolution = { text: 'Rebuilt.', language: 'EN' as const };
    expect(isReturned(concern({ status: 'IN_PROGRESS', revisionNo: 2, resolution }))).toBe(true);
    expect(isReturned(concern({ status: 'IN_PROGRESS', revisionNo: 1, resolution: null }))).toBe(
      false,
    );
  });

  test('a target date must be today or later when set or changed; an unchanged past one is kept', () => {
    const before = concern({ targetResolutionDate: '2026-10-01' });
    const values = concernFormValuesOf(before);
    expect(checkConcernForm(values, TODAY, before).targetResolutionDate).toBeNull();
    expect(
      checkConcernForm({ ...values, targetResolutionDate: '2026-10-02' }, TODAY, before)
        .targetResolutionDate,
    ).toBe('DATE_IN_PAST');
    expect(
      checkConcernForm({ ...values, targetResolutionDate: '2026-10-02' }, TODAY, null)
        .targetResolutionDate,
    ).toBe('DATE_IN_PAST');
    expect(checkConcernForm({ ...values, priorityItemId: '' }, TODAY, before).priorityItemId).toBe(
      'REQUIRED',
    );
  });
});

describe('the escalation list (acceptance criterion 2)', () => {
  test('defaults to open only, which needs no history read; the other views need it', () => {
    expect(DEFAULT_ESCALATION_VIEW).toBe('open');
    expect(needsHistory('open')).toBe(false);
    expect(needsHistory('resolved')).toBe(true);
    expect(needsHistory('all')).toBe(true);
  });

  test('each view matches its status; all matches every one', () => {
    const resolved = escalation({ status: 'RESOLVED' });
    expect(matchesEscalationView(resolved, 'open')).toBe(false);
    expect(matchesEscalationView(resolved, 'resolved')).toBe(true);
    expect(matchesEscalationView(resolved, 'withdrawn')).toBe(false);
    expect(matchesEscalationView(resolved, 'all')).toBe(true);
  });

  test('the open escalations come from the concerns that carry one, open ones listed first', () => {
    const entries = [
      { concern: concern(), project: summary },
      { concern: concern({ id: 'x', openEscalation: null }), project: summary },
    ];
    const [open, ...rest] = openEscalationsOf(entries);
    expect(open?.escalation.status).toBe('OPEN');
    expect(rest).toHaveLength(0);
    if (open === undefined) {
      return;
    }
    const ended = {
      ...open,
      escalation: escalation({
        id: 'old',
        status: 'RESOLVED',
        escalatedAt: '2026-10-05T00:00:00Z',
      }),
    };
    expect([ended, open].sort(byEscalation).map((entry) => entry.escalation.status)).toEqual([
      'OPEN',
      'RESOLVED',
    ]);
  });

  test('age is whole days to the end, or to now', () => {
    expect(
      escalationAgeDays({
        escalatedAt: '2026-10-01T09:00:00Z',
        resolvedAt: '2026-10-04T10:00:00Z',
      }),
    ).toBe(3);
    expect(
      escalationAgeDays(
        { escalatedAt: '2026-10-01T09:00:00Z', resolvedAt: null },
        new Date('2026-10-02T08:00:00Z'),
      ),
    ).toBe(0);
  });
});

describe('what is offered (access.ts)', () => {
  const manager = managerSession().user;
  const entity = entitySession().user;
  const departmentManager = reviewerSession().user;

  test('the Project Manager and the delivering entity raise; the department manager does not', () => {
    expect(canRaiseConcerns(manager, project)).toBe(true);
    expect(canRaiseConcerns(entity, project)).toBe(true);
    expect(canRaiseConcerns(departmentManager, project)).toBe(false);
    expect(canRaiseConcerns(manager, { ...project, status: 'COMPLETED' })).toBe(false);
  });

  test('priority (MOD-037) is edited by the internal Project Manager until validation, never by an external one', () => {
    expect(canEditConcern(manager, project, concern())).toBe(true);
    expect(canEditConcern(entity, project, concern())).toBe(false);
    // An external Project Manager: management is AHDA's (ADR-013).
    expect(canEditConcern(entity, { ...project, projectManagerUserId: entity.id }, concern())).toBe(
      false,
    );
    expect(canEditConcern(manager, project, concern({ status: 'PENDING_VALIDATION' }))).toBe(false);
    expect(canEditConcern(manager, project, concern({ status: 'CLOSED' }))).toBe(false);
  });

  test('the lifecycle commands follow the state', () => {
    expect(canStartConcern(manager, project, concern({ status: 'ASSIGNED' }))).toBe(true);
    expect(canStartConcern(manager, project, concern({ status: 'OPEN' }))).toBe(false);
    expect(canSubmitResolution(manager, project, concern({ status: 'IN_PROGRESS' }))).toBe(true);
    expect(canCloseConcern(manager, project, concern({ status: 'RESOLVED' }))).toBe(false);
    expect(
      canCloseConcern(manager, project, concern({ status: 'RESOLVED', openEscalation: null })),
    ).toBe(true);
  });

  test('one open escalation at a time, never once resolved', () => {
    expect(canEscalateConcern(manager, project, concern())).toBe(false);
    expect(canEscalateConcern(manager, project, concern({ openEscalation: null }))).toBe(true);
    expect(
      canEscalateConcern(manager, project, concern({ openEscalation: null, status: 'RESOLVED' })),
    ).toBe(false);
    expect(canEscalateConcern(entity, project, concern({ openEscalation: null }))).toBe(false);
  });

  test('the addressed role resolves; its escalator withdraws', () => {
    const open = escalation();
    expect(canResolveEscalation(departmentManager, open, 'R03')).toBe(true);
    expect(canResolveEscalation(manager, open, 'R03')).toBe(false);
    // Roles unreadable: any internal person but the escalator, and the API decides.
    expect(canResolveEscalation(departmentManager, open, null)).toBe(true);
    expect(canResolveEscalation(manager, open, null)).toBe(false);
    expect(canResolveEscalation(entity, open, null)).toBe(false);
    expect(canResolveEscalation(departmentManager, escalation({ status: 'RESOLVED' }), 'R03')).toBe(
      false,
    );
    expect(canWithdrawEscalation(manager, open)).toBe(true);
    expect(canWithdrawEscalation(departmentManager, open)).toBe(false);
  });
});
