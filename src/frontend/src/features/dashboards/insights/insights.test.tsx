import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test, vi } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import { catalogueFor, dashboardView, withDashboards } from '@/test/dashboardFixtures.ts';
import { sessionFor } from '@/test/identityAccessFixtures.ts';
import { type MockApi, mockApi, page } from '@/test/mockApi.ts';
import {
  fullInsights,
  INITIATIVE_A,
  NO_SECTIONS,
  PORTFOLIO_A,
  portfolioCard,
  standardDesignOf,
} from '@/test/portfolioInsightFixtures.ts';
import { type PortfolioInsights } from '@/features/dashboards/api/insightTypes.ts';
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
  // The headline cards and the portfolio charts are the designed visuals (17 Nov 2026), calculated from the same figures.
  api.on('GET', /^\/portfolio-insights\/design$/, {
    body: standardDesignOf(insights as PortfolioInsights),
  });
  return api;
}

const section = (id: string) => document.querySelector<HTMLElement>(`[data-section="${id}"]`);

/** The headline cards of the design (17 Nov 2026), gathered in one element to query. */
function headlineCards(): HTMLElement {
  const cards = document.createElement('div');
  document.querySelectorAll('[data-visual="CARD"]').forEach((card) => {
    cards.append(card.cloneNode(true));
  });
  return cards;
}

const cardsDrawn = () => document.querySelectorAll('[data-visual="CARD"]').length;

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

/** A link as its path and parameters, in any order. */
function linkOf(href: string | null | undefined) {
  const [path, query = ''] = (href ?? '').split('?');
  return { path, params: Object.fromEntries(new URLSearchParams(query)) };
}

describe('the sections', () => {
  test('each section shows only when answered, and none of a null one', async () => {
    dashboardApi({ ...NO_SECTIONS, stages: fullInsights().stages, onPlan: fullInsights().onPlan });
    renderApp({ path: DASHBOARD, session: session() });
    await screen.findByRole('heading', { level: 2, name: en('dashboards.insights.byStatus') });
    expect(cardsDrawn()).toBe(0);
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
    // No card, chart or insight section: only what the dashboard's own view draws (its map and widgets) may show.
    await waitFor(() => {
      expect(document.querySelector('[data-section="designed"]')).not.toBeNull();
    });
    expect(
      document.querySelectorAll('[data-section="designed"] .visual:not(.visual--section)'),
    ).toHaveLength(0);
    expect(
      [...document.querySelectorAll<HTMLElement>('[data-section-code]')].map(
        (element) => element.dataset.sectionCode,
      ),
      // The entity completion donut and table answer under their own permission, whatever the insights do.
    ).toEqual(['ENTITY_COMPLETION', 'DASHBOARD_WIDGETS', 'ENTITY_COMPLETION_TABLE']);
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
    expect(cardsDrawn()).toBe(0);
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
      expect(cardsDrawn()).toBe(5);
    });
    const headline = headlineCards();
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
      expect(cardsDrawn()).toBe(6);
    });
    const headline = headlineCards();
    // Review round 8, point 11: a card's amount is compact, with the full figure on hover.
    expect(plain(headline.textContent)).toContain('1.5 million SAR');
    expect(plain(headline.querySelector('[title*="SAR"]')?.getAttribute('title'))).toBe(
      '1,500,000 SAR',
    );
    expect(plain(headline.textContent)).toContain('Value entered for 167 of 167');
    expect(plain(headline.textContent)).toContain('Overdue actions: 4');
  });

  test('a table has its total row from `total`, not from adding the rows; a name opens its projects, then its card', async () => {
    dashboardApi(fullInsights());
    renderApp({ path: DASHBOARD, session: session() });
    await waitFor(() => {
      expect(section('portfolio')).not.toBeNull();
    });
    const table = present(section('portfolio'));
    const total = present(table.querySelector<HTMLElement>('tr[data-row="total"]'));
    expect(within(total).getByRole('rowheader').textContent).toBe(en('dashboards.insights.total'));
    expect(within(total).getAllByRole('cell')[1]?.textContent).toBe('200');
    const links = within(table).getAllByRole('link');
    expect(linkOf(links[0]?.getAttribute('href'))).toEqual({
      path: '/projects',
      params: { registered: 'true', strategicPortfolioItemId: PORTFOLIO_A },
    });
    expect(links[1]?.getAttribute('href')).toBe(`/reports/cards/portfolio/${PORTFOLIO_A}`);
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
    const links = within(initiative).getAllByRole('link');
    expect(linkOf(links[0]?.getAttribute('href'))).toEqual({
      path: '/projects',
      params: { registered: 'true', strategicInitiativeItemId: INITIATIVE_A },
    });
    expect(links[1]?.getAttribute('href')).toBe(`/reports/cards/initiative/${INITIATIVE_A}`);
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
    // The charts are the design's (17 and 18 Nov 2026): by portfolio, status and stage as bars, planned and actual as columns, the on-plan
    // indicator as a donut, and round 7 item 5's entity completion donut as a section.
    // By portfolio and by status; these insights report no current stage, so that chart is not drawn.
    expect(document.querySelectorAll('.visual[data-visual="BAR"]')).toHaveLength(2);
    expect(document.querySelectorAll('.visual[data-visual="COLUMN"]')).toHaveLength(1);
    expect(document.querySelectorAll('.visual[data-visual="DONUT"]')).toHaveLength(1);
    expect(
      document.querySelectorAll('[data-section-code="ENTITY_COMPLETION"] .insights__chart'),
    ).toHaveLength(1);
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
  test("review round 8, point 01: every count opens its records with the dashboard's filters", async () => {
    dashboardApi(fullInsights());
    renderApp({ path: `${DASHBOARD}&period=2026`, session: session() });
    await waitFor(() => {
      expect(section('reasons')).not.toBeNull();
    });
    const link = (element: Element | null | undefined) => linkOf(element?.getAttribute('href'));
    // The headline cards are designed visuals, read apart from the insights.
    await screen.findByRole('link', { name: en('dashboards.insights.registered') });
    expect(link(screen.getByRole('link', { name: en('dashboards.insights.registered') }))).toEqual({
      path: '/projects',
      params: { period: '2026', registered: 'true' },
    });
    expect(
      link(screen.getByRole('link', { name: en('dashboards.insights.needingFollowUp') })),
    ).toEqual({ path: '/projects/follow-up', params: { period: '2026' } });
    expect(link(section('onPlan')?.querySelector('[data-row="notMeasured"] dd a'))).toEqual({
      path: '/projects',
      params: { period: '2026', registered: 'true', onPlan: 'NOT_MEASURED' },
    });
    expect(link(section('reasons')?.querySelector('[data-row="stalled"] dd a'))).toEqual({
      path: '/projects/follow-up',
      params: { period: '2026', mainReason: 'STALLED' },
    });
    expect(link(document.querySelector('.bars__link'))).toEqual({
      path: '/projects',
      params: { period: '2026', registered: 'true', strategicPortfolioItemId: PORTFOLIO_A },
    });
    expect(describeViolations(await accessibilityViolations())).toBe('');
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
      await screen.findByRole('heading', {
        name: new RegExp(`^${en('dashboards.insights.completion')} · `),
        level: 3,
      }),
      '.insights__chart',
    );

  test('the centre is the average of the entities with requests; the ring and its legend are those entities, the rest are counted under it', async () => {
    completionApi();
    renderApp({ path: DASHBOARD, session: session() });

    const chart = await card();
    // The API's simple average: each entity with requests counts once.
    await waitFor(() => {
      expect(plain(chart.querySelector('.donut__value')?.textContent)).toBe('68%');
    });
    expect(plain(chart.querySelector('.donut__label')?.textContent)).toBe(
      en('dashboards.insights.completionAverage'),
    );
    expect(chart.querySelectorAll('.donut__segment')).toHaveLength(5);
    const legend = [...chart.querySelectorAll('.completion-donut__legend li')].map((item) =>
      plain(item.textContent),
    );
    expect(legend).toEqual([
      'Entity 420%',
      'Entity 350%',
      'Entity 280%',
      'Entity 590%',
      'Entity 1100%',
    ]);
    // Entity 6 had no request; Entity 7 is inactive and not counted.
    expect(chart.textContent).toContain(
      en('dashboards.insights.completionWithRequests', { count: 5 }),
    );
    expect(chart.textContent).toContain(
      en('dashboards.insights.completionNoRequests', { count: 1 }),
    );
  });

  test('each entity in the legend opens its request monitor, its counts in its tooltip', async () => {
    completionApi();
    renderApp({ path: DASHBOARD, session: session() });
    const chart = await card();

    const link = await within(chart).findByRole('link', { name: 'Entity 4' });
    expect(link.getAttribute('href')).toBe(
      `/external-requests/monitor?externalEntityId=${entity(4).id}`,
    );
    expect(link.getAttribute('title')).toBe(
      en('dashboards.insights.completionCounts', { accepted: 2, issued: 10 }),
    );
  });

  test('"see more" opens the whole entity table; a band in the address still filters it', async () => {
    completionApi();
    renderApp({ path: `${DASHBOARD}&completionBand=from90`, session: session() });
    const chart = await card();

    const more = await within(chart).findByRole('link', {
      name: en('dashboards.insights.completionSeeMore'),
    });
    expect(more.getAttribute('href')).not.toContain('completionBand');
    expect(more.getAttribute('href')).toMatch(/#entity-completion$/);

    const table = await screen.findByRole('table', {
      name: en('externalParticipation.completion.title'),
    });
    await waitFor(() => {
      expect(
        within(table)
          .getAllByRole('link')
          .map((link) => link.textContent),
      ).toEqual(['Entity 5', 'Entity 1']);
    });
    expect(
      screen.getByRole('link', { name: en('externalParticipation.completion.showAll') }),
    ).toBeTruthy();
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

describe('review round 8, point 14: one dashboard for the Executive', () => {
  test('the Executive opens on five headline figures, then the top risks and the decisions waiting, before any chart or table', async () => {
    const api = dashboardApi(fullInsights());
    withDashboards(api, {
      catalogue: catalogueFor(['R07']),
      views: { PORTFOLIO: dashboardView('PORTFOLIO') },
    });
    api.on('GET', /^\/approval-tasks$/, { body: page([]) });
    // withDashboards above registers the design's defaults; the insights are this test's.
    api.on('GET', /^\/portfolio-insights$/, { body: fullInsights() });
    renderApp({ path: DASHBOARD, session: sessionFor(['R07']) });

    const headline = present(await waitFor(() => present(section('executive-headline'))));
    const cards = within(headline).getAllByRole('listitem');
    expect(cards).toHaveLength(5);
    expect(
      within(headline)
        .getByRole('link', { name: en('dashboards.executive.stalled') })
        .getAttribute('href'),
    ).toContain('onPlan=STALLED');
    expect(
      within(headline)
        .getByRole('link', { name: en('dashboards.executive.criticalRisks') })
        .getAttribute('href'),
    ).toBe('/risks/critical');
    expect(
      within(headline)
        .getByRole('link', { name: en('dashboards.executive.waitingDecision') })
        .getAttribute('href'),
    ).toBe('/approvals/inbox');

    const attention = present(section('executive-attention'));
    const firstTable = document.querySelector('main table');
    const firstChart = screen.getByRole('heading', {
      level: 2,
      name: en('dashboards.insights.byStatus'),
    });
    // The headline, then the attention, then the charts and the tables, in the page's order.
    expect(
      headline.compareDocumentPosition(attention) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
    expect(
      attention.compareDocumentPosition(firstChart) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
    if (firstTable !== null) {
      expect(
        attention.compareDocumentPosition(firstTable) & Node.DOCUMENT_POSITION_FOLLOWING,
      ).toBeTruthy();
    }
    // One dashboard: the page and the menu call it "the Executive dashboard", and the menu has no second dashboard.
    expect(
      screen.getByRole('heading', { level: 1, name: en('dashboards.landing.R07') }),
    ).toBeTruthy();
    expect(screen.queryByRole('link', { name: en('common.menu.portfolioDashboard') })).toBeNull();
    expect(screen.queryByRole('link', { name: en('common.menu.dashboards') })).toBeNull();
  });
});
