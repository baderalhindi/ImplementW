import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test, vi } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import { dashboardView, withDashboards } from '@/test/dashboardFixtures.ts';
import { sessionFor } from '@/test/identityAccessFixtures.ts';
import { type MockApi, mockApi, page } from '@/test/mockApi.ts';
import {
  fullInsights,
  INITIATIVE_A,
  NO_SECTIONS,
  PORTFOLIO_A,
  portfolioCard,
} from '@/test/portfolioInsightFixtures.ts';
import {
  entitySession,
  PROJECT_ID,
  projectDetail,
  projectSummary,
  withProject,
  withProjectLookups,
} from '@/test/projectFixtures.ts';
import { myRequest, myUpdatePage } from '@/test/monthlyUpdateFixtures.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';

// RPMO's figures on the Portfolio Dashboard, the printable cards and the Home's own views (the 5 Nov 2026 review §3).

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);
const plain = (text: string | null | undefined) => (text ?? '').replace(/[⁨⁩]/g, '');
const DASHBOARD = '/dashboards?dashboard=PORTFOLIO';

function dashboardApi(insights: unknown): MockApi {
  const api = mockApi();
  withProjectLookups(api);
  withDashboards(api, { views: { PORTFOLIO: dashboardView('PORTFOLIO') } });
  api.on('GET', /^\/portfolio-insights$/, { body: insights });
  return api;
}

const section = (id: string) => document.querySelector<HTMLElement>(`[data-section="${id}"]`);

function present(element: HTMLElement | null): HTMLElement {
  if (element === null) {
    throw new Error('The element is not on the page');
  }
  return element;
}

/** The nearest `selector` around an element, which the test expects to be there. */
function around(element: HTMLElement, selector: string): HTMLElement {
  const found = element.closest<HTMLElement>(selector);
  if (found === null) {
    throw new Error(`Nothing matches ${selector}`);
  }
  return found;
}
const session = () => sessionFor(['R02']);

describe('the sections', () => {
  test('each section shows only when answered, and none of a null one', async () => {
    dashboardApi({ ...NO_SECTIONS, stages: fullInsights().stages, onPlan: fullInsights().onPlan });
    renderApp({ path: DASHBOARD, session: session() });
    await screen.findByRole('heading', { level: 2, name: en('dashboards.insights.byStatus') });
    expect(section('headline')).toBeNull();
    expect(section('portfolio')).toBeNull();
    expect(section('sector')).toBeNull();
    expect(section('top')).toBeNull();
    expect(
      screen.getByRole('heading', { level: 2, name: en('dashboards.insights.onPlan') }),
    ).toBeTruthy();
  });

  test('projects by status and, apart from them, by current stage with their value (round 7, item 1)', async () => {
    dashboardApi({
      ...NO_SECTIONS,
      stages: fullInsights().stages,
      byCurrentStage: [
        {
          stageCode: 'DESIGN',
          name: { en: 'Design', ar: 'التصميم' },
          projects: 3,
          valueSar: '9000000.00',
          valueEntered: 3,
        },
        {
          stageCode: 'EXECUTION',
          name: { en: 'Execution', ar: 'التنفيذ' },
          projects: 5,
          valueSar: '41000000.00',
          valueEntered: 4,
        },
        { stageCode: null, name: null, projects: 2, valueSar: '0.00', valueEntered: 0 },
      ],
    });
    renderApp({ path: DASHBOARD, session: session() });
    await screen.findByRole('heading', {
      level: 2,
      name: en('dashboards.insights.byCurrentStage'),
    });
    expect(
      screen.getByRole('heading', { level: 2, name: en('dashboards.insights.byStatus') }),
    ).toBeTruthy();
    const stages = plain(section('current-stage')?.textContent);
    expect(stages).toContain('Execution');
    expect(stages).toContain(en('dashboards.insights.noCurrentStage'));
    expect(stages).toContain('41,000,000');
  });

  test('nothing at all when every section is null', async () => {
    const api = dashboardApi(NO_SECTIONS);
    renderApp({ path: DASHBOARD, session: session() });
    await waitFor(() => {
      expect(api.requestsTo('GET', /^\/portfolio-insights$/)).toHaveLength(1);
    });
    expect(document.querySelector('.insights')).toBeNull();
  });

  test('no project reads "no data", never zeros', async () => {
    const empty = fullInsights();
    dashboardApi({
      ...empty,
      headline: {
        ...empty.headline,
        registeredProjects: 0,
        inProgressProjects: 0,
        valueEntered: 0,
        measured: 0,
        totalValueSar: '0.00',
      },
    });
    renderApp({ path: DASHBOARD, session: session() });
    await screen.findByText(en('dashboards.noData'));
    expect(section('headline')).toBeNull();
    expect(document.querySelector('.insights')).toBeNull();
  });

  test('the headline: six cards with coverage, a simple average, value hidden when null, unmeasured not zero', async () => {
    const full = fullInsights();
    dashboardApi({
      ...full,
      headline: {
        ...full.headline,
        totalValueSar: null,
        averagePlannedPercent: null,
        averageActualPercent: null,
        gapPoints: null,
        onPlan: null,
        measured: 0,
      },
    });
    renderApp({ path: DASHBOARD, session: session() });
    await waitFor(() => {
      expect(section('headline')).not.toBeNull();
    });
    const headline = present(section('headline'));
    expect(within(headline).queryByText(en('dashboards.insights.totalValue'))).toBeNull();
    expect(plain(headline.textContent)).toContain('In progress: 120');
    expect(plain(headline.textContent)).toContain('Progress measured for 0 of 167');
    expect(within(headline).getAllByText(en('dashboards.insights.simpleAverage'))).toHaveLength(2);
    expect(within(headline).getAllByText(/Not available/)).toHaveLength(4);
  });

  test('the value card and its coverage when the caller may see amounts', async () => {
    dashboardApi(fullInsights());
    renderApp({ path: DASHBOARD, session: session() });
    await waitFor(() => {
      expect(section('headline')).not.toBeNull();
    });
    const headline = present(section('headline'));
    expect(plain(headline.textContent)).toContain('1,500,000 SAR');
    expect(plain(headline.textContent)).toContain('Value entered for 167 of 167');
    expect(plain(headline.textContent)).toContain('Overdue actions: 4');
  });

  test('a table has its total row from `total`, not from adding the rows, and links to the card', async () => {
    dashboardApi(fullInsights());
    renderApp({ path: DASHBOARD, session: session() });
    await waitFor(() => {
      expect(section('portfolio')).not.toBeNull();
    });
    const table = present(section('portfolio'));
    const total = present(table.querySelector<HTMLElement>('tr[data-row="total"]'));
    expect(within(total).getByRole('rowheader').textContent).toBe(en('dashboards.insights.total'));
    expect(within(total).getAllByRole('cell')[1]?.textContent).toBe('200');
    expect(within(table).getAllByRole('link')[0]?.getAttribute('href')).toBe(
      `/reports/cards/portfolio/${PORTFOLIO_A}`,
    );
  });

  test('initiatives group under their portfolio; a sector shows spend as not available and its split', async () => {
    dashboardApi(fullInsights());
    renderApp({ path: DASHBOARD, session: session() });
    await waitFor(() => {
      expect(section('initiative')).not.toBeNull();
    });
    const initiative = present(section('initiative'));
    const group = initiative.querySelector('.insights__group th');
    expect(group?.getAttribute('scope')).toBe('rowgroup');
    expect(within(initiative).getAllByRole('link')[0]?.getAttribute('href')).toBe(
      `/reports/cards/initiative/${INITIATIVE_A}`,
    );
    const sector = present(section('sector'));
    expect(plain(sector.textContent)).toContain('Spend measured for 0 of 167');
    expect(
      within(sector).getAllByText(en('dashboards.insights.notAvailable')).length,
    ).toBeGreaterThan(0);
    expect(sector.querySelector('.insights__split li')).not.toBeNull();
  });

  test('stages, on-plan incl. not measured, reasons and the top ten with a way to the full list', async () => {
    dashboardApi(fullInsights());
    renderApp({ path: DASHBOARD, session: session() });
    await waitFor(() => {
      expect(section('reasons')).not.toBeNull();
    });
    expect(plain(section('stages')?.textContent)).toContain('71.9%');
    expect(section('onPlan')?.querySelector('[data-row="notMeasured"] dd')?.textContent).toBe('70');
    expect(section('reasons')?.querySelector('[data-row="total"] dd')?.textContent).toBe('12');
    const top = present(section('top'));
    expect(
      within(top).getByRole('link', { name: 'Coastal road upgrade' }).getAttribute('href'),
    ).toBe(`/projects/${PROJECT_ID}`);
    expect(
      within(top)
        .getByRole('link', { name: en('dashboards.insights.fullFollowUp') })
        .getAttribute('href'),
    ).toBe('/projects/follow-up');
    // Round 7, item 5: the entity completion donut joins the four charts.
    expect(document.querySelectorAll('.insights__chart')).toHaveLength(5);
    expect(describeViolations(await accessibilityViolations())).toBe('');
  });

  test('the filters reach the read', async () => {
    const api = dashboardApi(fullInsights());
    renderApp({ path: `${DASHBOARD}&regionItemId=r1&period=2026`, session: session() });
    await waitFor(() => {
      expect(api.requestsTo('GET', /^\/portfolio-insights$/).length).toBeGreaterThan(0);
    });
    const read = api.requestsTo('GET', /^\/portfolio-insights$/)[0];
    expect(read?.query.get('period')).toBe('2026');
  });
});

describe('the printable cards', () => {
  test('a group card: summary, every project, unmeasured not zero, print button', async () => {
    const api = mockApi();
    withProjectLookups(api);
    api.on('GET', /^\/portfolio-insights\/cards$/, { body: portfolioCard() });
    const print = vi.fn();
    vi.stubGlobal('print', print);
    renderApp({ path: `/reports/cards/portfolio/${PORTFOLIO_A}`, session: session() });

    await screen.findByRole('link', { name: 'Coastal road upgrade' });
    const read = api.requestsTo('GET', /^\/portfolio-insights\/cards$/)[0];
    expect(read?.query.get('dimension')).toBe('PORTFOLIO');
    expect(read?.query.get('itemId')).toBe(PORTFOLIO_A);
    expect(section('cardSummary')).not.toBeNull();
    const row = around(screen.getByRole('link', { name: 'Coastal road upgrade' }), 'tr');
    expect(within(row).getAllByText(en('dashboards.insights.notAvailable')).length).toBeGreaterThan(
      0,
    );
    expect(document.querySelector('.print-card')).not.toBeNull();
    await userEvent.click(
      screen.getByRole('button', { name: en('dashboards.insights.card.print') }),
    );
    expect(print).toHaveBeenCalledOnce();
    vi.unstubAllGlobals();
  });

  test('without the permission the card is not offered', async () => {
    const api = mockApi();
    renderApp({ path: `/reports/cards/portfolio/${PORTFOLIO_A}`, session: sessionFor(['R04']) });
    await screen.findByText(en('common.forbidden.title'));
    expect(api.requestsTo('GET', /^\/portfolio-insights\/cards$/)).toHaveLength(0);
  });

  test('a project card shows amounts only with FINANCIAL_VIEW_SENSITIVE', async () => {
    const api = mockApi();
    withProjectLookups(api);
    withProject(api, projectDetail({ id: PROJECT_ID, registrationBudgetSar: '1500000.00' }));
    const seen = renderApp({ path: `/projects/${PROJECT_ID}/card`, session: session() });
    await screen.findByRole('heading', { name: 'Coastal road upgrade' });
    expect(await screen.findByText(en('projects.fields.registrationBudget'))).toBeTruthy();
    seen.unmount();

    renderApp({ path: `/projects/${PROJECT_ID}/card`, session: entitySession() });
    await screen.findByRole('heading', { name: 'Coastal road upgrade' });
    expect(screen.queryByText(en('projects.fields.registrationBudget'))).toBeNull();
  });
});

describe('the Home views', () => {
  function homeApi(): MockApi {
    const api = mockApi();
    api
      .on('GET', /^\/dashboards$/, { body: page([]) })
      .on('GET', /^\/approval-tasks$/, { body: page([]) })
      .on('GET', /^\/approval-instances$/, { body: page([]) })
      .on('GET', /^\/notifications$/, { body: page([]) })
      .on('GET', /^\/notifications\/unread-count$/, { body: { unreadCount: 0 } })
      .on('GET', /^\/external-update-requests$/, { body: page([]) });
    return api;
  }

  const withFollowUp = (source: 'ENTITY_UPDATE' | 'MANUAL') =>
    projectDetail({
      id: PROJECT_ID,
      followUp: {
        currentObstacle: null,
        nextAction: null,
        nextActionOwner: null,
        nextActionDueDate: '2026-11-20',
        declaredPlannedPercent: null,
        declaredActualPercent: null,
        declaredPlannedSpendPercent: null,
        declaredActualSpendPercent: null,
        declaredAsOf: '2026-10-31',
        declaredSource: source,
        entityUpdateCompliant: null,
      },
    });

  test('a manager is shown their projects, by the data and no role', async () => {
    const api = homeApi();
    const person = sessionFor(['R06']);
    api.on('GET', /^\/projects$/, (request) => ({
      body: page(
        request.query.get('projectManagerUserId') === person.user.id
          ? [
              projectSummary({
                indicators: {
                  gapPoints: -20,
                  onPlan: 'STALLED',
                  daysRemaining: 12,
                  actionDue: 'OVERDUE',
                  followUpReasons: ['STALLED'],
                },
              }),
            ]
          : [],
      ),
    }));
    withProject(api, withFollowUp('MANUAL'));
    api.on('GET', /^\/my-update-requests$/, { body: myUpdatePage([myRequest()]) });
    api.on('POST', /mark-opened$/, { status: 204 });
    renderApp({ path: '/', session: person });

    const heading = await screen.findByRole('heading', { name: en('home.managed.title') });
    const view = around(heading, 'section');
    const row = around(
      await within(view).findByRole('link', { name: 'Coastal road upgrade' }),
      'tr',
    );
    expect(within(row).getByText(en('projects.onPlan.STALLED'))).toBeTruthy();
    expect(plain(row.textContent)).toContain('12 days');
    expect(row.textContent).toContain('2026-11-20');
    expect(within(row).getAllByRole('cell').at(-1)?.textContent).toBe('1');
  });

  test('someone managing nothing is shown no such view', async () => {
    const api = homeApi();
    api.on('GET', /^\/projects$/, { body: page([]) });
    renderApp({ path: '/', session: sessionFor(['R06']) });
    await screen.findByRole('heading', { name: en('home.work.title') });
    await waitFor(() => {
      expect(api.requestsTo('GET', /^\/projects$/).length).toBeGreaterThan(0);
    });
    expect(screen.queryByRole('heading', { name: en('home.managed.title') })).toBeNull();
  });

  test('an entity user sees the last update only when it was theirs, the next due, and opens the window', async () => {
    const api = homeApi();
    api.on('GET', /^\/projects$/, { body: page([projectSummary()]) });
    withProject(api, withFollowUp('ENTITY_UPDATE'));
    api.on('GET', /^\/my-update-requests$/, { body: myUpdatePage([myRequest()]) });
    renderApp({ path: '/', session: entitySession() });

    // The window opens by itself at this sign-in; close it to reach the Home's own button.
    await userEvent.click(
      await screen.findByRole('button', { name: en('externalParticipation.monthly.window.close') }),
    );
    const heading = await screen.findByRole('heading', { name: en('home.entity.title') });
    const view = around(heading, 'section');
    const row = around(
      await within(view).findByRole('link', { name: 'Coastal road upgrade' }),
      'tr',
    );
    expect(row.textContent).toContain('2026-10-31');
    expect(row.textContent).toContain('2026-11-10');
    await userEvent.click(within(view).getByRole('button', { name: /Open the monthly update/ }));
    expect(await screen.findByRole('dialog', { name: /update$/ })).toBeTruthy();
  });

  test("a manual follow-up is not the entity's update", async () => {
    const api = homeApi();
    api.on('GET', /^\/projects$/, { body: page([projectSummary()]) });
    withProject(api, withFollowUp('MANUAL'));
    renderApp({ path: '/', session: entitySession() });
    const heading = await screen.findByRole('heading', { name: en('home.entity.title') });
    const view = around(heading, 'section');
    const row = around(
      await within(view).findByRole('link', { name: 'Coastal road upgrade' }),
      'tr',
    );
    expect(row.textContent).not.toContain('2026-10-31');
  });
});

describe('Projects by location', () => {
  const CITY = '00000000-0000-4000-8000-00000000c001';
  const REGION = '00000000-0000-4000-8000-00000000c002';

  function mapApi(latitude: number | null): MockApi {
    const api = mockApi();
    withProjectLookups(api);
    withDashboards(api, {
      views: {
        PORTFOLIO: {
          ...dashboardView('PORTFOLIO'),
          locations: [{ regionItemId: REGION, cityItemId: CITY, projectCount: 7 }],
          places: [
            { id: REGION, label: { ar: 'الحاضرة', en: 'Hadira' }, parentItemId: null },
            {
              id: CITY,
              label: { ar: 'الهفوف', en: 'Hofuf' },
              parentItemId: REGION,
              latitude,
              longitude: latitude === null ? null : 49.587,
            },
          ],
        },
      },
    });
    api.on('GET', /^\/portfolio-insights$/, { body: fullInsights() });
    api.on('GET', /^\/projects$/, { body: page([projectSummary()]) });
    return api;
  }

  test('pressing a city opens its figures and projects, read for that city', async () => {
    const user = userEvent.setup();
    const api = mapApi(25.3647);
    renderApp({ path: DASHBOARD, session: sessionFor(['R02']) });

    expect(
      await screen.findByRole('region', { name: en('dashboards.locations.mapLabel') }),
    ).toBeTruthy();
    await user.click(await screen.findByRole('button', { name: 'Hofuf' }));

    await waitFor(() => {
      expect(
        api
          .requestsTo('GET', /^\/portfolio-insights$/)
          .some((r) => r.query.get('cityItemId') === CITY),
      ).toBe(true);
    });
    expect(
      api.requestsTo('GET', /^\/projects$/).some((r) => r.query.get('cityItemId') === CITY),
    ).toBe(true);
    expect(await screen.findByRole('heading', { name: 'Hofuf' })).toBeTruthy();
    await user.click(screen.getByRole('button', { name: en('dashboards.locations.close') }));
    expect(screen.queryByRole('heading', { name: 'Hofuf' })).toBeNull();
  });

  test('with no coordinates there is no map, and the list still shows', async () => {
    mapApi(null);
    renderApp({ path: DASHBOARD, session: sessionFor(['R02']) });

    expect(await screen.findByText('Hofuf')).toBeTruthy();
    expect(screen.queryByRole('region', { name: en('dashboards.locations.mapLabel') })).toBeNull();
  });
});

describe('the entity completion donut (round 7, item 5)', () => {
  const entity = (n: number, isActive = true) => ({
    id: `00000000-0000-4000-8000-00000000000${String(n)}`,
    code: `E${String(n)}`,
    name: { ar: `جهة ${String(n)}`, en: `Entity ${String(n)}` },
    isActive,
  });
  const row = (n: number, issued: number, accepted: number) => ({
    externalEntityId: entity(n).id,
    issued,
    submitted: accepted,
    accepted,
    overdue: 0,
    completionPercent: (accepted * 100) / issued,
  });

  function completionApi(): MockApi {
    return dashboardApi(fullInsights())
      .on('GET', /^\/entity-completion-rates$/, {
        body: {
          items: [row(4, 10, 2), row(3, 10, 5), row(2, 10, 8), row(5, 10, 9), row(1, 10, 10)],
          page: 1,
          pageSize: 100,
          totalCount: 5,
          averagePercent: 68,
        },
      })
      .on('GET', /^\/organization-units$/, {
        body: {
          departments: [],
          externalEntities: [1, 2, 3, 4, 5, 6].map((n) => entity(n)).concat(entity(7, false)),
        },
      });
  }

  const card = async () =>
    around(
      await screen.findByRole('heading', { name: en('dashboards.insights.completion'), level: 3 }),
      '.insights__chart',
    );

  test('shows all entities together in the centre and how many entities are in each band', async () => {
    completionApi();
    renderApp({ path: DASHBOARD, session: session() });

    const chart = await card();
    // (10 + 8 + 5 + 2 + 9) accepted of 50 issued: accepted over issued, not the mean of the rates.
    await waitFor(() => {
      expect(plain(chart.querySelector('.donut__value')?.textContent)).toBe('68%');
    });
    const legend = within(chart)
      .getAllByRole('button')
      .map((button) => plain(button.textContent));
    expect(legend).toEqual([
      `${en('dashboards.insights.completionBands.from90')} (2)`,
      `${en('dashboards.insights.completionBands.from70')} (1)`,
      `${en('dashboards.insights.completionBands.from50')} (1)`,
      `${en('dashboards.insights.completionBands.below50')} (1)`,
      `${en('dashboards.insights.completionBands.noRequests')} (1)`,
    ]);
  });

  test('choosing a band lists its entities, lowest rate first; an entity with no requests is listed by name', async () => {
    completionApi();
    const user = userEvent.setup();
    renderApp({ path: DASHBOARD, session: session() });
    const chart = await card();

    await user.click(
      await within(chart).findByRole('button', {
        name: new RegExp(en('dashboards.insights.completionBands.from90')),
      }),
    );
    const listed = within(chart)
      .getAllByRole('listitem')
      .filter((item) => item.closest('.completion-band__list') !== null)
      .map((item) => plain(item.textContent));
    expect(listed).toEqual(['Entity 590%', 'Entity 1100%']);

    await user.click(
      within(chart).getByRole('button', {
        name: new RegExp(en('dashboards.insights.completionBands.noRequests')),
      }),
    );
    expect(within(chart).getByText('Entity 6')).toBeTruthy();
    expect(within(chart).queryByText('Entity 7')).toBeNull();
  });

  test('covers the period chosen on the table, which stays as it was', async () => {
    const api = completionApi();
    const user = userEvent.setup();
    renderApp({ path: DASHBOARD, session: session() });
    await card();

    await user.selectOptions(
      await screen.findByLabelText(en('externalParticipation.completion.period')),
      'ThisYear',
    );

    await waitFor(() => {
      const periods = api
        .requestsTo('GET', /^\/entity-completion-rates$/)
        .map((request) => request.query.get('period'));
      expect(periods.filter((period) => period === 'ThisYear').length).toBe(2);
    });
    expect(
      screen.getByRole('table', { name: en('externalParticipation.completion.title') }),
    ).toBeTruthy();
  });
});
