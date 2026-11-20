import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { labelOfValue } from '@/features/dashboards/presentation.ts';
import { sarCompact } from '@/shared/format/money.ts';
import { translate } from '@/shared/i18n/i18n.ts';
import { dashboardView, withDashboards } from '@/test/dashboardFixtures.ts';
import { sessionFor } from '@/test/identityAccessFixtures.ts';
import { type MockApi, mockApi, problem } from '@/test/mockApi.ts';
import { AGGREGATIONS_OF, designFieldsFixture, FIELD } from '@/test/portfolioInsightFixtures.ts';
import { withProjectLookups } from '@/test/projectFixtures.ts';
import { renderApp } from '@/test/renderApp.tsx';

import {
  type DesignView,
  type FieldAggregation,
  type Visual,
  type VisualGroup,
  type VisualResult,
  type VisualType,
} from './designApi.ts';

// The designer's data fields (18 Nov 2026, docs/architecture/dashboard-designer-data-fields.md): the Portfolio Manager picks any governed field
// with a calculation, groups by a status field and filters per visual. These tests hold the designer to the contract's API shapes and rules; the
// controls are found by role and by the names the contract gives (field and source names from i18n), not by the markup.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);
const plain = (text: string | null | undefined) => (text ?? '').replace(/[⁨⁩]/g, '');
const escaped = (text: string) => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

const DESIGNER = '/dashboards/design';
const DASHBOARD = '/dashboards?dashboard=PORTFOLIO';
const AGGREGATIONS: readonly string[] = [
  'COUNT',
  'SUM',
  'AVERAGE',
  'MIN',
  'MAX',
  'COUNT_TRUE',
  'SHARE_TRUE',
];

const FIELDS = designFieldsFixture().fields;

function present<T>(value: T | null | undefined, what = 'The element'): T {
  if (value === null || value === undefined) {
    throw new Error(`${what} is not there`);
  }
  return value;
}

function descriptor(key: string) {
  return present(
    FIELDS.find((field) => field.key === key),
    key,
  );
}

/** A field's English name: dashboards.designer.field.<PROJECTION_CODE with "." → "_">.<FIELD_CODE>. */
function fieldName(key: string): string {
  const { projectionCode, fieldCode } = descriptor(key);
  return en(`dashboards.designer.field.${projectionCode.replace('.', '_')}.${fieldCode}`);
}

/**
 * A field tick's accessible name: the field's name, then its type's word ("Actual progress%"), and nothing else, so "Actual progress" does not
 * also match "Actual progress over time".
 */
function fieldNamePattern(key: string): RegExp {
  const typeWord = en(`dashboards.designer.fieldType.${descriptor(key).type}`);
  return new RegExp(`^${escaped(fieldName(key))}\\s*(${escaped(typeWord)})?$`);
}

/** A source's English name: dashboards.designer.fieldSource.<PROJECTION_CODE with "." → "_">. */
function sourceName(projectionCode: string): string {
  return en(`dashboards.designer.fieldSource.${projectionCode.replace('.', '_')}`);
}

/** The projections of the fixture, in the register's order. */
const SOURCES = [...new Set(FIELDS.map((field) => field.projectionCode))];

/** What the mocked API calculates for a visual: one group, a figure per value (or measure). */
function calculated({ children: _children, ...visual }: Visual): VisualResult {
  // These designs hold no container, so a result has no children.
  const entries = visual.values?.length ?? visual.measures.length;
  const group: VisualGroup = {
    key: null,
    label: null,
    projects: 3,
    measured: 3,
    valueEntered: 3,
    values: Array.from({ length: entries }, () => 1),
    onPlan: null,
  };
  return { ...visual, groups: visual.visualType === 'SECTION' ? [] : [group] };
}

function visualsSent(body: unknown): Visual[] {
  return (body as { visuals: Visual[] }).visuals;
}

const NOTHING_SAVED: DesignView = { isStandard: true, updatedAt: null, visuals: [] };

/**
 * The designer's API: the design read (nothing saved, unless a test gives one), the fields, and a preview and a save that echo the visuals
 * sent with figures. With `figures`, the preview answers each visual with the saved design's own groups, so a test reads them in the designer too.
 */
function designerApi(
  design: DesignView = NOTHING_SAVED,
  figures = false,
  fieldsReadable = true,
): MockApi {
  const api = mockApi();
  withProjectLookups(api);
  withDashboards(api, { views: { PORTFOLIO: dashboardView('PORTFOLIO') } });
  const answer = (visuals: Visual[]): VisualResult[] =>
    visuals.map((visual, index) =>
      figures
        ? { ...calculated(visual), groups: design.visuals[index]?.groups ?? [] }
        : calculated(visual),
    );
  api
    .on('GET', /^\/portfolio-insights\/design$/, { body: design })
    .on(
      'GET',
      /^\/portfolio-insights\/design\/fields$/,
      fieldsReadable ? { body: designFieldsFixture() } : problem(403, 'PERMISSION_DENIED'),
    )
    .on('POST', /^\/portfolio-insights\/design\/preview$/, (request) => ({
      body: { isStandard: false, updatedAt: null, visuals: answer(visualsSent(request.body)) },
    }))
    .on('PUT', /^\/portfolio-insights\/design$/, (request) => ({
      body: {
        isStandard: false,
        updatedAt: '2026-11-18T09:00:00Z',
        visuals: answer(visualsSent(request.body)),
      },
    }));
  return api;
}

const session = () => sessionFor(['R02']);

/** The settings pane of the selected visual, where the Data panel is. */
const settings = () => screen.getByRole('region', { name: en('dashboards.designer.properties') });

/** Places a new visual from the gallery; the designer selects it, so its settings open. A line's tile waits for the fields. */
async function addVisual(user: ReturnType<typeof userEvent.setup>, type: VisualType) {
  await screen.findByRole('heading', { level: 1, name: en('dashboards.designer.title') });
  const tile = await waitFor(() => {
    const button = screen.getByRole('button', { name: en(`dashboards.designer.type.${type}`) });
    expect(button.hasAttribute('disabled')).toBe(false);
    return button;
  });
  await user.click(tile);
}

/** A field's tick in the Data panel: a checkbox where the visual takes several values, a radio where it takes one. */
function fieldControl(key: string): HTMLInputElement {
  const name = fieldNamePattern(key);
  const pane = within(settings());
  const control = pane.queryByRole('checkbox', { name }) ?? pane.getByRole('radio', { name });
  if (!(control instanceof HTMLInputElement)) {
    throw new Error(`The tick of ${key} is not an input`);
  }
  return control;
}

async function chooseField(user: ReturnType<typeof userEvent.setup>, key: string) {
  await user.click(await waitFor(() => fieldControl(key)));
}

/** The Data panel's search box. */
function searchBox(): HTMLElement {
  const pane = within(settings());
  return pane.queryByRole('searchbox') ?? pane.getByRole('textbox', { name: /search|find/i });
}

const optionValues = (select: HTMLSelectElement) =>
  Array.from(select.options).map((option) => option.value);

/** The chosen values' calculation selects: the selects in the pane whose options are aggregations. */
function aggregationSelects(): HTMLSelectElement[] {
  return within(settings())
    .getAllByRole('combobox')
    .filter((element): element is HTMLSelectElement => element instanceof HTMLSelectElement)
    .filter((select) => {
      const values = optionValues(select);
      return values.length > 0 && values.every((value) => AGGREGATIONS.includes(value));
    });
}

/** Picks a field in a select, by its key as the option's value or by its name as the option's words. */
async function selectField(
  user: ReturnType<typeof userEvent.setup>,
  select: HTMLElement,
  key: string,
) {
  const options = Array.from((select as HTMLSelectElement).options);
  const option =
    options.find((candidate) => candidate.value === key) ??
    options.find((candidate) => plain(candidate.textContent).includes(fieldName(key)));
  await user.selectOptions(select, present(option, `An option for ${key}`));
}

/** The visual being designed as the last preview sent it: the design starts empty, so it is the only one. */
function lastPreviewed(api: MockApi): Visual | undefined {
  const previews = api.requestsTo('POST', /^\/portfolio-insights\/design\/preview$/);
  return visualsSent(previews.at(-1)?.body ?? { visuals: [] }).at(-1);
}

async function previewed(api: MockApi, expected: Partial<Visual>) {
  await waitFor(() => {
    expect(lastPreviewed(api)).toMatchObject(expected);
  });
}

/** Whether `a` comes before `b` in the document. */
const before = (a: Node, b: Node) =>
  (a.compareDocumentPosition(b) & Node.DOCUMENT_POSITION_FOLLOWING) !== 0;

/** A source's heading in the Data panel: its name as text, not an option of a select. */
function sourceHeading(projectionCode: string): HTMLElement {
  return present(
    within(settings())
      .getAllByText(sourceName(projectionCode))
      .find((element) => element.closest('select') === null),
    `The heading of ${projectionCode}`,
  );
}

describe('the Data panel', () => {
  test('lists the portfolio figures first, then each field under its own source', async () => {
    designerApi();
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await addVisual(user, 'COLUMN');
    await waitFor(() => fieldControl(FIELD.ACTUAL_EXPENDITURE));

    // The eight fixed figures come before the first projection.
    const projects = within(settings()).getByRole('checkbox', {
      name: new RegExp(`^${escaped(en('dashboards.designer.measure.PROJECTS'))}$`),
    });
    expect(before(projects, sourceHeading(present(SOURCES[0])))).toBe(true);

    // Each field sits after its source's heading and before the next source's.
    SOURCES.forEach((source, index) => {
      const heading = sourceHeading(source);
      const next = SOURCES[index + 1];
      FIELDS.filter((field) => field.projectionCode === source).forEach((field) => {
        const control = fieldControl(field.key);
        expect(before(heading, control), `${field.key} after its source`).toBe(true);
        if (next !== undefined) {
          expect(before(control, sourceHeading(next)), `${field.key} before ${next}`).toBe(true);
        }
      });
    });
  });

  test('the search keeps only the fields whose name matches', async () => {
    designerApi();
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await addVisual(user, 'COLUMN');
    await waitFor(() => fieldControl(FIELD.ACTUAL_EXPENDITURE));

    await user.type(searchBox(), 'expenditure');
    await waitFor(() => {
      expect(
        within(settings()).queryByRole('checkbox', {
          name: fieldNamePattern(FIELD.OPEN_RISKS),
        }),
      ).toBeNull();
    });
    expect(fieldControl(FIELD.ACTUAL_EXPENDITURE)).toBeTruthy();
  });
});

describe('a value from a field', () => {
  test('starts with its type’s first calculation and replaces the fixed figures', async () => {
    const api = designerApi();
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await addVisual(user, 'BAR');
    await chooseField(user, FIELD.ACTUAL_EXPENDITURE);

    await previewed(api, {
      visualType: 'BAR',
      measures: [],
      values: [{ field: FIELD.ACTUAL_EXPENDITURE, aggregation: present(AGGREGATIONS_OF.SAR[0]) }],
    });
    // The automatic title names the field ("Count of Actual expenditure to date").
    expect(plain(lastPreviewed(api)?.title.en)).toContain(fieldName(FIELD.ACTUAL_EXPENDITURE));
  });

  test('offers only the calculations its type allows: a percentage has no sum', async () => {
    const api = designerApi();
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await addVisual(user, 'BAR');

    await chooseField(user, FIELD.ACTUAL_PERCENT);
    const percent = await waitFor(() => present(aggregationSelects()[0], 'A calculation select'));
    expect(optionValues(percent)).toEqual(AGGREGATIONS_OF.PERCENT);
    expect(optionValues(percent)).not.toContain('SUM');
    await user.selectOptions(percent, 'AVERAGE');
    await previewed(api, { values: [{ field: FIELD.ACTUAL_PERCENT, aggregation: 'AVERAGE' }] });

    // An amount may be summed.
    await chooseField(user, FIELD.ACTUAL_EXPENDITURE);
    await waitFor(() => {
      expect(optionValues(present(aggregationSelects()[0]))).toEqual(AGGREGATIONS_OF.SAR);
    });
    await user.selectOptions(present(aggregationSelects()[0]), 'SUM');
    await previewed(api, {
      values: [{ field: FIELD.ACTUAL_EXPENDITURE, aggregation: 'SUM' satisfies FieldAggregation }],
    });
  });

  // A table takes columns instead (dashboard-designer-power-table.md); a column chart takes the most field values of the charts.
  test('a column chart takes up to three values, a bar one', async () => {
    const api = designerApi();
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await addVisual(user, 'COLUMN');
    const four = [
      FIELD.APPROVED_BUDGET,
      FIELD.ACTUAL_EXPENDITURE,
      FIELD.FORECAST_AT_COMPLETION,
      FIELD.OPEN_RISKS,
    ];
    for (const key of four.slice(0, 3)) {
      await chooseField(user, key);
    }
    // The fourth is not taken: its tick is refused (disabled) or has no effect.
    const fourth = fieldControl(present(four[3]));
    if (!fourth.disabled) {
      await user.click(fourth);
    }
    await waitFor(() => {
      expect(lastPreviewed(api)?.values?.map((value) => value.field)).toEqual(four.slice(0, 3));
    });
    expect(fieldControl(present(four[3])).checked).toBe(false);

    // A bar draws one: a second field takes the first one's place.
    await addVisual(user, 'BAR');
    await chooseField(user, FIELD.ACTUAL_EXPENDITURE);
    await chooseField(user, FIELD.OPEN_RISKS);
    await waitFor(() => {
      expect(lastPreviewed(api)?.values?.map((value) => value.field)).toEqual([FIELD.OPEN_RISKS]);
    });
  });
});

describe('grouping and filters', () => {
  test('grouping by a status field sends the field, with no project grouping', async () => {
    const api = designerApi();
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await addVisual(user, 'BAR');
    await chooseField(user, FIELD.ACTUAL_EXPENDITURE);

    const groupBy = within(settings()).getByRole('combobox', {
      name: en('dashboards.designer.groupByLabel'),
    });
    await selectField(user, groupBy, FIELD.OVERALL_HEALTH);
    await previewed(api, { groupField: FIELD.OVERALL_HEALTH, groupBy: 'NONE' });
  });

  test('a filter keeps the projects with a value ticked', async () => {
    const api = designerApi();
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await addVisual(user, 'BAR');
    await chooseField(user, FIELD.ACTUAL_EXPENDITURE);

    await addFilter(user, FIELD.FINANCIAL_CONDITION, 'RED');
    await previewed(api, { filters: [{ field: FIELD.FINANCIAL_CONDITION, values: ['RED'] }] });
  });
});

/** Adds a filter on a CODE field and ticks one of its values. */
async function addFilter(user: ReturnType<typeof userEvent.setup>, key: string, value: string) {
  const pane = within(settings());
  const selectsBefore = pane.getAllByRole('combobox');
  await user.click(pane.getByRole('button', { name: /filter/i }));
  // The filter's field picker is the select the new filter brings.
  const picker = await waitFor(() =>
    present(
      within(settings())
        .getAllByRole('combobox')
        .find((select) => !selectsBefore.includes(select)),
      'The new filter’s field select',
    ),
  );
  await selectField(user, picker, key);
  const word = labelOfValue(descriptor(key).projectionCode, value, null, 'en', en).text;
  await user.click(
    await waitFor(() =>
      within(settings()).getByRole('checkbox', { name: new RegExp(`^${escaped(word)}$`) }),
    ),
  );
}

describe('saving', () => {
  test('sends the visual’s values, grouping field and filters as the preview did', async () => {
    const api = designerApi();
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await addVisual(user, 'BAR');
    await chooseField(user, FIELD.ACTUAL_EXPENDITURE);
    await user.selectOptions(
      await waitFor(() => present(aggregationSelects()[0], 'A calculation select')),
      'SUM',
    );
    await selectField(
      user,
      within(settings()).getByRole('combobox', { name: en('dashboards.designer.groupByLabel') }),
      FIELD.OVERALL_HEALTH,
    );
    await addFilter(user, FIELD.FINANCIAL_CONDITION, 'RED');
    const expected = {
      measures: [],
      values: [{ field: FIELD.ACTUAL_EXPENDITURE, aggregation: 'SUM' }],
      groupField: FIELD.OVERALL_HEALTH,
      groupBy: 'NONE',
      filters: [{ field: FIELD.FINANCIAL_CONDITION, values: ['RED'] }],
    } satisfies Partial<Visual>;
    await previewed(api, expected);

    await user.click(screen.getByRole('button', { name: en('dashboards.designer.save') }));
    await waitFor(() => {
      expect(api.requestsTo('PUT', /^\/portfolio-insights\/design$/)).toHaveLength(1);
    });
    const put = present(api.requestsTo('PUT', /^\/portfolio-insights\/design$/)[0]);
    expect(visualsSent(put.body).at(-1)).toMatchObject(expected);
    // The API takes `children` only on a container: no other visual sends any, not even [].
    expect(visualsSent(put.body).map((visual) => visual.children ?? null)).toEqual([null]);
  });
});

describe('a saved data visual', () => {
  const tableTitle = 'Money, progress and risks by health';
  const cardTitle = 'Projects with an expenditure';
  const group = (key: string | null, values: (number | null)[]): VisualGroup => ({
    key,
    label: null,
    projects: 4,
    measured: 3,
    valueEntered: 3,
    values,
    onPlan: null,
  });
  const saved: DesignView = {
    isStandard: false,
    updatedAt: '2026-11-18T09:00:00Z',
    visuals: [
      {
        title: { ar: cardTitle, en: cardTitle },
        visualType: 'CARD',
        measures: [],
        groupBy: 'NONE',
        layoutColumn: 0,
        layoutRow: 0,
        layoutWidth: 4,
        layoutHeight: 2,
        // A count of an amount field is a count, not an amount.
        values: [{ field: FIELD.ACTUAL_EXPENDITURE, aggregation: 'COUNT' }],
        valueTypes: ['SAR'],
        groups: [group(null, [7])],
      },
      {
        title: { ar: tableTitle, en: tableTitle },
        visualType: 'TABLE',
        measures: [],
        groupBy: 'NONE',
        layoutColumn: 0,
        layoutRow: 2,
        layoutWidth: 12,
        layoutHeight: 5,
        values: [
          { field: FIELD.ACTUAL_EXPENDITURE, aggregation: 'SUM' },
          { field: FIELD.ACTUAL_PERCENT, aggregation: 'AVERAGE' },
          { field: FIELD.OPEN_RISKS, aggregation: 'SUM' },
        ],
        groupField: FIELD.OVERALL_HEALTH,
        filters: null,
        // The API names each value's type, so the dashboard formats without the designer's field list.
        valueTypes: ['SAR', 'PERCENT', 'COUNT'],
        groups: [group('GREEN', [64553236.5, 55, 12]), group(null, [null, null, 0])],
      },
    ],
  };

  // A dashboard viewer may not read the designer's field list: the dashboard formats from the visual's own value types.
  test.each([
    ['on the dashboard', DASHBOARD, false],
    ['in the designer', DESIGNER, true],
  ])('draws each value in its type’s format %s', async (_where, path, fieldsReadable) => {
    designerApi(saved, true, fieldsReadable);
    renderApp({ path, session: session() });

    const table = await screen.findByRole('table', { name: tableTitle });
    const rows = within(table).getAllByRole('row');
    const green = present(
      rows.find((row) =>
        plain(row.textContent).includes(
          labelOfValue('PROGRESS.PROJECT_HEALTH_STATUS', 'GREEN', null, 'en', en).text,
        ),
      ),
      'The GREEN row',
    );
    const cells = within(green)
      .getAllByRole('cell')
      .map((cell) => plain(cell.textContent));
    expect(cells).toEqual([plain(sarCompact('64553236.5', en)), '55%', '12']);

    // The projects whose health is not revealed are "not set"; a sum with nothing revealed is not available, a count is 0.
    const notSet = present(
      rows.find((row) => plain(row.textContent).includes(en('dashboards.designer.notSet'))),
      'The not-set row',
    );
    const notAvailable = en('dashboards.insights.notAvailable');
    expect(
      within(notSet)
        .getAllByRole('cell')
        .map((cell) => plain(cell.textContent)),
    ).toEqual([notAvailable, notAvailable, '0']);

    const card = present(screen.getByText(cardTitle).closest('section'), 'The card');
    expect(plain(card.textContent)).toContain('7');
    expect(plain(card.textContent)).not.toContain(en('common.money.sar', { amount: '7' }));
  });
});

describe('more fields: yes/no, dates and figures over time', () => {
  const groupBySelect = () =>
    within(settings()).getByRole('combobox', { name: en('dashboards.designer.groupByLabel') });

  /** Adds a filter on `key` and ticks the value whose words are `word`. */
  async function addFilterTick(
    user: ReturnType<typeof userEvent.setup>,
    key: string,
    word: string,
  ) {
    const pane = within(settings());
    const selectsBefore = pane.getAllByRole('combobox');
    await user.click(pane.getByRole('button', { name: en('dashboards.designer.addFilter') }));
    const picker = await waitFor(() =>
      present(
        within(settings())
          .getAllByRole('combobox')
          .find((select) => !selectsBefore.includes(select)),
        'The new filter’s field select',
      ),
    );
    await selectField(user, picker, key);
    await user.click(
      await waitFor(() =>
        within(settings()).getByRole('checkbox', { name: new RegExp(`^${escaped(word)}$`) }),
      ),
    );
  }

  test('a yes/no field counts or shares its yeses, a gauge its share, and filters by Yes or No', async () => {
    const api = designerApi();
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await addVisual(user, 'BAR');
    await chooseField(user, FIELD.IS_STRATEGIC);
    await previewed(api, { values: [{ field: FIELD.IS_STRATEGIC, aggregation: 'COUNT' }] });

    const calculation = await waitFor(() =>
      present(aggregationSelects()[0], 'A calculation select'),
    );
    expect(optionValues(calculation)).toEqual(['COUNT', 'COUNT_TRUE', 'SHARE_TRUE']);
    await user.selectOptions(calculation, 'COUNT_TRUE');
    await previewed(api, { values: [{ field: FIELD.IS_STRATEGIC, aggregation: 'COUNT_TRUE' }] });
    await user.selectOptions(present(aggregationSelects()[0]), 'SHARE_TRUE');
    await previewed(api, { values: [{ field: FIELD.IS_STRATEGIC, aggregation: 'SHARE_TRUE' }] });

    await addFilterTick(user, FIELD.IS_STRATEGIC, en('dashboards.designer.yes'));
    await previewed(api, { filters: [{ field: FIELD.IS_STRATEGIC, values: ['TRUE'] }] });

    // A gauge draws a yes/no field as its share only.
    await addVisual(user, 'GAUGE');
    await chooseField(user, FIELD.IS_STRATEGIC);
    await previewed(api, {
      visualType: 'GAUGE',
      values: [{ field: FIELD.IS_STRATEGIC, aggregation: 'SHARE_TRUE' }],
    });
  });

  test('grouping by a date field sends its unit, by month then by year; a date filter ticks years', async () => {
    const api = designerApi();
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await addVisual(user, 'BAR');
    await chooseField(user, FIELD.ACTUAL_EXPENDITURE);

    await selectField(user, groupBySelect(), FIELD.PLANNED_END_DATE);
    await previewed(api, {
      groupBy: 'NONE',
      groupField: FIELD.PLANNED_END_DATE,
      groupDateUnit: 'MONTH',
    });

    const unit = within(settings()).getByRole('combobox', {
      name: en('dashboards.designer.dateUnitLabel'),
    });
    expect(optionValues(unit as HTMLSelectElement)).toEqual(['YEAR', 'MONTH']);
    await user.selectOptions(unit, 'YEAR');
    await previewed(api, { groupField: FIELD.PLANNED_END_DATE, groupDateUnit: 'YEAR' });

    // A status field takes no unit.
    await selectField(user, groupBySelect(), FIELD.OVERALL_HEALTH);
    await previewed(api, { groupField: FIELD.OVERALL_HEALTH });
    expect(lastPreviewed(api)?.groupDateUnit ?? null).toBeNull();

    const year = String(new Date().getUTCFullYear());
    await addFilterTick(user, FIELD.PLANNED_END_DATE, year);
    await previewed(api, { filters: [{ field: FIELD.PLANNED_END_DATE, values: [year] }] });
  });

  test('a line from the gallery draws the published history, grouped by the period’s end by month', async () => {
    const api = designerApi();
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await addVisual(user, 'LINE');

    // The history's source is marked as over time.
    expect(
      within(settings()).getAllByText(en('dashboards.designer.overTime')).length,
    ).toBeGreaterThan(0);
    await waitFor(() => {
      expect(fieldControl(FIELD.HISTORY_ACTUAL_PERCENT).checked).toBe(true);
    });
    await chooseField(user, FIELD.HISTORY_PLANNED_PERCENT);
    await selectField(user, groupBySelect(), FIELD.HISTORY_PERIOD_END);
    // A line groups by its fields only: no project attribute is offered.
    expect(optionValues(groupBySelect() as HTMLSelectElement)).not.toContain('PORTFOLIO');

    await previewed(api, {
      visualType: 'LINE',
      measures: [],
      groupBy: 'NONE',
      values: [
        { field: FIELD.HISTORY_ACTUAL_PERCENT, aggregation: 'AVERAGE' },
        { field: FIELD.HISTORY_PLANNED_PERCENT, aggregation: 'COUNT' },
      ],
      groupField: FIELD.HISTORY_PERIOD_END,
      groupDateUnit: 'MONTH',
    });
    expect(lastPreviewed(api)?.filters ?? null).toBeNull();
  });

  test('a figure over time replaces the current values, and the panel says so', async () => {
    const api = designerApi();
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await addVisual(user, 'COLUMN');
    await chooseField(user, FIELD.ACTUAL_EXPENDITURE);
    await previewed(api, { values: [{ field: FIELD.ACTUAL_EXPENDITURE, aggregation: 'COUNT' }] });

    await chooseField(user, FIELD.HISTORY_ACTUAL_PERCENT);
    expect(
      await within(settings()).findByText(en('dashboards.designer.replacedByHistory')),
    ).toBeTruthy();
    await previewed(api, {
      values: [{ field: FIELD.HISTORY_ACTUAL_PERCENT, aggregation: 'COUNT' }],
      groupBy: 'NONE',
      groupField: FIELD.HISTORY_PERIOD_START,
      groupDateUnit: 'MONTH',
    });
    expect(fieldControl(FIELD.ACTUAL_EXPENDITURE).checked).toBe(false);

    // A card draws no figure over time: the history is not offered there.
    await addVisual(user, 'CARD');
    await waitFor(() => fieldControl(FIELD.ACTUAL_EXPENDITURE));
    expect(
      within(settings()).queryByRole('checkbox', {
        name: fieldNamePattern(FIELD.HISTORY_ACTUAL_PERCENT),
      }),
    ).toBeNull();
  });

  describe('drawn', () => {
    const lineTitle = 'Published progress by month';
    const strategicTitle = 'Projects by strategic';
    const cardTitle = 'Earliest planned end';
    const group = (key: string | null, values: (number | null)[]): VisualGroup => ({
      key,
      label: null,
      projects: 4,
      measured: 3,
      valueEntered: 3,
      values,
      onPlan: null,
    });
    const saved: DesignView = {
      isStandard: false,
      updatedAt: '2026-11-18T09:00:00Z',
      visuals: [
        {
          title: { ar: cardTitle, en: cardTitle },
          visualType: 'CARD',
          measures: [],
          groupBy: 'NONE',
          layoutColumn: 0,
          layoutRow: 0,
          layoutWidth: 4,
          layoutHeight: 2,
          values: [
            { field: FIELD.PLANNED_END_DATE, aggregation: 'MIN' },
            { field: FIELD.IS_STRATEGIC, aggregation: 'SHARE_TRUE' },
          ],
          valueTypes: ['DATE', 'BOOLEAN'],
          // 20515 days after 1970-01-01 is 3 March 2026.
          groups: [group(null, [20515, 40])],
        },
        {
          title: { ar: lineTitle, en: lineTitle },
          visualType: 'LINE',
          measures: [],
          groupBy: 'NONE',
          layoutColumn: 0,
          layoutRow: 2,
          layoutWidth: 12,
          layoutHeight: 5,
          values: [
            { field: FIELD.HISTORY_ACTUAL_PERCENT, aggregation: 'AVERAGE' },
            { field: FIELD.HISTORY_PLANNED_PERCENT, aggregation: 'AVERAGE' },
          ],
          groupField: FIELD.HISTORY_PERIOD_END,
          groupDateUnit: 'MONTH',
          valueTypes: ['PERCENT', 'PERCENT'],
          groups: [group('2026-01', [20, 25]), group('2026-02', [35, null])],
        },
        {
          title: { ar: strategicTitle, en: strategicTitle },
          visualType: 'TABLE',
          measures: [],
          groupBy: 'NONE',
          layoutColumn: 0,
          layoutRow: 7,
          layoutWidth: 6,
          layoutHeight: 4,
          values: [{ field: FIELD.ACTUAL_EXPENDITURE, aggregation: 'COUNT' }],
          groupField: FIELD.IS_STRATEGIC,
          valueTypes: ['SAR'],
          groups: [group('TRUE', [5]), group('FALSE', [2]), group(null, [1])],
        },
      ],
    };

    test('on the dashboard, a date reads as a date, a share as a percentage, and the line has a table of its values', async () => {
      designerApi(saved, true, false);
      const user = userEvent.setup();
      renderApp({ path: DASHBOARD, session: session() });

      const card = present((await screen.findByText(cardTitle)).closest('section'), 'The card');
      expect(plain(card.textContent)).toContain('3 Mar 2026');
      expect(plain(card.textContent)).toContain('40%');

      const chart = screen.getByRole('img', {
        name: (name) => plain(name).startsWith(`${lineTitle}:`),
      });
      expect(chart).toBeTruthy();
      await user.click(screen.getByText(en('dashboards.trend.showTable')));
      const table = await screen.findByRole('table', { name: lineTitle });
      const rows = within(table)
        .getAllByRole('row')
        .map((row) => plain(row.textContent));
      expect(rows[1]).toBe('Jan 202620%25%');
      expect(rows[2]).toBe(`Feb 202635%${en('dashboards.insights.notAvailable')}`);
    });

    test('a yes/no grouping reads Yes, No and Not set on the dashboard, where the field list answers 403', async () => {
      designerApi(saved, true, false);
      renderApp({ path: DASHBOARD, session: session() });

      const table = await screen.findByRole('table', { name: strategicTitle });
      const heads = within(table)
        .getAllByRole('rowheader')
        .map((cell) => plain(cell.textContent));
      expect(heads).toEqual([
        en('dashboards.designer.yes'),
        en('dashboards.designer.no'),
        en('dashboards.designer.notSet'),
      ]);
    });
  });
});

describe('the gallery without the sections list (19 Nov 2026)', () => {
  const sectionTitle = 'Top 10 to follow up';
  const withSection: DesignView = {
    isStandard: false,
    updatedAt: '2026-11-18T09:00:00Z',
    visuals: [
      {
        title: { ar: sectionTitle, en: sectionTitle },
        visualType: 'SECTION',
        measures: [],
        groupBy: 'NONE',
        section: 'TOP_FOLLOW_UP',
        layoutColumn: 0,
        layoutRow: 0,
        layoutWidth: 12,
        layoutHeight: 6,
        groups: [],
      },
    ],
  };

  test('offers the visual types only; a section on the design can still be removed', async () => {
    designerApi(withSection);
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await screen.findByRole('heading', { level: 1, name: en('dashboards.designer.title') });

    const gallery = screen.getByRole('region', { name: en('dashboards.designer.gallery') });
    const types = [
      'CARD',
      'BAR',
      'COLUMN',
      'LINE',
      'PIE',
      'DONUT',
      'TABLE',
      'GAUGE',
      'MAP',
      'TEXT',
      'CONTAINER',
    ];
    // The visual types, the Map and Text box tiles (20 Nov 2026), the Container tile (19 Nov 2026), and the Widgets group of the view's
    // widgets no visual draws (its own test is below). The Map places the one section the gallery offers (designerMapText.test.tsx).
    const widgets = within(gallery).getByRole('region', {
      name: en('dashboards.designer.widgets'),
    });
    expect(
      within(gallery)
        .getAllByRole('button')
        .filter((button) => !widgets.contains(button))
        .map((button) => plain(button.textContent)),
    ).toEqual(types.map((type) => en(`dashboards.designer.type.${type}`)));
    expect(within(gallery).queryByRole('button', { name: new RegExp(sectionTitle) })).toBeNull();

    await user.click(
      await screen.findByRole('button', {
        name: (name) =>
          plain(name) === plain(en('dashboards.designer.editVisual', { title: sectionTitle })),
      }),
    );
    await user.click(
      within(settings()).getByRole('button', { name: en('dashboards.designer.removeSection') }),
    );
    // Gone from the canvas, and the design is changed (a section comes back only with the standard design).
    await waitFor(() => {
      expect(screen.queryByRole('region', { name: sectionTitle })).toBeNull();
    });
    expect(screen.getByText(en('dashboards.designer.unsaved'))).toBeTruthy();
  });
});

describe('chart axes (19 Nov 2026, axes contract)', () => {
  const X = en('dashboards.designer.axisX');
  const Y = en('dashboards.designer.axisY');
  const format = () =>
    within(settings()).getByRole('region', { name: en('dashboards.designer.format') });
  /** The Format part's box of an axis, by its X-axis or Y-axis label. */
  const formatBox = (label: string) => within(format()).getByRole('group', { name: label });
  /** The Data panel's box of an axis: the first box of that label, before the Format part's. */
  const dataBox = (label: string) =>
    present(within(settings()).getAllByRole('group', { name: label })[0]);

  test('a column chart groups on its X-axis and takes its values on the Y-axis; a bar chart the other way round', async () => {
    designerApi();
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });

    await addVisual(user, 'COLUMN');
    expect(
      within(dataBox(X)).getByRole('combobox', { name: en('dashboards.designer.groupByLabel') }),
    ).toBeTruthy();
    expect(within(dataBox(Y)).getByRole('searchbox')).toBeTruthy();
    expect(formatBox(X)).toBeTruthy();

    await addVisual(user, 'BAR');
    expect(
      within(dataBox(Y)).getByRole('combobox', { name: en('dashboards.designer.groupByLabel') }),
    ).toBeTruthy();
    expect(within(dataBox(X)).getByRole('searchbox')).toBeTruthy();

    // Any other type keeps its one Data box and has no Format part.
    await addVisual(user, 'TABLE');
    expect(within(settings()).queryByRole('group', { name: X })).toBeNull();
    expect(
      within(settings()).queryByRole('region', { name: en('dashboards.designer.format') }),
    ).toBeNull();
  });

  test('sends null for the default axes, then the titles, range and hidden axis set, in the preview and the save', async () => {
    const api = designerApi();
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await addVisual(user, 'COLUMN');
    await previewed(api, { visualType: 'COLUMN', axes: null });

    const value = within(formatBox(Y));
    await user.type(
      value.getByRole('textbox', { name: en('dashboards.designer.axisTitleAr') }),
      'التقدم',
    );
    await user.type(
      value.getByRole('textbox', { name: en('dashboards.designer.axisTitleEn') }),
      ' Progress ',
    );
    await user.type(
      value.getByRole('spinbutton', { name: en('dashboards.designer.axisStart') }),
      '0',
    );
    await user.type(
      value.getByRole('spinbutton', { name: en('dashboards.designer.axisEnd') }),
      '100',
    );
    await user.click(
      within(formatBox(X)).getByRole('checkbox', { name: en('dashboards.designer.showAxis') }),
    );
    const axes = {
      category: { show: false, title: null },
      value: {
        show: true,
        title: { ar: 'التقدم', en: 'Progress' },
        min: 0,
        max: 100,
        gridlines: true,
      },
    };
    await previewed(api, { axes });

    await user.click(screen.getByRole('button', { name: en('dashboards.designer.save') }));
    await waitFor(() => {
      expect(api.requestsTo('PUT', /^\/portfolio-insights\/design$/)).toHaveLength(1);
    });
    const put = present(api.requestsTo('PUT', /^\/portfolio-insights\/design$/)[0]);
    expect(visualsSent(put.body).at(-1)?.axes).toEqual(axes);
  });

  test('a start not below the end is marked on the field and not sent', async () => {
    const api = designerApi();
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await addVisual(user, 'LINE');

    const value = within(formatBox(Y));
    await user.click(value.getByRole('checkbox', { name: en('dashboards.designer.gridlines') }));
    const start = value.getByRole('spinbutton', { name: en('dashboards.designer.axisStart') });
    await user.type(start, '50');
    await user.type(
      value.getByRole('spinbutton', { name: en('dashboards.designer.axisEnd') }),
      '10',
    );

    expect(await value.findByText(en('dashboards.designer.axisRangeOrder'))).toBeTruthy();
    expect(start.getAttribute('aria-invalid')).toBe('true');
    const valueAxisSent = async (expected: Record<string, unknown>) => {
      await waitFor(() => {
        expect(lastPreviewed(api)?.axes?.value).toMatchObject(expected);
      });
    };
    await valueAxisSent({ gridlines: false, min: null, max: null });
    // Put right, the range is sent.
    await user.clear(start);
    await user.type(start, '5');
    await valueAxisSent({ min: 5, max: 10 });
    expect(value.queryByText(en('dashboards.designer.axisRangeOrder'))).toBeNull();
  });

  describe('drawn', () => {
    const chartTitle = 'Progress by portfolio';
    const design = (axes: Visual['axes']): DesignView => ({
      isStandard: false,
      updatedAt: '2026-11-18T09:00:00Z',
      visuals: [
        {
          title: { ar: chartTitle, en: chartTitle },
          visualType: 'COLUMN',
          measures: ['AVERAGE_PLANNED', 'AVERAGE_ACTUAL'],
          groupBy: 'PORTFOLIO',
          layoutColumn: 0,
          layoutRow: 0,
          layoutWidth: 12,
          layoutHeight: 5,
          axes,
          groups: [
            {
              key: 'portfolio-a',
              label: null,
              projects: 4,
              measured: 3,
              valueEntered: 3,
              values: [55, 30],
              onPlan: null,
            },
          ],
        },
      ],
    });
    const chart = async () =>
      present((await screen.findByText(chartTitle)).closest('section'), 'The chart');

    test('the value axis writes round ticks in the values’ format, and each axis its title', async () => {
      designerApi(
        design({
          category: { show: true, title: { ar: 'كل محفظة', en: 'Each portfolio' } },
          value: {
            show: true,
            title: { ar: 'متوسط', en: 'Average progress' },
            min: null,
            max: null,
            gridlines: true,
          },
        }),
        true,
        false,
      );
      renderApp({ path: DASHBOARD, session: session() });
      const body = within(await chart());
      expect(await body.findByText('80%')).toBeTruthy();
      expect(body.getByText('100%')).toBeTruthy();
      expect(body.getByText('Each portfolio')).toBeTruthy();
      expect(body.getByText('Average progress')).toBeTruthy();
    });

    test('a hidden value axis draws no tick labels and no title; the columns keep their figures', async () => {
      designerApi(
        design({
          category: { show: true, title: null },
          value: {
            show: false,
            title: { ar: 'متوسط', en: 'Average progress' },
            min: null,
            max: null,
            gridlines: true,
          },
        }),
        true,
        false,
      );
      renderApp({ path: DASHBOARD, session: session() });
      const body = within(await chart());
      expect(await body.findByText('55%')).toBeTruthy();
      expect(body.queryByText('80%')).toBeNull();
      expect(body.queryByText('100%')).toBeNull();
      expect(body.queryByText('Average progress')).toBeNull();
    });
  });
});
