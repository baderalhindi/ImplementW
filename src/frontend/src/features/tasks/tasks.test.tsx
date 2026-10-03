import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { type Session } from '@/features/identity-access/session/sessionApi.ts';
import { type ProjectDetail } from '@/features/projects/api/types.ts';
import { type Language, translate } from '@/shared/i18n/i18n.ts';
import { type MockApi, mockApi, problem } from '@/test/mockApi.ts';
import {
  ENTITY_USER_ID,
  entitySession,
  OTHER_PERSON_ID,
  projectDetail,
  PROJECT_ID,
  reviewerSession,
  withProject,
  withProjectLookups,
} from '@/test/projectFixtures.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';
import { EXCAVATION_ID, PAVING_ID } from '@/test/scheduleFixtures.ts';
import {
  BRIDGE_ID,
  coastalTasks,
  day,
  DRAINAGE_ID,
  FENCING_ID,
  HIGH_PRIORITY_ID,
  PARENT_ID,
  PLOT_SURVEY_ID,
  task,
  TASK_ETAG,
  taskProjects,
  type TaskState,
  UNRELATED_USER_ID,
  withTasks,
} from '@/test/taskFixtures.ts';

// SCR-047 Project Tasks, SCR-063 My Tasks, SCR-064 Team Tasks, SCR-065 Overdue Tasks, SCR-066 Updates Required and
// MOD-010 to MOD-015 (TASK-049), against the WF-04 API (TASK-048). Acceptance criteria: (1) overdue tasks are told
// apart without relying on colour; (2) assigning a task to someone without a role on the project is refused by the
// API and the refusal is shown; (3) the keyboard reaches every interactive element of MOD-012 Task Detail.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);

const TASKS_PATH = `/projects/${PROJECT_ID}/tasks`;

/** The Coastal road upgrade, ACTIVE, managed by the entity's user, Huda (TASK-048 D-11). */
function activeProject(overrides: Partial<ProjectDetail> = {}): ProjectDetail {
  return projectDetail({
    status: 'ACTIVE',
    projectManagerUserId: ENTITY_USER_ID,
    activatedAt: '2026-10-01T10:00:00Z',
    ...overrides,
  });
}

function open(
  path: string,
  state: TaskState = {},
  {
    session = entitySession(),
    project = activeProject(),
    language = 'en',
  }: { session?: Session; project?: ProjectDetail; language?: Language } = {},
): MockApi {
  const api = withTasks(withProject(withProjectLookups(mockApi()), project), state);
  renderApp({ path, session, language });
  return api;
}

/** A task's MOD-012, opened from its title in a table. */
async function openTask(title: string): Promise<HTMLElement> {
  await userEvent.click(await screen.findByRole('button', { name: title }));
  return screen.findByRole('dialog', { name: title });
}

async function press(scope: HTMLElement, name: string) {
  await userEvent.click(within(scope).getByRole('button', { name }));
}

/** The table row holding a task's title. */
function rowOf(title: string): HTMLElement {
  const row = screen.getByRole('button', { name: title }).closest('tr');
  if (row === null) {
    throw new Error(`No row for ${title}.`);
  }
  return row;
}

function titlesIn(table: HTMLElement): string[] {
  return within(table)
    .getAllByRole('row')
    .slice(1)
    .map((row) => within(row).getAllByRole('button')[0]?.textContent ?? '');
}

/** Every element a keyboard user can operate inside `scope`, in document order. */
function interactive(scope: HTMLElement): HTMLElement[] {
  return [
    ...scope.querySelectorAll<HTMLElement>('a[href], button, input, select, textarea, [tabindex]'),
  ].filter((element) => !(element as HTMLButtonElement).disabled && element.tabIndex !== -1);
}

describe('SCR-047 Project Tasks', () => {
  test('lists each parent followed by its subtasks, with status, owner, planned dates and progress; open work by default', async () => {
    open(TASKS_PATH);

    const table = await screen.findByRole('region', { name: en('tasks.project.caption') });
    expect(titlesIn(table)).toEqual([
      'Site preparation',
      'Fence the site',
      'Lay drainage',
      'Survey the plot',
    ]);
    const fencing = within(rowOf('Fence the site'));
    expect(fencing.getByText(en('tasks.status.IN_PROGRESS'))).toBeTruthy();
    expect(await fencing.findByText('Nora Manager')).toBeTruthy();
    expect(fencing.getByText('20%')).toBeTruthy();
    expect(within(rowOf('Survey the plot')).getByText(en('common.people.you'))).toBeTruthy();
    expect(
      within(rowOf('Site preparation')).getByText(en('tasks.table.subtasks', { count: 2 })),
    ).toBeTruthy();

    fireEvent.change(screen.getByLabelText(en('tasks.filter.label')), { target: { value: 'all' } });
    expect(titlesIn(table)).toContain('Clear the site');
  });

  test('criterion 1: an overdue task carries a warning icon and the words, and its row a thick edge, not colour alone', async () => {
    open(TASKS_PATH);

    await screen.findByRole('region', { name: en('tasks.project.caption') });
    const fencing = rowOf('Fence the site');
    const flag = within(fencing).getByText(en('tasks.overdue.many', { days: 3 }));
    expect(flag.className).toBe('overdue-flag');
    // A drawn, visible icon, decorative to a screen reader, which hears the words.
    const icon = flag.querySelector('svg');
    expect(icon).not.toBeNull();
    expect(icon?.hasAttribute('hidden')).toBe(false);
    expect(icon?.querySelectorAll('path').length).toBeGreaterThan(0);
    expect(icon?.getAttribute('aria-hidden')).toBe('true');
    expect(fencing.className).toBe('row--overdue');
    expect(fencing.getAttribute('data-overdue')).toBe('true');
    // The words are in the row's text, which is what a screen reader reads with the title.
    expect(fencing.textContent).toContain(en('tasks.overdue.many', { days: 3 }));

    const survey = rowOf('Survey the plot');
    expect(survey.className).toBe('');
    expect(survey.querySelector('.overdue-flag')).toBeNull();
  });

  test('empty: the Project Manager is invited to add the first task; anyone else is told who plans', async () => {
    open(TASKS_PATH, { tasks: [], dependencies: [] });

    expect(await screen.findByText(en('tasks.empty.project.title'))).toBeTruthy();
    expect(screen.getByText(en('tasks.empty.project.manager'))).toBeTruthy();
    expect(screen.getByRole('button', { name: en('tasks.actions.create') })).toBeTruthy();
  });

  test('empty, for someone who does not plan the project: no action, and who will', async () => {
    open(TASKS_PATH, { tasks: [], dependencies: [] }, { session: reviewerSession() });

    expect(await screen.findByText(en('tasks.empty.project.waiting'))).toBeTruthy();
    expect(screen.queryByRole('button', { name: en('tasks.actions.create') })).toBeNull();
  });

  test('a choice that matches nothing says so and how to see more', async () => {
    open(TASKS_PATH);

    fireEvent.change(await screen.findByLabelText(en('tasks.filter.label')), {
      target: { value: 'CANCELLED' },
    });
    expect(screen.getByText(en('tasks.empty.filtered.title'))).toBeTruthy();
    expect(screen.getByText(en('tasks.empty.filtered.body'))).toBeTruthy();
  });

  test('without TASK_VIEW the tab says so instead of failing', async () => {
    const api = open(TASKS_PATH);
    api.on('GET', /^\/project-tasks$/, problem(403, 'PERMISSION_DENIED'));

    expect(await screen.findByText(en('tasks.forbidden'))).toBeTruthy();
  });

  test('the schedule unreadable (403) names activities by short id and the form says why it offers none', async () => {
    open(TASKS_PATH, { schedule: false });

    const dialog = await openTask('Survey the plot');
    expect(within(dialog).getByText('ab000000')).toBeTruthy();
    await press(dialog, en('tasks.actions.edit'));
    const form = await screen.findByRole('dialog', { name: en('tasks.form.editTitle') });
    expect(within(form).getByText(en('tasks.form.activitiesUnreadable'))).toBeTruthy();
  });
});

describe('MOD-010 Add Task, MOD-011 Edit Task, MOD-013 Add Subtask', () => {
  test('a finish before the start is refused on the field and nothing is sent; the inputs are then sent as typed', async () => {
    const api = open(TASKS_PATH);
    api.on('POST', /^\/project-tasks$/, {
      status: 201,
      body: task(),
      headers: { ETag: TASK_ETAG },
    });

    await userEvent.click(await screen.findByRole('button', { name: en('tasks.actions.create') }));
    const dialog = await screen.findByRole('dialog', { name: en('tasks.form.createTitle') });
    await userEvent.type(within(dialog).getByLabelText(/^Title/), 'Order signage');
    fireEvent.change(within(dialog).getByLabelText(/^Planned start/), {
      target: { value: day(5) },
    });
    fireEvent.change(within(dialog).getByLabelText(/^Planned finish/), {
      target: { value: day(4) },
    });
    await press(dialog, en('common.actions.save'));

    expect(within(dialog).getByText(en('tasks.fieldErrors.finishBeforeStart'))).toBeTruthy();
    expect(api.requestsTo('POST', /^\/project-tasks$/)).toHaveLength(0);

    fireEvent.change(within(dialog).getByLabelText(/^Planned finish/), {
      target: { value: day(8) },
    });
    fireEvent.change(within(dialog).getByLabelText(/^Schedule activity/), {
      target: { value: PAVING_ID },
    });
    fireEvent.change(await within(dialog).findByLabelText(/^Priority/), {
      target: { value: HIGH_PRIORITY_ID },
    });
    await userEvent.click(
      within(dialog).getByLabelText(en('tasks.owner.self', { name: 'Huda Contractor' })),
    );
    await press(dialog, en('common.actions.save'));

    await waitFor(() => {
      expect(api.requestsTo('POST', /^\/project-tasks$/)).toHaveLength(1);
    });
    expect(api.requestsTo('POST', /^\/project-tasks$/)[0]?.body).toEqual({
      projectId: PROJECT_ID,
      parentTaskId: null,
      scheduleActivityId: PAVING_ID,
      title: { text: 'Order signage', language: 'en' },
      description: null,
      assigneeUserId: ENTITY_USER_ID,
      priorityItemId: HIGH_PRIORITY_ID,
      plannedStartDate: day(5),
      plannedFinishDate: day(8),
    });
    // The new task's MOD-012 opens, and the screen says it was added.
    expect(await screen.findByRole('dialog', { name: 'Survey the plot' })).toBeTruthy();
    expect(screen.getByText(en('tasks.done.created'))).toBeTruthy();
  });

  test('MOD-011 sends the whole plan with the ETag it was shown, the owner unchanged, and returns to MOD-012', async () => {
    const api = open(TASKS_PATH);
    api.on('PUT', /^\/project-tasks\/[^/]+$/, { body: task(), headers: { ETag: '"42"' } });

    const detail = await openTask('Survey the plot');
    await press(detail, en('tasks.actions.edit'));
    const form = await screen.findByRole('dialog', { name: en('tasks.form.editTitle') });
    const title = within(form).getByLabelText(/^Title/);
    await userEvent.clear(title);
    await userEvent.type(title, 'Survey the whole plot');
    await press(form, en('common.actions.save'));

    await waitFor(() => {
      expect(api.requestsTo('PUT', /^\/project-tasks\//)).toHaveLength(1);
    });
    const put = api.requestsTo('PUT', /^\/project-tasks\//)[0];
    expect(put?.path).toBe(`/project-tasks/${PLOT_SURVEY_ID}`);
    expect(put?.headers.get('If-Match')).toBe(TASK_ETAG);
    expect(put?.body).toMatchObject({
      title: { text: 'Survey the whole plot', language: 'en' },
      assigneeUserId: ENTITY_USER_ID,
      plannedStartDate: day(-2),
      plannedFinishDate: day(4),
    });
    expect(await screen.findByRole('dialog', { name: 'Survey the plot' })).toBeTruthy();
    expect(screen.getByText(en('tasks.done.updated'))).toBeTruthy();
  });

  test('MOD-013 adds a subtask under its parent, on the parent’s schedule activity and dates', async () => {
    const api = open(TASKS_PATH);
    api.on('POST', /^\/project-tasks$/, { status: 201, body: task() });

    const parent = await openTask('Site preparation');
    await press(parent, en('tasks.actions.addSubtask'));
    const form = await screen.findByRole('dialog', { name: en('tasks.form.subtaskTitle') });
    expect(
      within(form).getByText(en('tasks.form.subtaskOf', { parent: 'Site preparation' })),
    ).toBeTruthy();
    expect(within(form).getByText('1.2 · Excavation')).toBeTruthy();
    await userEvent.type(within(form).getByLabelText(/^Title/), 'Mark the boundary');
    await press(form, en('common.actions.save'));

    await waitFor(() => {
      expect(api.requestsTo('POST', /^\/project-tasks$/)).toHaveLength(1);
    });
    expect(api.requestsTo('POST', /^\/project-tasks$/)[0]?.body).toMatchObject({
      parentTaskId: PARENT_ID,
      scheduleActivityId: EXCAVATION_ID,
      plannedStartDate: day(-10),
      plannedFinishDate: day(5),
      // No owner until one is chosen.
      assigneeUserId: null,
    });
    expect(await screen.findByRole('dialog', { name: 'Site preparation' })).toBeTruthy();
  });
});

describe('MOD-015 Add Dependency', () => {
  test('a dependency that would close a cycle is refused with the chain before any request; a valid one is sent', async () => {
    const api = open(TASKS_PATH);
    api.on('POST', /^\/task-dependencies$/, { status: 201, body: {} });

    await userEvent.click(
      await screen.findByRole('button', { name: en('tasks.actions.addDependency') }),
    );
    const dialog = await screen.findByRole('dialog', { name: en('tasks.dependency.title') });
    const successor = within(dialog).getByLabelText(/^Task held back/);
    fireEvent.change(within(dialog).getByLabelText(/^First task/), {
      target: { value: PLOT_SURVEY_ID },
    });
    fireEvent.change(successor, { target: { value: DRAINAGE_ID } });

    const chain = 'Lay drainage → Survey the plot → Lay drainage';
    expect(within(dialog).getByText(en('tasks.dependency.circular', { chain }))).toBeTruthy();
    await press(dialog, en('tasks.dependency.confirm'));
    expect(api.requestsTo('POST', /^\/task-dependencies$/)).toHaveLength(0);
    expect(document.activeElement).toBe(successor);

    fireEvent.change(within(dialog).getByLabelText(/^First task/), {
      target: { value: FENCING_ID },
    });
    await userEvent.click(within(dialog).getByLabelText(en('tasks.dependencyType.SS')));
    await press(dialog, en('tasks.dependency.confirm'));
    await waitFor(() => {
      expect(api.requestsTo('POST', /^\/task-dependencies$/)).toHaveLength(1);
    });
    expect(api.requestsTo('POST', /^\/task-dependencies$/)[0]?.body).toEqual({
      predecessorTaskId: FENCING_ID,
      successorTaskId: DRAINAGE_ID,
      dependencyType: 'SS',
    });
  });
});

describe('MOD-012 Task Detail', () => {
  test('criterion 3: Tab reaches every interactive element in reading order, also a form opened with the keyboard; axe finds nothing', async () => {
    open(TASKS_PATH);

    const dialog = await openTask('Lay drainage');
    await within(dialog).findByText(en('tasks.detail.holdsBack'));
    const elements = interactive(dialog);
    expect(elements.map((element) => element.textContent)).toEqual([
      en('tasks.commands.unblock'),
      en('tasks.commands.reportProgress'),
      en('tasks.actions.removeDependency'),
      en('tasks.actions.addDependency'),
      en('tasks.actions.edit'),
      en('tasks.actions.assign'),
      en('tasks.actions.close'),
    ]);
    elements[0]?.focus();
    const reached = [document.activeElement];
    for (let step = 1; step < elements.length; step += 1) {
      await userEvent.tab();
      reached.push(document.activeElement);
    }
    expect(reached).toEqual(elements);

    // Report progress, opened from the keyboard: its field and buttons join the order where they appear.
    elements[1]?.focus();
    await userEvent.keyboard('{Enter}');
    const percent = await within(dialog).findByLabelText(/^Percentage complete/);
    expect(elements[1]?.getAttribute('aria-expanded')).toBe('true');
    await userEvent.tab();
    expect(document.activeElement).toBe(percent);
    await userEvent.tab();
    expect(document.activeElement).toBe(
      within(dialog).getByRole('button', { name: en('tasks.progress.confirm') }),
    );

    const violations = await accessibilityViolations();
    expect(violations, describeViolations(violations)).toEqual([]);
  });

  test('a blocked task is not offered completion; unblocking sends the ETag and says so in the dialog', async () => {
    const api = open(TASKS_PATH);
    api.on('POST', /^\/project-tasks\/[^/]+\/unblock$/, {
      body: task({ id: DRAINAGE_ID, status: 'IN_PROGRESS' }),
    });

    const dialog = await openTask('Lay drainage');
    expect(
      within(dialog).getByText(
        en('tasks.detail.blockedBecause', { reason: 'Waiting for the permit' }),
      ),
    ).toBeTruthy();
    expect(
      within(dialog).queryByRole('button', { name: en('tasks.commands.complete') }),
    ).toBeNull();
    expect(within(dialog).getByText(en('tasks.detail.unblockFirst'))).toBeTruthy();

    await press(dialog, en('tasks.commands.unblock'));
    expect(await within(dialog).findByText(en('tasks.done.unblocked'))).toBeTruthy();
    const unblock = api.requestsTo('POST', /\/unblock$/)[0];
    expect(unblock?.path).toBe(`/project-tasks/${DRAINAGE_ID}/unblock`);
    expect(unblock?.headers.get('If-Match')).toBe(TASK_ETAG);
  });

  test('a start held back by a predecessor is explained, and the API’s refusal shown', async () => {
    const api = open(TASKS_PATH);
    api.on('POST', /\/start$/, problem(422, 'TASK_DEPENDENCY_UNMET'));

    const dialog = await openTask('Survey the plot');
    expect(
      within(dialog).getByText(
        en('tasks.detail.waitingFor', { list: 'Lay drainage (finish to start)' }),
      ),
    ).toBeTruthy();
    await press(dialog, en('tasks.commands.start'));
    expect(await within(dialog).findByText(en('tasks.problems.dependencyUnmet'))).toBeTruthy();
  });

  test('progress: a figure out of range is refused on the field before sending; a valid one is sent', async () => {
    const api = open(TASKS_PATH);
    api.on('POST', /\/report-progress$/, { body: task({ id: FENCING_ID }) });

    const dialog = await openTask('Fence the site');
    await press(dialog, en('tasks.commands.reportProgress'));
    const field = within(dialog).getByLabelText(/^Percentage complete/);
    await userEvent.clear(field);
    await userEvent.type(field, '120');
    await press(dialog, en('tasks.progress.confirm'));
    expect(within(dialog).getByText(en('tasks.fieldErrors.percentOutOfRange'))).toBeTruthy();
    expect(api.requestsTo('POST', /\/report-progress$/)).toHaveLength(0);

    await userEvent.clear(field);
    await userEvent.type(field, '62.5');
    await press(dialog, en('tasks.progress.confirm'));
    expect(await within(dialog).findByText(en('tasks.done.progressReported'))).toBeTruthy();
    expect(api.requestsTo('POST', /\/report-progress$/)[0]?.body).toEqual({
      actualPercentComplete: 62.5,
    });
  });

  test('blocking needs a reason, sent in the language it was typed in', async () => {
    const api = open(TASKS_PATH);
    api.on('POST', /\/block$/, { body: task({ status: 'BLOCKED' }) });

    const dialog = await openTask('Survey the plot');
    await press(dialog, en('tasks.commands.block'));
    await press(dialog, en('tasks.block.confirm'));
    expect(within(dialog).getByText(en('common.fieldErrors.required'))).toBeTruthy();
    await userEvent.type(within(dialog).getByLabelText(/^Why is it blocked/), 'Rain');
    await press(dialog, en('tasks.block.confirm'));

    expect(await within(dialog).findByText(en('tasks.done.blocked'))).toBeTruthy();
    expect(api.requestsTo('POST', /\/block$/)[0]?.body).toEqual({
      reason: { text: 'Rain', language: 'en' },
    });
  });

  test('the Project Manager reopens a completed subtask, reached from its parent; a free task is cancelled after confirming', async () => {
    const extra = task({
      id: 'bd000000-0000-4000-8000-000000000f10',
      title: { text: 'Order signage', language: 'EN' },
    });
    const api = open(TASKS_PATH, { tasks: [...coastalTasks(), extra] });
    api
      .on('POST', /\/reopen$/, { body: task() })
      .on('POST', /\/cancel$/, { body: task({ status: 'CANCELLED' }) });

    const parent = await openTask('Site preparation');
    await press(parent, 'Clear the site');
    const clearing = await screen.findByRole('dialog', { name: 'Clear the site' });
    await press(clearing, en('tasks.commands.reopen'));
    expect(await within(clearing).findByText(en('tasks.done.reopened'))).toBeTruthy();
    // A task's MOD-012 opened from another's replaces it: closing returns to the list.
    await press(clearing, en('tasks.actions.close'));
    expect(screen.queryByRole('dialog')).toBeNull();

    const signage = await openTask('Order signage');
    await press(signage, en('tasks.commands.cancel'));
    expect(within(signage).getByText(en('tasks.cancel.body'))).toBeTruthy();
    await press(signage, en('tasks.cancel.confirm'));
    expect(await within(signage).findByText(en('tasks.done.cancelled'))).toBeTruthy();
    expect(api.requestsTo('POST', /\/cancel$/)).toHaveLength(1);
  });
});

describe('criterion 2: MOD-014 Assign Task', () => {
  test('someone without a role on the project, given by user id: the API’s refusal is shown on the owner field and nothing changes', async () => {
    const api = open(TASKS_PATH);
    api.on(
      'PUT',
      /^\/project-tasks\/[^/]+$/,
      problem(422, 'TASK_ASSIGNEE_NOT_ELIGIBLE', [
        { field: 'assigneeUserId', code: 'NOT_ALLOWED' },
      ]),
    );

    const detail = await openTask('Survey the plot');
    await press(detail, en('tasks.actions.assign'));
    const dialog = await screen.findByRole('dialog', { name: en('tasks.assign.title') });
    const other = within(dialog).getByLabelText(en('tasks.owner.other'));
    await userEvent.click(other);
    // Without USER_VIEW (R04 today) the person is given by user id.
    await userEvent.type(within(dialog).getByLabelText(/^The person's user ID/), UNRELATED_USER_ID);
    await press(dialog, en('tasks.assign.confirm'));

    expect(await within(dialog).findByRole('alert')).toHaveProperty(
      'textContent',
      en('tasks.problems.assigneeNotEligible'),
    );
    expect(within(dialog).getByText(en('tasks.fieldErrors.assigneeNotEligible'))).toBeTruthy();
    expect(other.getAttribute('aria-invalid')).toBe('true');
    await waitFor(() => {
      expect(document.activeElement).toBe(other);
    });
    const put = api.requestsTo('PUT', /^\/project-tasks\//)[0];
    expect(put?.headers.get('If-Match')).toBe(TASK_ETAG);
    expect(put?.body).toMatchObject({
      assigneeUserId: UNRELATED_USER_ID,
      title: { text: 'Survey the plot', language: 'en' },
      plannedStartDate: day(-2),
    });
    // The dialog stays open with the choice made, so it can be changed.
    expect(screen.getByRole('dialog', { name: en('tasks.assign.title') })).toBeTruthy();
  });

  test('with USER_VIEW the person is found by search, and the refusal is shown the same way', async () => {
    const api = open(TASKS_PATH, { userSearch: true });
    api.on(
      'PUT',
      /^\/project-tasks\/[^/]+$/,
      problem(422, 'TASK_ASSIGNEE_NOT_ELIGIBLE', [
        { field: 'assigneeUserId', code: 'NOT_ALLOWED' },
      ]),
    );

    await press(await openTask('Survey the plot'), en('tasks.actions.assign'));
    const dialog = await screen.findByRole('dialog', { name: en('tasks.assign.title') });
    await userEvent.click(within(dialog).getByLabelText(en('tasks.owner.other')));
    await userEvent.type(
      await within(dialog).findByLabelText(en('identityAccess.userPicker.searchLabel')),
      'sami',
    );
    await press(dialog, en('common.actions.search'));
    await userEvent.click(await within(dialog).findByRole('button', { name: /Sami Unrelated/ }));
    await press(dialog, en('tasks.assign.confirm'));

    expect(
      await within(dialog).findByText(en('tasks.fieldErrors.assigneeNotEligible')),
    ).toBeTruthy();
    expect(api.requestsTo('PUT', /^\/project-tasks\//)[0]?.body).toMatchObject({
      assigneeUserId: UNRELATED_USER_ID,
    });
  });

  test('someone who works on the project is offered by name; once assigned, MOD-012 is shown again', async () => {
    const api = open(TASKS_PATH);
    api.on('PUT', /^\/project-tasks\/[^/]+$/, { body: task({ assigneeUserId: OTHER_PERSON_ID }) });

    await press(await openTask('Survey the plot'), en('tasks.actions.assign'));
    const dialog = await screen.findByRole('dialog', { name: en('tasks.assign.title') });
    await userEvent.click(await within(dialog).findByLabelText('Nora Manager'));
    await press(dialog, en('tasks.assign.confirm'));

    expect(await screen.findByRole('dialog', { name: 'Survey the plot' })).toBeTruthy();
    expect(screen.getByText(en('tasks.done.assigned'))).toBeTruthy();
    expect(api.requestsTo('PUT', /^\/project-tasks\//)[0]?.body).toMatchObject({
      assigneeUserId: OTHER_PERSON_ID,
    });
  });
});

describe('SCR-063 to SCR-066, across projects', () => {
  test('SCR-063 My Tasks: the person’s tasks from every project, with the project named; completed ones behind the filter', async () => {
    open('/tasks');

    const table = await screen.findByRole('region', { name: en('tasks.lists.mine.caption') });
    expect(titlesIn(table)).toEqual([
      'Inspect the bridge',
      'Survey the plot',
      'Site preparation',
      'Lay drainage',
    ]);
    expect(
      within(rowOf('Inspect the bridge')).getByRole('link', { name: 'Harbour bridge repair' }),
    ).toBeTruthy();
    fireEvent.change(screen.getByLabelText(en('tasks.filter.label')), { target: { value: 'all' } });
    expect(titlesIn(table)).toContain('Clear the site');
    // One read of the projects, then each project's tasks: there is no cross-project query (TASK-048 F-8).
    expect(
      screen.getByRole('heading', { level: 1, name: en('tasks.lists.mine.title') }),
    ).toBeTruthy();
  });

  test('SCR-064 Team Tasks: every task of the projects the person manages, whoever owns it', async () => {
    open('/tasks/team');

    const table = await screen.findByRole('region', { name: en('tasks.lists.team.caption') });
    expect(titlesIn(table)).toEqual([
      'Fence the site',
      'Survey the plot',
      'Site preparation',
      'Lay drainage',
    ]);
    expect(await within(rowOf('Fence the site')).findByText('Nora Manager')).toBeTruthy();
    expect(
      within(rowOf('Fence the site')).getByText(
        en('tasks.table.subtaskOf', { parent: 'Site preparation' }),
      ),
    ).toBeTruthy();
  });

  test('SCR-065 Overdue Tasks: most overdue first, each flagged with the icon and the words', async () => {
    open('/tasks/overdue');

    const table = await screen.findByRole('region', { name: en('tasks.lists.overdue.caption') });
    expect(titlesIn(table)).toEqual(['Fence the site', 'Inspect the bridge']);
    expect(
      within(rowOf('Fence the site')).getByText(en('tasks.overdue.many', { days: 3 })),
    ).toBeTruthy();
    expect(within(rowOf('Inspect the bridge')).getByText(en('tasks.overdue.one'))).toBeTruthy();
    expect(screen.queryByLabelText(en('tasks.filter.label'))).toBeNull();
  });

  test('SCR-066 Updates Required: each task says what it is waiting for', async () => {
    open('/tasks/updates');

    await screen.findByRole('region', { name: en('tasks.lists.updates.caption') });
    expect(
      within(rowOf('Survey the plot')).getByText(en('tasks.updates.notStarted', { date: day(-2) })),
    ).toBeTruthy();
    expect(within(rowOf('Lay drainage')).getByText(en('tasks.updates.noProgress'))).toBeTruthy();
    expect(
      within(rowOf('Fence the site')).getByText(en('tasks.updates.staleProgress', { days: 10 })),
    ).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Inspect the bridge' })).toBeNull();
  });

  test.each([
    ['/tasks', 'tasks.empty.mine.title', 'tasks.empty.mine.body'],
    ['/tasks/team', 'tasks.empty.team.title', 'tasks.empty.team.body'],
    ['/tasks/overdue', 'tasks.empty.overdue.title', 'tasks.empty.overdue.body'],
    ['/tasks/updates', 'tasks.empty.updates.title', 'tasks.empty.updates.body'],
  ])('%s has an empty state of its own', async (path, title, body) => {
    open(path, { tasks: [] });

    expect(await screen.findByText(en(title))).toBeTruthy();
    expect(screen.getByText(en(body))).toBeTruthy();
  });

  test('Team Tasks for someone who manages no project says so', async () => {
    const projects = taskProjects().map((project) => ({
      ...project,
      projectManagerUserId: OTHER_PERSON_ID,
    }));
    open('/tasks/team', { projects });

    expect(await screen.findByText(en('tasks.empty.team.noProjectTitle'))).toBeTruthy();
    expect(screen.getByText(en('tasks.empty.team.noProjectBody'))).toBeTruthy();
  });

  test('without TASK_VIEW the lists say so instead of failing', async () => {
    const api = open('/tasks');
    api.on('GET', /^\/project-tasks$/, problem(403, 'PERMISSION_DENIED'));

    expect(await screen.findByText(en('tasks.forbidden'))).toBeTruthy();
  });

  test('an owner works a task on another manager’s project from the list, but may not plan it; the list is read again after', async () => {
    const api = open('/tasks');
    api.on('POST', /\/complete$/, { body: task({ id: BRIDGE_ID, status: 'COMPLETED' }) });

    const dialog = await openTask('Inspect the bridge');
    expect(within(dialog).queryByRole('button', { name: en('tasks.actions.edit') })).toBeNull();
    expect(within(dialog).queryByRole('button', { name: en('tasks.actions.assign') })).toBeNull();
    expect(within(dialog).queryByRole('button', { name: en('tasks.commands.cancel') })).toBeNull();
    const reads = api.requestsTo('GET', /^\/projects$/).length;
    await press(dialog, en('tasks.commands.complete'));

    expect(await within(dialog).findByText(en('tasks.done.completed'))).toBeTruthy();
    await waitFor(() => {
      expect(api.requestsTo('GET', /^\/projects$/).length).toBeGreaterThan(reads);
    });
    expect(api.requestsTo('POST', /\/complete$/)[0]?.path).toBe(
      `/project-tasks/${BRIDGE_ID}/complete`,
    );
  });

  test('in Arabic the list and its overdue flag read in Arabic, and axe finds nothing', async () => {
    open('/tasks/overdue', {}, { language: 'ar' });

    const flag = await screen.findByText(translate('ar', 'tasks.overdue.many', { days: 3 }));
    expect(flag.querySelector('svg')).not.toBeNull();
    const violations = await accessibilityViolations();
    expect(violations, describeViolations(violations)).toEqual([]);
  });
});
