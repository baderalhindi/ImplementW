import { describe, expect, test } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import {
  ENTITY_USER_ID,
  entitySession,
  projectDetail,
  REVIEWER_ID,
  reviewerSession,
} from '@/test/projectFixtures.ts';
import {
  activity,
  baseline,
  baselinedActivities,
  candidate,
  chain,
  dependency,
  EXCAVATION_ID,
  PAVING_ID,
  plannedActivities,
  scheduleTask,
  SUMMARY_ID,
  SURVEY_ID,
} from '@/test/scheduleFixtures.ts';

import {
  canDecideBaseline,
  candidateOf,
  canEditSchedule,
  isPlanFrozen,
  needsChangeAuthorization,
} from './access.ts';
import { checkActivity, checkDate, parentChoices, toActivityRequest } from './activityForm.ts';
import {
  addDays,
  checkDays,
  checkDependency,
  cyclePath,
  dayNumber,
  daySpan,
  dependencyConflicts,
  latestFinish,
  moveDates,
  startConstraint,
} from './dependencyRules.ts';
import {
  activityTree,
  barPosition,
  daysForDistance,
  descendantsOf,
  MAX_TICKS,
  ticksOf,
  timelineOf,
} from './gantt.ts';
import { varianceLabel } from './presentation.ts';

// The rules behind SCR-060, SCR-045 and SCR-061 (TASK-047), each mirroring the WF-03 backend (TASK-046 D-3, D-4, D-5).

const t = (key: string, params?: Record<string, string | number>) => translate('en', key, params);

describe('dates are UTC calendar dates; every day is a working day (TASK-046 F-3, F-13)', () => {
  test('adding days crosses months and years, and a span counts both ends', () => {
    expect(addDays('2026-11-30', 1)).toBe('2026-12-01');
    expect(addDays('2026-12-31', 1)).toBe('2027-01-01');
    expect(addDays('2026-03-01', -1)).toBe('2026-02-28');
    expect(daySpan('2026-11-01', '2026-11-05')).toBe(5);
    expect(dayNumber('1970-01-02')).toBe(1);
  });
});

describe('a cycle is found before anything is sent (acceptance criterion 2, VAL-SCH-008)', () => {
  const edges = chain(); // 1.1 → 1.2 → 2

  test('a dependency closing a chain back to its start is a cycle, and the chain is named', () => {
    expect(cyclePath(edges, PAVING_ID, SURVEY_ID)).toEqual([SURVEY_ID, EXCAVATION_ID, PAVING_ID]);
    expect(cyclePath(edges, EXCAVATION_ID, SURVEY_ID)).toEqual([SURVEY_ID, EXCAVATION_ID]);
  });

  test('a dependency along the chain, or between unlinked activities, is no cycle', () => {
    expect(cyclePath(edges, SURVEY_ID, PAVING_ID)).toBeNull();
    expect(cyclePath([], SURVEY_ID, PAVING_ID)).toBeNull();
  });

  test('an activity depending on itself is a cycle of one', () => {
    expect(cyclePath(edges, SURVEY_ID, SURVEY_ID)).toEqual([SURVEY_ID]);
  });

  test('MOD-015 refuses, in the API order: missing ends, the same activity, a linked pair, a cycle, a bad lag', () => {
    const check = (values: Partial<Parameters<typeof checkDependency>[0]>) =>
      checkDependency(
        {
          predecessorActivityId: '',
          successorActivityId: '',
          dependencyType: 'FS',
          lagDays: '0',
          ...values,
        },
        edges,
      );
    expect(check({}).codes).toMatchObject({
      predecessorActivityId: 'REQUIRED',
      successorActivityId: 'REQUIRED',
    });
    expect(
      check({ predecessorActivityId: SURVEY_ID, successorActivityId: SURVEY_ID }).codes
        .successorActivityId,
    ).toBe('SAME_ACTIVITY');
    // The pair is unique whatever the type: SS on an existing FS pair is still linked.
    expect(
      check({
        predecessorActivityId: SURVEY_ID,
        successorActivityId: EXCAVATION_ID,
        dependencyType: 'SS',
      }).codes.successorActivityId,
    ).toBe('DEPENDENCY_EXISTS');
    const circular = check({ predecessorActivityId: PAVING_ID, successorActivityId: SURVEY_ID });
    expect(circular.codes.successorActivityId).toBe('CIRCULAR');
    expect(circular.cycle).toEqual([SURVEY_ID, EXCAVATION_ID, PAVING_ID]);
    const valid = check({ predecessorActivityId: SURVEY_ID, successorActivityId: PAVING_ID });
    expect(Object.values(valid.codes).every((code) => code === null)).toBe(true);
    expect(check({ dependencyType: '' }).codes.dependencyType).toBe('REQUIRED');
  });

  test.each([
    ['', null],
    ['0', null],
    ['9999', null],
    ['10000', 'OUT_OF_RANGE'],
    ['-1', 'MALFORMED'],
    ['1.5', 'MALFORMED'],
    ['two', 'MALFORMED'],
  ])('a lag of "%s" is %s', (lag, code) => {
    expect(checkDays(lag, { min: 0, required: false })).toBe(code);
  });
});

describe('the earliest start a dependency allows, as ScheduleCalculation computes it (D-3)', () => {
  const activities = baselinedActivities();

  test('FS: the working day after the predecessor finishes, moved by the lag', () => {
    expect(startConstraint(EXCAVATION_ID, 10, activities, chain(), 'plan')?.earliest).toBe(
      '2026-11-06',
    );
    expect(
      startConstraint(EXCAVATION_ID, 10, activities, [dependency({ lagDays: 3 })], 'plan')
        ?.earliest,
    ).toBe('2026-11-09');
  });

  test('SS: the predecessor start, moved by the lag', () => {
    const ss = [dependency({ dependencyType: 'SS', lagDays: 2 })];
    expect(startConstraint(EXCAVATION_ID, 10, activities, ss, 'plan')?.earliest).toBe('2026-11-03');
  });

  test('FF: no finish before the predecessor finishes, so the start is that finish less the duration', () => {
    const ff = [dependency({ dependencyType: 'FF' })];
    // 1.1 finishes 2026-11-05; a 10-day activity finishing then starts 2026-10-27.
    expect(startConstraint(EXCAVATION_ID, 10, activities, ff, 'plan')?.earliest).toBe('2026-10-27');
  });

  test('the forecast track reads the predecessors’ forecasts, the plan track their plans', () => {
    // 1.2 is forecast to finish 2026-11-17 against a plan of 2026-11-15.
    expect(startConstraint(PAVING_ID, 10, activities, chain(), 'forecast')?.earliest).toBe(
      '2026-11-18',
    );
    expect(startConstraint(PAVING_ID, 10, activities, chain(), 'plan')?.earliest).toBe(
      '2026-11-16',
    );
  });

  test('an activity with no predecessor has no constraint; the latest of several binds', () => {
    expect(startConstraint(SURVEY_ID, 5, activities, chain(), 'plan')).toBeNull();
    const two = [
      dependency({ predecessorActivityId: SURVEY_ID, successorActivityId: PAVING_ID }),
      dependency({
        predecessorActivityId: EXCAVATION_ID,
        successorActivityId: PAVING_ID,
        lagDays: 1,
      }),
    ];
    const binding = startConstraint(PAVING_ID, 10, activities, two, 'plan');
    expect(binding?.earliest).toBe('2026-11-17');
    expect(binding?.dependency.predecessorActivityId).toBe(EXCAVATION_ID);
  });
});

describe('a drag keeps the duration and never breaks a dependency (SCR-045)', () => {
  const activities = baselinedActivities();
  const paving = activities.find((item) => item.id === PAVING_ID) ?? activity();

  test('a move later is taken as dragged, duration kept', () => {
    expect(moveDates(paving, 5, activities, chain(), 'forecast')).toEqual({
      start: '2026-11-23',
      finish: '2026-12-02',
      heldBy: null,
    });
  });

  test('a move before the earliest start is held there, with the dependency that holds it', () => {
    const moved = moveDates(paving, -4, activities, chain(), 'forecast');
    expect(moved.start).toBe('2026-11-18');
    expect(moved.finish).toBe('2026-11-27');
    expect(moved.heldBy?.dependency.predecessorActivityId).toBe(EXCAVATION_ID);
  });

  test('a forecast left before what its predecessors’ forecasts allow is a conflict; the plan never is', () => {
    const slipped = activities.map((item) =>
      item.id === EXCAVATION_ID ? { ...item, forecastFinishDate: '2026-11-20' } : item,
    );
    expect([...dependencyConflicts(slipped, chain(), 'forecast')]).toEqual([PAVING_ID]);
    expect(dependencyConflicts(activities, chain(), 'forecast').size).toBe(0);
    expect(dependencyConflicts(plannedActivities(), chain(), 'plan').size).toBe(0);
  });

  test('the project finish is the latest live leaf’s, a cancelled one excluded', () => {
    expect(latestFinish(activities, 'plan')).toBe('2026-11-25');
    expect(latestFinish(activities, 'forecast')).toBe('2026-11-27');
    const cancelled = activities.map((item) =>
      item.id === PAVING_ID ? { ...item, status: 'CANCELLED' as const } : item,
    );
    expect(latestFinish(cancelled, 'forecast')).toBe('2026-11-17');
    expect(latestFinish([], 'plan')).toBeNull();
  });
});

describe('the Gantt geometry is in per cent of the timeline, so it fits any width (criterion 3)', () => {
  test('the work breakdown is depth first, siblings in the API order, an orphan at the top level', () => {
    const rows = activityTree(baselinedActivities());
    expect(rows.map((row) => [row.activity.wbsCode, row.depth])).toEqual([
      ['1', 0],
      ['1.1', 1],
      ['1.2', 1],
      ['2', 0],
    ]);
    const orphan = activity({ id: 'x', parentActivityId: 'missing', wbsCode: '9' });
    expect(activityTree([orphan])[0]?.depth).toBe(0);
  });

  test('a bar is placed by its offset and length over the timeline', () => {
    const timeline = timelineOf([
      { start: '2026-11-01', finish: '2026-11-10' },
      { start: '2026-11-06', finish: '2026-11-20' },
    ]);
    expect(timeline).toEqual({ first: dayNumber('2026-11-01'), days: 20 });
    if (timeline === null) {
      return;
    }
    expect(barPosition(timeline, '2026-11-01', '2026-11-20')).toEqual({ offset: 0, width: 100 });
    expect(barPosition(timeline, '2026-11-06', '2026-11-10')).toEqual({ offset: 25, width: 25 });
    expect(timelineOf([])).toBeNull();
  });

  test('a pointer distance becomes whole days in proportion to the track width', () => {
    const timeline = { first: 0, days: 40 };
    expect(daysForDistance(50, 400, timeline)).toBe(5);
    expect(daysForDistance(-26, 400, timeline)).toBe(-3);
    expect(daysForDistance(50, 0, timeline)).toBe(0);
  });

  test('the axis is weekly when short, monthly when long, never more than MAX_TICKS labels', () => {
    const short = ticksOf({ first: dayNumber('2026-11-01'), days: 28 });
    expect(short.map((tick) => tick.date)).toEqual([
      '2026-11-01',
      '2026-11-08',
      '2026-11-15',
      '2026-11-22',
    ]);
    const year = ticksOf({ first: dayNumber('2026-11-10'), days: 365 });
    expect(year[0]?.date).toBe('2026-12-01');
    expect(year.every((tick) => tick.date.endsWith('-01'))).toBe(true);
    const decade = ticksOf({ first: dayNumber('2026-01-01'), days: 3650 });
    expect(decade.length).toBeLessThanOrEqual(MAX_TICKS);
    expect(decade.every((tick) => tick.offset >= 0 && tick.offset <= 92)).toBe(true);
  });
});

describe('the activity form checks what ScheduleActivityRequest.Validate checks', () => {
  const valid = {
    parentActivityId: '',
    wbsCode: '3',
    name: 'Drainage',
    requestedStartDate: '2026-12-01',
    plannedDurationDays: '5',
    sortOrder: '4',
  };

  test('a valid activity has no error, and its request carries no calculated date', () => {
    expect(Object.values(checkActivity(valid)).every((code) => code === null)).toBe(true);
    expect(toActivityRequest(valid, 'ar', null)).toEqual({
      parentActivityId: null,
      wbsCode: '3',
      name: { text: 'Drainage', language: 'ar' },
      requestedStartDate: '2026-12-01',
      plannedDurationDays: 5,
      sortOrder: 4,
    });
  });

  test('blank, too long, malformed and out-of-range inputs are refused with the API’s codes', () => {
    expect(
      checkActivity({
        ...valid,
        wbsCode: '',
        name: ' ',
        requestedStartDate: '',
        plannedDurationDays: '0',
        sortOrder: '-1',
      }),
    ).toEqual({
      wbsCode: 'REQUIRED',
      name: 'REQUIRED',
      requestedStartDate: 'REQUIRED',
      plannedDurationDays: 'OUT_OF_RANGE',
      sortOrder: 'MALFORMED',
    });
    expect(checkActivity({ ...valid, wbsCode: 'x'.repeat(51) }).wbsCode).toBe('MAX_LENGTH');
    expect(checkActivity({ ...valid, plannedDurationDays: '10000' }).plannedDurationDays).toBe(
      'OUT_OF_RANGE',
    );
    expect(checkDate('2026-02-30')).toBe('MALFORMED');
    expect(checkDate('2026-02-28')).toBeNull();
  });

  test('an unchanged name keeps the language it was entered in', () => {
    const survey = activity();
    expect(toActivityRequest({ ...valid, name: 'Survey' }, 'ar', survey).name).toEqual({
      text: 'Survey',
      language: 'en',
    });
  });

  test('a parent is never the activity itself, one under it, a dependency end, or cancelled (D-4)', () => {
    const activities = [
      ...baselinedActivities(),
      activity({ id: 'free', wbsCode: '3', parentActivityId: null }),
      activity({ id: 'gone', wbsCode: '4', parentActivityId: null, status: 'CANCELLED' }),
    ];
    const summary = activities[0] ?? activity();
    const ids = (editing: typeof summary | null) =>
      parentChoices(activities, chain(), editing).map((item) => item.id);
    expect(ids(null)).toEqual([SUMMARY_ID, 'free']);
    expect(ids(summary)).toEqual(['free']);
    expect([...descendantsOf(SUMMARY_ID, activities)].sort()).toEqual(
      [SUMMARY_ID, SURVEY_ID, EXCAVATION_ID].sort(),
    );
  });
});

describe('who is offered what (TASK-046 D-12, ADR-013)', () => {
  const active = projectDetail({ status: 'ACTIVE', projectManagerUserId: ENTITY_USER_ID });

  test('only the project’s own Project Manager edits, on an approved or active project', () => {
    expect(canEditSchedule(entitySession().user, active)).toBe(true);
    expect(canEditSchedule(entitySession().user, { ...active, status: 'APPROVED_PLANNED' })).toBe(
      true,
    );
    expect(canEditSchedule(entitySession().user, { ...active, status: 'SUBMITTED' })).toBe(false);
    expect(canEditSchedule(reviewerSession().user, active)).toBe(false);
  });

  test('the candidate on its way, and the freeze while it is with WF-11', () => {
    expect(candidateOf([baseline()])).toBeNull();
    expect(candidateOf([candidate({ status: 'RETURNED' }), baseline()])?.id).toBe(candidate().id);
    expect(isPlanFrozen([candidate({ status: 'SUBMITTED' })])).toBe(true);
    expect(isPlanFrozen([candidate(), baseline()])).toBe(false);
  });

  test('MOD-018 goes to an AHDA person whose inbox decides this revision, never its submitter', () => {
    const submitted = candidate({ status: 'SUBMITTED' });
    const task = scheduleTask();
    expect(canDecideBaseline(reviewerSession().user, submitted, task)).toBe(true);
    expect(canDecideBaseline(entitySession().user, submitted, task)).toBe(false);
    // An AHDA person who submitted it themselves is not offered it either, whatever their reach (WF-11's own rule).
    const own = scheduleTask({ instance: { ...task.instance, requestedByUserId: REVIEWER_ID } });
    expect(canDecideBaseline(reviewerSession().user, submitted, own)).toBe(false);
    expect(canDecideBaseline(reviewerSession().user, submitted, null)).toBe(false);
    expect(canDecideBaseline(reviewerSession().user, candidate(), task)).toBe(false);
    // A task of another revision, or of another subject, decides nothing here.
    expect(canDecideBaseline(reviewerSession().user, { ...submitted, revisionNo: 2 }, task)).toBe(
      false,
    );
    const other = scheduleTask({
      instance: { ...task.instance, subject: { ...task.instance.subject, module: 'Project' } },
    });
    expect(canDecideBaseline(reviewerSession().user, submitted, other)).toBe(false);
  });

  test('a rebaseline names a change authorisation; replacing a Declared Baseline does not (D-13)', () => {
    expect(needsChangeAuthorization(baseline())).toBe(true);
    expect(needsChangeAuthorization(baseline({ baselineType: 'DECLARED' }))).toBe(false);
    expect(needsChangeAuthorization(null)).toBe(false);
  });
});

describe('a variance reads as words, and none is never 0 (TASK-046 D-9)', () => {
  test.each([
    [null, 'No baseline'],
    [0, 'On the baseline'],
    [1, '1 day late'],
    [-1, '1 day early'],
  ])('%s days reads "%s"', (days, text) => {
    expect(varianceLabel(days, t)).toBe(text);
  });

  test('several days are counted', () => {
    expect(varianceLabel(3, t)).toBe(t('schedule.variance.late', { days: 3 }));
    expect(varianceLabel(-2, t)).toBe(t('schedule.variance.early', { days: 2 }));
  });
});
