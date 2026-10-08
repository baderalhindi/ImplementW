import { describe, expect, test } from 'vitest';

import { canReviewFinancials } from '@/features/financial-kpi/access.ts';
import { type FinancialProgressUpdateDetail } from '@/features/financial-kpi/api/types.ts';
import { canReview } from '@/features/progress/access.ts';
import { type ProgressSubmissionDetail } from '@/features/progress/api/types.ts';
import { type ProjectStatus } from '@/features/projects/api/types.ts';
import { concernProject, managerSession } from '@/test/concernFixtures.ts';
import { reviewerSession } from '@/test/projectFixtures.ts';
import {
  check,
  closureCase,
  completionCase,
  completionEffected,
  obligation,
  suspensionInEffect,
  suspensionRequest,
} from '@/test/suspensionClosureFixtures.ts';

import {
  canRaiseCase,
  checkCloseoutForm,
  closeoutStagesOf,
  isWaivableNow,
  obligationCaseOf,
  obligationCommands,
  toCompletionRequest,
} from './closeoutRules.ts';
import {
  activationStateOf,
  approvalStateOf,
  canActivate,
  canEditRecord,
  canStartReview,
  canWithdrawRecord,
  GOVERNED_STATUSES,
} from './governedRequest.ts';
import {
  canRaiseSuspensionRequest,
  checkSuspensionSubmission,
  SUSPENSION_WITHDRAWABLE,
  suspensionViewOf,
  toSuspensionRequest,
} from './suspensionRules.ts';

const nora = managerSession().user;
const faisal = reviewerSession().user;

describe('approval and activation are two dimensions (TASK-062 D-5, TASK-063 D-4)', () => {
  test('only an effected record is in effect; an approved one is approved and pending', () => {
    for (const status of GOVERNED_STATUSES) {
      expect(activationStateOf(status)).toBe(
        status === 'EFFECTED' ? 'EFFECTED' : status === 'APPROVED' ? 'PENDING' : 'NOT_APPLICABLE',
      );
    }
    expect(approvalStateOf('APPROVED')).toBe('APPROVED');
    expect(approvalStateOf('EFFECTED')).toBe('APPROVED');
  });
});

describe('the closeout is two sequential stages (acceptance criterion 1)', () => {
  test('an ACTIVE project may start Stage 1 only; Stage 2 waits for it', () => {
    const stages = closeoutStagesOf('ACTIVE', [], []);
    expect(stages.completion.state).toBe('READY');
    expect(stages.closure.state).toBe('LOCKED');
    expect(canRaiseCase(nora, concernProject(), stages, 'completion')).toBe(true);
    expect(canRaiseCase(nora, concernProject(), stages, 'closure')).toBe(false);
  });

  test('while a completion case is open, neither stage can be raised again', () => {
    const stages = closeoutStagesOf('ACTIVE', [completionCase({ status: 'APPROVED' })], []);
    expect(stages.completion.state).toBe('IN_PROGRESS');
    expect(stages.closure.state).toBe('LOCKED');
    for (const stage of ['completion', 'closure'] as const) {
      expect(canRaiseCase(nora, concernProject(), stages, stage)).toBe(false);
    }
  });

  test('an approved completion does not open Stage 2: only its activation (the project COMPLETED) does', () => {
    expect(
      closeoutStagesOf('ACTIVE', [completionCase({ status: 'APPROVED' })], []).closure.state,
    ).toBe('LOCKED');
    const completed = closeoutStagesOf('COMPLETED', [completionEffected()], []);
    expect(completed.completion.state).toBe('DONE');
    expect(completed.closure.state).toBe('READY');
    expect(canRaiseCase(nora, concernProject({ status: 'COMPLETED' }), completed, 'closure')).toBe(
      true,
    );
  });

  test('a rejected or withdrawn completion leaves Stage 1 ready to raise again', () => {
    const stages = closeoutStagesOf('ACTIVE', [completionCase({ status: 'REJECTED' })], []);
    expect(stages.completion).toEqual({ state: 'READY', current: null });
  });

  test('a SUSPENDED project holds Stage 1 and may close terminally in Stage 2', () => {
    const suspended = closeoutStagesOf('SUSPENDED', [], []);
    expect(suspended).toMatchObject({
      completion: { state: 'ON_HOLD' },
      closure: { state: 'READY' },
      terminal: true,
    });
    const closed = closeoutStagesOf(
      'CLOSED',
      [],
      [
        closureCase({
          completionCaseId: null,
          outcome: 'TERMINATED_WITHOUT_COMPLETION',
          status: 'EFFECTED',
        }),
      ],
    );
    expect(closed.completion.state).toBe('SKIPPED');
    expect(closed.closure.state).toBe('DONE');
  });

  test('a CLOSED project has both stages done and offers nothing', () => {
    const project = concernProject({ status: 'CLOSED' });
    const stages = closeoutStagesOf(
      'CLOSED',
      [completionEffected()],
      [closureCase({ status: 'EFFECTED' })],
    );
    expect([stages.completion.state, stages.closure.state]).toEqual(['DONE', 'DONE']);
    expect(canRaiseCase(nora, project, stages, 'closure')).toBe(false);
  });

  test.each<ProjectStatus>(['DRAFT', 'SUBMITTED', 'APPROVED_PLANNED'])(
    'before ACTIVE (%s) neither stage is available',
    (status) => {
      const stages = closeoutStagesOf(status, [], []);
      expect([stages.completion.state, stages.closure.state]).toEqual(['NOT_YET', 'LOCKED']);
    },
  );
});

describe('readiness and obligations', () => {
  test('a waiver is offered only on a criterion that failed and may be waived', () => {
    expect(isWaivableNow(check('PROGRESS_REPORTED', 'FAIL'))).toBe(true);
    expect(isWaivableNow(check('PROGRESS_REPORTED', 'PASS'))).toBe(false);
    expect(isWaivableNow(check('PROGRESS_REPORTED', 'WAIVED'))).toBe(false);
    expect(isWaivableNow(check('DECISIONS_SETTLED', 'FAIL'))).toBe(false);
  });

  test('an obligation is recorded against the open or effected completion case, or an open terminal closure', () => {
    expect(obligationCaseOf(closeoutStagesOf('ACTIVE', [], []))).toBeNull();
    expect(obligationCaseOf(closeoutStagesOf('COMPLETED', [completionEffected()], []))).toEqual({
      completionCaseId: completionEffected().id,
      closureCaseId: null,
    });
    const terminal = closureCase({
      completionCaseId: null,
      outcome: 'TERMINATED_WITHOUT_COMPLETION',
    });
    expect(obligationCaseOf(closeoutStagesOf('SUSPENDED', [], [terminal]))).toEqual({
      completionCaseId: null,
      closureCaseId: terminal.id,
    });
  });

  test('the Project Manager moves an obligation; AHDA waives it; a settled one or a closed project offers nothing', () => {
    const project = concernProject();
    expect(obligationCommands(nora, project, obligation())).toEqual(['start', 'satisfy', 'cancel']);
    expect(obligationCommands(nora, project, obligation({ status: 'IN_PROGRESS' }))).toEqual([
      'satisfy',
      'cancel',
    ]);
    expect(obligationCommands(faisal, project, obligation())).toEqual(['waive']);
    expect(obligationCommands(nora, project, obligation({ status: 'SATISFIED' }))).toEqual([]);
    expect(obligationCommands(faisal, concernProject({ status: 'CLOSED' }), obligation())).toEqual(
      [],
    );
  });

  test('the completion date is never after today; the narrative and date are sent as typed, never computed', () => {
    expect(
      checkCloseoutForm(
        { actualProjectCompletionDate: '2026-10-09', narrative: '' },
        'completion',
        '2026-10-08',
      ).actualProjectCompletionDate,
    ).toBe('NOT_ALLOWED');
    expect(
      toCompletionRequest(
        { actualProjectCompletionDate: '2026-10-06', narrative: '  Handed over. ' },
        'en',
        null,
      ),
    ).toEqual({
      actualProjectCompletionDate: '2026-10-06',
      completionNarrative: { text: 'Handed over.', language: 'en' },
    });
  });
});

describe('suspension and resumption (WF-09)', () => {
  test('the register views: open, approved pending activation, in effect, historical', () => {
    expect(suspensionViewOf(suspensionRequest())).toBe('OPEN');
    expect(suspensionViewOf(suspensionRequest({ status: 'APPROVED' }))).toBe('PENDING_ACTIVATION');
    expect(suspensionViewOf(suspensionInEffect())).toBe('IN_EFFECT');
    const resumed = suspensionInEffect();
    expect(
      suspensionViewOf({
        ...resumed,
        suspension: resumed.suspension && {
          ...resumed.suspension,
          endedAt: '2026-11-01T00:00:00Z',
        },
      }),
    ).toBe('HISTORICAL');
    expect(suspensionViewOf(suspensionRequest({ requestType: 'RESUME', status: 'EFFECTED' }))).toBe(
      'HISTORICAL',
    );
  });

  test('a suspension is of an ACTIVE project, a resumption of a SUSPENDED one, one open request of each type', () => {
    const active = concernProject();
    const suspended = concernProject({ status: 'SUSPENDED' });
    expect(canRaiseSuspensionRequest(nora, active, [], 'SUSPEND')).toBe(true);
    expect(canRaiseSuspensionRequest(nora, active, [], 'RESUME')).toBe(false);
    expect(canRaiseSuspensionRequest(nora, active, [suspensionRequest()], 'SUSPEND')).toBe(false);
    expect(
      canRaiseSuspensionRequest(
        nora,
        active,
        [suspensionRequest({ status: 'APPROVED' })],
        'SUSPEND',
      ),
    ).toBe(false);
    expect(canRaiseSuspensionRequest(nora, suspended, [suspensionInEffect()], 'RESUME')).toBe(true);
    expect(canRaiseSuspensionRequest(faisal, suspended, [], 'RESUME')).toBe(false);
  });

  test('submission needs an effective date from today (UTC); a resumption carries no planned resumption', () => {
    const values = { reason: 'Dispute', requestedEffectiveDate: '', plannedResumptionDate: '' };
    expect(checkSuspensionSubmission(values, 'SUSPEND', '2026-10-08').requestedEffectiveDate).toBe(
      'REQUIRED',
    );
    expect(
      checkSuspensionSubmission(
        { ...values, requestedEffectiveDate: '2026-10-07' },
        'SUSPEND',
        '2026-10-08',
      ).requestedEffectiveDate,
    ).toBe('NOT_ALLOWED');
    expect(
      checkSuspensionSubmission(
        { ...values, requestedEffectiveDate: '2026-10-08', plannedResumptionDate: '2026-10-08' },
        'SUSPEND',
        '2026-10-08',
      ).plannedResumptionDate,
    ).toBe('OUT_OF_RANGE');
    expect(
      toSuspensionRequest(
        { ...values, requestedEffectiveDate: '2026-10-08', plannedResumptionDate: '2026-12-01' },
        'RESUME',
        'en',
        null,
      ).plannedResumptionDate,
    ).toBeNull();
  });
});

describe('who is offered what (navigation only; the API decides)', () => {
  test('the raiser edits and withdraws; AHDA starts the review and activates, never the raiser', () => {
    const project = concernProject();
    const submitted = suspensionRequest({ status: 'SUBMITTED' });
    expect(canEditRecord(nora, suspensionRequest(), project)).toBe(true);
    expect(canWithdrawRecord(nora, submitted, project, SUSPENSION_WITHDRAWABLE)).toBe(true);
    expect(canWithdrawRecord(nora, suspensionRequest(), project, SUSPENSION_WITHDRAWABLE)).toBe(
      false,
    );
    expect(canStartReview(faisal, submitted, project)).toBe(true);
    expect(canStartReview(nora, submitted, project)).toBe(false);
    expect(canActivate(faisal, suspensionRequest({ status: 'APPROVED' }), project)).toBe(true);
    expect(canActivate(nora, suspensionRequest({ status: 'APPROVED' }), project)).toBe(false);
  });

  test('nothing is offered on a CLOSED project (TASK-063 D-8)', () => {
    const closed = concernProject({ status: 'CLOSED' });
    expect(canEditRecord(nora, completionCase(), closed)).toBe(false);
    expect(canStartReview(faisal, completionCase({ status: 'SUBMITTED' }), closed)).toBe(false);
    expect(canActivate(faisal, completionCase({ status: 'APPROVED' }), closed)).toBe(false);
  });

  test('no review of progress or financials is offered on a CLOSED project', () => {
    const submission = {
      status: 'SUBMITTED',
      submittedByUserId: nora.id,
    } as ProgressSubmissionDetail;
    expect(canReview(faisal, concernProject(), submission)).toBe(true);
    expect(canReview(faisal, concernProject({ status: 'CLOSED' }), submission)).toBe(false);
    const update = {
      status: 'SUBMITTED',
      submittedByUserId: nora.id,
    } as FinancialProgressUpdateDetail;
    expect(canReviewFinancials(faisal, concernProject(), update)).toBe(true);
    expect(canReviewFinancials(faisal, concernProject({ status: 'CLOSED' }), update)).toBe(false);
  });
});
