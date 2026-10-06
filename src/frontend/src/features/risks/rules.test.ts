import { describe, expect, test } from 'vitest';

import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { type ProjectStatus } from '@/features/projects/api/types.ts';
import { entitySession, projectSummary, reviewerSession } from '@/test/projectFixtures.ts';
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
  const manager: SessionUser = entitySession().user;
  const ahda: SessionUser = reviewerSession().user;
  const project = (status: ProjectStatus) =>
    projectSummary({ status, projectManagerUserId: manager.id });

  test("the project's own Project Manager registers and manages risks while the project allows it", () => {
    expect(canRegisterRisks(manager, project('ACTIVE'))).toBe(true);
    expect(canRegisterRisks(manager, project('COMPLETED'))).toBe(false);
    expect(canRegisterRisks(ahda, project('ACTIVE'))).toBe(false);
    expect(canManageRisk(manager, project('COMPLETED'), risk())).toBe(true);
    expect(canManageRisk(manager, project('CLOSED'), risk())).toBe(false);
    expect(canManageRisk(manager, project('ACTIVE'), risk({ status: 'CLOSED' }))).toBe(false);
  });

  test('assessing, accepting and reopening are offered to internal people the project reaches, never to an external one', () => {
    const active = project('ACTIVE');
    expect(canAssessRisk(ahda, active, risk())).toBe(true);
    expect(canAssessRisk(manager, active, risk())).toBe(false);
    expect(canAcceptRisk(ahda, active, risk())).toBe(true);
    expect(canAcceptRisk(ahda, active, risk({ status: 'IDENTIFIED' }))).toBe(false);
    expect(canAcceptRisk(ahda, active, risk({ acceptedUntil: day(5) }))).toBe(false);
    expect(canRevokeAcceptance(ahda, active, risk({ acceptedUntil: day(5) }))).toBe(true);
    expect(canReopenRisk(ahda, active, risk({ status: 'CLOSED' }))).toBe(true);
    expect(canReopenRisk(ahda, active, risk())).toBe(false);
    expect(canReopenRisk(manager, active, risk({ status: 'CLOSED' }))).toBe(false);
  });

  test('treatment needs a live action and no acceptance; monitoring follows assessment or treatment', () => {
    const active = project('ACTIVE');
    expect(canStartTreatment(manager, active, risk(), true)).toBe(true);
    expect(canStartTreatment(manager, active, risk(), false)).toBe(false);
    expect(canStartTreatment(manager, active, risk({ acceptedUntil: day(3) }), true)).toBe(false);
    expect(canStartTreatment(manager, active, risk({ status: 'TREATMENT' }), true)).toBe(false);
    expect(canMonitorRisk(manager, active, risk({ id: ASPHALT_ID, status: 'TREATMENT' }))).toBe(
      true,
    );
    expect(canMonitorRisk(manager, active, risk({ status: 'IDENTIFIED' }))).toBe(false);
  });
});
