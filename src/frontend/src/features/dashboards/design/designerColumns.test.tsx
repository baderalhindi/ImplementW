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
  standardDesignOf,
} from '@/test/portfolioInsightFixtures.ts';
import { withProjectLookups } from '@/test/projectFixtures.ts';
import { renderApp } from '@/test/renderApp.tsx';

import { type DesignView, type Visual, type VisualResult } from './designApi.ts';

// A table section's columns (20 Nov 2026, docs/architecture/dashboard-designer-table-columns.md): its settings list them under Columns, the
// first column is not listed, an unticked one is left out of the table in the designer and on the dashboard, and the last ticked one stays.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);
const plain = (text: string | null | undefined) => (text ?? '').replace(/[⁨⁩]/g, '').trim();

const DESIGNER = '/dashboards/design';
const DASHBOARD = '/dashboards?dashboard=PORTFOLIO';

const BREAKDOWN_HEADINGS = [
  'projects',
  'value',
  'measured',
  'averagePlanned',
  'averageActual',
  'gap',
  'label',
  'needingFollowUp',
].map((key) => en(`dashboards.insights.${key}`));
const SPEND_HEADINGS = ['plannedSpend', 'actualSpend', 'split'].map((key) =>
  en(`dashboards.insights.${key}`),
);

function present<T>(value: T | null | undefined, what = 'The element'): T {
  if (value === null || value === undefined) {
    throw new Error(`${what} is not there`);
  }
  return value;
}

function visualsSent(body: unknown): Visual[] {
  return (body as { visuals: Visual[] }).visuals;
}

const echo = (visuals: Visual[]): VisualResult[] =>
  visuals.map(({ children: _children, ...visual }) => ({ ...visual, groups: [] }));

/** The Portfolio dashboard's API with every insight answered and this design read (the standard one unless given). */
function api(design: DesignView = standardDesignOf(fullInsights())): MockApi {
  const mock = mockApi();
  withProjectLookups(mock);
  withDashboards(mock, { views: { PORTFOLIO: dashboardView('PORTFOLIO') } });
  return mock
    .on('GET', /^\/portfolio-insights$/, { body: fullInsights() })
    .on('GET', /^\/portfolio-insights\/design$/, { body: design })
    .on('GET', /^\/portfolio-insights\/design\/fields$/, { body: designFieldsFixture() })
    .on('POST', /^\/portfolio-insights\/design\/preview$/, (request) => ({
      body: { isStandard: false, updatedAt: null, visuals: echo(visualsSent(request.body)) },
    }))
    .on('PUT', /^\/portfolio-insights\/design$/, (request) => ({
      body: {
        isStandard: false,
        updatedAt: '2026-11-20T09:00:00Z',
        visuals: echo(visualsSent(request.body)),
      },
    }));
}

const session = () => sessionFor(['R02']);

const settings = () => screen.getByRole('region', { name: en('dashboards.designer.properties') });

const columnsGroup = () =>
  within(settings()).getByRole('group', { name: en('dashboards.designer.columns') });

const ticks = () =>
  within(columnsGroup())
    .getAllByRole('checkbox')
    .map((box) => plain(box.closest('label')?.textContent));

async function edit(user: ReturnType<typeof userEvent.setup>, title: string) {
  await user.click(
    await screen.findByRole('button', {
      name: (name) => plain(name) === plain(en('dashboards.designer.editVisual', { title })),
    }),
  );
}

async function saved(mock: MockApi, user: ReturnType<typeof userEvent.setup>): Promise<Visual[]> {
  await user.click(screen.getByRole('button', { name: en('dashboards.designer.save') }));
  await waitFor(() => {
    expect(mock.requestsTo('PUT', /^\/portfolio-insights\/design$/)).toHaveLength(1);
  });
  return visualsSent(mock.requestsTo('PUT', /^\/portfolio-insights\/design$/)[0]?.body);
}

const sentBy = (visuals: Visual[], title: string) =>
  present(
    visuals.find((visual) => visual.title.en === title),
    title,
  );

/** The insight section drawn under `id` (data-section), on the designer or the dashboard. */
const drawn = (id: string) =>
  present(document.querySelector<HTMLElement>(`[data-section="${id}"]`), `The ${id} section`);

/** Each table's column headings, without the selection column, in order. */
function headings(scope: HTMLElement): string[][] {
  return [...scope.querySelectorAll('table')].map((table) =>
    [...table.querySelectorAll('thead th')]
      .map((cell) => plain(cell.textContent))
      .filter((text) => text !== ''),
  );
}

/** The cells of each body and total row of a table, the selection cell left out. */
function rowWidths(table: HTMLTableElement): number[] {
  return [...table.querySelectorAll('tbody tr:not(.insights__group), tfoot tr')].map(
    (row) => row.querySelectorAll('th, td:not(.cell--select)').length,
  );
}

describe('the settings of a table section', () => {
  test('list its columns after the first, in its own headings; the sector lists its two tables apart', async () => {
    api();
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });

    await edit(user, 'By sector');
    expect(ticks()).toEqual([...BREAKDOWN_HEADINGS, ...SPEND_HEADINGS]);
    expect(
      within(columnsGroup())
        .getAllByRole('group')
        .map((group) => plain(group.querySelector('legend')?.textContent)),
    ).toEqual([en('dashboards.insights.bySector'), en('dashboards.insights.sectorSpend')]);
    expect(ticks()).not.toContain(en('dashboards.insights.sector'));

    await edit(user, 'Top 10 to follow up');
    expect(ticks()).toEqual([
      en('projects.fields.title'),
      en('dashboards.insights.portfolio'),
      en('projects.followUpList.reason'),
      en('projects.followUpList.obstacle'),
      en('projects.followUpList.nextAction'),
    ]);

    await edit(user, 'By portfolio');
    expect(ticks()).toEqual(BREAKDOWN_HEADINGS);

    await edit(user, 'Update completion by entity (table)');
    expect(ticks()).toEqual(
      ['issued', 'submitted', 'accepted', 'overdue', 'percent'].map((key) =>
        en(`externalParticipation.completion.${key}`),
      ),
    );

    // A section that is not a table has no columns to choose.
    await edit(user, 'Why projects need follow-up');
    expect(
      within(settings()).queryByRole('group', { name: en('dashboards.designer.columns') }),
    ).toBeNull();
  });

  test('unticking Value and Gap on By sector leaves them out of its table and saves them as hiddenColumns', async () => {
    const mock = api();
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await edit(user, 'By sector');
    const before = headings(drawn('sector'));
    expect(before[0]).toContain(en('dashboards.insights.value'));
    const widthsBefore = rowWidths(present(drawn('sector').querySelector('table')));

    await user.click(
      within(columnsGroup()).getByRole('checkbox', { name: en('dashboards.insights.value') }),
    );
    await user.click(
      within(columnsGroup()).getByRole('checkbox', { name: en('dashboards.insights.gap') }),
    );

    await waitFor(() => {
      expect(headings(drawn('sector'))[0]).toEqual([
        en('dashboards.insights.sector'),
        ...BREAKDOWN_HEADINGS.filter(
          (text) =>
            text !== en('dashboards.insights.value') && text !== en('dashboards.insights.gap'),
        ),
      ]);
    });
    // The body and total rows lose the same two cells; the spend table keeps all of its columns.
    expect(rowWidths(present(drawn('sector').querySelector('table')))).toEqual(
      widthsBefore.map((width) => width - 2),
    );
    expect(headings(drawn('sector'))[1]).toEqual([
      en('dashboards.insights.sector'),
      ...SPEND_HEADINGS,
    ]);

    const sent = await saved(mock, user);
    expect(sentBy(sent, 'By sector').hiddenColumns).toEqual(['VALUE', 'GAP']);
    // Every other table section sends null; any other visual sends none.
    for (const visual of sent.filter((one) => one.title.en !== 'By sector')) {
      const isTable = [
        'PORTFOLIO_TABLE',
        'INITIATIVE_TABLE',
        'STATUS_TABLE',
        'CURRENT_STAGE_TABLE',
        'TOP_FOLLOW_UP',
        'ENTITY_COMPLETION_TABLE',
      ].includes(visual.section ?? '');
      if (isTable) {
        expect(visual.hiddenColumns, visual.title.en).toBeNull();
      } else {
        expect('hiddenColumns' in visual, visual.title.en).toBe(false);
      }
    }
  });

  test('ticking every column again sends null', async () => {
    const mock = api();
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await edit(user, 'By sector');
    const value = () =>
      within(columnsGroup()).getByRole('checkbox', { name: en('dashboards.insights.value') });
    await user.click(value());
    expect((value() as HTMLInputElement).checked).toBe(false);
    await user.click(value());
    expect(headings(drawn('sector'))[0]).toContain(en('dashboards.insights.value'));

    const sent = await saved(mock, user);
    expect(sentBy(sent, 'By sector').hiddenColumns).toBeNull();
  });

  test('the last ticked column stays ticked', async () => {
    api();
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await edit(user, 'Top 10 to follow up');
    const box = (key: string) =>
      within(columnsGroup()).getByRole<HTMLInputElement>('checkbox', { name: en(key) });

    for (const key of [
      'projects.fields.title',
      'dashboards.insights.portfolio',
      'projects.followUpList.reason',
      'projects.followUpList.obstacle',
    ]) {
      await user.click(box(key));
    }
    const last = box('projects.followUpList.nextAction');
    expect(last.checked).toBe(true);
    expect(last.disabled).toBe(true);
    await user.click(last);
    expect(box('projects.followUpList.nextAction').checked).toBe(true);
    expect(headings(drawn('top'))[0]).toEqual([
      en('projects.fields.formalProjectId'),
      en('projects.followUpList.nextAction'),
    ]);

    // Ticking another frees it again.
    await user.click(box('projects.followUpList.reason'));
    expect(box('projects.followUpList.nextAction').disabled).toBe(false);
  });

  test("an initiative's group rows span the columns left", async () => {
    api();
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await edit(user, 'By initiative');
    const span = () =>
      drawn('initiative').querySelector('.insights__group th')?.getAttribute('colspan');
    // The selection, the name and eight figures.
    expect(span()).toBe('10');

    for (const key of ['measured', 'averagePlanned', 'label']) {
      await user.click(
        within(columnsGroup()).getByRole('checkbox', { name: en(`dashboards.insights.${key}`) }),
      );
    }
    expect(span()).toBe('7');
    const table = present(drawn('initiative').querySelector('table'));
    expect(rowWidths(table).every((width) => width === 6)).toBe(true);
  });
});

describe('the dashboard', () => {
  test("draws a table section without its design's hidden columns, header, cells and total", async () => {
    const standard = standardDesignOf(fullInsights());
    api({
      ...standard,
      isStandard: false,
      visuals: standard.visuals.map((visual) =>
        visual.section === 'SECTOR_TABLE'
          ? { ...visual, hiddenColumns: ['VALUE', 'GAP', 'SPLIT'] }
          : visual.section === 'TOP_FOLLOW_UP'
            ? { ...visual, hiddenColumns: ['OBSTACLE'] }
            : visual,
      ),
    });
    renderApp({ path: DASHBOARD, session: session() });

    await waitFor(() => {
      expect(document.querySelector('[data-section="sector"]')).not.toBeNull();
    });
    const [breakdown, spend] = headings(drawn('sector'));
    expect(breakdown).not.toContain(en('dashboards.insights.value'));
    expect(breakdown).not.toContain(en('dashboards.insights.gap'));
    expect(breakdown).toContain(en('dashboards.insights.measured'));
    expect(spend).toEqual([
      en('dashboards.insights.sector'),
      en('dashboards.insights.plannedSpend'),
      en('dashboards.insights.actualSpend'),
    ]);
    expect(drawn('sector').querySelector('.insights__split')).toBeNull();
    const table = present(drawn('sector').querySelector('table'));
    expect(rowWidths(table).every((width) => width === 7)).toBe(true);

    expect(headings(drawn('top'))[0]).not.toContain(en('projects.followUpList.obstacle'));
    expect(within(drawn('top')).queryByText('Permit delay')).toBeNull();
    // Other tables are drawn whole.
    expect(headings(drawn('portfolio'))[0]).toContain(en('dashboards.insights.value'));
  });
});
