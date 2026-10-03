import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { type Session } from '@/features/identity-access/session/sessionApi.ts';
import { type ProjectDetail } from '@/features/projects/api/types.ts';
import { type Language, translate } from '@/shared/i18n/i18n.ts';
import { TASK_ID } from '@/test/approvalFixtures.ts';
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
import {
  ACTIVITY_ETAG,
  activity,
  baseline,
  baselinedActivities,
  candidate,
  CANDIDATE_ID,
  CHANGE_AUTHORIZATION_ID,
  dependency,
  EXCAVATION_ID,
  PAVING_ID,
  plannedActivities,
  projectSchedule,
  scheduleRun,
  scheduleTask,
  type ScheduleState,
  SURVEY_ID,
  withSchedule,
} from '@/test/scheduleFixtures.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';

// SCR-060 Schedule Manager, SCR-045 Gantt View, SCR-061 Baseline View and MOD-015, MOD-017, MOD-018 (TASK-047),
// against the WF-03 API (TASK-046). Acceptance criteria: (1) the Gantt tells the Approved Baseline from the Current
// Forecast; (2) a circular dependency is refused with an explanation before the API is called; (3) the view is usable
// on a 1366 px laptop without a horizontal scroll trap — jsdom has no layout, so here the chart is checked to place
// every bar in per cent of its timeline; the browser check is in schedule-ui.md §4.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);

const SCHEDULE_PATH = `/projects/${PROJECT_ID}/schedule`;
const GANTT_PATH = `${SCHEDULE_PATH}/gantt`;
const BASELINES_PATH = `${SCHEDULE_PATH}/baselines`;

/** An ACTIVE project whose Project Manager is the entity's user (TASK-046 D-12). */
function activeProject(overrides: Partial<ProjectDetail> = {}): ProjectDetail {
  return projectDetail({
    status: 'ACTIVE',
    projectManagerUserId: ENTITY_USER_ID,
    activatedAt: '2026-10-02T10:00:00Z',
    ...overrides,
  });
}

function open(
  session: Session,
  state: ScheduleState = {},
  {
    path = SCHEDULE_PATH,
    project = activeProject(),
    language = 'en',
  }: { path?: string; project?: ProjectDetail; language?: Language } = {},
): MockApi {
  const api = withSchedule(withProject(withProjectLookups(mockApi()), project), state);
  renderApp({ path, session, language });
  return api;
}

/** Before any baseline: the plan moves, and the forecast follows it. */
const PLANNING: ScheduleState = {
  schedule: projectSchedule({ activeBaselineId: null }),
  activities: plannedActivities(),
  baselines: [],
  health: [],
};

async function openDialog(buttonName: string, titleKey: string) {
  await userEvent.click(await screen.findByRole('button', { name: buttonName }));
  return screen.findByRole('dialog', { name: en(titleKey) });
}

/** The table's row at `index` (0 is the header), to query within. */
function rowAt(table: HTMLElement, index: number) {
  const row = within(table).getAllByRole('row')[index];
  if (row === undefined) {
    throw new Error(`The table has no row ${String(index)}.`);
  }
  return within(row);
}

function escape(text: string): string {
  return text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

/** Accessible names interpolate the activity, bidi-isolated as translate() does. */
const editName = (activity: string) => en('schedule.actions.editActivity', { activity });
const reforecastName = (activity: string) =>
  en('schedule.actions.reforecastActivity', { activity });
const moveName = (activity: string) => en('schedule.gantt.moveLabel', { activity });

const labelled = (dialog: HTMLElement, key: string) =>
  within(dialog).getByLabelText(new RegExp(`^${escape(en(key))}`));

describe('SCR-060 Schedule Manager', () => {
  test('lists the work breakdown with plan, Current Forecast, Approved Baseline and the API’s variance side by side', async () => {
    open(entitySession());

    const table = await screen.findByRole('region', { name: en('schedule.activities.caption') });
    const rows = within(table).getAllByRole('row');
    expect(rows.map((row) => within(row).queryAllByRole('cell')[0]?.textContent)).toEqual([
      undefined,
      '1',
      '1.1',
      '1.2',
      '2',
    ]);
    const paving = rowAt(table, 4);
    expect(paving.getAllByText('2026-11-16')).toHaveLength(2); // planned and baseline start
    expect(paving.getByText('2026-11-18')).toBeTruthy(); // forecast start
    expect(paving.getByText(en('schedule.variance.late', { days: 2 }))).toBeTruthy();
    expect(paving.getByText(en('schedule.activities.after', { list: '1.2 FS' }))).toBeTruthy();

    // The live Schedule Health as WF-03 stored it, and the two finishes.
    expect(screen.getByText(en('schedule.health.AMBER'))).toBeTruthy();
    expect(
      screen.getByText(en('schedule.summary.forecastFinish', { date: '2026-11-27' })),
    ).toBeTruthy();
  });

  test('before a baseline nothing is measured, and the summary says the forecast follows the plan', async () => {
    open(entitySession(), PLANNING);

    expect(await screen.findByText(en('schedule.summary.noActiveBaseline'))).toBeTruthy();
    expect(screen.getByText(en('schedule.summary.forecastFollowsPlan'))).toBeTruthy();
    expect(screen.getAllByText(en('schedule.variance.none')).length).toBe(4);
    expect(screen.getByText(en('schedule.summary.healthNone'))).toBeTruthy();
    expect(screen.queryByRole('button', { name: reforecastName('2 · Paving') })).toBeNull();
  });

  test('the Project Manager sets up a schedule; anyone else is told who will', async () => {
    const api = open(entitySession(), {
      schedule: null,
      activities: [],
      dependencies: [],
      baselines: [],
    });
    api.on('POST', /^\/project-schedules$/, { status: 201, body: projectSchedule() });

    await userEvent.click(
      await screen.findByRole('button', { name: en('schedule.actions.initialize') }),
    );

    expect(await screen.findByText(en('schedule.done.initialized'))).toBeTruthy();
    expect(api.requestsTo('POST', /^\/project-schedules$/)[0]?.body).toEqual({
      projectId: PROJECT_ID,
    });
  });

  test('a reader who does not plan the schedule sees it without any change offered', async () => {
    open(reviewerSession(), { schedule: null, activities: [], dependencies: [], baselines: [] });

    expect(await screen.findByText(en('schedule.notInitialized.waiting'))).toBeTruthy();
    expect(screen.queryByRole('button', { name: en('schedule.actions.initialize') })).toBeNull();
  });

  test('while a candidate is with AHDA the plan is frozen: no activity or dependency change is offered, forecasts still are', async () => {
    open(entitySession(), { baselines: [candidate({ status: 'SUBMITTED' }), baseline()] });

    expect(await screen.findByText(en('schedule.summary.frozen'))).toBeTruthy();
    expect(screen.queryByRole('button', { name: en('schedule.actions.addActivity') })).toBeNull();
    expect(screen.queryByRole('button', { name: en('schedule.actions.addDependency') })).toBeNull();
    expect(screen.queryByRole('button', { name: editName('1.1 · Survey') })).toBeNull();
    expect(screen.getByRole('button', { name: reforecastName('2 · Paving') })).toBeTruthy();
  });

  test('adds an activity: the inputs are sent, never a calculated date', async () => {
    const api = open(entitySession(), PLANNING);
    api.on('POST', /^\/schedule-activities$/, { status: 201, body: activity() });

    const dialog = await openDialog(
      en('schedule.actions.addActivity'),
      'schedule.activity.createTitle',
    );
    await userEvent.type(labelled(dialog, 'schedule.activity.wbsCode'), '3');
    await userEvent.type(labelled(dialog, 'schedule.activity.name'), 'Drainage');
    await userEvent.type(labelled(dialog, 'schedule.activity.duration'), '0');
    await userEvent.click(within(dialog).getByRole('button', { name: en('common.actions.save') }));

    // Out of range is flagged on the field, with its own message, and nothing is sent.
    expect(within(dialog).getByText(en('schedule.fieldErrors.durationOutOfRange'))).toBeTruthy();
    expect(api.requestsTo('POST', /^\/schedule-activities$/)).toHaveLength(0);

    await userEvent.clear(labelled(dialog, 'schedule.activity.duration'));
    await userEvent.type(labelled(dialog, 'schedule.activity.duration'), '4');
    await userEvent.click(within(dialog).getByRole('button', { name: en('common.actions.save') }));

    expect(await screen.findByText(en('schedule.done.activityCreated'))).toBeTruthy();
    expect(api.requestsTo('POST', /^\/schedule-activities$/)[0]?.body).toEqual({
      projectId: PROJECT_ID,
      parentActivityId: null,
      wbsCode: '3',
      name: { text: 'Drainage', language: 'en' },
      requestedStartDate: '2026-11-01',
      plannedDurationDays: 4,
      sortOrder: 5,
    });
  });

  test('edits an activity with the ETag it read, and a WBS code taken lands as the API says', async () => {
    const api = open(entitySession(), PLANNING);
    api.on('PUT', /^\/schedule-activities\/[^/]+$/, problem(409, 'SCHEDULE_WBS_CODE_EXISTS'));

    const dialog = await openDialog(editName('1.1 · Survey'), 'schedule.activity.editTitle');
    const name = await within(dialog).findByLabelText(
      new RegExp(`^${en('schedule.activity.name')}`),
    );
    await userEvent.clear(name);
    await userEvent.type(name, 'Topographic survey');
    await userEvent.click(within(dialog).getByRole('button', { name: en('common.actions.save') }));

    expect(await within(dialog).findByText(en('schedule.problems.wbsCodeExists'))).toBeTruthy();
    const sent = api.requestsTo('PUT', /^\/schedule-activities\/[^/]+$/)[0];
    expect(sent?.path).toBe(`/schedule-activities/${SURVEY_ID}`);
    expect(sent?.headers.get('If-Match')).toBe(ACTIVITY_ETAG);
  });

  test('a reforecast before the predecessor’s forecast allows is refused on the field and not sent', async () => {
    const api = open(entitySession());
    api.on('POST', /^\/schedule-activities\/[^/]+\/reforecast$/, { body: activity() });

    const dialog = await openDialog(reforecastName('2 · Paving'), 'schedule.reforecast.title');
    const start = await within(dialog).findByLabelText(
      new RegExp(`^${en('schedule.reforecast.start')}`),
    );
    fireEvent.change(start, { target: { value: '2026-11-15' } });

    expect(
      within(dialog).getByText(
        en('schedule.constraint.held', {
          date: '2026-11-18',
          predecessor: '1.2',
          type: en('schedule.dependencyTypeShort.FS'),
          lag: en('schedule.days.many', { days: 0 }),
        }),
      ),
    ).toBeTruthy();
    await userEvent.click(
      within(dialog).getByRole('button', { name: en('schedule.reforecast.confirm') }),
    );
    expect(api.requestsTo('POST', /reforecast$/)).toHaveLength(0);

    fireEvent.change(start, { target: { value: '2026-11-20' } });
    const finish = labelled(dialog, 'schedule.reforecast.finish');
    fireEvent.change(finish, { target: { value: '2026-11-29' } });
    await userEvent.click(
      within(dialog).getByRole('button', { name: en('schedule.reforecast.confirm') }),
    );

    expect(await screen.findByText(en('schedule.done.reforecast'))).toBeTruthy();
    const sent = api.requestsTo('POST', /reforecast$/)[0];
    expect(sent?.path).toBe(`/schedule-activities/${PAVING_ID}/reforecast`);
    expect(sent?.body).toEqual({
      forecastStartDate: '2026-11-20',
      forecastFinishDate: '2026-11-29',
    });
    expect(sent?.headers.get('If-Match')).toBe(ACTIVITY_ETAG);
  });

  test('has no axe violation', async () => {
    open(entitySession());
    await screen.findByRole('region', { name: en('schedule.activities.caption') });
    const violations = await accessibilityViolations();
    expect(violations, describeViolations(violations)).toEqual([]);
  });
});

describe('MOD-015 Add Dependency refuses a cycle before the API is called (acceptance criterion 2)', () => {
  async function openAddDependency(state: ScheduleState = PLANNING) {
    const api = open(entitySession(), state);
    api.on('POST', /^\/schedule-dependencies$/, { status: 201, body: dependency() });
    const dialog = await openDialog(
      en('schedule.actions.addDependency'),
      'schedule.dependency.title',
    );
    return { api, dialog };
  }

  test('2 → 1.1 would close 1.1 → 1.2 → 2: the chain is explained on the field and nothing is sent', async () => {
    const { api, dialog } = await openAddDependency();

    await userEvent.selectOptions(labelled(dialog, 'schedule.dependency.predecessor'), PAVING_ID);
    await userEvent.selectOptions(labelled(dialog, 'schedule.dependency.successor'), SURVEY_ID);

    const message = en('schedule.dependency.circular', { chain: '1.1 → 1.2 → 2 → 1.1' });
    // Explained as soon as the pair is chosen, on the input it concerns.
    const successor = labelled(dialog, 'schedule.dependency.successor');
    expect(within(dialog).getByText(message)).toBeTruthy();
    expect(successor.getAttribute('aria-invalid')).toBe('true');
    expect(successor.getAttribute('aria-describedby')).toContain(
      within(dialog).getByText(message).id,
    );

    await userEvent.click(
      within(dialog).getByRole('button', { name: en('schedule.dependency.confirm') }),
    );

    expect(within(dialog).getByRole('alert').textContent).toBe(
      en('common.form.fixErrors', { count: 1 }),
    );
    expect(document.activeElement).toBe(successor);
    expect(api.requestsTo('POST', /^\/schedule-dependencies$/)).toHaveLength(0);
  });

  test('the same activity at both ends, and a pair already linked, are refused before sending too', async () => {
    const { api, dialog } = await openAddDependency();

    await userEvent.selectOptions(labelled(dialog, 'schedule.dependency.predecessor'), SURVEY_ID);
    await userEvent.selectOptions(labelled(dialog, 'schedule.dependency.successor'), SURVEY_ID);
    expect(within(dialog).getByText(en('schedule.fieldErrors.sameActivity'))).toBeTruthy();

    await userEvent.selectOptions(labelled(dialog, 'schedule.dependency.successor'), EXCAVATION_ID);
    expect(within(dialog).getByText(en('schedule.problems.dependencyExists'))).toBeTruthy();

    await userEvent.click(
      within(dialog).getByRole('button', { name: en('schedule.dependency.confirm') }),
    );
    expect(api.requestsTo('POST', /^\/schedule-dependencies$/)).toHaveLength(0);
  });

  test('a valid dependency is sent as typed, the lag in working days', async () => {
    const { api, dialog } = await openAddDependency();

    await userEvent.selectOptions(labelled(dialog, 'schedule.dependency.predecessor'), SURVEY_ID);
    await userEvent.selectOptions(labelled(dialog, 'schedule.dependency.successor'), PAVING_ID);
    await userEvent.click(within(dialog).getByLabelText(en('schedule.dependencyType.SS')));
    await userEvent.clear(labelled(dialog, 'schedule.dependency.lag'));
    await userEvent.type(labelled(dialog, 'schedule.dependency.lag'), '2');
    await userEvent.click(
      within(dialog).getByRole('button', { name: en('schedule.dependency.confirm') }),
    );

    expect(await screen.findByText(en('schedule.done.dependencyAdded'))).toBeTruthy();
    expect(api.requestsTo('POST', /^\/schedule-dependencies$/)[0]?.body).toEqual({
      predecessorActivityId: SURVEY_ID,
      successorActivityId: PAVING_ID,
      dependencyType: 'SS',
      lagDays: 2,
    });
  });

  test('a cycle closed meanwhile by someone else is the API’s refusal, explained in the dialog', async () => {
    const { api, dialog } = await openAddDependency();
    api.on('POST', /^\/schedule-dependencies$/, problem(422, 'SCHEDULE_DEPENDENCY_CIRCULAR'));

    await userEvent.selectOptions(labelled(dialog, 'schedule.dependency.predecessor'), SURVEY_ID);
    await userEvent.selectOptions(labelled(dialog, 'schedule.dependency.successor'), PAVING_ID);
    await userEvent.click(
      within(dialog).getByRole('button', { name: en('schedule.dependency.confirm') }),
    );

    expect(
      await within(dialog).findByText(en('schedule.problems.dependencyCircular')),
    ).toBeTruthy();
  });

  test('has no axe violation with the cycle explained', async () => {
    const { dialog } = await openAddDependency();
    await userEvent.selectOptions(labelled(dialog, 'schedule.dependency.predecessor'), PAVING_ID);
    await userEvent.selectOptions(labelled(dialog, 'schedule.dependency.successor'), SURVEY_ID);
    const violations = await accessibilityViolations();
    expect(violations, describeViolations(violations)).toEqual([]);
  });
});

describe('SCR-045 Gantt View tells the Approved Baseline from the Current Forecast (acceptance criterion 1)', () => {
  test('the legend names both, each with its own mark, and each row draws both bars differently', async () => {
    open(reviewerSession(), {}, { path: GANTT_PATH });

    const legend = await screen.findByRole('region', { name: en('schedule.legend.title') });
    const forecastItem = within(legend).getByText(en('schedule.legend.forecast')).closest('li');
    const baselineItem = within(legend).getByText(en('schedule.legend.baseline')).closest('li');
    expect(forecastItem?.querySelector('.gantt-legend__swatch')?.className).toContain(
      'gantt__bar--forecast',
    );
    expect(baselineItem?.querySelector('.gantt-legend__swatch')?.className).toContain(
      'gantt__bar--baseline',
    );
    expect(
      within(legend).getByText(
        en('schedule.baseline.name', {
          version: 1,
          revision: 1,
          type: en('schedule.baselineType.APPROVED'),
        }),
      ),
    ).toBeTruthy();

    const forecast = screen.getByTestId('forecast-bar-2');
    const baselineBar = screen.getByTestId('baseline-bar-2');
    expect(forecast.className).toContain('gantt__bar--forecast');
    expect(baselineBar.className).toContain('gantt__bar--baseline');
    expect(forecast.className).not.toContain('gantt__bar--baseline');
    // The forecast is two days later than the baseline: the same width, a later start.
    expect(forecast.style.inlineSize).toBe(baselineBar.style.inlineSize);
    expect(parseFloat(forecast.style.insetInlineStart)).toBeGreaterThan(
      parseFloat(baselineBar.style.insetInlineStart),
    );
    // A screen reader hears both ranges and the variance in the row.
    expect(
      screen.getByText(
        new RegExp(
          escape(en('schedule.gantt.baselineRange', { start: '2026-11-16', finish: '2026-11-25' })),
        ),
      ),
    ).toBeTruthy();
  });

  test('every bar and tick is placed in per cent of the timeline, so the chart fits its width (criterion 3)', async () => {
    open(reviewerSession(), {}, { path: GANTT_PATH });
    await screen.findByTestId('forecast-bar-1.1');
    const placed = [...document.querySelectorAll<HTMLElement>('.gantt__bar, .gantt__tick')];
    expect(placed.length).toBeGreaterThan(0);
    for (const element of placed) {
      expect(element.style.insetInlineStart).toMatch(/%$/);
      if (element.classList.contains('gantt__bar')) {
        expect(element.style.inlineSize).toMatch(/%$/);
      }
    }
  });

  test('a reader who does not plan the schedule cannot move anything', async () => {
    open(reviewerSession(), {}, { path: GANTT_PATH });
    await screen.findByTestId('forecast-bar-1.1');
    expect(screen.queryAllByRole('slider')).toHaveLength(0);
  });

  test('a forecast left behind its predecessor’s slipped forecast is marked, and the legend says why', async () => {
    const activities = baselinedActivities().map((item) =>
      item.id === EXCAVATION_ID ? { ...item, forecastFinishDate: '2026-11-20' } : item,
    );
    open(reviewerSession(), { activities }, { path: GANTT_PATH });

    expect((await screen.findByTestId('forecast-bar-2')).className).toContain(
      'gantt__bar--conflict',
    );
    expect(screen.getAllByText(en('schedule.legend.conflict')).length).toBeGreaterThan(0);
  });

  test.each<Language>(['en', 'ar'])('has no axe violation (%s)', async (language) => {
    open(entitySession(), {}, { path: GANTT_PATH, language });
    await screen.findByTestId('forecast-bar-1.1');
    const violations = await accessibilityViolations();
    expect(violations, describeViolations(violations)).toEqual([]);
  });
});

describe('SCR-045 drag-adjustment is held to the dependency rules', () => {
  test('before a baseline, moving a bar with the keyboard moves the plan: the requested start is PUT with the ETag', async () => {
    const api = open(entitySession(), PLANNING, { path: GANTT_PATH });
    api.on('PUT', /^\/schedule-activities\/[^/]+$/, { body: activity() });

    const bar = await screen.findByRole('slider', { name: moveName('1.1 · Survey') });
    bar.focus();
    await userEvent.keyboard('{ArrowRight}{ArrowRight}{ArrowRight}');
    expect(bar.getAttribute('aria-valuetext')).toBe(
      en('schedule.gantt.forecastRange', { start: '2026-11-04', finish: '2026-11-08' }),
    );
    await userEvent.keyboard('{Enter}');

    expect(
      await screen.findByText(
        en('schedule.done.planMoved', {
          activity: '1.1',
          start: '2026-11-04',
          finish: '2026-11-08',
        }),
      ),
    ).toBeTruthy();
    const sent = api.requestsTo('PUT', /^\/schedule-activities\/[^/]+$/)[0];
    expect(sent?.path).toBe(`/schedule-activities/${SURVEY_ID}`);
    expect(sent?.headers.get('If-Match')).toBe(ACTIVITY_ETAG);
    expect(sent?.body).toEqual({
      parentActivityId: activity().parentActivityId,
      wbsCode: '1.1',
      name: { text: 'Survey', language: 'en' },
      requestedStartDate: '2026-11-04',
      plannedDurationDays: 5,
      sortOrder: 1,
    });
  });

  test('a move before the predecessor allows is held at the earliest start and explained; nothing is sent', async () => {
    const api = open(entitySession(), PLANNING, { path: GANTT_PATH });

    const bar = await screen.findByRole('slider', { name: moveName('1.2 · Excavation') });
    bar.focus();
    await userEvent.keyboard('{ArrowLeft}{ArrowLeft}');

    const held = en('schedule.constraint.held', {
      date: '2026-11-06',
      predecessor: '1.1',
      type: en('schedule.dependencyTypeShort.FS'),
      lag: en('schedule.days.many', { days: 0 }),
    });
    expect(document.querySelector('.gantt__status')?.textContent).toBe(held);
    expect(bar.getAttribute('aria-valuetext')).toBe(
      en('schedule.gantt.forecastRange', { start: '2026-11-06', finish: '2026-11-15' }),
    );
    await userEvent.keyboard('{Enter}');

    expect(await screen.findByText(`${held} ${en('schedule.gantt.unchanged')}`)).toBeTruthy();
    expect(api.requestsTo('GET', /^\/schedule-activities\/[^/]+$/)).toHaveLength(0);
    expect(api.requestsTo('PUT', /^\/schedule-activities\/[^/]+$/)).toHaveLength(0);
  });

  test('after a baseline, dragging a bar with the pointer reforecasts it; the baseline bar does not move', async () => {
    const api = open(entitySession(), {}, { path: GANTT_PATH });
    api.on('POST', /^\/schedule-activities\/[^/]+\/reforecast$/, { body: activity() });

    const bar = await screen.findByRole('slider', { name: moveName('2 · Paving') });
    const baselineBefore = screen.getByTestId('baseline-bar-2').style.insetInlineStart;
    // The timeline runs 2026-10-25 to 2026-12-04 (the dates, a week either side): 41 days on a 410 px track.
    const track = bar.parentElement;
    if (track === null) {
      throw new Error('The bar has no track.');
    }
    track.getBoundingClientRect = () => ({ width: 410 }) as DOMRect;

    fireEvent.pointerDown(bar, { clientX: 200, pointerId: 1, button: 0 });
    expect(bar.getAttribute('data-pointer-capture')).toBe('1');
    fireEvent.pointerMove(bar, { clientX: 250, pointerId: 1 });
    expect(screen.getByTestId('baseline-bar-2').style.insetInlineStart).toBe(baselineBefore);
    fireEvent.pointerUp(bar, { clientX: 250, pointerId: 1 });

    expect(
      await screen.findByText(
        en('schedule.done.forecastMoved', {
          activity: '2',
          start: '2026-11-23',
          finish: '2026-12-02',
        }),
      ),
    ).toBeTruthy();
    const sent = api.requestsTo('POST', /reforecast$/)[0];
    expect(sent?.path).toBe(`/schedule-activities/${PAVING_ID}/reforecast`);
    expect(sent?.body).toEqual({
      forecastStartDate: '2026-11-23',
      forecastFinishDate: '2026-12-02',
    });
    expect(sent?.headers.get('If-Match')).toBe(ACTIVITY_ETAG);
  });

  test('in Arabic the timeline mirrors: the left arrow moves a bar later', async () => {
    const api = open(entitySession(), PLANNING, { path: GANTT_PATH, language: 'ar' });
    api.on('PUT', /^\/schedule-activities\/[^/]+$/, { body: activity() });

    const bar = await screen.findByRole('slider', {
      name: translate('ar', 'schedule.gantt.moveLabel', { activity: '1.1 · Survey' }),
    });
    bar.focus();
    await userEvent.keyboard('{ArrowLeft}{Enter}');

    await waitFor(() => {
      expect(api.requestsTo('PUT', /^\/schedule-activities\/[^/]+$/)).toHaveLength(1);
    });
    expect(api.requestsTo('PUT', /^\/schedule-activities\/[^/]+$/)[0]?.body).toMatchObject({
      requestedStartDate: '2026-11-02',
    });
  });

  test('an activity changed since the chart was drawn is not overwritten: nothing is sent and the chart is read again', async () => {
    const api = open(entitySession(), PLANNING, { path: GANTT_PATH });
    api.on('GET', new RegExp(`^/schedule-activities/${SURVEY_ID}$`), {
      body: { ...plannedActivities()[1], updatedAt: '2026-10-03T09:00:00Z' },
      headers: { ETag: '"32"' },
    });

    const bar = await screen.findByRole('slider', { name: moveName('1.1 · Survey') });
    bar.focus();
    await userEvent.keyboard('{ArrowRight}{Enter}');

    expect(await screen.findByText(en('schedule.done.stale'))).toBeTruthy();
    expect(api.requestsTo('PUT', /^\/schedule-activities\/[^/]+$/)).toHaveLength(0);
  });

  test('Escape cancels a move without sending anything', async () => {
    const api = open(entitySession(), PLANNING, { path: GANTT_PATH });
    const bar = await screen.findByRole('slider', { name: moveName('1.1 · Survey') });
    bar.focus();
    await userEvent.keyboard('{ArrowRight}{Escape}');
    expect(await screen.findByText(en('schedule.gantt.cancelled'))).toBeTruthy();
    expect(bar.getAttribute('aria-valuetext')).toBe(
      en('schedule.gantt.forecastRange', { start: '2026-11-01', finish: '2026-11-05' }),
    );
    expect(api.requestsTo('GET', /^\/schedule-activities\/[^/]+$/)).toHaveLength(0);
  });

  test('while a candidate is under review, the plan’s bars cannot be moved and the person is told why', async () => {
    open(
      entitySession(),
      { ...PLANNING, baselines: [candidate({ status: 'SUBMITTED' })] },
      { path: GANTT_PATH },
    );
    expect(await screen.findByText(en('schedule.gantt.frozen'))).toBeTruthy();
    expect(screen.queryAllByRole('slider')).toHaveLength(0);
  });
});

describe('SCR-061 Baseline View, MOD-017 Submit Baseline and MOD-018 Approve Baseline', () => {
  test('lists the baselines and shows the ACTIVE one’s frozen copy with the API’s variance', async () => {
    open(entitySession(), { baselines: [candidate(), baseline()] }, { path: BASELINES_PATH });

    const list = await screen.findByRole('region', { name: en('schedule.baselines.caption') });
    expect(within(list).getAllByRole('row')).toHaveLength(3);
    const copy = await screen.findByRole('region', { name: en('schedule.baselines.copyCaption') });
    const paving = rowAt(copy, 4);
    expect(paving.getByText('2')).toBeTruthy();
    expect(paving.getByText('Paving')).toBeTruthy();
    expect(paving.getByText(en('schedule.variance.late', { days: 2 }))).toBeTruthy();
    expect(
      screen.getByText(
        en('schedule.baselines.copyDependency', {
          predecessor: '1.2',
          successor: '2',
          type: en('schedule.dependencyTypeShort.FS'),
          lag: en('schedule.days.many', { days: 0 }),
        }),
      ),
    ).toBeTruthy();
  });

  test('a candidate has no frozen copy until it activates, and says so', async () => {
    open(
      entitySession(),
      { baselines: [candidate(), baseline()] },
      {
        path: `${BASELINES_PATH}?baselineId=${CANDIDATE_ID}`,
      },
    );
    expect(await screen.findByText(en('schedule.baselines.candidateNoCopy'))).toBeTruthy();
  });

  test('the Project Manager creates a candidate from the working schedule', async () => {
    const api = open(entitySession(), { baselines: [baseline()] }, { path: BASELINES_PATH });
    api.on('POST', /^\/project-baselines$/, { status: 201, body: candidate() });

    await userEvent.click(
      await screen.findByRole('button', { name: en('schedule.actions.createCandidate') }),
    );

    expect(await screen.findByText(en('schedule.done.candidateCreated'))).toBeTruthy();
    expect(api.requestsTo('POST', /^\/project-baselines$/)[0]?.body).toEqual({
      projectId: PROJECT_ID,
    });
  });

  test('MOD-017 submits the first baseline without a change authorisation and says what happens', async () => {
    const api = open(
      entitySession(),
      { ...PLANNING, baselines: [candidate({ versionNo: 1 })] },
      {
        path: BASELINES_PATH,
      },
    );
    api.on('POST', /\/submit$/, { body: candidate({ versionNo: 1, status: 'SUBMITTED' }) });

    const dialog = await openDialog(en('schedule.actions.submitBaseline'), 'schedule.submit.title');
    expect(within(dialog).getByText(en('schedule.submit.consequence'))).toBeTruthy();
    expect(within(dialog).getByText('2026-11-25')).toBeTruthy(); // the working schedule's planned finish
    expect(
      within(dialog).queryByLabelText(new RegExp(en('schedule.submit.changeAuthorization'))),
    ).toBeNull();
    await userEvent.click(
      within(dialog).getByRole('button', { name: en('schedule.submit.confirm') }),
    );

    expect(await screen.findByText(en('schedule.done.baselineSubmitted'))).toBeTruthy();
    const sent = api.requestsTo('POST', /\/submit$/)[0];
    expect(sent?.path).toBe(`/project-baselines/${CANDIDATE_ID}/submit`);
    expect(sent?.body).toEqual({ changeAuthorizationId: null });
  });

  test('MOD-017: a rebaseline needs the change authorisation’s ID; under a Light profile it activates at once', async () => {
    const api = open(
      entitySession(),
      { baselines: [candidate(), baseline()] },
      { path: BASELINES_PATH },
    );
    api.on('POST', /\/submit$/, { body: candidate({ status: 'ACTIVE' }) });

    const dialog = await openDialog(en('schedule.actions.submitBaseline'), 'schedule.submit.title');
    const field = labelled(dialog, 'schedule.submit.changeAuthorization');
    await userEvent.type(field, 'CR-12');
    await userEvent.click(
      within(dialog).getByRole('button', { name: en('schedule.submit.confirm') }),
    );
    expect(field.getAttribute('aria-invalid')).toBe('true');
    expect(api.requestsTo('POST', /\/submit$/)).toHaveLength(0);

    await userEvent.clear(field);
    await userEvent.type(field, CHANGE_AUTHORIZATION_ID);
    await userEvent.click(
      within(dialog).getByRole('button', { name: en('schedule.submit.confirm') }),
    );

    expect(await screen.findByText(en('schedule.done.baselineActivated'))).toBeTruthy();
    expect(api.requestsTo('POST', /\/submit$/)[0]?.body).toEqual({
      changeAuthorizationId: CHANGE_AUTHORIZATION_ID,
    });
  });

  test('MOD-017: an unconfigured approval route is explained, and the candidate stays a draft', async () => {
    const api = open(
      entitySession(),
      { ...PLANNING, baselines: [candidate({ versionNo: 1 })] },
      {
        path: BASELINES_PATH,
      },
    );
    api.on('POST', /\/submit$/, problem(422, 'CONFIGURATION_MISSING'));

    const dialog = await openDialog(en('schedule.actions.submitBaseline'), 'schedule.submit.title');
    await userEvent.click(
      within(dialog).getByRole('button', { name: en('schedule.submit.confirm') }),
    );
    expect(
      await within(dialog).findByText(en('schedule.problems.configurationMissing')),
    ).toBeTruthy();
  });

  test('MOD-018: an AHDA reviewer decides on the task in their inbox', async () => {
    const run = scheduleRun();
    const api = open(
      reviewerSession(),
      { baselines: [candidate({ status: 'SUBMITTED' }), baseline()], inbox: [scheduleTask()] },
      { path: BASELINES_PATH },
    );
    api.on('POST', /^\/approval-tasks\/[^/]+\/(approve|return|reject)$/, { body: run });

    const dialog = await openDialog(
      en('schedule.actions.decideBaseline'),
      'schedule.approve.title',
    );
    expect(await within(dialog).findByText(/Huda Contractor/)).toBeTruthy();
    expect(api.requestsTo('GET', /^\/approval-instances$/)[0]?.query.get('subjectId')).toBe(
      CANDIDATE_ID,
    );

    // Returning needs a reason the Project Manager reads.
    await userEvent.click(within(dialog).getByLabelText(en('schedule.approve.decisions.return')));
    await userEvent.click(
      within(dialog).getByRole('button', { name: en('schedule.approve.confirm') }),
    );
    expect(api.requestsTo('POST', /^\/approval-tasks\//)).toHaveLength(0);

    await userEvent.click(within(dialog).getByLabelText(en('schedule.approve.decisions.approve')));
    expect(within(dialog).getByText(en('schedule.approve.consequences.approve'))).toBeTruthy();
    await userEvent.click(
      within(dialog).getByRole('button', { name: en('schedule.approve.confirm') }),
    );

    expect(await screen.findByText(en('schedule.done.approved'))).toBeTruthy();
    const sent = api.requestsTo('POST', /^\/approval-tasks\//)[0];
    expect(sent?.path).toBe(`/approval-tasks/${TASK_ID}/approve`);
    expect(sent?.body).toEqual({});
  });

  test('MOD-018 is never offered to the person who submitted the baseline (ADR-013)', async () => {
    open(
      entitySession(),
      { baselines: [candidate({ status: 'SUBMITTED' }), baseline()], runs: [scheduleRun()] },
      { path: BASELINES_PATH },
    );
    expect(
      await screen.findByRole('link', { name: en('schedule.baselines.reviewHistory') }),
    ).toBeTruthy();
    expect(
      screen.queryByRole('button', { name: en('schedule.actions.decideBaseline') }),
    ).toBeNull();
  });

  test('MOD-018 is not offered to a person without approval permissions, and nothing fails', async () => {
    const api = open(
      reviewerSession(),
      { baselines: [candidate({ status: 'SUBMITTED' }), baseline()] },
      { path: BASELINES_PATH },
    );
    api.on('GET', /^\/approval-instances$/, problem(403, 'PERMISSION_DENIED'));
    api.on('GET', /^\/approval-tasks$/, problem(403, 'PERMISSION_DENIED'));
    await screen.findByRole('region', { name: en('schedule.baselines.caption') });
    await waitFor(() => {
      expect(api.requestsTo('GET', /^\/approval-tasks$/)).toHaveLength(1);
    });
    expect(
      screen.queryByRole('button', { name: en('schedule.actions.decideBaseline') }),
    ).toBeNull();
    expect(screen.queryByRole('alert')).toBeNull();
  });

  test('a delegated decision says on whose behalf it is taken', async () => {
    open(
      reviewerSession(),
      {
        baselines: [candidate({ status: 'SUBMITTED' }), baseline()],
        inbox: [scheduleTask({ onBehalfOfUserId: OTHER_PERSON_ID })],
      },
      { path: BASELINES_PATH },
    );
    const dialog = await openDialog(
      en('schedule.actions.decideBaseline'),
      'schedule.approve.title',
    );
    expect(
      await within(dialog).findByText(
        en('schedule.approve.onBehalfOf', { person: 'Nora Manager' }),
      ),
    ).toBeTruthy();
  });

  test('has no axe violation with MOD-018 open', async () => {
    open(
      reviewerSession(),
      { baselines: [candidate({ status: 'SUBMITTED' }), baseline()], inbox: [scheduleTask()] },
      { path: BASELINES_PATH },
    );
    const dialog = await openDialog(
      en('schedule.actions.decideBaseline'),
      'schedule.approve.title',
    );
    await within(dialog).findByText(/Huda Contractor/);
    const violations = await accessibilityViolations();
    expect(violations, describeViolations(violations)).toEqual([]);
  });
});

describe('the Schedule tab sits in the workspace', () => {
  test('its three views are linked from each other, the current one marked', async () => {
    open(entitySession(), {}, { path: GANTT_PATH });
    const nav = await screen.findByRole('navigation', { name: en('schedule.nav.label') });
    expect(
      within(nav)
        .getByRole('link', { name: en('schedule.nav.gantt') })
        .getAttribute('aria-current'),
    ).toBe('page');
    expect(
      within(nav)
        .getByRole('link', { name: en('schedule.nav.manager') })
        .getAttribute('href'),
    ).toBe(SCHEDULE_PATH);
    expect(
      screen
        .getByRole('link', { name: en('projects.workspace.tabs.schedule') })
        .getAttribute('aria-current'),
    ).toBe('page');
  });

  test('a read refused for want of SCHEDULE_VIEW says so instead of failing', async () => {
    const api = withSchedule(withProject(withProjectLookups(mockApi()), activeProject()));
    api.on('GET', /^\/schedule-activities$/, problem(403, 'PERMISSION_DENIED'));
    renderApp({ path: SCHEDULE_PATH, session: entitySession() });

    expect(await screen.findByText(en('schedule.forbidden'))).toBeTruthy();
    expect(screen.queryByRole('alert')).toBeNull();
  });
});
