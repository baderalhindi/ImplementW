import { describe, expect, test } from 'vitest';

import { entitySession, projectSummary, reviewerSession } from '@/test/projectFixtures.ts';
import {
  CERTIFICATE_TYPE_ID,
  coastalMilestones,
  coastalRevisions,
  DESIGN_ID,
  FOUNDATIONS_ID,
  HANDOVER_ID,
  milestone,
  milestoneProjects,
  pendingEvidence,
  PHOTO_TYPE_ID,
  photoLink,
  requiredEvidence,
  revision,
} from '@/test/milestoneFixtures.ts';
import { day } from '@/test/taskFixtures.ts';
import { todayUtc } from '@/features/tasks/taskRules.ts';

import { canChangeMilestone, canClaimAchievement, canPlanMilestones } from './access.ts';
import {
  achievementState,
  checkClaim,
  checkMilestoneForm,
  EMPTY_MILESTONE_FORM,
  evidenceAllowsSubmission,
  evidenceRule,
  liveEvidence,
  matchesFilter,
  type MilestoneEntry,
  nextClaimKind,
  openRevision,
  overdueDays,
  revisionsOf,
  toMilestoneRequest,
} from './milestoneRules.ts';

// The rules MOD-016, MOD-019, SCR-046 and SCR-062 apply before anything is sent (TASK-051), mirroring TASK-050.

const today = todayUtc();
const huda = entitySession().user;
const faisal = reviewerSession().user;
const coastal = projectSummary({ status: 'ACTIVE', projectManagerUserId: huda.id });

function entry(id: string): MilestoneEntry {
  const found = coastalMilestones().find((candidate) => candidate.id === id);
  if (found === undefined) {
    throw new Error(id);
  }
  return {
    milestone: found,
    project: milestoneProjects()[0] ?? coastal,
    revisions: revisionsOf(id, coastalRevisions()),
  };
}

describe('where a claim stands', () => {
  test('from the newest revision; an accepted one is kept while a correction is prepared, reviewed or returned', () => {
    expect(achievementState([]).kind).toBe('unclaimed');
    expect(achievementState(revisionsOf(FOUNDATIONS_ID, coastalRevisions())).kind).toBe('draft');
    expect(achievementState(revisionsOf(DESIGN_ID, coastalRevisions())).kind).toBe('returned');
    expect(achievementState(revisionsOf(HANDOVER_ID, coastalRevisions())).kind).toBe('accepted');

    const accepted = revision({ id: 'r1', status: 'ACCEPTED', isCurrent: true });
    const correction = revision({ id: 'r2', revisionNo: 2, status: 'SUBMITTED' });
    const state = achievementState(revisionsOf(FOUNDATIONS_ID, [accepted, correction]));
    expect(state.kind).toBe('submitted');
    expect(state.kind === 'submitted' && state.accepted?.id).toBe('r1');
  });

  test('the next claim: none while one is open or the milestone is cancelled; after a return; a correction once accepted', () => {
    const planned = milestone();
    expect(nextClaimKind(planned, [])).toBe('first');
    expect(nextClaimKind(planned, [revision()])).toBeNull();
    expect(nextClaimKind(planned, [revision({ status: 'RETURNED' })])).toBe('afterReturn');
    expect(
      nextClaimKind(milestone({ status: 'ACHIEVED' }), [
        revision({ status: 'ACCEPTED', isCurrent: true }),
      ]),
    ).toBe('correction');
    expect(nextClaimKind(milestone({ status: 'CANCELLED' }), [])).toBeNull();
    expect(openRevision([revision({ status: 'SUBMITTED' })])?.status).toBe('SUBMITTED');
    expect(openRevision([revision({ status: 'RETURNED' })])).toBeNull();
  });
});

describe('criterion 1: the evidence rule is the one the server states', () => {
  test('no EVIDENCE_POLICY published is "pending": nothing is mandatory and submission is allowed', () => {
    const rule = evidenceRule(pendingEvidence());
    expect(rule.kind).toBe('pending');
    expect(evidenceAllowsSubmission(rule)).toBe(true);
  });

  test('a mandatory type not held blocks submission and is named; once held it does not', () => {
    const missing = evidenceRule(requiredEvidence());
    expect(missing).toEqual({
      kind: 'required',
      mandatory: [CERTIFICATE_TYPE_ID],
      missing: [CERTIFICATE_TYPE_ID],
    });
    expect(evidenceAllowsSubmission(missing)).toBe(false);

    const held = evidenceRule(
      requiredEvidence({ satisfiedEvidenceTypeItemIds: [CERTIFICATE_TYPE_ID, PHOTO_TYPE_ID] }),
    );
    expect(held.kind === 'required' && held.missing).toEqual([]);
    expect(evidenceAllowsSubmission(held)).toBe(true);
  });

  test('a policy that requires nothing for the category, and an undetermined one', () => {
    expect(evidenceRule(pendingEvidence({ evidencePolicyVersionId: 'v1' })).kind).toBe(
      'notRequired',
    );
    expect(evidenceAllowsSubmission({ kind: 'undetermined' })).toBe(false);
  });

  test('only VALID evidence on an active link is in force', () => {
    const ended = photoLink({ id: 'ended', unlinkedAt: '2026-10-01T00:00:00Z' });
    const live = photoLink();
    const withdrawn = photoLink({
      id: 'withdrawn',
      evidence: photoLink().evidence.map((piece) => ({ ...piece, id: 'w', status: 'WITHDRAWN' })),
    });
    expect(
      liveEvidence(pendingEvidence({ links: [ended, live, withdrawn] })).map((piece) => piece.id),
    ).toEqual([photoLink().evidence[0]?.id]);
  });
});

describe('dates and filters', () => {
  test('a PLANNED milestone is overdue once its forecast has passed, not on the day itself', () => {
    expect(overdueDays(milestone({ forecastDate: day(-2) }), today)).toBe(2);
    expect(overdueDays(milestone({ forecastDate: today }), today)).toBeNull();
    expect(overdueDays(milestone({ forecastDate: day(-2), status: 'ACHIEVED' }), today)).toBeNull();
  });

  test('a claim is dated, not after today', () => {
    expect(
      checkClaim({ claimedAchievementDate: '', narrative: '' }, today).claimedAchievementDate,
    ).toBe('REQUIRED');
    expect(
      checkClaim({ claimedAchievementDate: day(1), narrative: '' }, today).claimedAchievementDate,
    ).toBe('DATE_IN_FUTURE');
    expect(
      checkClaim({ claimedAchievementDate: today, narrative: '' }, today).claimedAchievementDate,
    ).toBeNull();
  });

  test('SCR-062 filters', () => {
    expect(matchesFilter(entry(DESIGN_ID), 'overdue', today)).toBe(true);
    expect(matchesFilter(entry(DESIGN_ID), 'returned', today)).toBe(true);
    expect(matchesFilter(entry(FOUNDATIONS_ID), 'draft', today)).toBe(true);
    expect(matchesFilter(entry(FOUNDATIONS_ID), 'overdue', today)).toBe(false);
    expect(matchesFilter(entry(HANDOVER_ID), 'achieved', today)).toBe(true);
    expect(matchesFilter(entry(HANDOVER_ID), 'planned', today)).toBe(false);
  });
});

describe('MOD-016', () => {
  test('a title, a category and a forecast date are required; a sort order is a whole number from 0', () => {
    expect(checkMilestoneForm(EMPTY_MILESTONE_FORM)).toEqual({
      title: 'REQUIRED',
      milestoneCategoryItemId: 'REQUIRED',
      forecastDate: 'REQUIRED',
      sortOrder: null,
    });
    expect(checkMilestoneForm({ ...EMPTY_MILESTONE_FORM, sortOrder: '-1' }).sortOrder).toBe(
      'OUT_OF_RANGE',
    );
    expect(checkMilestoneForm({ ...EMPTY_MILESTONE_FORM, sortOrder: '1.5' }).sortOrder).toBe(
      'OUT_OF_RANGE',
    );
  });

  test('the request sends no activity as null and a blank sort order as 0', () => {
    expect(
      toMilestoneRequest(
        {
          title: ' Design approval ',
          milestoneCategoryItemId: 'cat',
          forecastDate: '2026-12-01',
          scheduleActivityId: '',
          sortOrder: '',
        },
        'en',
        null,
      ),
    ).toEqual({
      title: { text: 'Design approval', language: 'en' },
      milestoneCategoryItemId: 'cat',
      forecastDate: '2026-12-01',
      scheduleActivityId: null,
      sortOrder: 0,
    });
  });
});

describe('access (navigation; the API decides again)', () => {
  test('the project’s own Project Manager plans while APPROVED_PLANNED or ACTIVE, and claims while ACTIVE', () => {
    expect(canPlanMilestones(huda, coastal)).toBe(true);
    expect(canPlanMilestones(huda, { ...coastal, status: 'APPROVED_PLANNED' })).toBe(true);
    expect(canPlanMilestones(faisal, coastal)).toBe(false);
    expect(canClaimAchievement(huda, coastal)).toBe(true);
    expect(canClaimAchievement(huda, { ...coastal, status: 'APPROVED_PLANNED' })).toBe(false);
    expect(canClaimAchievement(faisal, coastal)).toBe(false);
    expect(canChangeMilestone(huda, coastal, { status: 'PLANNED' })).toBe(true);
    expect(canChangeMilestone(huda, coastal, { status: 'ACHIEVED' })).toBe(false);
  });
});
