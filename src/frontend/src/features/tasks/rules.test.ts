import { describe, expect, test } from 'vitest';

import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { entitySession, ENTITY_USER_ID, OTHER_PERSON_ID } from '@/test/projectFixtures.ts';
import {
  BRIDGE_ID,
  CLEARING_ID,
  coastalDependencies,
  coastalTasks,
  day,
  dependency,
  DRAINAGE_ID,
  FENCING_ID,
  harbourTasks,
  PARENT_ID,
  PLOT_SURVEY_ID,
  task,
  taskProjects,
} from '@/test/taskFixtures.ts';

import { canExecuteTask, canManageTasks, taskCommands, type TaskRights } from './access.ts';
import { type ProjectTaskDetail, type TaskDependencyType } from './api/types.ts';
import { assigneeOf, assigneeValue } from './assignee.ts';
import {
  checkPercent,
  checkTaskDependency,
  checkTaskForm,
  emptyTaskForm,
  isMet,
  listEntries,
  overdueDays,
  STALE_UPDATE_DAYS,
  type TaskEntry,
  todayUtc,
  unmetDependencies,
  updateReasons,
} from './taskRules.ts';

// WF-04's rules as the screens apply them (TASK-048 D-4 to D-8) and the four lists' definitions (SCR-063–066).

const today = todayUtc();
const me = entitySession().user;

function entries(): TaskEntry[] {
  const [coastal, harbour] = taskProjects();
  if (coastal === undefined || harbour === undefined) {
    throw new Error('Two projects expected.');
  }
  return [
    ...coastalTasks().map((item) => ({ task: item, project: coastal })),
    ...harbourTasks().map((item) => ({ task: item, project: harbour })),
  ];
}

const titles = (list: TaskEntry[]) => list.map((entry) => entry.task.title.text);

function coastal(id: string): ProjectTaskDetail {
  const found = coastalTasks().find((item) => item.id === id);
  if (found === undefined) {
    throw new Error(`No fixture task ${id}.`);
  }
  return found;
}

describe('overdue (acceptance criterion 1)', () => {
  test('an open task whose planned finish has passed is overdue by the whole days since', () => {
    expect(overdueDays(task({ plannedFinishDate: day(-3) }), today)).toBe(3);
    expect(overdueDays(task({ status: 'BLOCKED', plannedFinishDate: day(-1) }), today)).toBe(1);
  });

  test('a task due today, or completed or cancelled late, is not overdue', () => {
    expect(overdueDays(task({ plannedFinishDate: today }), today)).toBeNull();
    expect(
      overdueDays(task({ status: 'COMPLETED', plannedFinishDate: day(-5) }), today),
    ).toBeNull();
    expect(
      overdueDays(task({ status: 'CANCELLED', plannedFinishDate: day(-5) }), today),
    ).toBeNull();
  });
});

describe('updates required (SCR-066)', () => {
  test('a start that is due, a started leaf without a figure, and a figure unchanged for a week', () => {
    expect(updateReasons(task({ plannedStartDate: day(-2) }), today)).toEqual([
      { kind: 'notStarted', since: day(-2) },
    ]);
    expect(updateReasons(task({ status: 'BLOCKED', actualStartDate: day(-1) }), today)).toEqual([
      { kind: 'noProgress' },
    ]);
    expect(
      updateReasons(
        task({
          status: 'IN_PROGRESS',
          actualPercentComplete: 20,
          updatedAt: `${day(-STALE_UPDATE_DAYS)}T09:00:00Z`,
        }),
        today,
      ),
    ).toEqual([{ kind: 'staleProgress', days: STALE_UPDATE_DAYS }]);
  });

  test('nothing is owed by a task starting today, a fresh figure, a parent, or finished work', () => {
    expect(updateReasons(task({ plannedStartDate: today }), today)).toEqual([]);
    const fresh = task({
      status: 'IN_PROGRESS',
      actualPercentComplete: 20,
      updatedAt: `${day(1 - STALE_UPDATE_DAYS)}T09:00:00Z`,
    });
    expect(updateReasons(fresh, today)).toEqual([]);
    // A parent's percentage is rolled up, never entered (TASK-048 D-7).
    expect(updateReasons(task({ status: 'IN_PROGRESS', subtaskCount: 2 }), today)).toEqual([]);
    expect(updateReasons(task({ status: 'COMPLETED', plannedStartDate: day(-9) }), today)).toEqual(
      [],
    );
  });
});

describe('the four lists (SCR-063–066)', () => {
  test('My tasks: every task the person owns, in any project, most overdue first, then by planned finish', () => {
    expect(titles(listEntries('mine', entries(), me.id, today))).toEqual([
      'Inspect the bridge', // overdue by 1 day, on Faisal's project
      'Clear the site', // completed: not overdue, finished earliest
      'Survey the plot',
      'Site preparation',
      'Lay drainage',
    ]);
  });

  test('Team tasks: every task of the projects the person manages, whoever owns it', () => {
    const team = titles(listEntries('team', entries(), me.id, today));
    expect(team).toContain('Fence the site'); // owned by Nora
    expect(team).not.toContain('Inspect the bridge'); // Faisal's project
    expect(team).toHaveLength(5);
  });

  test('Overdue: the person’s own and their projects’ open tasks past their finish, most overdue first', () => {
    expect(titles(listEntries('overdue', entries(), me.id, today))).toEqual([
      'Fence the site',
      'Inspect the bridge',
    ]);
  });

  test('Updates required: due starts and missing or stale figures, among the person’s own and their projects’', () => {
    expect(titles(listEntries('updates', entries(), me.id, today)).sort()).toEqual([
      'Fence the site',
      'Lay drainage',
      'Survey the plot',
    ]);
    // Nora's railings on Faisal's project are neither Huda's nor her project's.
    expect(titles(listEntries('updates', entries(), OTHER_PERSON_ID, today))).toContain(
      'Paint the railings',
    );
  });
});

describe('dependencies (TASK-048 D-6)', () => {
  test('F is met by completion, S by an actual start', () => {
    const started = task({ status: 'BLOCKED', actualStartDate: day(-1) });
    expect(isMet('FS', started)).toBe(false);
    expect(isMet('SS', started)).toBe(true);
    expect(isMet('SF', task())).toBe(false);
    expect(isMet('FF', task({ status: 'COMPLETED' }))).toBe(true);
  });

  test('FS and SS hold back the start, FF and SF the completion', () => {
    const tasks = coastalTasks();
    const links = [
      dependency(),
      dependency({ id: 'x', predecessorTaskId: FENCING_ID, dependencyType: 'FF' }),
    ];
    expect(unmetDependencies(PLOT_SURVEY_ID, true, tasks, links).map((d) => d.id)).toEqual([
      dependency().id,
    ]);
    expect(unmetDependencies(PLOT_SURVEY_ID, false, tasks, links).map((d) => d.id)).toEqual(['x']);
  });

  test('MOD-015 refuses, in the API’s order, a missing end, the same task, a pair already linked, a cycle, a broken link', () => {
    const tasks = coastalTasks();
    const links = coastalDependencies();
    const check = (
      predecessorTaskId: string,
      successorTaskId: string,
      type: TaskDependencyType = 'FS',
    ) =>
      checkTaskDependency(
        { predecessorTaskId, successorTaskId, dependencyType: type },
        tasks,
        links,
      );

    expect(check('', PLOT_SURVEY_ID).codes.predecessorTaskId).toBe('REQUIRED');
    expect(check(DRAINAGE_ID, DRAINAGE_ID).codes.successorTaskId).toBe('SAME_TASK');
    expect(check(DRAINAGE_ID, PLOT_SURVEY_ID).codes.successorTaskId).toBe('DEPENDENCY_EXISTS');
    const cycle = check(PLOT_SURVEY_ID, DRAINAGE_ID);
    expect(cycle.codes.successorTaskId).toBe('CIRCULAR');
    expect(cycle.cycle).toEqual([DRAINAGE_ID, PLOT_SURVEY_ID]);
    // Fencing has started, so a new FS from the not-yet-started survey would be broken on arrival.
    expect(check(PLOT_SURVEY_ID, FENCING_ID).codes.successorTaskId).toBe('DEPENDENCY_UNMET');
    // SS from Drainage (started) to Fencing is met already.
    expect(check(DRAINAGE_ID, FENCING_ID, 'SS').codes.successorTaskId).toBeNull();
  });
});

describe('what MOD-012 offers (TASK-048 §3)', () => {
  const all: TaskRights = { manage: true, execute: true, reopen: true };
  const executeOnly: TaskRights = { manage: true, execute: true, reopen: false };
  const commands = (
    item: ProjectTaskDetail,
    rights = all,
    parent: ProjectTaskDetail | null = null,
  ) => taskCommands(item, rights, parent, coastalDependencies());

  test('a blocked task is never offered completion: it is unblocked first', () => {
    expect(commands(coastal(DRAINAGE_ID))).toEqual(['unblock', 'reportProgress']);
  });

  test('reopening needs the reopen right, not edit or execution rights', () => {
    const done = task({ status: 'COMPLETED' });
    expect(commands(done)).toEqual(['reopen']);
    expect(commands(done, executeOnly)).toEqual([]);
  });

  test('a subtask is not reopened under a completed parent', () => {
    const clearing = coastal(CLEARING_ID);
    expect(commands(clearing, all, task({ id: PARENT_ID, status: 'IN_PROGRESS' }))).toEqual([
      'reopen',
    ]);
    expect(commands(clearing, all, task({ id: PARENT_ID, status: 'COMPLETED' }))).toEqual([]);
  });

  test('only a free task is cancelled: no live subtask, no dependency', () => {
    expect(commands(task({ id: 'free' }))).toEqual(['start', 'block', 'cancel']);
    expect(commands(task())).toEqual(['start', 'block']); // the survey waits for drainage
    expect(commands(task({ id: 'parent', status: 'IN_PROGRESS', subtaskCount: 1 }))).toEqual([
      'complete',
      'block',
    ]);
  });

  test('planning is the Project Manager’s; execution theirs and the owner’s, on an ACTIVE project only', () => {
    const user: SessionUser = me;
    expect(canManageTasks(user, { status: 'ACTIVE', projectManagerUserId: ENTITY_USER_ID })).toBe(
      true,
    );
    expect(canManageTasks(user, { status: 'ACTIVE', projectManagerUserId: OTHER_PERSON_ID })).toBe(
      false,
    );
    expect(
      canExecuteTask(user, { status: 'ACTIVE', projectManagerUserId: OTHER_PERSON_ID }, task()),
    ).toBe(true);
    expect(
      canExecuteTask(
        user,
        { status: 'APPROVED_PLANNED', projectManagerUserId: ENTITY_USER_ID },
        task(),
      ),
    ).toBe(false);
    const bridge = harbourTasks().find((item) => item.id === BRIDGE_ID);
    expect(bridge?.assigneeUserId).toBe(ENTITY_USER_ID);
  });
});

describe('the forms', () => {
  test('a finish before the start is refused on the finish; a blank title is required', () => {
    const codes = checkTaskForm({ ...emptyTaskForm(day(5), day(4)), title: ' ' });
    expect(codes.title).toBe('REQUIRED');
    expect(codes.plannedFinishDate).toBe('DATE_BEFORE_START');
    expect(
      checkTaskForm({ ...emptyTaskForm(day(4), day(4)), title: 'A' }).plannedFinishDate,
    ).toBeNull();
  });

  test('a percentage is 0 to 100 to four places (ADR-009)', () => {
    expect(checkPercent('')).toBe('REQUIRED');
    expect(checkPercent('-1')).toBe('MALFORMED');
    expect(checkPercent('42.12345')).toBe('MALFORMED');
    expect(checkPercent('100.5')).toBe('OUT_OF_RANGE');
    expect(checkPercent('100')).toBeNull();
    expect(checkPercent('62.5')).toBeNull();
  });

  test('an owner given by user id must be one; someone else must be chosen', () => {
    const other = { ...assigneeValue(null), choice: 'other' };
    expect(assigneeOf(assigneeValue(null), false)).toEqual({ userId: null, code: null });
    expect(assigneeOf(other, false).code).toBe('REQUIRED');
    expect(assigneeOf({ ...other, typedId: 'sami' }, false).code).toBe('MALFORMED');
    expect(assigneeOf({ ...other, typedId: ` ${OTHER_PERSON_ID.toUpperCase()} ` }, false)).toEqual({
      userId: OTHER_PERSON_ID,
      code: null,
    });
    expect(assigneeOf(other, true).code).toBe('REQUIRED');
  });
});
