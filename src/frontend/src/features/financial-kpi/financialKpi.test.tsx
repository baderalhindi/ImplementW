import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { type Session } from '@/features/identity-access/session/sessionApi.ts';
import { type ProjectDetail } from '@/features/projects/api/types.ts';
import { todayUtc } from '@/features/tasks/taskRules.ts';
import { type Language, translate } from '@/shared/i18n/i18n.ts';
import {
  ASSIGNMENT_ID,
  commitment,
  DAYS_UNIT_ID,
  financialPosition,
  financialSnapshot,
  financialUpdate,
  KPI_DEFINITION_ID,
  kpiAssignment,
  measurement,
  MEASUREMENT_3_ID,
  MEASUREMENT_ETAG,
  measurements,
  monthBefore,
  sourceModes,
  targetVersions,
  UPDATE_ETAG,
  UPDATE_ID,
  withFinancials,
  withKpis,
} from '@/test/financialKpiFixtures.ts';
import { type MockApi, mockApi, page, problem } from '@/test/mockApi.ts';
import { withProgress } from '@/test/progressFixtures.ts';
import {
  ENTITY_USER_ID,
  entitySession,
  projectDetail,
  projectSummary,
  PROJECT_ID,
  REVIEWER_ID,
  reviewerSession,
  withProject,
  withProjectLookups,
} from '@/test/projectFixtures.ts';
import { OTHER_PROJECT_ID } from '@/test/taskFixtures.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';

// SCR-049 Project Financials, SCR-071 Financial Progress, SCR-050 Project KPIs, SCR-072 KPI Register, SCR-073 KPI
// History/Trend, MOD-023 and MOD-024 (TASK-053), against the WF-14 API (TASK-052). Acceptance criteria: (1) a value
// that is Missing, Stale or N/A renders with an explicit label and icon, never blank, never 0, never green; (2) the
// trend reflects a target-version change without back-editing older points. Workbook check: a KPI with no measurement
// for the current period shows "No data", not 0 or green.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);
const sar = (amount: string) => en('financialKpi.figures.sar', { amount });

const FINANCIALS = `/projects/${PROJECT_ID}/financials`;
const KPIS = `/projects/${PROJECT_ID}/kpis`;
const TREND = `${KPIS}/${ASSIGNMENT_ID}`;
const SAVED_ETAG = '"72"';
const CHART = en('financialKpi.trend.title', { kpi: 'On-time delivery', unit: 'Percent' });

function activeProject(overrides: Partial<ProjectDetail> = {}): ProjectDetail {
  return projectDetail({
    status: 'ACTIVE',
    projectManagerUserId: ENTITY_USER_ID,
    activatedAt: '2026-08-01T00:00:00Z',
    ...overrides,
  });
}

function open(
  path: string,
  session: Session,
  configure: (api: MockApi) => MockApi = (api) => withKpis(withFinancials(api)),
  {
    language = 'en',
    project = activeProject(),
  }: { language?: Language; project?: ProjectDetail } = {},
): MockApi {
  const api = configure(withProgress(withProject(withProjectLookups(mockApi()), project)));
  renderApp({ path, session, language });
  return api;
}

/** No figure on the page reads as zero: no "0", "0.00" or "SAR 0.00" anywhere in it. */
function expectNoZero(container: HTMLElement) {
  expect(container.textContent).not.toMatch(/(^|[^0-9.,])0(\.0+)?([^0-9.,]|$)/);
}

/** No success colour: the only green is a rating the API gave from a measured value. */
function expectNoGreen(container: HTMLElement) {
  expect(container.querySelector('.badge--positive')).toBeNull();
}

/** The element at `index`, or a failure naming what is missing. */
function nth<T>(items: T[], index: number): T {
  const item = items[index];
  if (item === undefined) {
    throw new Error(`Nothing at ${String(index)}.`);
  }
  return item;
}

function stateFlag(container: HTMLElement, key: string) {
  const flag = within(container).getAllByText(en(key))[0];
  if (flag === undefined) {
    throw new Error(`No ${key} flag.`);
  }
  return flag;
}

describe('criterion 1 on SCR-049: an Unknown financial figure says so, never 0 and never green', () => {
  test('a STALE official record and a MISSING live position show their state, with icon, in grey and amber', async () => {
    open(FINANCIALS, entitySession());
    const official = await screen.findByRole('region', { name: /Official financial position/ });
    const live = screen.getByRole('region', { name: /Current financial position/ });

    const stale = stateFlag(official, 'financialKpi.value.STALE');
    expect(stale.getAttribute('data-value-state')).toBe('STALE');
    expect(stale.className).toBe('value-state value-state--warning');
    expect(stale.querySelector('svg')).not.toBeNull();
    const missing = stateFlag(live, 'financialKpi.value.MISSING');
    expect(missing.getAttribute('data-value-state')).toBe('MISSING');
    expect(missing.className).toBe('value-state value-state--neutral');

    for (const region of [official, live]) {
      const status = within(region).getByText(en('financialKpi.financialStatus.UNKNOWN'));
      expect(status.className).toBe('badge badge--neutral');
      expectNoGreen(region);
      // The budget is the one figure known, with its own provenance; nothing Unknown reads as 0.
      expect(within(region).getByText(sar('1,000,000.00'))).toBeTruthy();
      expect(region.textContent).not.toContain(sar('0.00'));
    }
    expect(within(live).getByText(en('financialKpi.financials.live.unpublished'))).toBeTruthy();
  });

  test('ADR-008 extended: every figure shows its source and as-of date; a manual one is marked manual', async () => {
    open(FINANCIALS, entitySession());
    const official = await screen.findByRole('region', { name: /Official financial position/ });

    const manual = within(official).getAllByText(en('financialKpi.source.MANUAL'));
    // Budget, actual and forecast: three figures, three provenance lines, each manual.
    expect(manual).toHaveLength(3);
    for (const marker of manual) {
      expect(marker.getAttribute('data-source')).toBe('MANUAL');
      expect(marker.querySelector('svg')).not.toBeNull();
    }
    const budgetSource = within(official).getByText(
      [
        en('financialKpi.provenance.asOf', { date: '2026-08-15' }),
        en('financialKpi.provenance.reference', { reference: 'Board minute 14' }),
      ].join(' · '),
      { exact: false },
    );
    expect(budgetSource).toBeTruthy();
    expect(
      within(official).getAllByText(en('financialKpi.provenance.asOf', { date: '2026-08-31' }), {
        exact: false,
      }),
    ).toHaveLength(2);
  });

  test('ADR-010: a withheld figure reads "Restricted", never "No data" and never 0', async () => {
    const live = financialPosition({
      valueStatus: 'MEASURED',
      maskedFields: ['actualExpenditureToDateSar', 'forecastAtCompletionSar'],
      financialStatus: 'UNKNOWN',
    });
    delete live.actualExpenditureToDateSar;
    delete live.forecastAtCompletionSar;
    open(FINANCIALS, entitySession(), (api) =>
      withKpis(withFinancials(api, { positions: [live] })),
    );
    const region = await screen.findByRole('region', { name: /Current financial position/ });

    const restricted = within(region).getAllByText(en('financialKpi.value.restricted'));
    expect(restricted).toHaveLength(2);
    expect(restricted[0]?.getAttribute('data-value-state')).toBe('RESTRICTED');
    expect(within(region).queryByText(en('financialKpi.value.MISSING'))).toBeNull();
    expect(region.textContent).not.toContain(sar('0.00'));
  });

  test('gate decision: financials are marked sensitive; no currency is chosen; the commitments field is hidden', async () => {
    open(FINANCIALS, entitySession(), (api) =>
      withKpis(withFinancials(api, { modes: sourceModes({ OPEN_COMMITMENT: 'INTEGRATED' }) })),
    );
    await screen.findByRole('region', { name: /Official financial position/ });

    expect(screen.getByText(en('financialKpi.sensitive.badge')).className).toBe(
      'badge badge--sensitive',
    );
    expect(screen.queryByRole('combobox', { name: /currency/i })).toBeNull();
    const sources = screen.getByRole('region', { name: en('financialKpi.sources.title') });
    expect(
      within(sources)
        .getAllByRole('term')
        .map((term) => term.textContent),
    ).toEqual([
      en('financialKpi.sources.field.APPROVED_BUDGET'),
      en('financialKpi.sources.field.ACTUAL_EXPENDITURE'),
      en('financialKpi.sources.field.FORECAST_AT_COMPLETION'),
    ]);
    expect(document.body.textContent).not.toMatch(/commitment/i);
  });

  test('SCR-049 has no accessibility violations', async () => {
    open(FINANCIALS, entitySession());
    await screen.findByRole('region', { name: /Official financial position/ });
    const violations = await accessibilityViolations();
    expect(violations, describeViolations(violations)).toEqual([]);
  });

  test('a person without FINANCIAL_VIEW is told so', async () => {
    open(FINANCIALS, entitySession(), (api) => withKpis(withFinancials(api, { readable: false })));
    expect(await screen.findByText(en('financialKpi.financials.forbidden'))).toBeTruthy();
  });
});

describe('MOD-023 Update Financial Progress', () => {
  async function openDialog() {
    await userEvent.click(
      await screen.findByRole('button', { name: en('financialKpi.actions.update') }),
    );
    return screen.findByRole('dialog', { name: en('financialKpi.update.title') });
  }

  function answerSave(api: MockApi) {
    api
      .on('PUT', new RegExp(`^/financial-progress-updates/${UPDATE_ID}$`), (request) => ({
        body: { ...financialUpdate(), ...(request.body as object) },
        headers: { ETag: SAVED_ETAG },
      }))
      .on('POST', new RegExp(`^/financial-progress-updates/${UPDATE_ID}/submit$`), {
        body: financialUpdate({ status: 'SUBMITTED' }),
      });
  }

  test('"No data" sends no figure at all — null, never "0.00" — then submits with the saved ETag', async () => {
    const api = open(FINANCIALS, entitySession());
    answerSave(api);
    const dialog = await openDialog();

    expect(within(dialog).queryByRole('combobox')).toBeNull();
    await userEvent.click(
      within(dialog).getByRole('radio', { name: en('financialKpi.update.status.MISSING') }),
    );
    expect(within(dialog).queryByLabelText(/Actual expenditure/)).toBeNull();
    await userEvent.click(
      within(dialog).getByRole('button', { name: en('financialKpi.update.saveAndSubmit') }),
    );

    await screen.findByText(en('financialKpi.done.updateSubmitted'));
    const [put] = api.requestsTo('PUT', /^\/financial-progress-updates\//);
    expect(put?.headers.get('If-Match')).toBe(UPDATE_ETAG);
    expect(put?.body).toEqual({
      actualExpenditureToDateSar: null,
      forecastAtCompletionSar: null,
      valueStatus: 'MISSING',
      narrative: null,
      sourceReference: null,
      asOfDate: '2026-09-30',
    });
    const [submit] = api.requestsTo('POST', /\/submit$/);
    expect(submit?.headers.get('If-Match')).toBe(SAVED_ETAG);
  });

  test('measured figures are SAR with two places; a grouped amount and a future as-of date are refused before sending', async () => {
    const api = open(FINANCIALS, entitySession());
    answerSave(api);
    const dialog = await openDialog();

    await userEvent.click(
      within(dialog).getByRole('radio', { name: en('financialKpi.update.status.MEASURED') }),
    );
    const actual = within(dialog).getByLabelText(/^Actual expenditure to date \(SAR\)/);
    await userEvent.type(actual, '1,250');
    const asOf = within(dialog).getByLabelText(/^As of/);
    await userEvent.clear(asOf);
    await userEvent.type(asOf, '2999-01-01');
    await userEvent.click(
      within(dialog).getByRole('button', { name: en('financialKpi.update.saveDraft') }),
    );
    expect(within(dialog).getByText(en('financialKpi.fieldErrors.amountMalformed'))).toBeTruthy();
    expect(within(dialog).getByText(en('financialKpi.fieldErrors.asOfInFuture'))).toBeTruthy();
    expect(api.requestsTo('PUT', /financial-progress-updates/)).toEqual([]);

    await userEvent.clear(actual);
    await userEvent.type(actual, '1250000');
    await userEvent.type(within(dialog).getByLabelText(/^Forecast at completion/), '1300000.5');
    await userEvent.clear(asOf);
    await userEvent.type(asOf, '2026-09-30');
    await userEvent.type(within(dialog).getByLabelText(/^Source reference/), 'Invoice register 09');
    await userEvent.click(
      within(dialog).getByRole('button', { name: en('financialKpi.update.saveDraft') }),
    );

    await screen.findByText(en('financialKpi.done.updateSaved'));
    expect(api.requestsTo('PUT', /financial-progress-updates/)[0]?.body).toMatchObject({
      actualExpenditureToDateSar: '1250000.00',
      forecastAtCompletionSar: '1300000.50',
      valueStatus: 'MEASURED',
      sourceReference: 'Invoice register 09',
    });
    expect(api.requestsTo('POST', /\/submit$/)).toEqual([]);
  });

  test('an INTEGRATED actual cannot be measured by hand: only the reasons for no figure are offered', async () => {
    open(FINANCIALS, entitySession(), (api) =>
      withKpis(withFinancials(api, { modes: sourceModes({ ACTUAL_EXPENDITURE: 'INTEGRATED' }) })),
    );
    const dialog = await openDialog();
    expect(within(dialog).getByText(en('financialKpi.update.actualIntegrated'))).toBeTruthy();
    expect(
      within(dialog).queryByRole('radio', { name: en('financialKpi.update.status.MEASURED') }),
    ).toBeNull();
  });

  test('MOD-023 has no accessibility violations', async () => {
    open(FINANCIALS, entitySession());
    await openDialog();
    const violations = await accessibilityViolations();
    expect(violations, describeViolations(violations)).toEqual([]);
  });

  test('AHDA reviews a submitted update; its submitter is never offered the review', async () => {
    const submitted = financialUpdate({
      status: 'SUBMITTED',
      submittedByUserId: ENTITY_USER_ID,
      submittedAt: '2026-10-01T09:00:00Z',
    });
    const api = open(FINANCIALS, reviewerSession(), (mock) =>
      withKpis(withFinancials(mock, { updates: [submitted] })),
    );
    api.on('POST', /\/start-review$/, { body: { ...submitted, status: 'UNDER_REVIEW' } });
    await userEvent.click(
      await screen.findByRole('button', { name: en('financialKpi.actions.review') }),
    );
    const dialog = await screen.findByRole('dialog', { name: en('financialKpi.review.title') });
    // The reviewer sees the state of what they decide on, not a 0.
    // What is known, the actual and the forecast: each "No data".
    expect(within(dialog).getAllByText(en('financialKpi.value.MISSING'))).toHaveLength(3);
    await userEvent.click(
      within(dialog).getByRole('button', { name: en('financialKpi.review.start') }),
    );
    await screen.findByText(en('financialKpi.done.reviewStarted'));
    expect(api.requestsTo('POST', /\/start-review$/)[0]?.headers.get('If-Match')).toBe(UPDATE_ETAG);
  });
});

describe('SCR-071 Financial Progress', () => {
  test('each published period, revision and budget version with its state and provenance; open commitments hidden', async () => {
    open(`${FINANCIALS}/history`, entitySession(), (api) =>
      withKpis(
        withFinancials(api, {
          commitments: [
            commitment(),
            commitment({ id: 'open', commitmentType: 'OPEN_COMMITMENT', amountSar: '5.00' }),
          ],
        }),
      ),
    );
    const snapshots = await screen.findByRole('region', {
      name: en('financialKpi.history.snapshotsCaption'),
    });
    expect(within(snapshots).getAllByText(en('financialKpi.value.STALE'))).toHaveLength(2);
    expect(within(snapshots).getByText(en('financialKpi.financialStatus.UNKNOWN'))).toBeTruthy();
    expect(within(snapshots).getByText(/Finance return 08/)).toBeTruthy();
    expectNoGreen(snapshots);

    const updates = screen.getByRole('region', { name: en('financialKpi.history.updatesCaption') });
    expect(within(updates).getAllByText(en('financialKpi.value.MISSING')).length).toBeGreaterThan(
      0,
    );
    expectNoZero(updates);

    const budgets = screen.getByRole('region', { name: en('financialKpi.history.budgetsCaption') });
    expect(within(budgets).getAllByRole('row')).toHaveLength(2);
    expect(within(budgets).getByText(sar('1,000,000.00'))).toBeTruthy();
    expect(within(budgets).queryByText(sar('5.00'))).toBeNull();
  });
});

describe('criterion 1 and the workbook check on SCR-050: no measurement this period is "No data", not 0 or green', () => {
  function row(assignmentId = ASSIGNMENT_ID) {
    const found = document.querySelector<HTMLElement>(`[data-kpi-assignment="${assignmentId}"]`);
    if (found === null) {
      throw new Error('No KPI row.');
    }
    return found;
  }

  test('the KPI with nothing recorded this month: "No data for this period", "Not rated" in grey, last published beside it', async () => {
    open(KPIS, entitySession());
    await screen.findByRole('link', { name: 'On-time delivery' });
    const kpi = row();
    const cells = within(kpi).getAllByRole('cell');

    const noData = within(kpi).getByText(en('financialKpi.kpis.noDataThisPeriod'));
    expect(noData.getAttribute('data-value-state')).toBe('ABSENT');
    expect(noData.className).toBe('value-state value-state--neutral');
    expect(noData.querySelector('svg')).not.toBeNull();
    expect(within(kpi).getByText(en('financialKpi.rag.UNKNOWN')).className).toBe(
      'badge badge--neutral',
    );
    expectNoGreen(kpi);
    // The "This period" cell holds no figure at all.
    expect(cells[2]?.querySelector('[data-value-state="MEASURED"]')).toBeNull();
    expectNoZero(nth(cells, 2));
    // The last published value is two months ago's, which was itself Unknown: said so, not 0.
    expect(within(nth(cells, 5)).getByText(en('financialKpi.value.MISSING'))).toBeTruthy();
    // The target in force is version 2.
    expect(
      within(kpi).getByText(en('financialKpi.kpis.targetVersion', { version: 2, target: '80' })),
    ).toBeTruthy();
  });

  test('N/A and Stale this period are their own states; N/A is not rated and neither is green', async () => {
    const thisMonth = monthBefore(0);
    const now = (id: string, status: 'NOT_APPLICABLE' | 'STALE', assignmentId: string) =>
      measurement({
        id,
        kpiAssignmentId: assignmentId,
        targetVersionNo: 2,
        targetValue: '80',
        periodStart: thisMonth.start,
        periodEnd: thisMonth.end,
        asOfDate: todayUtc(),
        measuredValue: null,
        valueStatus: status,
        ragStatus: status === 'NOT_APPLICABLE' ? 'NOT_APPLICABLE' : 'UNKNOWN',
        status: 'DRAFT',
      });
    open(KPIS, entitySession(), (api) =>
      withKpis(withFinancials(api), {
        assignments: [kpiAssignment(), kpiAssignment({ id: 'second' })],
        targets: targetVersions().flatMap((target) => [
          target,
          { ...target, id: `${target.id}-2`, kpiAssignmentId: 'second' },
        ]),
        measurements: [now('na', 'NOT_APPLICABLE', ASSIGNMENT_ID), now('stale', 'STALE', 'second')],
      }),
    );
    await screen.findAllByRole('link', { name: 'On-time delivery' });

    const na = row();
    expect(
      stateFlag(na, 'financialKpi.value.NOT_APPLICABLE').getAttribute('data-value-state'),
    ).toBe('NOT_APPLICABLE');
    // The value's flag and the rating's badge both say "Not applicable", neither in a colour.
    expect(
      within(na)
        .getAllByText(en('financialKpi.rag.NOT_APPLICABLE'))
        .map((element) => element.className),
    ).toEqual(['value-state value-state--neutral', 'badge badge--neutral']);
    const stale = row('second');
    expect(stateFlag(stale, 'financialKpi.value.STALE').className).toBe(
      'value-state value-state--warning',
    );
    for (const kpi of [na, stale]) {
      expectNoGreen(kpi);
      expectNoZero(nth(within(kpi).getAllByRole('cell'), 2));
    }
  });

  test('without the KPI catalogue a KPI is named by its id, and its row still says "No data for this period"', async () => {
    open(KPIS, entitySession(), (api) => withKpis(withFinancials(api), { catalogue: false }));
    expect(
      await screen.findByRole('link', { name: en('financialKpi.kpi.unnamed', { id: 'f6000000' }) }),
    ).toBeTruthy();
    expect(within(row()).getByText(en('financialKpi.kpis.noDataThisPeriod'))).toBeTruthy();
  });

  test('SCR-050 has no accessibility violations, in Arabic too', async () => {
    open(KPIS, entitySession(), undefined, { language: 'ar' });
    await screen.findByRole('link', { name: 'التسليم في الموعد' });
    const violations = await accessibilityViolations();
    expect(violations, describeViolations(violations)).toEqual([]);
  });
});

describe('MOD-024 Update KPI Value and publication', () => {
  test('a new value is pinned to the target in force; "Not applicable" sends null for this month, then submits', async () => {
    const api = open(KPIS, entitySession());
    const created = measurement({ id: 'new', status: 'DRAFT' });
    api
      .on('POST', /^\/kpi-measurements$/, { status: 201, body: created, headers: { ETag: '"91"' } })
      .on('POST', /^\/kpi-measurements\/new\/submit$/, {
        body: { ...created, status: 'SUBMITTED' },
      });

    await userEvent.click(
      await screen.findByRole('button', { name: en('financialKpi.actions.recordValue') }),
    );
    const dialog = await screen.findByRole('dialog', { name: /Update KPI value/ });
    expect(dialog.querySelector('[data-pinned-version]')?.getAttribute('data-pinned-version')).toBe(
      '2',
    );
    await userEvent.click(
      within(dialog).getByRole('radio', {
        name: en('financialKpi.kpiValue.status.NOT_APPLICABLE'),
      }),
    );
    expect(within(dialog).queryByLabelText(/^Value/)).toBeNull();
    await userEvent.click(
      within(dialog).getByRole('button', { name: en('financialKpi.update.saveAndSubmit') }),
    );

    await screen.findByText(en('financialKpi.done.valueSubmitted'));
    const month = monthBefore(0);
    expect(api.requestsTo('POST', /^\/kpi-measurements$/)[0]?.body).toEqual({
      kpiAssignmentId: ASSIGNMENT_ID,
      periodStart: month.start,
      periodEnd: month.end,
      measuredValue: null,
      valueStatus: 'NOT_APPLICABLE',
      asOfDate: todayUtc(),
      narrative: null,
    });
    expect(api.requestsTo('POST', /\/submit$/)[0]?.headers.get('If-Match')).toBe('"91"');
  });

  test('a DRAFT is edited with its ETag and keeps the version it was pinned to', async () => {
    const draft = measurement({
      id: 'draft',
      status: 'DRAFT',
      periodStart: monthBefore(0).start,
      periodEnd: monthBefore(0).end,
    });
    const api = open(KPIS, entitySession(), (mock) =>
      withKpis(withFinancials(mock), { measurements: [draft, ...measurements()] }),
    );
    api.on('PUT', /^\/kpi-measurements\/draft$/, { body: draft, headers: { ETag: '"92"' } });
    expect(
      screen.queryByRole('button', { name: en('financialKpi.actions.recordValue') }),
    ).toBeNull();
    await userEvent.click(
      await screen.findByRole('button', { name: en('financialKpi.actions.editValue') }),
    );
    const dialog = await screen.findByRole('dialog', { name: /Update KPI value/ });
    // Pinned to version 1 when recorded, although version 2 is in force now.
    expect(
      (await within(dialog).findByText(/target version/)).getAttribute('data-pinned-version'),
    ).toBe('1');
    const value = within(dialog).getByLabelText(/^Value/);
    await userEvent.clear(value);
    await userEvent.type(value, '93.5');
    await userEvent.click(
      within(dialog).getByRole('button', { name: en('financialKpi.update.saveDraft') }),
    );

    await screen.findByText(en('financialKpi.done.valueSaved'));
    const [put] = api.requestsTo('PUT', /kpi-measurements/);
    expect(put?.headers.get('If-Match')).toBe(MEASUREMENT_ETAG);
    expect(put?.body).toMatchObject({ measuredValue: 93.5, valueStatus: 'MEASURED' });
  });

  test('with no approved target no value can be recorded, and the row says why', async () => {
    open(KPIS, entitySession(), (api) =>
      withKpis(withFinancials(api), { targets: [], measurements: [] }),
    );
    expect(await screen.findByText(en('financialKpi.kpis.noTarget'))).toBeTruthy();
    expect(
      screen.queryByRole('button', { name: en('financialKpi.actions.recordValue') }),
    ).toBeNull();
  });

  test('AHDA publishes a submitted value with its ETag; the person who recorded it is never offered that', async () => {
    const api = open(KPIS, reviewerSession());
    api.on('POST', new RegExp(`^/kpi-measurements/${MEASUREMENT_3_ID}/publish$`), {
      body: measurements()[0],
    });
    await userEvent.click(
      await screen.findByRole('button', { name: en('financialKpi.actions.publishValue') }),
    );
    const dialog = await screen.findByRole('dialog', { name: /Publish KPI value/ });
    expect(
      await within(dialog).findByText(
        en('financialKpi.kpis.targetVersion', { version: 2, target: '80' }),
      ),
    ).toBeTruthy();
    await userEvent.click(
      within(dialog).getByRole('button', { name: en('financialKpi.publish.confirm') }),
    );
    await screen.findByText(en('financialKpi.done.valuePublished'));
    expect(api.requestsTo('POST', /\/publish$/)[0]?.headers.get('If-Match')).toBe(MEASUREMENT_ETAG);
  });

  test('an AHDA user is not offered to publish a value they recorded themselves', async () => {
    open(KPIS, reviewerSession(), (api) =>
      withKpis(withFinancials(api), {
        measurements: measurements().map((item) =>
          item.status === 'SUBMITTED' ? { ...item, recordedByUserId: REVIEWER_ID } : item,
        ),
      }),
    );
    await screen.findByRole('link', { name: 'On-time delivery' });
    expect(
      screen.queryByRole('button', { name: en('financialKpi.actions.publishValue') }),
    ).toBeNull();
  });

  test('the entity user who recorded a value is never offered to publish it (ADR-013)', async () => {
    open(KPIS, entitySession());
    await screen.findByRole('link', { name: 'On-time delivery' });
    expect(
      screen.queryByRole('button', { name: en('financialKpi.actions.publishValue') }),
    ).toBeNull();
  });
});

describe('criterion 2 on SCR-073: a target change never back-edits older points', () => {
  test('each point keeps the target it was rated against; the line steps from 95 to 80 at the change', async () => {
    open(TREND, entitySession());
    const chart = await screen.findByRole('img', { name: CHART });

    const segments = [...chart.querySelectorAll('[data-target-version]')].map((segment) => [
      segment.getAttribute('data-target-version'),
      segment.getAttribute('data-target-value'),
      segment.getAttribute('data-first-period'),
    ]);
    expect(segments).toEqual([
      ['1', '95', monthBefore(3).start],
      ['2', '80', monthBefore(1).start],
    ]);
    const points = [...chart.querySelectorAll('circle[data-value]')].map((point) => [
      point.getAttribute('data-period'),
      point.getAttribute('data-value'),
      point.getAttribute('data-pinned-target-version'),
      point.getAttribute('data-pinned-target'),
    ]);
    // The older value stays against version 1's 95, although 80 is in force now.
    expect(points).toEqual([
      [monthBefore(3).start, '92', '1', '95'],
      [monthBefore(1).start, '92', '2', '80'],
    ]);
  });

  test('a period without a value is marked "No data" in its own row, never plotted at 0', async () => {
    open(TREND, entitySession());
    const chart = await screen.findByRole('img', { name: CHART });
    const gap = chart.querySelector(`[data-period="${monthBefore(2).start}"]`);
    expect(gap?.tagName.toLowerCase()).toBe('g');
    expect(gap?.getAttribute('data-value-state')).toBe('MISSING');
    expect(gap?.textContent).toBe(en('financialKpi.trend.gap.MISSING'));
    expect(chart.querySelectorAll('circle')).toHaveLength(2);
  });

  test('the table lists each value with its own pinned version; superseded target versions keep their figures', async () => {
    open(TREND, entitySession());
    const values = await screen.findByRole('region', {
      name: en('financialKpi.trend.valuesCaption'),
    });
    const rows = within(values).getAllByRole('row').slice(1);
    expect(rows.map((row) => within(row).getAllByRole('cell')[2]?.textContent)).toEqual([
      en('financialKpi.kpis.targetVersion', { version: 2, target: '80' }),
      en('financialKpi.kpis.targetVersion', { version: 1, target: '95' }),
      en('financialKpi.kpis.targetVersion', { version: 1, target: '95' }),
    ]);
    expect(within(nth(rows, 1)).getByText(en('financialKpi.value.MISSING'))).toBeTruthy();
    expectNoGreen(nth(rows, 1));

    const targets = screen.getByRole('region', { name: en('financialKpi.trend.targetsCaption') });
    expect(within(targets).getByText(en('financialKpi.versionStatus.SUPERSEDED'))).toBeTruthy();
    expect(within(targets).getByText('95')).toBeTruthy();
  });

  test('SCR-073 has no accessibility violations', async () => {
    open(TREND, entitySession());
    await screen.findByRole('img', { name: CHART });
    const violations = await accessibilityViolations();
    expect(violations, describeViolations(violations)).toEqual([]);
  });
});

describe('SCR-072 KPI Register', () => {
  const projects = [
    projectSummary({ status: 'ACTIVE', projectManagerUserId: ENTITY_USER_ID }),
    projectSummary({
      id: OTHER_PROJECT_ID,
      title: { text: 'Harbour bridge repair', language: 'EN' },
      status: 'ACTIVE',
    }),
  ];

  function openRegister() {
    const api = withKpis(withProjectLookups(mockApi()), {
      assignments: [
        kpiAssignment(),
        kpiAssignment({ id: 'harbour', projectId: OTHER_PROJECT_ID, unitItemId: DAYS_UNIT_ID }),
      ],
      aggregate: {
        isUnitCompatible: false,
        unitItemId: null,
        isPartial: true,
        coverage: 'PARTIAL',
        measuredCount: 1,
        meanValue: null,
        ragCounts: { green: 0, amber: 1, red: 0, unknown: 0, notApplicable: 0 },
        exclusions: [
          {
            projectId: OTHER_PROJECT_ID,
            kpiDefinitionId: KPI_DEFINITION_ID,
            reason: 'NO_PUBLISHED_FIGURE',
          },
        ],
      },
    });
    api.on('GET', /^\/projects$/, { body: page(projects, 200) });
    renderApp({ path: '/kpis', session: reviewerSession() });
    return api;
  }

  test('every visible project’s KPIs, each "No data for this period" where nothing is recorded', async () => {
    const api = openRegister();
    await screen.findByRole('heading', { level: 1, name: en('financialKpi.register.title') });
    const table = await screen.findByRole('region', { name: en('financialKpi.register.caption') });
    expect(within(table).getAllByText(en('financialKpi.kpis.noDataThisPeriod'))).toHaveLength(2);
    expect(within(table).getByRole('link', { name: 'Harbour bridge repair' })).toBeTruthy();
    expectNoGreen(table);
    const [aggregate] = api.requestsTo('GET', /^\/kpi-portfolio-aggregates$/);
    expect(aggregate?.query.getAll('projectId').sort()).toEqual(
      [PROJECT_ID, OTHER_PROJECT_ID].sort(),
    );
  });

  test('the aggregate across projects says when units differ and what it left out', async () => {
    openRegister();
    const aggregates = await screen.findByRole('region', {
      name: en('financialKpi.aggregate.title'),
    });
    expect(within(aggregates).getByText(en('financialKpi.aggregate.unitsDiffer'))).toBeTruthy();
    expect(
      within(aggregates).getByText(en('financialKpi.aggregate.coverage.PARTIAL')),
    ).toBeTruthy();
    expect(
      within(aggregates).getByText(
        en('financialKpi.aggregate.exclusion', {
          project: 'Harbour bridge repair',
          reason: en('financialKpi.aggregate.reason.NO_PUBLISHED_FIGURE'),
        }),
      ),
    ).toBeTruthy();
  });

  test('with nothing counted the aggregate says there is no value — not that the units differ', async () => {
    const api = withKpis(withProjectLookups(mockApi()), {
      aggregate: {
        isUnitCompatible: false,
        unitItemId: null,
        isPartial: true,
        coverage: 'NONE',
        measuredCount: 0,
        meanValue: null,
        ragCounts: { green: 0, amber: 0, red: 0, unknown: 1, notApplicable: 0 },
        exclusions: [
          {
            projectId: PROJECT_ID,
            kpiDefinitionId: KPI_DEFINITION_ID,
            reason: 'VALUE_NOT_MEASURED',
          },
        ],
      },
    });
    api.on('GET', /^\/projects$/, { body: page(projects.slice(0, 1), 200) });
    renderApp({ path: '/kpis', session: reviewerSession() });
    const aggregates = await screen.findByRole('region', {
      name: en('financialKpi.aggregate.title'),
    });
    expect(within(aggregates).getByText(en('financialKpi.aggregate.noMean'))).toBeTruthy();
    expect(within(aggregates).queryByText(en('financialKpi.aggregate.unitsDiffer'))).toBeNull();
    expect(within(aggregates).getByText(en('financialKpi.aggregate.coverage.NONE'))).toBeTruthy();
  });

  test('the "No data this period" filter, and its empty state for a choice nothing matches', async () => {
    openRegister();
    const filter = await screen.findByRole('combobox', {
      name: en('financialKpi.register.filter'),
    });
    await userEvent.selectOptions(filter, 'noData');
    const table = screen.getByRole('region', { name: en('financialKpi.register.caption') });
    expect(within(table).getAllByRole('row')).toHaveLength(3);
    await userEvent.selectOptions(filter, 'red');
    expect(screen.getByText(en('financialKpi.register.filteredEmpty'))).toBeTruthy();
  });

  test('with nothing assigned anywhere the register says so', async () => {
    const api = withKpis(withProjectLookups(mockApi()), { assignments: [] });
    api.on('GET', /^\/projects$/, { body: page(projects, 200) });
    renderApp({ path: '/kpis', session: reviewerSession() });
    expect(await screen.findByText(en('financialKpi.register.empty'))).toBeTruthy();
  });

  test('a refused aggregate is said, and the list still shows', async () => {
    const api = withKpis(withProjectLookups(mockApi()));
    api
      .on('GET', /^\/projects$/, { body: page(projects.slice(0, 1), 200) })
      .on('GET', /^\/kpi-portfolio-aggregates$/, problem(403, 'PERMISSION_DENIED'));
    renderApp({ path: '/kpis', session: reviewerSession() });
    expect(await screen.findByText(en('financialKpi.aggregate.unavailable'))).toBeTruthy();
    expect(screen.getByRole('region', { name: en('financialKpi.register.caption') })).toBeTruthy();
  });
});

describe('the official and live financial views stay apart', () => {
  test('a measured official record shows its figures and the API’s rating; the live view its own', async () => {
    open(FINANCIALS, entitySession(), (api) =>
      withKpis(
        withFinancials(api, {
          snapshots: [
            financialSnapshot({
              valueStatus: 'MEASURED',
              actualExpenditureToDateSar: '400000.00',
              forecastAtCompletionSar: '950000.00',
              financialStatus: 'GREEN',
            }),
          ],
        }),
      ),
    );
    const official = await screen.findByRole('region', { name: /Official financial position/ });
    expect(within(official).getByText(sar('400,000.00'))).toBeTruthy();
    expect(within(official).getByText(en('financialKpi.financialStatus.GREEN')).className).toBe(
      'badge badge--positive',
    );
    const live = screen.getByRole('region', { name: /Current financial position/ });
    expect(within(live).queryByText(sar('400,000.00'))).toBeNull();
    expectNoGreen(live);
  });
});
