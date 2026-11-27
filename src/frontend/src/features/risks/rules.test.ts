import { describe, expect, test } from 'vitest';

import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { type ProjectStatus } from '@/features/projects/api/types.ts';
import { permissionsOf } from '@/features/identity-access/permissions/permissions.ts';
import { sessionFor } from '@/test/identityAccessFixtures.ts';
import { allPermissions, shippedPermissions } from '@/test/permissionFixtures.ts';
import {
  entityManagerSession,
  OTHER_DEPARTMENT_ID,
  projectSummary,
  reviewerSession,
  viewerSession,
} from '@/test/projectFixtures.ts';
import {
  ASPHALT_ID,
  coastalRisks,
  compactMatrix,
  COST_DIMENSION_ID,
  harbourRisks,
  risk,
  SCHEDULE_DIMENSION_ID,
  standardMatrix,
} from '@/test/riskFixtures.ts';
import { day } from '@/test/taskFixtures.ts';

import {
  canAcceptRisk,
  canAssessRisk,
  canManageRisk,
  canMonitorRisk,
  canRegisterRisks,
  canReopenRisk,
  canRevokeAcceptance,
  canStartTreatment,
} from './access.ts';
import {
  assessmentValuesOf,
  byExposure,
  checkAcceptance,
  checkAssessment,
  checkClosure,
  checkRiskForm,
  criticalRating,
  emptyRiskForm,
  isCritical,
  isReviewDue,
  matchesView,
  overallImpact,
  ratingAt,
  riskMatrixOf,
  riskSummary,
  severityStep,
  toAssessCommand,
  toCloseCommand,
  withOwner,
} from './riskRules.ts';

const today = day(0);

describe('the matrix is the published version, never a built-in one (acceptance criterion 1)', () => {
  test('axes, ratings and cells are read from the version', () => {
    const matrix = riskMatrixOf(standardMatrix());
    expect(matrix.probabilityLevels.map((entry) => entry.level)).toEqual([1, 2, 3, 4, 5]);
    expect(matrix.impactLevels).toEqual([1, 2, 3, 4, 5]);
    expect(matrix.dimensions.map((dimension) => dimension.id)).toEqual([
      COST_DIMENSION_ID,
      SCHEDULE_DIMENSION_ID,
    ]);
    expect(matrix.ratings.map((rating) => rating.code)).toEqual([
      'LOW',
      'MEDIUM',
      'HIGH',
      'CRITICAL',
    ]);
    expect(ratingAt(matrix, 4, 5)?.code).toBe('CRITICAL');
    expect(ratingAt(matrix, 1, 1)?.code).toBe('LOW');
  });

  test('another version draws another matrix: its own size, ratings (sorted by their order) and mapping', () => {
    const matrix = riskMatrixOf(compactMatrix());
    expect(matrix.impactLevels).toEqual([1, 2, 3]);
    expect(matrix.ratings.map((rating) => rating.code)).toEqual(['MINOR', 'MAJOR']);
    expect(ratingAt(matrix, 3, 3)?.code).toBe('MAJOR');
    expect(ratingAt(matrix, 4, 5)).toBeNull();
    expect(criticalRating(matrix)?.code).toBe('MAJOR');
  });

  test('an unmapped cell has no rating, and an unknown rating no colour step', () => {
    const resolution = standardMatrix();
    resolution.content.riskMatrixCells = resolution.content.riskMatrixCells.filter(
      (cell) => !(cell.probabilityLevel === 1 && cell.impactLevel === 1),
    );
    const matrix = riskMatrixOf(resolution);
    expect(ratingAt(matrix, 1, 1)).toBeNull();
    expect(severityStep(matrix, 'UNKNOWN')).toBeNull();
  });

  test('colour steps spread the published order over five steps, the most severe always the top one', () => {
    const matrix = riskMatrixOf(standardMatrix());
    expect(['LOW', 'MEDIUM', 'HIGH', 'CRITICAL'].map((code) => severityStep(matrix, code))).toEqual(
      [1, 2, 4, 5],
    );
    const compact = riskMatrixOf(compactMatrix());
    expect([severityStep(compact, 'MINOR'), severityStep(compact, 'MAJOR')]).toEqual([1, 5]);
  });

  test('critical is the most severe published rating, on an open risk only', () => {
    const matrix = riskMatrixOf(standardMatrix());
    expect(isCritical(risk(), matrix)).toBe(true);
    expect(isCritical(risk({ status: 'CLOSED' }), matrix)).toBe(false);
    expect(isCritical(risk({ currentAssessment: null, status: 'IDENTIFIED' }), matrix)).toBe(false);
    // Under a version whose top rating is another code, the same risk is not critical.
    expect(isCritical(risk(), riskMatrixOf(compactMatrix()))).toBe(false);
  });
});

describe('the register', () => {
  test('most severe first, then the earliest review; unassessed after rated, closed last', () => {
    const matrix = riskMatrixOf(standardMatrix());
    const entries = coastalRisks().map((item) => ({ risk: item, project: projectSummary() }));
    expect(entries.sort(byExposure(matrix)).map((entry) => entry.risk.title.text)).toEqual([
      'Flooding of the works',
      'Asphalt price rise',
      'Utility relocation delay',
      'Survey access refused',
    ]);
  });

  test('a review is due on its date, and never on a closed risk', () => {
    expect(isReviewDue(risk({ nextReviewDate: today }), today)).toBe(true);
    expect(isReviewDue(risk({ nextReviewDate: day(1) }), today)).toBe(false);
    expect(isReviewDue(risk({ status: 'CLOSED', nextReviewDate: day(-1) }), today)).toBe(false);
    expect(matchesView(risk({ status: 'IDENTIFIED' }), 'unassessed', today)).toBe(true);
    expect(matchesView(risk({ status: 'CLOSED' }), 'open', today)).toBe(false);
  });

  test("the dashboard's inputs count open risks by the server's ratings; without the matrix, critical is unknown", () => {
    const risks = [...coastalRisks(), ...harbourRisks()];
    const summary = riskSummary(risks, riskMatrixOf(standardMatrix()), today);
    expect(summary.open).toBe(4);
    expect(summary.unassessed).toBe(1);
    expect(summary.reviewDue).toBe(1);
    expect(summary.critical).toBe(2);
    expect(summary.byStatus).toEqual({
      IDENTIFIED: 1,
      ASSESSED: 1,
      TREATMENT: 1,
      MONITORING: 1,
      CLOSED: 1,
    });
    expect(summary.byRating.map((entry) => [entry.code, entry.count])).toEqual([
      ['LOW', 0],
      ['MEDIUM', 1],
      ['HIGH', 0],
      ['CRITICAL', 2],
    ]);
    expect(summary.cells.get('4:5')).toBe(1);
    expect(summary.cells.get('1:2')).toBeUndefined();

    const blind = riskSummary(risks, null, today);
    expect(blind.critical).toBeNull();
    expect(blind.byRating.map((entry) => entry.code)).toEqual(['MEDIUM', 'CRITICAL']);
  });
});

describe('client-side checks mirror the API', () => {
  test('MOD-035: a blank or whitespace-only closure rationale is refused (acceptance criterion 2)', () => {
    expect(checkClosure('')).toEqual({ rationale: 'REQUIRED' });
    expect(checkClosure('   \n ')).toEqual({ rationale: 'REQUIRED' });
    expect(checkClosure('x'.repeat(2001))).toEqual({ rationale: 'MAX_LENGTH' });
    expect(checkClosure('Mitigated.')).toEqual({ rationale: null });
    expect(toCloseCommand('  Mitigated.  ', 'ar')).toEqual({
      rationale: { text: 'Mitigated.', language: 'ar' },
    });
  });

  test('MOD-030: required fields, an identified date not after today, no review before it', () => {
    expect(checkRiskForm(emptyRiskForm(today), today)).toEqual({
      title: 'REQUIRED',
      description: 'REQUIRED',
      riskCategoryItemId: 'REQUIRED',
      identifiedDate: null,
      nextReviewDate: null,
    });
    const values = {
      title: 'T',
      description: 'D',
      riskCategoryItemId: 'c',
      identifiedDate: day(1),
      nextReviewDate: day(0),
    };
    expect(checkRiskForm(values, today)).toMatchObject({
      identifiedDate: 'DATE_IN_FUTURE',
      nextReviewDate: 'DATE_BEFORE_START',
    });
  });

  test('MOD-033 re-sends every register field with the new owner, keeping the original text language', () => {
    const request = withOwner(risk(), null, 'ar');
    expect(request).toEqual({
      title: { text: 'Flooding of the works', language: 'en' },
      description: { text: 'Heavy rain floods the excavation and halts work.', language: 'en' },
      riskCategoryItemId: risk().riskCategoryItemId,
      ownerUserId: null,
      identifiedDate: risk().identifiedDate,
      nextReviewDate: risk().nextReviewDate,
    });
  });

  test('MOD-032: a probability and one level per dimension of the version; only levels are sent', () => {
    const matrix = riskMatrixOf(standardMatrix());
    const blank = assessmentValuesOf(matrix, null);
    expect(checkAssessment(blank, matrix)).toEqual({
      probabilityLevel: 'REQUIRED',
      [`impact-${COST_DIMENSION_ID}`]: 'REQUIRED',
      [`impact-${SCHEDULE_DIMENSION_ID}`]: 'REQUIRED',
      rationale: null,
    });
    const values = {
      probabilityLevel: '3',
      impacts: { [COST_DIMENSION_ID]: '2', [SCHEDULE_DIMENSION_ID]: '4' },
      rationale: '',
    };
    expect(toAssessCommand(values, matrix, 'en')).toEqual({
      probabilityLevel: 3,
      impacts: [
        { impactDimensionItemId: COST_DIMENSION_ID, impactLevel: 2, rationale: null },
        { impactDimensionItemId: SCHEDULE_DIMENSION_ID, impactLevel: 4, rationale: null },
      ],
      rationale: null,
    });
    expect(overallImpact([2, 4])).toBe(4);
    expect(overallImpact([2, null])).toBeNull();
  });

  test('a reassessment starts from the latest levels that still fit the version', () => {
    const previous = {
      probabilityLevel: 4,
      impacts: [
        { impactDimensionItemId: COST_DIMENSION_ID, impactLevel: 5 },
        { impactDimensionItemId: SCHEDULE_DIMENSION_ID, impactLevel: 3 },
      ],
    };
    expect(assessmentValuesOf(riskMatrixOf(standardMatrix()), previous)).toEqual({
      probabilityLevel: '4',
      impacts: { [COST_DIMENSION_ID]: '5', [SCHEDULE_DIMENSION_ID]: '3' },
      rationale: '',
    });
    // Version 2 has three levels and no schedule dimension: what no longer fits starts blank.
    expect(assessmentValuesOf(riskMatrixOf(compactMatrix()), previous)).toEqual({
      probabilityLevel: '',
      impacts: { [COST_DIMENSION_ID]: '' },
      rationale: '',
    });
  });

  test('an acceptance ends after today and gives its reason', () => {
    expect(checkAcceptance({ expiresOn: today, rationale: '' }, today)).toEqual({
      expiresOn: 'EXPIRY_NOT_AFTER_TODAY',
      rationale: 'REQUIRED',
    });
    expect(checkAcceptance({ expiresOn: day(1), rationale: 'OK' }, today)).toEqual({
      expiresOn: null,
      rationale: null,
    });
  });
});

describe('what the screens offer (access.ts: navigation, not protection)', () => {
  const manager: SessionUser = entityManagerSession().user;
  const ahda: SessionUser = reviewerSession().user;
  const portfolio: SessionUser = sessionFor(['R02']).user;
  const otherManager: SessionUser = sessionFor(['R04']).user;
  const assigned: SessionUser = sessionFor(['R05']).user;
  const viewer: SessionUser = viewerSession().user;
  const executive: SessionUser = sessionFor(['R07']).user;
  const held = (user: SessionUser) => permissionsOf(shippedPermissions(user));
  const project = (status: ProjectStatus) =>
    projectSummary({ status, projectManagerUserId: manager.id });

  test('a holder of RISK_MANAGE on the project registers and manages risks while the project allows it', () => {
    const register = (user: SessionUser, status: ProjectStatus = 'ACTIVE') =>
      canRegisterRisks(user, held(user), project(status));
    expect(register(manager)).toBe(true);
    expect(register(manager, 'COMPLETED')).toBe(false);
    // D1-02: R02 (ALL) and R03 (DEPT) hold RISK_MANAGE, and the API accepts their POST /risks.
    expect(register(ahda)).toBe(true);
    expect(register(portfolio)).toBe(true);
    // R04 at OWN reaches only the project it manages; R05, R06 and R07 do not hold RISK_MANAGE.
    expect(register(otherManager)).toBe(false);
    expect(register(assigned)).toBe(false);
    expect(register(viewer)).toBe(false);
    expect(register(executive)).toBe(false);
    // R03's department is not another department's.
    const elsewhere = reviewerSession(OTHER_DEPARTMENT_ID).user;
    expect(register(elsewhere)).toBe(false);
    expect(canManageRisk(manager, held(manager), project('COMPLETED'), risk())).toBe(true);
    expect(canManageRisk(manager, held(manager), project('CLOSED'), risk())).toBe(false);
    expect(
      canManageRisk(manager, held(manager), project('ACTIVE'), risk({ status: 'CLOSED' })),
    ).toBe(false);
  });

  test('assessing, accepting and reopening follow RISK_ASSESS, RISK_ACCEPT and RISK_REOPEN (C-03, D1-03)', () => {
    const active = project('ACTIVE');
    for (const user of [ahda, portfolio]) {
      expect(canAssessRisk(user, held(user), active, risk())).toBe(true);
      expect(canAcceptRisk(user, held(user), active, risk())).toBe(true);
      expect(canRevokeAcceptance(user, held(user), active, risk({ acceptedUntil: day(5) }))).toBe(
        true,
      );
      expect(canReopenRisk(user, held(user), active, risk({ status: 'CLOSED' }))).toBe(true);
    }
    // R04, R05, R06 and R07 hold none of the three: the API answers them 403.
    for (const user of [manager, otherManager, assigned, viewer, executive]) {
      expect(canAssessRisk(user, held(user), active, risk())).toBe(false);
      expect(canAcceptRisk(user, held(user), active, risk())).toBe(false);
      expect(canRevokeAcceptance(user, held(user), active, risk({ acceptedUntil: day(5) }))).toBe(
        false,
      );
      expect(canReopenRisk(user, held(user), active, risk({ status: 'CLOSED' }))).toBe(false);
    }
    expect(canAcceptRisk(ahda, held(ahda), active, risk({ status: 'IDENTIFIED' }))).toBe(false);
    expect(canAcceptRisk(ahda, held(ahda), active, risk({ acceptedUntil: day(5) }))).toBe(false);
    expect(canReopenRisk(ahda, held(ahda), active, risk())).toBe(false);
  });

  test('an external person is offered no assessment even when a grant says so (ADR-013)', () => {
    const everything = permissionsOf(allPermissions());
    expect(canAssessRisk(manager, everything, project('ACTIVE'), risk())).toBe(false);
    expect(canAcceptRisk(manager, everything, project('ACTIVE'), risk())).toBe(false);
  });

  test('treatment needs a live action and no acceptance; monitoring follows assessment or treatment', () => {
    const active = project('ACTIVE');
    const permissions = held(manager);
    expect(canStartTreatment(manager, permissions, active, risk(), true)).toBe(true);
    expect(canStartTreatment(manager, permissions, active, risk(), false)).toBe(false);
    expect(
      canStartTreatment(manager, permissions, active, risk({ acceptedUntil: day(3) }), true),
    ).toBe(false);
    expect(
      canStartTreatment(manager, permissions, active, risk({ status: 'TREATMENT' }), true),
    ).toBe(false);
    expect(
      canMonitorRisk(manager, permissions, active, risk({ id: ASPHALT_ID, status: 'TREATMENT' })),
    ).toBe(true);
    expect(canMonitorRisk(manager, permissions, active, risk({ status: 'IDENTIFIED' }))).toBe(
      false,
    );
  });
});
