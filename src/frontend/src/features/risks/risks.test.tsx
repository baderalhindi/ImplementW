import { cleanup, screen, waitFor, within } from '@testing-library/react';
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
import {
  ACTION_ETAG,
  BARRIER_ACTION_ID,
  compactMatrix,
  COST_DIMENSION_ID,
  FLOODING_ID,
  risk,
  RISK_ETAG,
  type RiskState,
  SCHEDULE_DIMENSION_ID,
  treatmentAction,
  UTILITY_ID,
  WEATHER_CATEGORY_ID,
  withRisks,
} from '@/test/riskFixtures.ts';
import { day } from '@/test/taskFixtures.ts';

// SCR-080 Risk Register, SCR-081 Critical Risks, SCR-082 Risk Detail and MOD-030 to MOD-035 (TASK-056), against the
// TASK-055 API. Acceptance criteria: (1) the risk matrix visualization reflects the currently published
// probability/impact configuration, not a hardcoded matrix; (2) closing a risk requires a non-empty closure rationale,
// enforced client- and server-side. The workbook's validation checks: change the published matrix and the heat-map
// re-renders on the next load; closing with an empty rationale is rejected.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);

const RISKS_PATH = `/projects/${PROJECT_ID}/risks`;
const FLOODING_PATH = `${RISKS_PATH}/${FLOODING_ID}`;

/** The Coastal road upgrade, ACTIVE, managed by the entity's user, Huda (ADR-013). */
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
  state: RiskState = {},
  {
    session = entitySession(),
    project = activeProject(),
    language = 'en',
  }: { session?: Session; project?: ProjectDetail; language?: Language } = {},
): MockApi {
  const api = withRisks(withProject(withProjectLookups(mockApi()), project), state);
  renderApp({ path, session, language });
  return api;
}

function titlesIn(table: HTMLElement): string[] {
  return within(table)
    .getAllByRole('row')
    .slice(1)
    .map((row) => within(row).getAllByRole('link')[0]?.textContent ?? '');
}

/** The heat-map's cells, row by row from the top (most probable), as `level:level=rating`. */
function matrixCells(table: HTMLElement): string[][] {
  return within(table)
    .getAllByRole('row')
    .slice(1)
    .map((row) =>
      Array.from(row.querySelectorAll<HTMLElement>('td')).map(
        (cell) => `${cell.dataset.cell ?? ''}=${cell.dataset.rating ?? ''}`,
      ),
    );
}

async function press(scope: HTMLElement, name: string) {
  await userEvent.click(within(scope).getByRole('button', { name }));
}

async function openDialog(name: string, title: string): Promise<HTMLElement> {
  await userEvent.click(await screen.findByRole('button', { name }));
  return screen.findByRole('dialog', { name: title });
}

describe('SCR-080 Risk Register', () => {
  test("lists the project's open risks most severe first, with the server's rating, levels and due reviews", async () => {
    open(RISKS_PATH);

    const table = await screen.findByRole('region', { name: en('risks.project.caption') });
    expect(titlesIn(table)).toEqual([
      'Flooding of the works',
      'Asphalt price rise',
      'Utility relocation delay',
    ]);
    const flooding = within(table)
      .getByRole('link', { name: 'Flooding of the works' })
      .closest('tr');
    expect(flooding?.className).toBe('row--overdue');
    expect(within(flooding as HTMLElement).getByText('Critical')).toBeTruthy();
    expect(
      within(flooding as HTMLElement).getByText(
        en('risks.table.levels', { probability: 4, impact: 5 }),
      ),
    ).toBeTruthy();
    expect(within(flooding as HTMLElement).getByText(en('risks.table.reviewDue'))).toBeTruthy();
    expect(within(flooding as HTMLElement).getByText('Weather')).toBeTruthy();
    // An unassessed risk says so: never a low rating (WF-06 §8).
    const utility = within(table)
      .getByRole('link', { name: 'Utility relocation delay' })
      .closest('tr');
    expect(within(utility as HTMLElement).getByText(en('risks.rating.none'))).toBeTruthy();
    expect(
      within(table).getByRole('link', { name: 'Flooding of the works' }).getAttribute('href'),
    ).toBe(FLOODING_PATH);

    const critical = document.querySelector('[data-summary="critical"] dd');
    expect(critical?.textContent).toBe('1');
    expect(describeViolations(await accessibilityViolations())).toBe('');
  });

  test('the rating filter offers the published ratings, and the views include closed risks', async () => {
    open(RISKS_PATH);
    await screen.findByRole('region', { name: en('risks.project.caption') });

    const ratingFilter = screen.getByRole('combobox', { name: en('risks.filter.rating') });
    expect(
      within(ratingFilter)
        .getAllByRole('option')
        .map((option) => option.textContent),
    ).toEqual([en('common.filters.any'), 'Low', 'Medium', 'High', 'Critical']);
    await userEvent.selectOptions(ratingFilter, 'MEDIUM');
    expect(titlesIn(screen.getByRole('region', { name: en('risks.project.caption') }))).toEqual([
      'Asphalt price rise',
    ]);
    await userEvent.selectOptions(ratingFilter, '');
    await userEvent.selectOptions(
      screen.getByRole('combobox', { name: en('risks.filter.view') }),
      'closed',
    );
    expect(titlesIn(screen.getByRole('region', { name: en('risks.project.caption') }))).toEqual([
      'Survey access refused',
    ]);
  });

  test('without the matrix the ratings still read in words, and the critical count is unknown, not zero', async () => {
    open(RISKS_PATH, { matrix: 'forbidden' });

    const table = await screen.findByRole('region', { name: en('risks.project.caption') });
    expect(within(table).getByText('Critical')).toBeTruthy();
    expect(document.querySelector('[data-summary="critical"] dd')?.textContent).toBe(
      en('risks.summary.unknown'),
    );
  });

  test('across projects, each risk names its project; reached from the sidebar', async () => {
    open('/risks');

    const table = await screen.findByRole('region', { name: en('risks.register.caption') });
    // Equally critical risks are ordered by their next review.
    expect(titlesIn(table)).toEqual([
      'Flooding of the works',
      'Deck corrosion',
      'Asphalt price rise',
      'Utility relocation delay',
    ]);
    expect(within(table).getAllByRole('link', { name: 'Harbour bridge repair' })).toHaveLength(1);
    expect(screen.getByRole('link', { name: en('risks.nav.register') })).toBeTruthy();
  });
});

describe('SCR-081 Critical Risks and the heat-map (acceptance criterion 1)', () => {
  test('draws the published version: its levels, labels and mapping, with open risks counted per cell', async () => {
    open('/risks/critical');

    const grid = await screen.findByRole('table', { name: en('risks.critical.matrixCaption') });
    const cells = matrixCells(grid);
    expect(cells).toHaveLength(5);
    expect(cells[0]).toEqual([
      '5:1=MEDIUM',
      '5:2=HIGH',
      '5:3=CRITICAL',
      '5:4=CRITICAL',
      '5:5=CRITICAL',
    ]);
    expect(cells[4]).toEqual(['1:1=LOW', '1:2=LOW', '1:3=LOW', '1:4=LOW', '1:5=MEDIUM']);
    expect(
      within(grid).getByRole('rowheader', {
        name: en('risks.matrix.probabilityLevel', { level: 5, label: 'Almost certain' }),
      }),
    ).toBeTruthy();
    expect(
      within(grid)
        .getAllByRole('columnheader')
        .map((header) => header.textContent),
    ).toEqual([
      en('risks.matrix.axes'),
      ...[1, 2, 3, 4, 5].map((level) => en('risks.matrix.impactLevel', { level })),
    ]);
    // Flooding (4 × 5) and Deck corrosion (5 × 4) are each counted in their cell; the closed risk is not.
    const flooding = grid.querySelector('[data-cell="4:5"]');
    expect(flooding?.textContent).toContain(en('risks.matrix.count', { count: 1 }));
    expect(grid.querySelector('[data-cell="1:2"]')?.textContent).toContain(
      en('risks.matrix.count', { count: 0 }),
    );
    expect(
      screen.getByText(en('risks.matrix.version', { version: 1, date: '2026-09-01' })),
    ).toBeTruthy();

    const critical = screen.getByRole('region', { name: en('risks.critical.caption') });
    expect(titlesIn(critical)).toEqual(['Flooding of the works', 'Deck corrosion']);
    expect(
      screen.getByText(en('risks.critical.criterion', { rating: 'Critical', version: 1 })),
    ).toBeTruthy();
    expect(describeViolations(await accessibilityViolations())).toBe('');
  });

  test('a re-published matrix re-renders the heat-map on the next load: another size, labels and mapping', async () => {
    open('/risks/critical');
    await screen.findByRole('table', { name: en('risks.critical.matrixCaption') });
    cleanup();

    // The workbook's check: the configuration changes, the screen is loaded again.
    open('/risks/critical', { matrix: compactMatrix() });
    const grid = await screen.findByRole('table', { name: en('risks.critical.matrixCaption') });
    expect(matrixCells(grid)).toEqual([
      ['3:1=MINOR', '3:2=MAJOR', '3:3=MAJOR'],
      ['2:1=MINOR', '2:2=MINOR', '2:3=MAJOR'],
      ['1:1=MINOR', '1:2=MINOR', '1:3=MINOR'],
    ]);
    expect(within(grid).getAllByText('Major')).toHaveLength(3);
    expect(within(grid).queryByText('Critical')).toBeNull();
    expect(
      screen.getByText(en('risks.matrix.version', { version: 2, date: '2026-10-01' })),
    ).toBeTruthy();
    // Critical now means version 2's top rating, which no risk carries.
    expect(
      screen.getByText(en('risks.critical.criterion', { rating: 'Major', version: 2 })),
    ).toBeTruthy();
    expect(screen.getByText(en('risks.empty.critical'))).toBeTruthy();
  });

  test('with no published matrix it says so and draws no grid in its stead', async () => {
    open('/risks/critical', { matrix: 'missing' });

    expect(await screen.findByText(en('risks.matrix.missing'))).toBeTruthy();
    expect(screen.getByText(en('risks.critical.needsMatrix'))).toBeTruthy();
    expect(screen.queryByRole('table')).toBeNull();
  });

  test('without CONFIGURATION_VIEW it says the matrix cannot be read', async () => {
    open('/risks/critical', { matrix: 'forbidden' });

    expect(await screen.findByText(en('risks.matrix.forbidden'))).toBeTruthy();
    expect(screen.queryByRole('table')).toBeNull();
  });
});

describe('SCR-082 Risk Detail', () => {
  test('shows the risk, its marked cell on the matrix in force, its assessment history and treatment plan', async () => {
    open(FLOODING_PATH, { actions: [treatmentAction()] });

    expect(
      await screen.findByRole('heading', { level: 2, name: 'Flooding of the works' }),
    ).toBeTruthy();
    const grid = screen.getByRole('table', { name: en('risks.detail.matrixCaption') });
    const marked = grid.querySelector('.risk-matrix__cell--marked');
    expect((marked as HTMLElement | null)?.dataset.cell).toBe('4:5');
    expect(marked?.textContent).toContain(en('risks.detail.marked'));
    const history = screen.getByRole('region', { name: en('risks.detail.assessmentsCaption') });
    expect(
      within(history).getByText(en('risks.detail.impact', { dimension: 'Cost', level: 5 })),
    ).toBeTruthy();
    expect(
      within(history).getByText(en('risks.detail.impact', { dimension: 'Schedule', level: 3 })),
    ).toBeTruthy();
    const plan = screen.getByRole('region', { name: en('risks.detail.actionsCaption') });
    expect(within(plan).getByText('Install flood barriers')).toBeTruthy();
    // The Project Manager (external) manages; assessing is AHDA's.
    expect(screen.getByRole('button', { name: en('risks.actions.close') })).toBeTruthy();
    expect(screen.queryByRole('button', { name: en('risks.actions.reassess') })).toBeNull();
    expect(describeViolations(await accessibilityViolations())).toBe('');
  });

  test('a rating given under another version is kept as given and said so', async () => {
    open(FLOODING_PATH, { matrix: compactMatrix() });

    expect(await screen.findByText(en('risks.detail.otherVersion'))).toBeTruthy();
    expect(screen.getAllByText('Critical').length).toBeGreaterThan(0);
  });

  test('a risk of another project is not shown under this one', async () => {
    open(`${RISKS_PATH}/${FLOODING_ID}`, {
      risks: [risk({ projectId: 'f9000000-0000-4000-8000-000000000999' })],
    });

    expect(await screen.findByText(en('risks.detail.notFound'))).toBeTruthy();
  });

  test('renders in Arabic without axe violations', async () => {
    open(FLOODING_PATH, {}, { language: 'ar' });

    await screen.findByRole('heading', { level: 2, name: 'Flooding of the works' });
    expect(
      screen.getByRole('table', { name: translate('ar', 'risks.detail.matrixCaption') }),
    ).toBeTruthy();
    expect(screen.getAllByText('حرج').length).toBeGreaterThan(0);
    const violations = await accessibilityViolations();
    expect(violations, describeViolations(violations)).toEqual([]);
  });
});

describe('MOD-035 Close Risk (acceptance criterion 2)', () => {
  test('an empty or blank rationale is refused before anything is sent', async () => {
    const api = open(FLOODING_PATH);
    const dialog = await openDialog(
      en('risks.actions.close'),
      en('risks.close.title', { risk: 'Flooding of the works' }),
    );

    await press(dialog, en('risks.close.confirm'));
    expect(within(dialog).getByText(en('risks.fieldErrors.rationaleRequired'))).toBeTruthy();
    expect(within(dialog).getByRole('alert').textContent).toBe(
      en('common.form.fixErrors', { count: 1 }),
    );

    await userEvent.type(
      within(dialog).getByRole('textbox', { name: /Closure rationale/ }),
      '    ',
    );
    await press(dialog, en('risks.close.confirm'));
    expect(within(dialog).getByText(en('risks.fieldErrors.rationaleRequired'))).toBeTruthy();
    expect(api.requestsTo('POST', /\/close$/)).toHaveLength(0);
  });

  test("the server's refusal of a blank rationale (400 REQUIRED) lands on the same field", async () => {
    const api = open(FLOODING_PATH);
    api.on(
      'POST',
      /^\/risks\/[^/]+\/close$/,
      problem(400, 'VALIDATION_FAILED', [{ field: 'rationale.text', code: 'REQUIRED' }]),
    );
    const dialog = await openDialog(
      en('risks.actions.close'),
      en('risks.close.title', { risk: 'Flooding of the works' }),
    );

    await userEvent.type(
      within(dialog).getByRole('textbox', { name: /Closure rationale/ }),
      'Done',
    );
    await press(dialog, en('risks.close.confirm'));

    const field = within(dialog).getByRole('textbox', { name: /Closure rationale/ });
    await waitFor(() => {
      expect(field.getAttribute('aria-invalid')).toBe('true');
    });
    expect(within(dialog).getByText(en('risks.fieldErrors.rationaleRequired'))).toBeTruthy();
    expect(screen.getByRole('dialog')).toBeTruthy();
  });

  test('closes with the rationale and the ETag, warning of the open actions', async () => {
    const api = open(FLOODING_PATH, { actions: [treatmentAction()] });
    api.on('POST', /^\/risks\/[^/]+\/close$/, {
      body: risk({ status: 'CLOSED' }),
      headers: { ETag: '"72"' },
    });
    const dialog = await openDialog(
      en('risks.actions.close'),
      en('risks.close.title', { risk: 'Flooding of the works' }),
    );
    expect(within(dialog).getByText(en('risks.close.openActions', { count: 1 }))).toBeTruthy();

    await userEvent.type(
      within(dialog).getByRole('textbox', { name: /Closure rationale/ }),
      '  Barriers installed; the rainy season is over.  ',
    );
    await press(dialog, en('risks.close.confirm'));

    expect(await screen.findByText(en('risks.done.closed'))).toBeTruthy();
    const [request] = api.requestsTo('POST', /\/close$/);
    expect(request?.path).toBe(`/risks/${FLOODING_ID}/close`);
    expect(request?.body).toEqual({
      rationale: { text: 'Barriers installed; the rainy season is over.', language: 'en' },
    });
    expect(request?.headers.get('If-Match')).toBe(RISK_ETAG);
  });

  test('a risk changed meanwhile (412) is read again, nothing saved', async () => {
    const api = open(FLOODING_PATH);
    api.on('POST', /^\/risks\/[^/]+\/close$/, problem(412, 'PRECONDITION_FAILED'));
    const dialog = await openDialog(
      en('risks.actions.close'),
      en('risks.close.title', { risk: 'Flooding of the works' }),
    );
    await userEvent.type(
      within(dialog).getByRole('textbox', { name: /Closure rationale/ }),
      'Done',
    );
    await press(dialog, en('risks.close.confirm'));

    expect(await screen.findByText(en('risks.done.stale'))).toBeTruthy();
    expect(screen.queryByRole('dialog')).toBeNull();
  });
});

describe('MOD-030 to MOD-034', () => {
  test('MOD-030 checks the form, then registers the risk and lands on it', async () => {
    const api = open(RISKS_PATH);
    api.on('POST', /^\/risks$/, {
      status: 201,
      body: risk({
        id: UTILITY_ID,
        title: { text: 'Late permits', language: 'EN' },
        status: 'IDENTIFIED',
        currentAssessment: null,
      }),
      headers: { ETag: RISK_ETAG },
    });
    const dialog = await openDialog(en('risks.actions.create'), en('risks.form.createTitle'));

    await press(dialog, en('common.actions.save'));
    expect(within(dialog).getByRole('alert').textContent).toBe(
      en('common.form.fixErrors', { count: 3 }),
    );
    expect(api.requestsTo('POST', /^\/risks$/)).toHaveLength(0);

    await userEvent.type(within(dialog).getByRole('textbox', { name: /Title/ }), 'Late permits');
    await userEvent.type(
      within(dialog).getByRole('textbox', { name: /Description/ }),
      'Road permits arrive late.',
    );
    await userEvent.selectOptions(
      within(dialog).getByRole('combobox', { name: /Category/ }),
      WEATHER_CATEGORY_ID,
    );
    await press(dialog, en('common.actions.save'));

    expect(await screen.findByText(en('risks.done.created'))).toBeTruthy();
    const [request] = api.requestsTo('POST', /^\/risks$/);
    expect(request?.body).toEqual({
      projectId: PROJECT_ID,
      title: { text: 'Late permits', language: 'en' },
      description: { text: 'Road permits arrive late.', language: 'en' },
      riskCategoryItemId: WEATHER_CATEGORY_ID,
      ownerUserId: null,
      identifiedDate: day(0),
      nextReviewDate: null,
    });
  });

  test('MOD-030 is not offered to a person who does not manage the project', async () => {
    open(RISKS_PATH, {}, { session: reviewerSession() });
    await screen.findByRole('region', { name: en('risks.project.caption') });
    expect(screen.queryByRole('button', { name: en('risks.actions.create') })).toBeNull();
  });

  test('MOD-031 re-sends the register fields with the ETag', async () => {
    const api = open(FLOODING_PATH);
    api.on('PUT', /^\/risks\/[^/]+$/, { body: risk(), headers: { ETag: '"72"' } });
    const dialog = await openDialog(en('risks.actions.edit'), en('risks.form.editTitle'));

    const title = within(dialog).getByRole('textbox', { name: /Title/ });
    await userEvent.clear(title);
    await userEvent.type(title, 'Flooding of the excavation');
    await press(dialog, en('common.actions.save'));

    expect(await screen.findByText(en('risks.done.updated'))).toBeTruthy();
    const [request] = api.requestsTo('PUT', /^\/risks\//);
    expect(request?.headers.get('If-Match')).toBe(RISK_ETAG);
    expect(request?.body).toMatchObject({
      title: { text: 'Flooding of the excavation', language: 'en' },
      riskCategoryItemId: WEATHER_CATEGORY_ID,
      ownerUserId: ENTITY_USER_ID,
    });
  });

  test("MOD-032 offers the version's levels, previews the cell and sends levels only", async () => {
    const api = open(FLOODING_PATH, {}, { session: reviewerSession() });
    api.on('POST', /^\/risks\/[^/]+\/assess$/, { body: risk(), headers: { ETag: '"72"' } });
    const dialog = await openDialog(
      en('risks.actions.reassess'),
      en('risks.assess.title', { risk: 'Flooding of the works' }),
    );

    const probability = within(dialog).getByRole('combobox', { name: /Probability/ });
    expect(
      within(probability)
        .getAllByRole('option')
        .map((option) => option.textContent),
    ).toEqual([
      en('risks.assess.chooseLevel'),
      ...['Rare', 'Unlikely', 'Possible', 'Likely', 'Almost certain'].map((label, index) =>
        en('risks.assess.levelOption', { level: index + 1, label }),
      ),
    ]);
    // A reassessment starts from the latest levels: 4, cost 5, schedule 3 → Critical.
    expect(within(dialog).getByRole('status').textContent).toBe(
      en('risks.assess.preview', { probability: 4, impact: 5, rating: 'Critical' }),
    );
    await userEvent.selectOptions(probability, '1');
    await userEvent.selectOptions(within(dialog).getByRole('combobox', { name: /Cost/ }), '2');
    expect(within(dialog).getByRole('status').textContent).toBe(
      en('risks.assess.preview', { probability: 1, impact: 3, rating: 'Low' }),
    );
    expect(dialog.querySelector('.risk-matrix__cell--marked')?.getAttribute('data-cell')).toBe(
      '1:3',
    );

    await press(dialog, en('risks.assess.confirm'));
    expect(await screen.findByText(en('risks.done.assessed'))).toBeTruthy();
    const [request] = api.requestsTo('POST', /\/assess$/);
    expect(request?.body).toEqual({
      probabilityLevel: 1,
      impacts: [
        { impactDimensionItemId: COST_DIMENSION_ID, impactLevel: 2, rationale: null },
        { impactDimensionItemId: SCHEDULE_DIMENSION_ID, impactLevel: 3, rationale: null },
      ],
      rationale: null,
    });
    expect(request?.headers.get('If-Match')).toBe(RISK_ETAG);
  });

  test('MOD-032 without a published matrix says so and offers nothing to send', async () => {
    const api = open(FLOODING_PATH, { matrix: 'missing' }, { session: reviewerSession() });
    const dialog = await openDialog(
      en('risks.actions.reassess'),
      en('risks.assess.title', { risk: 'Flooding of the works' }),
    );

    expect(within(dialog).getByText(en('risks.matrix.missing'))).toBeTruthy();
    expect(within(dialog).queryByRole('button', { name: en('risks.assess.confirm') })).toBeNull();
    expect(api.requestsTo('POST', /\/assess$/)).toHaveLength(0);
  });

  test("MOD-033 re-sends the risk with the new owner; an ineligible owner's refusal is on the owner field", async () => {
    const api = open(FLOODING_PATH);
    api.on(
      'PUT',
      /^\/risks\/[^/]+$/,
      problem(422, 'RISK_OWNER_NOT_ELIGIBLE', [{ field: 'ownerUserId', code: 'NOT_ALLOWED' }]),
    );
    const dialog = await openDialog(en('risks.actions.assignOwner'), en('risks.owner.title'));

    await userEvent.click(within(dialog).getByRole('radio', { name: en('tasks.owner.other') }));
    await userEvent.type(within(dialog).getByRole('textbox', { name: /user ID/ }), OTHER_PERSON_ID);
    await press(dialog, en('risks.owner.confirm'));

    expect(await within(dialog).findByText(en('risks.problems.ownerNotEligible'))).toBeTruthy();
    const [request] = api.requestsTo('PUT', /^\/risks\//);
    expect(request?.body).toMatchObject({
      title: { text: 'Flooding of the works', language: 'en' },
      ownerUserId: OTHER_PERSON_ID,
    });
    expect(request?.headers.get('If-Match')).toBe(RISK_ETAG);
  });

  test('MOD-034 plans a treatment action for the risk', async () => {
    const api = open(FLOODING_PATH);
    api.on('POST', /^\/risk-treatment-actions$/, {
      status: 201,
      body: treatmentAction(),
      headers: { ETag: ACTION_ETAG },
    });
    const dialog = await openDialog(en('risks.actions.addAction'), en('risks.action.createTitle'));

    await userEvent.selectOptions(
      within(dialog).getByRole('combobox', { name: /Response/ }),
      'CONTINGENCY',
    );
    await userEvent.type(
      within(dialog).getByRole('textbox', { name: /Action/ }),
      'Standby pumps on site',
    );
    await press(dialog, en('common.actions.save'));

    expect(await screen.findByText(en('risks.done.actionCreated'))).toBeTruthy();
    const [request] = api.requestsTo('POST', /^\/risk-treatment-actions$/);
    expect(request?.body).toEqual({
      riskId: FLOODING_ID,
      title: { text: 'Standby pumps on site', language: 'en' },
      description: null,
      actionType: 'CONTINGENCY',
      ownerUserId: ENTITY_USER_ID,
      dueDate: null,
    });
  });

  test("an action's command is confirmed and sent with the action's own ETag", async () => {
    const api = open(FLOODING_PATH, { actions: [treatmentAction()] });
    api.on('POST', /^\/risk-treatment-actions\/[^/]+\/start$/, {
      body: treatmentAction({ status: 'IN_PROGRESS' }),
    });
    await userEvent.click(
      await screen.findByRole('button', {
        name: en('risks.actionCommand.start.named', { action: 'Install flood barriers' }),
      }),
    );
    const dialog = await screen.findByRole('dialog', {
      name: en('risks.actionCommand.start.title'),
    });
    await press(dialog, en('risks.actionCommand.start.confirm'));

    expect(await screen.findByText(en('risks.actionCommand.start.done'))).toBeTruthy();
    const [request] = api.requestsTo('POST', /\/start$/);
    expect(request?.path).toBe(`/risk-treatment-actions/${BARRIER_ACTION_ID}/start`);
    expect(request?.headers.get('If-Match')).toBe(ACTION_ETAG);
  });
});
