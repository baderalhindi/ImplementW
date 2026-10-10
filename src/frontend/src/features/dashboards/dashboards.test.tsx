import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { type Session } from '@/features/identity-access/session/sessionApi.ts';
import { translate } from '@/shared/i18n/i18n.ts';
import {
  catalogueFor,
  DEPARTMENT_A,
  DEPARTMENT_B,
  dashboardView,
  unknownWidget,
  widget,
  withDashboards,
  type DashboardState,
} from '@/test/dashboardFixtures.ts';
import { sessionFor } from '@/test/identityAccessFixtures.ts';
import { type MockApi, mockApi, page, problem } from '@/test/mockApi.ts';
import {
  entitySession,
  projectDetail,
  PROJECT_ID,
  projectSummary,
  withProject,
  withProjectLookups,
} from '@/test/projectFixtures.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';

import { type DashboardWidgetResult } from './api/types.ts';

// TASK-070, the FG-01 dashboards UI over TASK-069's API. Acceptance criteria: (1) DSH-009 has no standalone route and
// renders only as a composed section of SCR-040; (2) every widget visibly states its semantic state and any stale or
// missing condition, never an unlabelled number; (3) each role's landing matches Blueprint §20.2. The workbook's
// checks: direct navigation to DSH-009 finds nothing; each role signs in to its §20.2 landing.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);
const ar = (key: string, params?: Record<string, string | number>) => translate('ar', key, params);

const dateTime = (iso: string) =>
  new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium', timeStyle: 'short' }).format(
    new Date(iso),
  );

const OVERVIEW_PATH = `/projects/${PROJECT_ID}`;

/** The element, or a failed test saying what was missing. */
function present<T>(value: T | null | undefined, what: string): T {
  if (value === null || value === undefined) {
    throw new Error(`No ${what}.`);
  }
  return value;
}

function homeAs(session: Session, state: DashboardState = {}): MockApi {
  const api = withDashboards(mockApi(), state);
  api.on('GET', /^\/departments$/, { body: page([]) });
  renderApp({ path: '/', session });
  return api;
}

function overviewAs(session: Session, state: DashboardState = {}): MockApi {
  const api = withDashboards(withProjectLookups(mockApi()), state);
  withProject(api, projectDetail({ status: 'ACTIVE', activatedAt: '2026-08-01T00:00:00Z' }));
  renderApp({ path: OVERVIEW_PATH, session });
  return api;
}

const widgetRegion = (name: string) => screen.findByRole('region', { name });

/** Every widget a Project Dashboard can show, one per state the API can answer. */
const PROJECT_WIDGETS: DashboardWidgetResult[] = [
  widget({ code: 'CURRENT_HEALTH', layoutSpan: 3 }),
  widget({
    code: 'PUBLISHED_HEALTH',
    title: { en: 'Published Overall Project Health', ar: 'الصحة العامة المنشورة' },
    projectionCode: 'PROGRESS.PUBLISHED_PROGRESS_SNAPSHOT',
    layoutColumn: 4,
    layoutSpan: 3,
    projection: {
      semanticState: 'PUBLISHED_OFFICIAL',
      freshness: 'STALE',
      asOf: '2026-09-20T10:00:00Z',
      coverage: 'COMPLETE',
    },
    data: {
      state: 'GREEN',
      figures: [
        { measure: 'ACTUAL_PERCENT', value: '40.5', unit: 'PERCENT', isMasked: false },
        { measure: 'ACTUAL_PERCENT_OVERRIDDEN', value: 'true', unit: 'BOOLEAN', isMasked: false },
      ],
      distribution: [],
      series: [],
    },
  }),
  widget({
    code: 'PROGRESS_TREND',
    title: { en: 'Published progress trend', ar: 'اتجاه التقدم المنشور' },
    widgetType: 'LINE_TREND',
    projectionCode: 'PROGRESS.PUBLISHED_PROGRESS_HISTORY',
    layoutRow: 3,
    layoutSpan: 12,
    projection: {
      semanticState: 'HISTORICAL_SNAPSHOT',
      freshness: 'FRESH',
      asOf: '2026-09-20T10:00:00Z',
      coverage: 'COMPLETE',
    },
    data: {
      state: null,
      figures: [],
      distribution: [],
      series: [
        {
          asOf: '2026-08-20T10:00:00Z',
          periodStart: '2026-08-01',
          periodEnd: '2026-08-31',
          state: 'AMBER',
          figures: [
            { measure: 'ACTUAL_PERCENT', value: '20', unit: 'PERCENT', isMasked: false },
            { measure: 'PLANNED_PERCENT', value: '25', unit: 'PERCENT', isMasked: false },
          ],
        },
        {
          asOf: '2026-09-20T10:00:00Z',
          periodStart: '2026-09-01',
          periodEnd: '2026-09-30',
          state: 'GREEN',
          figures: [
            { measure: 'ACTUAL_PERCENT', value: '40.5', unit: 'PERCENT', isMasked: false },
            { measure: 'PLANNED_PERCENT', value: null, unit: 'PERCENT', isMasked: false },
          ],
        },
      ],
    },
  }),
  unknownWidget('MISSING', {
    code: 'SCHEDULE_HEALTH',
    title: { en: 'Current schedule health', ar: 'صحة الجدول الزمني الحالية' },
    projectionCode: 'SCHEDULE.SCHEDULE_HEALTH_STATUS',
    sourceDomain: 'WF-03',
    layoutRow: 2,
  }),
  unknownWidget('RESTRICTED', {
    code: 'RISK_EXPOSURE',
    title: { en: 'Open risks by rating', ar: 'المخاطر المفتوحة حسب التصنيف' },
    projectionCode: 'RISK.RISK_EXPOSURE',
    sourceDomain: 'WF-06',
    widgetType: 'STATUS_DISTRIBUTION',
    layoutRow: 4,
  }),
  unknownWidget('SOURCE_UNAVAILABLE', {
    code: 'KPI_CONDITION',
    title: { en: 'KPI condition', ar: 'حالة مؤشرات الأداء' },
    projectionCode: 'FINANCIAL_KPI.KPI_CONDITION',
    sourceDomain: 'WF-14',
    projection: {
      semanticState: 'PUBLISHED_OFFICIAL',
      freshness: 'UNKNOWN',
      asOf: null,
      coverage: 'NONE',
    },
    layoutRow: 4,
    layoutColumn: 7,
  }),
  unknownWidget('NOT_APPLICABLE', {
    code: 'REPORTING_COMPLETENESS',
    title: { en: 'Reporting completeness', ar: 'اكتمال التقارير' },
    projectionCode: 'PROGRESS.REPORTING_COMPLETENESS',
    layoutRow: 2,
    layoutColumn: 9,
  }),
  widget({
    code: 'CURRENT_FINANCIALS',
    title: { en: 'Current financial position', ar: 'الوضع المالي الحالي' },
    projectionCode: 'FINANCIAL_KPI.FINANCIAL_POSITION',
    sourceDomain: 'WF-14',
    layoutRow: 5,
    data: {
      state: 'AMBER',
      figures: [
        { measure: 'APPROVED_BUDGET', value: '1250000.00', unit: 'SAR', isMasked: false },
        { measure: 'ACTUAL_EXPENDITURE_TO_DATE', value: null, unit: 'SAR', isMasked: true },
        { measure: 'FORECAST_AT_COMPLETION', value: null, unit: 'SAR', isMasked: false },
      ],
      distribution: [],
      series: [],
    },
    maskedFields: ['actualExpenditureToDateSar'],
    drillTargetScreenId: 'SCR-049',
  }),
];

const projectState = (widgets = PROJECT_WIDGETS): DashboardState => ({
  catalogue: catalogueFor(['R04']),
  views: { PROJECT: dashboardView('PROJECT', { widgets }) },
});

const pmSession = () => sessionFor(['R04']);

describe('acceptance criterion 1: DSH-009 has no standalone route', () => {
  test.each([
    '/dashboards',
    '/dashboards/PROJECT',
    `/dashboards/PROJECT?projectId=${PROJECT_ID}`,
    '/dashboards/project',
    `/projects/${PROJECT_ID}/dashboard`,
    '/project-dashboard',
  ])('navigating directly to %s finds no page and reads no dashboard', async (path) => {
    const api = withDashboards(mockApi());
    renderApp({ path, session: sessionFor(['R02']) });

    expect(
      await screen.findByRole('heading', { level: 1, name: en('common.notFound.title') }),
    ).toBeTruthy();
    expect(api.requestsTo('GET', /^\/dashboards/)).toHaveLength(0);
  });

  test('the Home never renders the Project Dashboard, whatever the address asks for', async () => {
    const api = withDashboards(mockApi());
    api.on('GET', /^\/departments$/, { body: page([]) });
    renderApp({ path: '/?dashboard=PROJECT', session: sessionFor(['R02']) });

    expect(
      await screen.findByRole('heading', { level: 1, name: en('dashboards.landing.R02') }),
    ).toBeTruthy();
    await waitFor(() => {
      expect(api.requestsTo('GET', /^\/dashboards\/PORTFOLIO$/).length).toBeGreaterThan(0);
    });
    expect(api.requestsTo('GET', /^\/dashboards\/PROJECT$/)).toHaveLength(0);
  });

  test('it renders as a section of SCR-040 Project Overview, bound to that project', async () => {
    const api = overviewAs(pmSession(), projectState());

    const panel = await waitFor(() => {
      return present(document.querySelector<HTMLElement>('.tabs__panel'), 'workspace panel');
    });
    const section = await within(panel).findByRole('region', { name: 'Project Dashboard' });
    expect(screen.getByRole('heading', { level: 1, name: 'Coastal road upgrade' })).toBeTruthy();
    expect(
      within(section).getByRole('heading', { level: 2, name: 'Project Dashboard' }),
    ).toBeTruthy();
    expect(
      within(section).getByRole('heading', { level: 3, name: 'Current Overall Project Health' }),
    ).toBeTruthy();

    const [read] = api.requestsTo('GET', /^\/dashboards\/PROJECT$/);
    expect(read?.query.get('projectId')).toBe(PROJECT_ID);
    expect(read?.query.has('departmentId')).toBe(false);
  });

  test('a Project Dashboard the API does not open for the person says so, and discloses nothing', async () => {
    overviewAs(pmSession(), { views: { PROJECT: problem(404, 'NOT_FOUND') } });

    expect(await screen.findByText(en('dashboards.project.unavailable'))).toBeTruthy();
    expect(screen.queryByRole('alert')).toBeNull();
  });
});

describe('acceptance criterion 2: every widget states its semantic state and its condition', () => {
  test('each widget carries its semantic state in words, and its as-of and source', async () => {
    overviewAs(pmSession(), projectState());

    for (const expected of PROJECT_WIDGETS) {
      const region = await widgetRegion(expected.title.en);
      expect(region.getAttribute('data-semantic-state')).toBe(expected.projection.semanticState);
      const badge = within(region).getByText(
        en(`dashboards.semanticState.${expected.projection.semanticState}`),
      );
      expect(badge.getAttribute('data-semantic-state')).toBe(expected.projection.semanticState);
      expect(
        within(region).getByText(
          expected.projection.asOf === null
            ? en('dashboards.asOf.none')
            : en('dashboards.asOf.at', { date: dateTime(expected.projection.asOf) }),
        ),
      ).toBeTruthy();
      expect(
        within(region).getByText(
          en('dashboards.source', {
            source: expected.sourceDomain,
            version: expected.projectionVersion,
          }),
        ),
      ).toBeTruthy();
    }
    // Live, published and historical each have their own badge style.
    expect(
      within(await widgetRegion('Current Overall Project Health')).getByText(
        en('dashboards.semanticState.CURRENT_LIVE'),
      ).className,
    ).toBe('badge badge--live');
    expect(
      within(await widgetRegion('Published Overall Project Health')).getByText(
        en('dashboards.semanticState.PUBLISHED_OFFICIAL'),
      ).className,
    ).toBe('badge badge--official');
    expect(
      within(await widgetRegion('Published progress trend')).getByText(
        en('dashboards.semanticState.HISTORICAL_SNAPSHOT'),
      ).className,
    ).toBe('badge badge--historical');
  });

  test('a stale value keeps its value, flagged Stale with its own as-of, never shown as current', async () => {
    overviewAs(pmSession(), projectState());

    const region = await widgetRegion('Published Overall Project Health');
    expect(region.getAttribute('data-freshness')).toBe('STALE');
    expect(region.className).toBe('widget widget--stale');
    const flag = within(region).getByText(en('dashboards.freshness.STALE'));
    expect(flag.closest('[data-value-state]')?.getAttribute('data-value-state')).toBe('STALE');
    expect(within(region).getByText(en('dashboards.freshness.staleValue'))).toBeTruthy();
    expect(within(region).getByText(en('progress.health.GREEN'))).toBeTruthy();
    expect(within(region).getByText('40.5%')).toBeTruthy();
    // ADR-009: the override is marked beside the figure, not listed as a figure of its own.
    expect(within(region).getByText(en('progress.figures.overridden'))).toBeTruthy();
    expect(
      within(region).queryByText(en('dashboards.measures.ACTUAL_PERCENT_OVERRIDDEN')),
    ).toBeNull();
  });

  test.each([
    ['Current schedule health', 'MISSING'],
    ['Open risks by rating', 'RESTRICTED'],
    ['KPI condition', 'SOURCE_UNAVAILABLE'],
    ['Reporting completeness', 'NOT_APPLICABLE'],
  ])('%s with no data reads %s in words, with no number and no colour', async (title, reason) => {
    overviewAs(pmSession(), projectState());

    const region = await widgetRegion(title);
    expect(region.getAttribute('data-freshness')).toBe('UNKNOWN');
    const flag = region.querySelector('[data-value-state]');
    expect(flag?.getAttribute('data-value-state')).toBe(reason);
    expect(flag?.textContent).toBe(en(`dashboards.unknown.${reason}.label`));
    expect(within(region).getByText(en(`dashboards.unknown.${reason}.explanation`))).toBeTruthy();
    // Nothing in the widget's body is a figure or a coloured status: no 0, no badge but the semantic one.
    expect(region.querySelector('.figure')).toBeNull();
    expect(region.querySelectorAll('.badge')).toHaveLength(1);
    expect(region.querySelector('.widget__unknown')?.textContent).not.toMatch(/\d/);
  });

  test('a figure withheld from the audience reads Restricted, and an absent one No data, never 0', async () => {
    overviewAs(pmSession(), projectState());

    const region = await widgetRegion('Current financial position');
    const figure = (measure: string) => {
      const term = within(region).getByText(en(`dashboards.measures.${measure}`));
      return present(term.parentElement?.querySelector('dd'), `figure ${measure}`);
    };
    expect(figure('APPROVED_BUDGET').textContent).toBe(
      en('dashboards.units.sar', { amount: '1,250,000.00' }),
    );
    expect(
      figure('ACTUAL_EXPENDITURE_TO_DATE')
        .querySelector('[data-value-state]')
        ?.getAttribute('data-value-state'),
    ).toBe('MASKED');
    expect(figure('ACTUAL_EXPENDITURE_TO_DATE').textContent).toBe(
      en('dashboards.figure.restricted'),
    );
    expect(figure('FORECAST_AT_COMPLETION').textContent).toBe(en('dashboards.figure.none'));
    expect(within(region).getByText(en('financialKpi.financialStatus.AMBER'))).toBeTruthy();
  });

  test('a trend draws the published history with a table of the same values; a gap is No data', async () => {
    overviewAs(pmSession(), projectState());

    const region = await widgetRegion('Published progress trend');
    expect(
      within(region).getByRole('img', {
        name: en('dashboards.trend.chartLabel', { title: 'Published progress trend', count: 2 }),
      }),
    ).toBeTruthy();
    const rows = within(within(region).getByRole('table')).getAllByRole('row');
    expect(rows).toHaveLength(3);
    const last = within(present(rows[2], 'last row'));
    expect(
      last.getByText(en('dashboards.trend.range', { start: '2026-09-01', end: '2026-09-30' })),
    ).toBeTruthy();
    expect(last.getByText('40.5%')).toBeTruthy();
    expect(last.getByText(en('dashboards.figure.none'))).toBeTruthy();
    // The planned line breaks at the gap: one segment is never drawn to a missing point.
    expect(region.querySelectorAll('path.trend__line--PLANNED_PERCENT')).toHaveLength(0);
    expect(region.querySelectorAll('circle.trend__point--PLANNED_PERCENT')).toHaveLength(1);
  });

  test('a population says how much it counted and what it left out, and a stale part makes it stale', async () => {
    homeAs(sessionFor(['R02']), {
      views: {
        PORTFOLIO: dashboardView('PORTFOLIO', {
          widgets: [
            widget({
              code: 'PUBLISHED_HEALTH',
              title: { en: 'Published Overall Project Health', ar: 'الصحة العامة المنشورة' },
              widgetType: 'STATUS_DISTRIBUTION',
              projectionCode: 'PROGRESS.PUBLISHED_PROGRESS_SNAPSHOT',
              projection: {
                semanticState: 'PUBLISHED_OFFICIAL',
                freshness: 'STALE',
                asOf: '2026-09-20T10:00:00Z',
                coverage: 'PARTIAL',
              },
              data: {
                state: null,
                figures: [],
                distribution: [
                  { key: 'GREEN', label: null, count: 1 },
                  { key: 'AMBER', label: null, count: 1 },
                ],
                series: [],
              },
              coverageDetail: {
                eligibleCount: 3,
                includedCount: 2,
                excludedCount: 1,
                staleCount: 1,
                exclusions: [{ reason: 'MISSING', count: 1 }],
              },
            }),
          ],
        }),
      },
    });

    const region = await widgetRegion('Published Overall Project Health');
    expect(region.querySelector('[data-coverage]')?.textContent).toBe(
      [
        en('dashboards.coverage.counted', { included: 2, eligible: 3 }),
        en('dashboards.coverage.leftOut', {
          reasons: en('dashboards.coverage.excluded.MISSING', { count: 1 }),
        }),
        en('dashboards.coverage.stale', { count: 1 }),
      ].join(' · '),
    );
    expect(within(region).getByText(en('dashboards.freshness.staleAggregate'))).toBeTruthy();
    const table = within(region).getByRole('table', { name: 'Published Overall Project Health' });
    expect(
      within(table).getByRole('columnheader', { name: en('dashboards.counted.projects') }),
    ).toBeTruthy();
    expect(table.querySelector('[data-bucket="GREEN"]')?.textContent).toBe(
      `${en('progress.health.GREEN')}1`,
    );
  });

  test("a risk rating reads in its matrix's own words; a zero the source counted is a count", async () => {
    homeAs(sessionFor(['R02']), {
      views: {
        PORTFOLIO: dashboardView('PORTFOLIO', {
          widgets: [
            widget({
              code: 'RISK_EXPOSURE',
              title: { en: 'Open risks by rating', ar: 'المخاطر المفتوحة حسب التصنيف' },
              widgetType: 'BAR_COLUMN',
              projectionCode: 'RISK.RISK_EXPOSURE',
              sourceDomain: 'WF-06',
              data: {
                state: null,
                figures: [
                  { measure: 'OPEN_RISKS', value: '3', unit: 'COUNT', isMasked: false },
                  { measure: 'REVIEW_OVERDUE', value: '0', unit: 'COUNT', isMasked: false },
                ],
                distribution: [
                  { key: 'HIGH', label: { en: 'High', ar: 'مرتفع' }, count: 2 },
                  { key: 'NOT_ASSESSED', label: null, count: 1 },
                ],
                series: [],
              },
              drillTargetScreenId: 'SCR-080',
            }),
          ],
        }),
      },
    });

    const region = await widgetRegion('Open risks by rating');
    expect(region.querySelector('[data-bucket="HIGH"]')?.textContent).toBe('High2');
    expect(region.querySelector('[data-bucket="NOT_ASSESSED"]')?.textContent).toBe(
      `${en('dashboards.values.NOT_ASSESSED')}1`,
    );
    const overdue = within(region).getByText(en('dashboards.measures.REVIEW_OVERDUE'));
    expect(overdue.parentElement?.querySelector('dd')?.textContent).toBe('0');
    // The cross-project register is SCR-080's portfolio form.
    expect(
      within(region)
        .getByRole('link', { name: `${en('dashboards.widget.open')} Open risks by rating` })
        .getAttribute('href'),
    ).toBe('/risks');
  });

  test("in SCR-040 a widget drills to the project's own screen; a restricted one drills nowhere", async () => {
    overviewAs(pmSession(), projectState());

    const financials = await widgetRegion('Current financial position');
    expect(within(financials).getByRole('link').getAttribute('href')).toBe(
      `/projects/${PROJECT_ID}/financials`,
    );
    expect(within(await widgetRegion('Open risks by rating')).queryByRole('link')).toBeNull();
  });
});

describe('acceptance criterion 3: each role lands on its Blueprint §20.2 dashboard', () => {
  test.each([
    ['R01', 'GOVERNANCE'],
    ['R02', 'PORTFOLIO'],
    ['R03', 'PORTFOLIO'],
    ['R04', 'GOVERNANCE'],
    ['R05', 'GOVERNANCE'],
    ['R06', 'PORTFOLIO'],
    ['R07', 'PORTFOLIO'],
  ])('%s lands on its §20.2 page, a rendering of the %s dashboard', async (role, code) => {
    const api = homeAs(sessionFor([role]), { catalogue: catalogueFor([role]) });

    expect(
      await screen.findByRole('heading', { level: 1, name: en(`dashboards.landing.${role}`) }),
    ).toBeTruthy();
    const dsh = `DSH-00${role.slice(2)}`;
    expect(
      screen.getByText(
        en('dashboards.home.rendering', {
          dashboardId: dsh,
          dashboard: catalogueFor([role]).find((entry) => entry.isDefaultLanding)?.name.en ?? '',
          version: 1,
        }),
      ),
    ).toBeTruthy();
    await waitFor(() => {
      expect(api.requestsTo('GET', new RegExp(`^/dashboards/${code}$`))).toHaveLength(1);
    });
    expect(api.requestsTo('GET', /^\/dashboards\/PROJECT$/)).toHaveLength(0);
  });

  test('R08 lands on the External Contributor Dashboard: its projects, each opening SCR-040', async () => {
    const api = withDashboards(mockApi(), { catalogue: catalogueFor(['R08'], true) });
    api.on('GET', /^\/projects$/, { body: page([projectSummary({ status: 'ACTIVE' })]) });
    renderApp({ path: '/', session: entitySession() });

    expect(
      await screen.findByRole('heading', { level: 1, name: en('dashboards.landing.R08') }),
    ).toBeTruthy();
    const link = await screen.findByRole('link', { name: 'Coastal road upgrade' });
    expect(link.getAttribute('href')).toBe(OVERVIEW_PATH);
    // ADR-019: nothing further — no other dashboard, no switch, no Portfolio or Governance read.
    expect(screen.queryByRole('navigation', { name: en('dashboards.home.switch') })).toBeNull();
    expect(api.requestsTo('GET', /^\/dashboards\/.+$/)).toHaveLength(0);
  });

  test("an entity's person sees the Project Dashboard in SCR-040 read only, in their entity's scope", async () => {
    overviewAs(entitySession(), {
      catalogue: catalogueFor(['R08'], true),
      views: { PROJECT: dashboardView('PROJECT') },
    });

    const section = await screen.findByRole('region', { name: 'Project Dashboard' });
    expect(
      await within(section).findByText(
        `${en('dashboards.project.entityRendering')} · ${en('dashboards.refreshedAt', {
          date: dateTime('2026-10-10T09:00:00Z'),
        })}`,
      ),
    ).toBeTruthy();
    expect(
      within(section).queryByRole('button', { name: en('dashboards.personalize.open') }),
    ).toBeNull();
  });

  test('with several roles the person lands as their first role, and may open the others', async () => {
    const api = homeAs(sessionFor(['R04', 'R02']), { catalogue: catalogueFor(['R04', 'R02']) });

    expect(
      await screen.findByRole('heading', { level: 1, name: en('dashboards.landing.R02') }),
    ).toBeTruthy();
    const switcher = screen.getByRole('navigation', { name: en('dashboards.home.switch') });
    const links = within(switcher).getAllByRole('link');
    expect(links.map((link) => link.getAttribute('href'))).toEqual(['/', '/?dashboard=GOVERNANCE']);
    expect(links[0]?.getAttribute('aria-current')).toBe('page');

    await userEvent.click(present(links[1], 'Governance link'));
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Governance Dashboard' }),
    ).toBeTruthy();
    await waitFor(() => {
      expect(api.requestsTo('GET', /^\/dashboards\/GOVERNANCE$/)).toHaveLength(1);
    });
  });

  test('a person whose roles open no dashboard is told so', async () => {
    homeAs(sessionFor([]), { catalogue: [] });

    expect(await screen.findByText(en('dashboards.home.none'))).toBeTruthy();
  });
});

describe('the department filter narrows the population and never widens it', () => {
  test("it offers only the dashboard's own departments, by name, and reads the one chosen", async () => {
    const api = withDashboards(mockApi());
    api.on('GET', /^\/departments$/, {
      body: page([
        {
          id: DEPARTMENT_A,
          code: 'A',
          name: { en: 'Roads', ar: 'الطرق' },
          parentDepartmentId: null,
          isActive: true,
        },
        {
          id: 'dddddddd-0000-4000-8000-0000000000c3',
          code: 'C',
          name: { en: 'Hidden', ar: 'مخفي' },
          parentDepartmentId: null,
          isActive: true,
        },
      ]),
    });
    renderApp({ path: '/', session: sessionFor(['R03']) });

    const select = await screen.findByRole('combobox', {
      name: en('dashboards.filter.department'),
    });
    await waitFor(() => {
      expect(within(select).getByRole('option', { name: 'Roads' })).toBeTruthy();
    });
    expect(
      within(select)
        .getAllByRole('option')
        .map((option) => option.textContent),
    ).toEqual([
      en('dashboards.filter.allDepartments'),
      en('projects.lookups.unknownItem', { id: DEPARTMENT_B.slice(0, 8) }),
      'Roads',
    ]);

    await userEvent.selectOptions(select, DEPARTMENT_A);
    await waitFor(() => {
      const reads = api.requestsTo('GET', /^\/dashboards\/PORTFOLIO$/);
      expect(reads.at(-1)?.query.get('departmentId')).toBe(DEPARTMENT_A);
    });
  });

  test('a department the person may not filter by is refused, and trying again shows all of theirs', async () => {
    const api = withDashboards(mockApi(), {
      views: {
        PORTFOLIO: problem(422, 'DASHBOARD_FILTER_VALUE_UNAUTHORIZED', [
          { field: 'departmentId', code: 'NOT_ALLOWED' },
        ]),
      },
    });
    api.on('GET', /^\/departments$/, problem(403, 'PERMISSION_DENIED'));
    renderApp({ path: `/?department=${DEPARTMENT_B}`, session: sessionFor(['R02']) });

    const alert = await screen.findByRole('alert');
    expect(within(alert).getByText(en('dashboards.problems.filterValueUnauthorized'))).toBeTruthy();
    withDashboards(api);
    await userEvent.click(within(alert).getByRole('button', { name: en('common.actions.retry') }));
    await waitFor(() => {
      expect(
        api
          .requestsTo('GET', /^\/dashboards\/PORTFOLIO$/)
          .at(-1)
          ?.query.has('departmentId'),
      ).toBe(false);
    });
  });
});

describe('ADR-019: the Portfolio Dashboard alone is personalised, by the roles that hold it', () => {
  const optional = (code: string, title: string, overrides: Partial<DashboardWidgetResult> = {}) =>
    widget({ code, title: { en: title, ar: title }, isOptionalVisibility: true, ...overrides });

  const portfolio = () =>
    dashboardView('PORTFOLIO', {
      widgets: [
        widget({
          code: 'PROJECTS_BY_LIFECYCLE',
          title: { en: 'Projects by lifecycle state', ar: 'x' },
        }),
        optional('CURRENT_HEALTH', 'Current health', { layoutRow: 2, layoutColumn: 1 }),
        optional('SCHEDULE_HEALTH', 'Schedule health', { layoutRow: 2, layoutColumn: 5 }),
        optional('KPI_CONDITION', 'KPI condition', {
          layoutRow: 3,
          layoutColumn: 1,
          isHidden: true,
        }),
      ],
    });

  test('R02 hides and reorders optional widgets; the governed ones are never offered', async () => {
    const api = homeAs(sessionFor(['R02']), { views: { PORTFOLIO: portfolio() } });
    api.on('POST', /^\/dashboards\/PORTFOLIO\/personalize$/, (request) => ({
      body: {
        code: 'PORTFOLIO',
        dashboardDefinitionId: 'x',
        widgets: (request.body as { widgets: unknown[] }).widgets,
      },
    }));

    expect(await widgetRegion('Current health')).toBeTruthy();
    expect(screen.queryByRole('region', { name: 'KPI condition' })).toBeNull();

    await userEvent.click(screen.getByRole('button', { name: en('dashboards.personalize.open') }));
    const dialog = await screen.findByRole('dialog', { name: en('dashboards.personalize.title') });
    const boxes = within(dialog).getAllByRole('checkbox');
    expect(boxes.map((box) => box.parentElement?.textContent)).toEqual([
      'Current health',
      'Schedule health',
      'KPI condition',
    ]);
    expect(within(dialog).queryByText('Projects by lifecycle state')).toBeNull();

    await userEvent.click(within(dialog).getByRole('checkbox', { name: 'Current health' }));
    await userEvent.click(
      within(dialog).getByRole('button', {
        name: `${en('dashboards.personalize.moveUp')} Schedule health`,
      }),
    );
    await userEvent.click(
      within(dialog).getByRole('button', { name: en('dashboards.personalize.save') }),
    );

    await waitFor(() => {
      expect(api.requestsTo('POST', /personalize$/)).toHaveLength(1);
    });
    const [saved] = api.requestsTo('POST', /personalize$/);
    expect(saved?.body).toEqual({
      widgets: [
        { widgetCode: 'SCHEDULE_HEALTH', isHidden: false, sortOrder: 1 },
        { widgetCode: 'CURRENT_HEALTH', isHidden: true, sortOrder: 2 },
        { widgetCode: 'KPI_CONDITION', isHidden: true, sortOrder: 3 },
      ],
    });
    expect(saved?.headers.get('Idempotency-Key')).not.toBeNull();
    expect(await screen.findByText(en('dashboards.personalize.saved'))).toBeTruthy();
  });

  test('restoring the standard layout resets it', async () => {
    const api = homeAs(sessionFor(['R07']), { views: { PORTFOLIO: portfolio() } });
    api.on('POST', /^\/dashboards\/PORTFOLIO\/reset-personalization$/, {
      body: { code: 'PORTFOLIO', dashboardDefinitionId: 'x', widgets: [] },
    });

    await userEvent.click(
      await screen.findByRole('button', { name: en('dashboards.personalize.open') }),
    );
    const dialog = await screen.findByRole('dialog');
    await userEvent.click(
      within(dialog).getByRole('button', { name: en('dashboards.personalize.reset') }),
    );

    expect(await screen.findByText(en('dashboards.personalize.wasReset'))).toBeTruthy();
    expect(api.requestsTo('POST', /reset-personalization$/)).toHaveLength(1);
  });

  test('a Viewer is not offered it', async () => {
    homeAs(sessionFor(['R06']), { views: { PORTFOLIO: portfolio() } });
    expect(await widgetRegion('Current health')).toBeTruthy();
    expect(screen.queryByRole('button', { name: en('dashboards.personalize.open') })).toBeNull();
  });

  test('the personal order and hidden widgets are applied on the dashboard', async () => {
    homeAs(sessionFor(['R02']), {
      views: {
        PORTFOLIO: dashboardView('PORTFOLIO', {
          widgets: [
            optional('CURRENT_HEALTH', 'Current health', { personalSortOrder: 2 }),
            optional('SCHEDULE_HEALTH', 'Schedule health', {
              layoutColumn: 5,
              personalSortOrder: 1,
            }),
          ],
        }),
      },
    });

    await widgetRegion('Current health');
    expect(
      [...document.querySelectorAll('.widget')].map((region) => region.getAttribute('data-widget')),
    ).toEqual(['SCHEDULE_HEALTH', 'CURRENT_HEALTH']);
  });
});

describe('bilingual and accessible (ADR-012)', () => {
  test('in Arabic the landing, the dashboard and its widgets read in Arabic, right to left', async () => {
    withDashboards(mockApi()).on('GET', /^\/departments$/, { body: page([]) });
    renderApp({ path: '/', language: 'ar', session: sessionFor(['R03']) });

    expect(
      await screen.findByRole('heading', { level: 1, name: ar('dashboards.landing.R03') }),
    ).toBeTruthy();
    expect(await screen.findByRole('region', { name: 'الصحة العامة الحالية' })).toBeTruthy();
    expect(screen.getByText(ar('dashboards.semanticState.CURRENT_LIVE'))).toBeTruthy();
    expect(document.documentElement.dir).toBe('rtl');
  });

  test('the Home has no accessibility violations', async () => {
    homeAs(sessionFor(['R02']), {
      views: { PORTFOLIO: dashboardView('PORTFOLIO', { widgets: PROJECT_WIDGETS }) },
    });
    await widgetRegion('Published progress trend');

    const violations = await accessibilityViolations();
    expect(violations, describeViolations(violations)).toEqual([]);
  });

  test('SCR-040 with its Project Dashboard has no accessibility violations', async () => {
    overviewAs(pmSession(), projectState());
    await widgetRegion('Published progress trend');

    const violations = await accessibilityViolations();
    expect(violations, describeViolations(violations)).toEqual([]);
  });
});
