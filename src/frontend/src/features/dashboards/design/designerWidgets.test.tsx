import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import { dashboardView, unknownWidget, widget, withDashboards } from '@/test/dashboardFixtures.ts';
import { sessionFor } from '@/test/identityAccessFixtures.ts';
import { type MockApi, mockApi } from '@/test/mockApi.ts';
import { designFieldsFixture } from '@/test/portfolioInsightFixtures.ts';
import { withProjectLookups } from '@/test/projectFixtures.ts';
import { renderApp } from '@/test/renderApp.tsx';

import { type DashboardWidgetResult } from '../api/types.ts';
import { type DesignView, type Visual, type VisualResult } from './designApi.ts';

// One visual per governed widget (19 Nov 2026, docs/architecture/dashboard-designer-widgets.md): a DASHBOARD_WIDGETS section becomes a
// DASHBOARD_WIDGET visual per widget the caller sees, in the designer as an unsaved change and on the dashboard as it is drawn.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);
const plain = (text: string | null | undefined) => (text ?? '').replace(/[⁨⁩]/g, '');

const DESIGNER = '/dashboards/design';
const DASHBOARD = '/dashboards?dashboard=PORTFOLIO';

const HEALTH = widget({
  code: 'CURRENT_HEALTH',
  title: { en: 'Current health', ar: 'الصحة الحالية' },
  layoutSpan: 4,
});
const LIFECYCLE = widget({
  code: 'PROJECTS_BY_LIFECYCLE',
  title: { en: 'Projects by lifecycle', ar: 'المشاريع حسب المرحلة' },
  widgetType: 'STATUS_DISTRIBUTION',
  layoutSpan: 6,
  data: {
    state: null,
    figures: [],
    distribution: [{ key: 'ACTIVE', label: null, count: 2 }],
    series: [],
  },
});
const HIDDEN = widget({
  code: 'SCHEDULE_HEALTH',
  title: { en: 'Schedule health', ar: 'صحة الجدول' },
  isOptionalVisibility: true,
  isHidden: true,
});
const RESTRICTED = unknownWidget('RESTRICTED', {
  code: 'FINANCIAL_POSITION',
  title: { en: 'Financial position', ar: 'الوضع المالي' },
});
const WIDGETS: DashboardWidgetResult[] = [HEALTH, LIFECYCLE, HIDDEN, RESTRICTED];

function visualsSent(body: unknown): Visual[] {
  return (body as { visuals: Visual[] }).visuals;
}

const echo = (visuals: Visual[]): VisualResult[] =>
  visuals.map((visual) => ({ ...visual, groups: [] }));

/** The Portfolio dashboard's API with these widgets; the design read is the standard one (all the widgets in one section) unless given. */
function api(design?: DesignView): MockApi {
  const mock = mockApi();
  withProjectLookups(mock);
  withDashboards(mock, { views: { PORTFOLIO: dashboardView('PORTFOLIO', { widgets: WIDGETS }) } });
  if (design !== undefined) {
    mock.on('GET', /^\/portfolio-insights\/design$/, { body: design });
  }
  return mock
    .on('GET', /^\/portfolio-insights\/design\/fields$/, { body: designFieldsFixture() })
    .on('POST', /^\/portfolio-insights\/design\/preview$/, (request) => ({
      body: { isStandard: false, updatedAt: null, visuals: echo(visualsSent(request.body)) },
    }))
    .on('PUT', /^\/portfolio-insights\/design$/, (request) => ({
      body: {
        isStandard: false,
        updatedAt: '2026-11-19T09:00:00Z',
        visuals: echo(visualsSent(request.body)),
      },
    }));
}

const session = () => sessionFor(['R02']);

function widgetVisual(
  code: string,
  title: string,
  [layoutColumn, layoutRow, layoutWidth, layoutHeight]: [number, number, number, number],
): VisualResult {
  return {
    title: { ar: title, en: title },
    visualType: 'SECTION',
    measures: [],
    groupBy: 'NONE',
    section: 'DASHBOARD_WIDGET',
    widgetCode: code,
    layoutColumn,
    layoutRow,
    layoutWidth,
    layoutHeight,
    groups: [],
  };
}

/** The drawn widget visuals on the dashboard, in the page's order: each one's title and the widget its card draws. */
function drawnWidgets() {
  return [...document.querySelectorAll<HTMLElement>('[data-section-code="DASHBOARD_WIDGET"]')].map(
    (frame) => ({
      title: plain(frame.querySelector('h3')?.textContent),
      widgets: [...frame.querySelectorAll<HTMLElement>('[data-widget]')].map(
        (card) => card.dataset.widget,
      ),
      headings: within(frame).getAllByRole('heading').length,
    }),
  );
}

describe('the designer splits the widgets section', () => {
  test('on load, into one visual per widget the caller sees, as an unsaved change that saves one DASHBOARD_WIDGET per widget', async () => {
    const mock = api();
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });

    expect(await screen.findByRole('region', { name: 'Current health' })).toBeTruthy();
    expect(screen.getByRole('region', { name: 'Projects by lifecycle' })).toBeTruthy();
    // Hidden, restricted: not split. The section of all of them is gone.
    expect(screen.queryByRole('region', { name: 'Schedule health' })).toBeNull();
    expect(screen.queryByRole('region', { name: 'Financial position' })).toBeNull();
    expect(screen.queryByRole('region', { name: 'Dashboard widgets' })).toBeNull();
    expect(screen.getByText(en('dashboards.designer.unsaved'))).toBeTruthy();
    // Every widget is placed: no Widgets group.
    expect(screen.queryByRole('region', { name: en('dashboards.designer.widgets') })).toBeNull();

    await user.click(screen.getByRole('button', { name: en('dashboards.designer.save') }));
    await waitFor(() => {
      expect(mock.requestsTo('PUT', /^\/portfolio-insights\/design$/)).toHaveLength(1);
    });
    const sent = visualsSent(mock.requestsTo('PUT', /^\/portfolio-insights\/design$/)[0]?.body);
    expect(sent.some((visual) => visual.section === 'DASHBOARD_WIDGETS')).toBe(false);
    expect(
      sent
        .filter((visual) => visual.section === 'DASHBOARD_WIDGET')
        .map(({ widgetCode, title, visualType, layoutWidth, layoutHeight }) => ({
          widgetCode,
          title,
          visualType,
          layoutWidth,
          layoutHeight,
        })),
      // Their spans (4 and 6) share the row's spare two columns, as the widget grid drew them; a metric card is 5 rows, a chart 7.
    ).toEqual([
      {
        widgetCode: 'CURRENT_HEALTH',
        title: HEALTH.title,
        visualType: 'SECTION',
        layoutWidth: 5,
        layoutHeight: 5,
      },
      {
        widgetCode: 'PROJECTS_BY_LIFECYCLE',
        title: LIFECYCLE.title,
        visualType: 'SECTION',
        layoutWidth: 7,
        layoutHeight: 7,
      },
    ]);
    // No other visual carries a widget code.
    expect(
      sent.filter((visual) => visual.section !== 'DASHBOARD_WIDGET' && 'widgetCode' in visual),
    ).toEqual([]);
  });

  test('a removed widget is offered under Widgets and can be added back', async () => {
    api();
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await screen.findByRole('region', { name: 'Projects by lifecycle' });

    await user.click(
      screen.getByRole('button', {
        name: (name) =>
          plain(name) ===
          plain(en('dashboards.designer.editVisual', { title: 'Projects by lifecycle' })),
      }),
    );
    const settings = screen.getByRole('region', { name: en('dashboards.designer.properties') });
    // A widget's settings are a section's: its titles and size, and remove.
    expect(within(settings).getByLabelText(en('dashboards.designer.titleEn'))).toHaveProperty(
      'value',
      'Projects by lifecycle',
    );
    expect(within(settings).queryByLabelText(en('dashboards.designer.typeLabel'))).toBeNull();
    await user.click(
      within(settings).getByRole('button', { name: en('dashboards.designer.removeSection') }),
    );
    await waitFor(() => {
      expect(screen.queryByRole('region', { name: 'Projects by lifecycle' })).toBeNull();
    });

    const group = screen.getByRole('region', { name: en('dashboards.designer.widgets') });
    const add = within(group).getByRole('button', {
      name: (name) =>
        plain(name) ===
        plain(en('dashboards.designer.addWidget', { title: 'Projects by lifecycle' })),
    });
    // Only what is missing: the placed widget is not offered.
    expect(within(group).getAllByRole('button')).toHaveLength(1);
    expect(plain(add.textContent)).toContain('Projects by lifecycle');
    expect(plain(add.textContent)).toContain(en('dashboards.designer.add'));

    await user.click(add);
    expect(await screen.findByRole('region', { name: 'Projects by lifecycle' })).toBeTruthy();
    expect(screen.queryByRole('region', { name: en('dashboards.designer.widgets') })).toBeNull();
  });
});

describe('the dashboard draws each widget as its own visual', () => {
  test('from a design with all the widgets in one section', async () => {
    api();
    renderApp({ path: DASHBOARD, session: session() });
    await screen.findByRole('region', { name: 'Current health' });

    // One card per visual, the visual's title its only heading; hidden and restricted widgets are not drawn.
    expect(drawnWidgets()).toEqual([
      { title: 'Current health', widgets: ['CURRENT_HEALTH'], headings: 1 },
      { title: 'Projects by lifecycle', widgets: ['PROJECTS_BY_LIFECYCLE'], headings: 1 },
    ]);
    expect(document.querySelector('[data-section-code="DASHBOARD_WIDGETS"]')).toBeNull();
  });

  test('from a split design; a restricted, hidden or missing widget leaves no empty row', async () => {
    api({
      isStandard: false,
      updatedAt: '2026-11-19T09:00:00Z',
      visuals: [
        widgetVisual('CURRENT_HEALTH', 'Health now', [0, 0, 6, 5]),
        widgetVisual('FINANCIAL_POSITION', 'Financial position', [6, 0, 6, 5]),
        widgetVisual('SCHEDULE_HEALTH', 'Schedule health', [0, 5, 6, 5]),
        widgetVisual('NO_LONGER_THERE', 'Gone', [6, 5, 6, 5]),
        widgetVisual('PROJECTS_BY_LIFECYCLE', 'Lifecycle', [0, 10, 6, 7]),
      ],
    });
    renderApp({ path: DASHBOARD, session: session() });
    const lifecycle = await screen.findByRole('region', { name: 'Lifecycle' });

    expect(drawnWidgets()).toEqual([
      { title: 'Health now', widgets: ['CURRENT_HEALTH'], headings: 1 },
      { title: 'Lifecycle', widgets: ['PROJECTS_BY_LIFECYCLE'], headings: 1 },
    ]);
    // Placement is free (19 Nov 2026): the widgets stay where they were placed, and the rows left empty by those not shown are taken out,
    // so the last widget rises to the row under the first, in its own column.
    expect(lifecycle.style.getPropertyValue('--visual-row')).toBe('6');
    expect(lifecycle.style.getPropertyValue('--visual-column')).toBe('1');
  });
});
