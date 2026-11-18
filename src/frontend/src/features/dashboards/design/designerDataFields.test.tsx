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
const AGGREGATIONS: readonly string[] = ['COUNT', 'SUM', 'AVERAGE', 'MIN', 'MAX'];

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

/** A source's English name: dashboards.designer.fieldSource.<PROJECTION_CODE with "." → "_">. */
function sourceName(projectionCode: string): string {
  return en(`dashboards.designer.fieldSource.${projectionCode.replace('.', '_')}`);
}

/** The projections of the fixture, in the register's order. */
const SOURCES = [...new Set(FIELDS.map((field) => field.projectionCode))];

/** What the mocked API calculates for a visual: one group, a figure per value (or measure). */
function calculated(visual: Visual): VisualResult {
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
      figures ? { ...visual, groups: design.visuals[index]?.groups ?? [] } : calculated(visual),
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

/** Places a new visual from the gallery; the designer selects it, so its settings open. */
async function addVisual(user: ReturnType<typeof userEvent.setup>, type: VisualType) {
  await screen.findByRole('heading', { level: 1, name: en('dashboards.designer.title') });
  await user.click(screen.getByRole('button', { name: en(`dashboards.designer.type.${type}`) }));
}

/** A field's tick in the Data panel: a checkbox where the visual takes several values, a radio where it takes one. */
function fieldControl(key: string): HTMLInputElement {
  const name = new RegExp(escaped(fieldName(key)));
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
    await addVisual(user, 'TABLE');
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
    await addVisual(user, 'TABLE');
    await waitFor(() => fieldControl(FIELD.ACTUAL_EXPENDITURE));

    await user.type(searchBox(), 'expenditure');
    await waitFor(() => {
      expect(
        within(settings()).queryByRole('checkbox', {
          name: new RegExp(escaped(fieldName(FIELD.OPEN_RISKS))),
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

  test('a table takes up to six values, a bar one', async () => {
    const api = designerApi();
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await addVisual(user, 'TABLE');
    const seven = [
      FIELD.APPROVED_BUDGET,
      FIELD.ACTUAL_EXPENDITURE,
      FIELD.FORECAST_AT_COMPLETION,
      FIELD.ACTUAL_PERCENT,
      FIELD.PLANNED_PERCENT,
      FIELD.OPEN_RISKS,
      FIELD.FINISH_VARIANCE_DAYS,
    ];
    for (const key of seven.slice(0, 6)) {
      await chooseField(user, key);
    }
    // The seventh is not taken: its tick is refused (disabled) or has no effect.
    const seventh = fieldControl(present(seven[6]));
    if (!seventh.disabled) {
      await user.click(seventh);
    }
    await waitFor(() => {
      expect(lastPreviewed(api)?.values?.map((value) => value.field)).toEqual(seven.slice(0, 6));
    });
    expect(fieldControl(present(seven[6])).checked).toBe(false);

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
