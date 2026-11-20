import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import { dashboardView, withDashboards } from '@/test/dashboardFixtures.ts';
import { sessionFor } from '@/test/identityAccessFixtures.ts';
import { type MockApi, mockApi } from '@/test/mockApi.ts';
import {
  designFieldsFixture,
  fullInsights,
  RISK_FIELD,
  riskFieldsFixture,
  standardDesignOf,
} from '@/test/portfolioInsightFixtures.ts';
import { PROJECT_ID, withProjectLookups } from '@/test/projectFixtures.ts';
import { renderApp } from '@/test/renderApp.tsx';
import { standardMatrix } from '@/test/riskFixtures.ts';

import {
  type DesignView,
  type FieldValue,
  type TableCell,
  type TableColumn,
  type Visual,
  type VisualGroup,
  type VisualResult,
  type VisualType,
} from './designApi.ts';
import { formatDayNumber } from './fields.ts';

// The Risks record source (20 Nov 2026, docs/architecture/dashboard-designer-record-sources.md): a card, chart or table counts risks (one row
// per risk) instead of projects. The Source select is found by its contract name (source.label), the fields by their i18n names.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);
const plain = (text: string | null | undefined) =>
  (text ?? '')
    .replace(/[\u2068\u2069\u00a0]/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();
const escaped = (text: string) => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

const DESIGNER = '/dashboards/design';
const DASHBOARD = '/dashboards?dashboard=PORTFOLIO';

const RISK_ID = 'f1000000-0000-4000-8000-000000000001';
const OWNER_ID = 'f2000000-0000-4000-8000-000000000001';

function present<T>(value: T | null | undefined, what = 'The element'): T {
  if (value === null || value === undefined) {
    throw new Error(`${what} is not there`);
  }
  return value;
}

function visualsSent(body: unknown): Visual[] {
  return (body as { visuals: Visual[] }).visuals;
}

/** What the mocked API answers for a visual: a column table's `table` (empty unless given), else one group with a 1 per value. */
function answer(visual: Visual, table: NonNullable<VisualResult['table']> | null): VisualResult {
  const { children: _children, ...rest } = visual;
  const columns = visual.columns ?? null;
  if (columns !== null && columns.length > 0) {
    return {
      ...rest,
      groups: [],
      table: table ?? {
        columnTypes: columns.map(() => 'COUNT'),
        rows: [],
        totals: null,
        truncated: false,
      },
    };
  }
  if (visual.visualType === 'SECTION' || visual.visualType === 'TEXT') {
    return { ...rest, groups: [] };
  }
  const entries = visual.values?.length ?? visual.measures.length;
  const group: VisualGroup = {
    key: null,
    label: null,
    projects: 1,
    measured: 1,
    valueEntered: 1,
    values: Array.from({ length: entries }, () => 1),
    onPlan: null,
  };
  return { ...rest, groups: [group] };
}

/**
 * The Portfolio dashboard's API: the insights, this design, the fields of each source (the risks' with `?source=RISKS`), and a preview and a
 * save that echo the visuals sent; a column table is answered with `table` (or an empty one).
 */
function api(
  design: DesignView = NOTHING_SAVED,
  table: NonNullable<VisualResult['table']> | null = null,
): MockApi {
  const mock = mockApi();
  withProjectLookups(mock);
  withDashboards(mock, { views: { PORTFOLIO: dashboardView('PORTFOLIO') } });
  const echo = (visuals: Visual[]) => visuals.map((visual) => answer(visual, table));
  return (
    mock
      .on('GET', /^\/portfolio-insights$/, { body: fullInsights() })
      .on('GET', /^\/portfolio-insights\/design$/, { body: design })
      // The risk matrix in force names the ratings and levels.
      .on('GET', /^\/configuration-resolutions\/RISK_MATRIX$/, { body: standardMatrix() })
      .on('GET', /^\/portfolio-insights\/design\/fields$/, (request) => ({
        body: request.query.get('source') === 'RISKS' ? riskFieldsFixture() : designFieldsFixture(),
      }))
      .on('POST', /^\/portfolio-insights\/design\/preview$/, (request) => ({
        body: { isStandard: false, updatedAt: null, visuals: echo(visualsSent(request.body)) },
      }))
      .on('PUT', /^\/portfolio-insights\/design$/, (request) => ({
        body: {
          isStandard: false,
          updatedAt: '2026-11-20T09:00:00Z',
          visuals: echo(visualsSent(request.body)),
        },
      }))
  );
}

const NOTHING_SAVED: DesignView = { isStandard: true, updatedAt: null, visuals: [] };

const session = () => sessionFor(['R02']);

const settings = () => screen.getByRole('region', { name: en('dashboards.designer.properties') });

/** The selected visual's Source select, or null where the visual takes none. */
function sourceSelect(): HTMLSelectElement | null {
  const select = within(settings()).queryByRole('combobox', {
    name: en('dashboards.designer.source.label'),
  });
  if (select !== null && !(select instanceof HTMLSelectElement)) {
    throw new Error('The Source control is not a select');
  }
  return select;
}

async function chooseSource(
  user: ReturnType<typeof userEvent.setup>,
  source: 'PROJECTS' | 'RISKS',
) {
  const select = await waitFor(() => present(sourceSelect(), 'The Source select'));
  await user.selectOptions(select, source);
}

async function addVisual(user: ReturnType<typeof userEvent.setup>, type: VisualType) {
  await screen.findByRole('heading', { level: 1, name: en('dashboards.designer.title') });
  const tile = await waitFor(() => {
    const button = screen.getByRole('button', { name: en(`dashboards.designer.type.${type}`) });
    expect(button.hasAttribute('disabled')).toBe(false);
    return button;
  });
  await user.click(tile);
}

function previews(mock: MockApi): Visual[][] {
  return mock
    .requestsTo('POST', /^\/portfolio-insights\/design\/preview$/)
    .map((request) => visualsSent(request.body));
}

function lastPreviewed(mock: MockApi): Visual | undefined {
  return previews(mock).at(-1)?.at(-1);
}

async function previewedLast(mock: MockApi, expected: Partial<Visual>) {
  await waitFor(() => {
    expect(lastPreviewed(mock)).toMatchObject(expected);
  });
}

/** A risk field's English name: dashboards.designer.field.RISK.<CODE>. */
function riskFieldName(key: string): string {
  const code = present(key.split('.').at(-1), key);
  return en(`dashboards.designer.field.RISK.${code}`);
}

/** A field tick's accessible name: the field's name, then perhaps its type's word. */
function riskFieldPattern(key: string): RegExp {
  const descriptor = present(
    riskFieldsFixture().fields.find((field) => field.key === key),
    key,
  );
  const typeWord = en(`dashboards.designer.fieldType.${descriptor.type}`);
  return new RegExp(`^${escaped(riskFieldName(key))}\\s*(${escaped(typeWord)})?$`);
}

const fieldColumn = (key: string): TableColumn => ({ kind: 'FIELD', key, aggregation: 'NONE' });
const dimension = (key: string): TableColumn => ({ kind: 'DIMENSION', key, aggregation: 'NONE' });

const RISK_COUNT: FieldValue[] = [{ field: RISK_FIELD.RISK, aggregation: 'COUNT' }];
const RISK_LIST: TableColumn[] = [
  fieldColumn(RISK_FIELD.TITLE),
  fieldColumn(RISK_FIELD.PROJECT),
  fieldColumn(RISK_FIELD.RATING),
  fieldColumn(RISK_FIELD.STATUS),
  fieldColumn(RISK_FIELD.OWNER),
  fieldColumn(RISK_FIELD.NEXT_REVIEW_DATE),
];

describe('the Source select', () => {
  test.each(['CARD', 'BAR', 'COLUMN', 'PIE', 'DONUT', 'LINE', 'TABLE'] as const)(
    'a new %s offers Projects and Risks, starting as Projects',
    async (type) => {
      const mock = api();
      const user = userEvent.setup();
      renderApp({ path: DESIGNER, session: session() });
      await addVisual(user, type);
      await previewedLast(mock, { visualType: type });

      const select = await waitFor(() => present(sourceSelect(), `The Source select of ${type}`));
      expect(select.value).toBe('PROJECTS');
      expect(
        Array.from(select.options).map((option) => [option.value, plain(option.text)]),
      ).toEqual([
        ['PROJECTS', en('dashboards.designer.source.PROJECTS')],
        ['RISKS', en('dashboards.designer.source.RISKS')],
      ]);
      // A visual starts as Projects, which is never sent.
      expect('source' in present(lastPreviewed(mock))).toBe(false);
    },
  );

  test.each(['GAUGE', 'MAP', 'TEXT', 'CONTAINER'] as const)('a %s has none', async (type) => {
    const mock = api();
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await addVisual(user, type);
    await previewedLast(mock, { visualType: type });
    expect(sourceSelect()).toBeNull();
  });

  test('a dashboard section has none', async () => {
    api(standardDesignOf(fullInsights()));
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await user.click(
      await screen.findByRole('button', {
        name: (name) =>
          plain(name) === plain(en('dashboards.designer.editVisual', { title: 'By sector' })),
      }),
    );
    await waitFor(() => {
      expect(
        within(settings()).getByRole('button', { name: en('dashboards.designer.makeEditable') }),
      ).toBeTruthy();
    });
    expect(sourceSelect()).toBeNull();
  });
});

describe('a visual switched to Risks', () => {
  test('a card counts the risks: source RISKS, no measures, Number of risks, from the risk fields', async () => {
    const mock = api();
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await addVisual(user, 'CARD');
    await previewedLast(mock, { visualType: 'CARD' });
    await chooseSource(user, 'RISKS');

    await previewedLast(mock, {
      visualType: 'CARD',
      source: 'RISKS',
      measures: [],
      values: RISK_COUNT,
    });
    // The risk fields are read with ?source=RISKS.
    await waitFor(() => {
      expect(
        mock
          .requestsTo('GET', /^\/portfolio-insights\/design\/fields$/)
          .some((request) => request.query.get('source') === 'RISKS'),
      ).toBe(true);
    });
    expect(present(sourceSelect()).value).toBe('RISKS');
    // The panel says the figures were replaced, as it does elsewhere.
    expect(within(settings()).getAllByRole('status').length).toBeGreaterThan(0);
    expect(plain(lastPreviewed(mock)?.title.en)).toMatch(/risks/i);
  });

  test('a donut counts the risks by Rating', async () => {
    const mock = api();
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await addVisual(user, 'DONUT');
    await previewedLast(mock, { visualType: 'DONUT' });
    await chooseSource(user, 'RISKS');

    await previewedLast(mock, {
      visualType: 'DONUT',
      source: 'RISKS',
      measures: [],
      values: RISK_COUNT,
      groupBy: 'NONE',
      groupField: RISK_FIELD.RATING,
    });
    // "Number of risks by Rating".
    const title = plain(lastPreviewed(mock)?.title.en);
    expect(title).toContain(riskFieldName(RISK_FIELD.RATING));
    expect(title).toMatch(/risks/i);
  });

  test('a table lists the risks: Title, Project, Rating, Status, Owner and Next review date, none summarized', async () => {
    const mock = api();
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await addVisual(user, 'TABLE');
    await previewedLast(mock, { visualType: 'TABLE' });
    await chooseSource(user, 'RISKS');

    await previewedLast(mock, {
      visualType: 'TABLE',
      source: 'RISKS',
      measures: [],
      groupBy: 'NONE',
      columns: RISK_LIST,
    });
    expect(lastPreviewed(mock)?.values ?? null).toBeNull();

    // Add column offers the project details, never the portfolio figures.
    await user.click(
      within(settings()).getByRole('button', { name: en('dashboards.designer.addColumn') }),
    );
    await waitFor(() => {
      expect(
        within(settings()).getAllByText(en('dashboards.designer.projectDetails')).length,
      ).toBeGreaterThan(0);
    });
    expect(within(settings()).queryByText(en('dashboards.designer.portfolioFigures'))).toBeNull();
    const sector = en('dashboards.designer.groupBy.SECTOR');
    const exact = (candidate: string) => plain(candidate) === sector;
    const pick = await waitFor(
      () =>
        within(settings()).queryByRole('button', { name: exact }) ??
        within(settings()).queryByRole('option', { name: exact }) ??
        within(settings()).queryByRole('menuitem', { name: exact }) ??
        within(settings()).getByRole('checkbox', { name: exact }),
    );
    await user.click(pick);
    await previewedLast(mock, { source: 'RISKS', columns: [...RISK_LIST, dimension('SECTOR')] });
  });

  test('the Data panel hides the Portfolio figures and lists the risk fields, with the project details', async () => {
    const mock = api();
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await addVisual(user, 'COLUMN');
    await previewedLast(mock, { visualType: 'COLUMN' });
    expect(
      within(settings()).getAllByText(en('dashboards.designer.portfolioFigures')).length,
    ).toBeGreaterThan(0);

    await chooseSource(user, 'RISKS');
    await previewedLast(mock, { source: 'RISKS' });
    await waitFor(() => {
      expect(within(settings()).queryByText(en('dashboards.designer.portfolioFigures'))).toBeNull();
    });
    // Risks' own fields under their source, and the project dimensions under Project details.
    expect(
      within(settings()).getAllByText(en('dashboards.designer.fieldSource.RISK')).length,
    ).toBeGreaterThan(0);
    expect(
      within(settings()).getAllByText(en('dashboards.designer.projectDetails')).length,
    ).toBeGreaterThan(0);
    const pane = within(settings());
    const count = riskFieldPattern(RISK_FIELD.RISK);
    const tick =
      pane.queryByRole('checkbox', { name: count }) ?? pane.getByRole('radio', { name: count });
    expect((tick as HTMLInputElement).checked).toBe(true);
    // No project field is offered for a risk.
    expect(
      pane.queryByText(
        en('dashboards.designer.field.FINANCIAL_KPI_FINANCIAL_POSITION.ACTUAL_EXPENDITURE_TO_DATE'),
      ),
    ).toBeNull();
  });

  test('a filter on the risk status is sent with the risk field', async () => {
    const mock = api();
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await addVisual(user, 'BAR');
    await previewedLast(mock, { visualType: 'BAR' });
    await chooseSource(user, 'RISKS');
    await previewedLast(mock, { source: 'RISKS' });

    await user.click(
      within(settings()).getByRole('button', { name: en('dashboards.designer.addFilter') }),
    );
    const picker = await waitFor(() =>
      within(settings()).getByRole<HTMLSelectElement>('combobox', {
        name: en('dashboards.designer.filterField'),
      }),
    );
    const option = present(
      Array.from(picker.options).find(
        (candidate) =>
          candidate.value === RISK_FIELD.STATUS ||
          plain(candidate.textContent) === riskFieldName(RISK_FIELD.STATUS),
      ),
      'The risk status option',
    );
    await user.selectOptions(picker, option);
    await user.click(
      await waitFor(() =>
        within(settings()).getByRole('checkbox', {
          name: new RegExp(`^${escaped(en('risks.status.TREATMENT'))}$`),
        }),
      ),
    );
    await previewedLast(mock, {
      source: 'RISKS',
      filters: [{ field: RISK_FIELD.STATUS, values: ['TREATMENT'] }],
    });
  });

  test.each(['CARD', 'DONUT', 'TABLE'] as const)(
    'a %s switched back to Projects draws the project figures again, and sends no source',
    async (type) => {
      const mock = api();
      const user = userEvent.setup();
      renderApp({ path: DESIGNER, session: session() });
      await addVisual(user, type);
      await previewedLast(mock, { visualType: type });
      const { title: _title, ...asPlaced } = present(lastPreviewed(mock));

      await chooseSource(user, 'RISKS');
      await previewedLast(mock, { source: 'RISKS' });
      await chooseSource(user, 'PROJECTS');
      await waitFor(() => {
        expect('source' in present(lastPreviewed(mock))).toBe(false);
      });
      const { title: _again, ...restored } = present(lastPreviewed(mock));
      expect(restored).toEqual(asPlaced);
    },
  );
});

describe('saving', () => {
  test('sends source RISKS on the risk visual only; no project visual sends a source', async () => {
    const mock = api();
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await addVisual(user, 'CARD');
    await addVisual(user, 'DONUT');
    await chooseSource(user, 'RISKS');
    await previewedLast(mock, { visualType: 'DONUT', source: 'RISKS' });

    await user.click(screen.getByRole('button', { name: en('dashboards.designer.save') }));
    await waitFor(() => {
      expect(mock.requestsTo('PUT', /^\/portfolio-insights\/design$/)).toHaveLength(1);
    });
    const sent = visualsSent(mock.requestsTo('PUT', /^\/portfolio-insights\/design$/)[0]?.body);
    expect(sent.map((visual) => [visual.visualType, visual.source ?? 'absent'])).toEqual([
      ['CARD', 'absent'],
      ['DONUT', 'RISKS'],
    ]);
    expect('source' in present(sent[0])).toBe(false);
    // No preview ever sent a source for a project visual.
    for (const visuals of previews(mock)) {
      for (const visual of visuals) {
        if (visual.source !== 'RISKS') {
          expect('source' in visual, visual.title.en).toBe(false);
        }
      }
    }
  });

  test('the standard design sends no source on any visual', async () => {
    const mock = api(standardDesignOf(fullInsights()));
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await screen.findByRole('heading', { level: 1, name: en('dashboards.designer.title') });
    await addVisual(user, 'CARD');
    await user.click(screen.getByRole('button', { name: en('dashboards.designer.save') }));
    await waitFor(() => {
      expect(mock.requestsTo('PUT', /^\/portfolio-insights\/design$/)).toHaveLength(1);
    });
    const sent = visualsSent(mock.requestsTo('PUT', /^\/portfolio-insights\/design$/)[0]?.body);
    expect(sent.filter((visual) => 'source' in visual).map((visual) => visual.title.en)).toEqual(
      [],
    );
  });
});

// Saved risk visuals as the API answers them: a card of 7 risks, a donut of 4 High and 3 Low, and the risk list.
const CARD_TITLE = 'Risks we track';
const DONUT_TITLE = 'Risks by rating';
const TABLE_TITLE = 'Risks list';
const REVIEW_DAY = 20_800; // 2026-12-13
const label = (en: string, ar: string) => ({ ar, en });
const cell = (extra: Partial<TableCell>): TableCell => ({
  key: null,
  label: null,
  value: null,
  ...extra,
});
const group = (key: string | null, labelOf: VisualGroup['label'], value: number): VisualGroup => ({
  key,
  label: labelOf,
  projects: 2,
  measured: 2,
  valueEntered: 2,
  values: [value],
  onPlan: null,
});
const RISK_TABLE: NonNullable<VisualResult['table']> = {
  columnTypes: ['TEXT', 'ITEM', 'CODE', 'CODE', 'ITEM', 'DATE'],
  rows: [
    {
      projects: 1,
      projectId: PROJECT_ID,
      cells: [
        cell({ key: RISK_ID, label: label('Flooding of the site', 'غرق الموقع') }),
        cell({ key: PROJECT_ID, label: label('Ring road', 'الطريق الدائري') }),
        cell({ key: 'HIGH', label: label('High', 'مرتفع') }),
        cell({ key: 'TREATMENT' }),
        cell({ key: OWNER_ID, label: label('Sara Ali', 'سارة علي') }),
        cell({ value: REVIEW_DAY }),
      ],
    },
  ],
  totals: null,
  truncated: false,
};

const SAVED_RISKS: DesignView = {
  isStandard: false,
  updatedAt: '2026-11-20T09:00:00Z',
  visuals: [
    {
      title: label(CARD_TITLE, CARD_TITLE),
      visualType: 'CARD',
      source: 'RISKS',
      measures: [],
      groupBy: 'NONE',
      layoutColumn: 0,
      layoutRow: 0,
      layoutWidth: 4,
      layoutHeight: 2,
      values: [{ field: RISK_FIELD.RISK, aggregation: 'COUNT' }],
      valueTypes: ['COUNT'],
      groups: [group(null, null, 7)],
    },
    {
      title: label(DONUT_TITLE, DONUT_TITLE),
      visualType: 'DONUT',
      source: 'RISKS',
      measures: [],
      groupBy: 'NONE',
      groupField: RISK_FIELD.RATING,
      layoutColumn: 4,
      layoutRow: 0,
      layoutWidth: 8,
      layoutHeight: 4,
      values: [{ field: RISK_FIELD.RISK, aggregation: 'COUNT' }],
      valueTypes: ['COUNT'],
      groups: [group('HIGH', label('High', 'مرتفع'), 4), group('LOW', label('Low', 'منخفض'), 3)],
    },
    {
      title: label(TABLE_TITLE, TABLE_TITLE),
      visualType: 'TABLE',
      source: 'RISKS',
      measures: [],
      groupBy: 'NONE',
      layoutColumn: 0,
      layoutRow: 4,
      layoutWidth: 12,
      layoutHeight: 6,
      values: null,
      columns: RISK_LIST,
      showTotals: false,
      groups: [],
      table: RISK_TABLE,
    },
  ],
};

/** A cell's words without its sort arrow or other hidden marks. */
function wordsOf(element: Element): string {
  const copy = element.cloneNode(true) as Element;
  copy.querySelectorAll('[aria-hidden="true"]').forEach((hidden) => {
    hidden.remove();
  });
  return plain(copy.textContent);
}

describe('risk visuals drawn on the dashboard', () => {
  test('a card reads the number of risks, a donut the risks by rating', async () => {
    api(SAVED_RISKS, RISK_TABLE);
    renderApp({ path: DASHBOARD, session: session() });

    const card = present((await screen.findByText(CARD_TITLE)).closest('section'), 'The card');
    expect(plain(card.textContent)).toContain('7');

    // The ring's caption repeats the title: the visual is the section around its heading.
    const donut = present(
      screen
        .getAllByText(DONUT_TITLE)
        .map((element) => element.closest('section'))
        .find((section) => section !== null),
      'The donut',
    );
    const words = plain(donut.textContent);
    expect(words).toContain('High');
    expect(words).toContain('4');
    expect(words).toContain('Low');
    expect(words).toContain('3');
  });

  test('a risk list reads each cell’s words, links the project and the risk, and writes the review date', async () => {
    api(SAVED_RISKS, RISK_TABLE);
    renderApp({ path: DASHBOARD, session: session() });

    const table = await screen.findByRole('table', { name: TABLE_TITLE });
    const heads = within(table)
      .getAllByRole('columnheader')
      .map(wordsOf)
      .filter((head) => head !== '');
    expect(heads).toEqual(RISK_LIST.map((column) => riskFieldName(column.key)));

    const row = present(within(table).getAllByRole('row').at(-1), 'The risk row');
    // Each cell, the row selection's checkbox left out; the first is the row's heading.
    const cells = [...row.querySelectorAll('th, td')]
      .filter(
        (one) =>
          !one.classList.contains('cell--select') &&
          one.querySelector('input[type="checkbox"]') === null,
      )
      .map(wordsOf);
    expect(cells).toEqual([
      'Flooding of the site',
      'Ring road',
      'High',
      en('risks.status.TREATMENT'),
      'Sara Ali',
      formatDayNumber(REVIEW_DAY, 'en'),
    ]);

    const project = within(table).getByRole('link', { name: 'Ring road' });
    expect(project.getAttribute('href')).toBe(`/projects/${PROJECT_ID}`);
    const risk = within(table).getByRole('link', { name: 'Flooding of the site' });
    expect(risk.getAttribute('href')).toBe(`/projects/${PROJECT_ID}/risks/${RISK_ID}`);
  });
});
