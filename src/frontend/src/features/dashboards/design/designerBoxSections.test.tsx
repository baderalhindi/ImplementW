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

import { TITLE_ROWS } from './containers.ts';
import {
  type DesignView,
  type Measure,
  type Visual,
  type VisualGroup,
  type VisualResult,
} from './designApi.ts';
import { overlaps } from './visuals.ts';

// The box sections become containers of cards (20 Nov 2026, docs/architecture/dashboard-designer-box-sections.md): Make editable on the
// On-plan indicator and the follow-up reasons sends a CONTAINER at the section's place, size and titles, holding a card per box. The API's
// nine new figures are mocked here (the API answers each card with its box's count).

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);
const ar = (key: string) => translate('ar', key);
const plain = (text: string | null | undefined) =>
  (text ?? '')
    .replace(/[\u2068\u2069\u00a0]/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();

const DESIGNER = '/dashboards/design';
const DASHBOARD = '/dashboards?dashboard=PORTFOLIO';

function present<T>(value: T | null | undefined, what = 'The element'): T {
  if (value === null || value === undefined) {
    throw new Error(`${what} is not there`);
  }
  return value;
}

const group = (values: (number | null)[]): VisualGroup => ({
  key: null,
  label: null,
  projects: 9,
  measured: 6,
  valueEntered: 6,
  values,
  onPlan: null,
});

/** The count the mocked API gives each figure: the box counts of fullInsights(), 7 for any other. */
function countOf(measure: Measure): number {
  const { onPlan, followUpReasons } = fullInsights();
  const counts: Partial<Record<Measure, number | undefined>> = {
    ON_TRACK: onPlan?.onTrack,
    LATE: onPlan?.late,
    STALLED: onPlan?.stalled,
    NOT_MEASURED: onPlan?.notMeasured,
    FOLLOW_UP_STALLED: followUpReasons?.stalled,
    FOLLOW_UP_SUSPENDED: followUpReasons?.suspended,
    FOLLOW_UP_LATE: followUpReasons?.late,
    FOLLOW_UP_ACTION_OVERDUE: followUpReasons?.overdueAction,
    FOLLOW_UP_OPEN_OBSTACLE: followUpReasons?.openObstacle,
    NEEDING_FOLLOW_UP: followUpReasons?.total,
  };
  return counts[measure] ?? 7;
}

function calculated(visual: Visual): VisualResult {
  const { children, ...rest } = visual;
  if (visual.visualType === 'CONTAINER') {
    return { ...rest, groups: [], children: (children ?? []).map(calculated) };
  }
  return {
    ...rest,
    groups: visual.visualType === 'SECTION' ? [] : [group(visual.measures.map(countOf))],
    children: null,
  };
}

function visualsSent(body: unknown): Visual[] {
  return (body as { visuals: Visual[] }).visuals;
}

function api(design: DesignView = standardDesignOf(fullInsights())): MockApi {
  const mock = mockApi();
  withProjectLookups(mock);
  withDashboards(mock, { views: { PORTFOLIO: dashboardView('PORTFOLIO') } });
  const view = (visuals: Visual[]) => ({
    isStandard: false,
    updatedAt: null,
    visuals: visuals.map(calculated),
  });
  return mock
    .on('GET', /^\/portfolio-insights$/, { body: fullInsights() })
    .on('GET', /^\/portfolio-insights\/design$/, { body: design })
    .on('GET', /^\/portfolio-insights\/design\/fields$/, { body: designFieldsFixture() })
    .on('POST', /^\/portfolio-insights\/design\/preview$/, (request) => ({
      body: view(visualsSent(request.body)),
    }))
    .on('PUT', /^\/portfolio-insights\/design$/, (request) => ({
      body: view(visualsSent(request.body)),
    }));
}

const session = () => sessionFor(['R02']);

const settings = () => screen.getByRole('region', { name: en('dashboards.designer.properties') });

const makeEditable = () =>
  within(settings()).getByRole('button', { name: en('dashboards.designer.makeEditable') });

/**
 * Opens the settings of the section titled `title`: the standard design also titles a chart "On-plan indicator", so the section is the
 * visual of that title whose settings offer Make editable.
 */
async function editSection(user: ReturnType<typeof userEvent.setup>, title: string) {
  const pencils = await screen.findAllByRole('button', {
    name: (name) => plain(name) === plain(en('dashboards.designer.editVisual', { title })),
  });
  for (const pencil of pencils) {
    await user.click(pencil);
    if (
      within(settings()).queryByRole('button', { name: en('dashboards.designer.makeEditable') }) !==
      null
    ) {
      return;
    }
  }
  throw new Error(`No section titled ${title} offers Make editable`);
}

async function saved(mock: MockApi, user: ReturnType<typeof userEvent.setup>): Promise<Visual[]> {
  await user.click(screen.getByRole('button', { name: en('dashboards.designer.save') }));
  await waitFor(() => {
    expect(mock.requestsTo('PUT', /^\/portfolio-insights\/design$/)).toHaveLength(1);
  });
  return visualsSent(mock.requestsTo('PUT', /^\/portfolio-insights\/design$/)[0]?.body);
}

/** The designer's container frame (the standard design has other regions of the section's title: the section's own, a chart). */
const containerFrame = () =>
  present(document.querySelector<HTMLElement>('[data-visual="CONTAINER"]'), 'The container');

/** Every card is inside the container's inner grid, and no two overlap. */
function expectLaidOut(container: Visual) {
  const children = container.children ?? [];
  for (const child of children) {
    expect(child.layoutColumn).toBeGreaterThanOrEqual(0);
    expect(child.layoutRow).toBeGreaterThanOrEqual(0);
    expect(child.layoutColumn + child.layoutWidth).toBeLessThanOrEqual(
      container.layoutWidth + 1e-9,
    );
    expect(child.layoutRow + child.layoutHeight).toBeLessThanOrEqual(
      container.layoutHeight - TITLE_ROWS + 1e-9,
    );
    for (const other of children) {
      if (other !== child) {
        expect(overlaps(child, other), `${child.title.en} and ${other.title.en}`).toBe(false);
      }
    }
  }
}

const ON_PLAN_BOXES: readonly (readonly [Measure, string])[] = [
  ['ON_TRACK', 'projects.onPlan.ON_TRACK'],
  ['LATE', 'projects.onPlan.LATE'],
  ['STALLED', 'projects.onPlan.STALLED'],
  ['NOT_MEASURED', 'dashboards.insights.notMeasured'],
];

const REASON_BOXES: readonly (readonly [Measure, string])[] = [
  ['FOLLOW_UP_STALLED', 'projects.followUpReason.STALLED'],
  ['FOLLOW_UP_SUSPENDED', 'projects.followUpReason.SUSPENDED'],
  ['FOLLOW_UP_LATE', 'projects.followUpReason.LATE'],
  ['FOLLOW_UP_ACTION_OVERDUE', 'projects.followUpReason.ACTION_OVERDUE'],
  ['FOLLOW_UP_OPEN_OBSTACLE', 'projects.followUpReason.OPEN_OBSTACLE'],
  ['NEEDING_FOLLOW_UP', 'dashboards.insights.total'],
];

/** The standard design with the On-plan indicator across the whole width, room for its four cards in one row. */
function withWideOnPlan(): DesignView {
  const design = standardDesignOf(fullInsights());
  return {
    ...design,
    visuals: design.visuals
      .filter((visual) => visual.section !== 'FOLLOW_UP_REASONS')
      .map((visual) =>
        visual.section === 'ON_PLAN_COUNTS' ? { ...visual, layoutWidth: 12 } : visual,
      ),
  };
}

describe('Make editable on a box section', () => {
  test('turns the On-plan indicator into a container at its place and titles with a card per box, in one row', async () => {
    const mock = api(withWideOnPlan());
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await editSection(user, 'On-plan indicator');
    expect(
      within(settings()).getByText(en('dashboards.designer.makeEditableBoxesHint')),
    ).toBeTruthy();
    await user.click(makeEditable());

    await waitFor(() => {
      expect(containerFrame()).toBeTruthy();
    });
    const frame = containerFrame();
    for (const [measure, label] of ON_PLAN_BOXES) {
      // Each card draws its box's count, as the API calculates it.
      expect(within(frame).getByText(en(label))).toBeTruthy();
      await waitFor(() => {
        expect(within(frame).getAllByText(String(countOf(measure))).length).toBeGreaterThan(0);
      });
    }

    const sent = await saved(mock, user);
    expect(sent.some((visual) => visual.section === 'ON_PLAN_COUNTS')).toBe(false);
    const container = present(
      sent.find((visual) => visual.visualType === 'CONTAINER'),
      'The container',
    );
    expect(container).toMatchObject({
      title: { ar: 'On-plan indicator', en: 'On-plan indicator' },
      measures: [],
      groupBy: 'NONE',
      layoutColumn: 0,
      layoutRow: 44,
      layoutWidth: 12,
      layoutHeight: TITLE_ROWS + 1.5,
    });
    expect(container.section ?? null).toBeNull();
    const children = container.children ?? [];
    expect(children.map((child) => child.visualType)).toEqual(['CARD', 'CARD', 'CARD', 'CARD']);
    expect(children.map((child) => child.measures)).toEqual(
      ON_PLAN_BOXES.map(([measure]) => [measure]),
    );
    expect(children.map((child) => child.title)).toEqual(
      ON_PLAN_BOXES.map(([, label]) => ({ ar: ar(label), en: en(label) })),
    );
    expect(new Set(children.map((child) => child.layoutRow))).toEqual(new Set([0]));
    expect(children.map((child) => child.layoutWidth)).toEqual([3, 3, 3, 3]);
    expect(children.map((child) => child.layoutHeight)).toEqual([1.5, 1.5, 1.5, 1.5]);
    expectLaidOut(container);
  });

  test('on the standard six columns, the four cards wrap into two rows of two and the container is as tall as they need', async () => {
    const mock = api();
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await editSection(user, 'On-plan indicator');
    await user.click(makeEditable());

    const sent = await saved(mock, user);
    const container = present(
      sent.find((visual) => visual.visualType === 'CONTAINER'),
      'The container',
    );
    expect(container).toMatchObject({
      visualType: 'CONTAINER',
      layoutColumn: 0,
      layoutRow: 44,
      layoutWidth: 6,
      layoutHeight: TITLE_ROWS + 3,
    });
    expect(
      (container.children ?? []).map((child) => [
        child.layoutColumn,
        child.layoutRow,
        child.layoutWidth,
      ]),
    ).toEqual([
      [0, 0, 3],
      [3, 0, 3],
      [0, 1.5, 3],
      [3, 1.5, 3],
    ]);
    expectLaidOut(container);
    // What the grown container reaches is moved down: no top-level visual overlaps another.
    for (const visual of sent) {
      for (const other of sent) {
        if (other !== visual) {
          expect(overlaps(visual, other), `${visual.title.en} and ${other.title.en}`).toBe(false);
        }
      }
    }
  });

  test('turns Why projects need follow-up into a container of six cards, the Total last', async () => {
    const mock = api();
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await editSection(user, 'Why projects need follow-up');
    await user.click(makeEditable());

    const frame = await waitFor(containerFrame);
    await waitFor(() => {
      expect(within(frame).getByText(en('dashboards.insights.total'))).toBeTruthy();
    });

    const sent = await saved(mock, user);
    expect(sent.some((visual) => visual.section === 'FOLLOW_UP_REASONS')).toBe(false);
    const container = present(
      sent.find((visual) => visual.visualType === 'CONTAINER'),
      'The container',
    );
    expect(container).toMatchObject({ visualType: 'CONTAINER', layoutColumn: 6, layoutWidth: 6 });
    const children = container.children ?? [];
    expect(children.map((child) => child.measures)).toEqual(
      REASON_BOXES.map(([measure]) => [measure]),
    );
    expect(children.map((child) => child.title)).toEqual(
      REASON_BOXES.map(([, label]) => ({ ar: ar(label), en: en(label) })),
    );
    expect(children.map((child) => child.layoutWidth)).toEqual([2, 2, 2, 2, 2, 2]);
    expectLaidOut(container);
  });

  test('Discard brings the section back', async () => {
    const mock = api();
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await editSection(user, 'On-plan indicator');
    await user.click(makeEditable());
    await waitFor(containerFrame);

    await user.click(screen.getByRole('button', { name: en('dashboards.designer.discard') }));
    await waitFor(() => {
      expect(document.querySelector('[data-visual="CONTAINER"]')).toBeNull();
    });
    await editSection(user, 'On-plan indicator');
    expect(makeEditable()).toBeTruthy();
    expect(mock.requestsTo('PUT', /^\/portfolio-insights\/design$/)).toHaveLength(0);
  });
});

describe('the new figures', () => {
  const NEW_FIGURES: readonly (readonly [Measure, string])[] = [
    ...ON_PLAN_BOXES.map(([measure, label]) => [measure, label] as const),
    ...REASON_BOXES.filter(([measure]) => measure !== 'NEEDING_FOLLOW_UP'),
  ];
  const source = (measure: Measure) =>
    en(
      `dashboards.designer.measureSource.${measure.startsWith('FOLLOW_UP_') ? 'FOLLOW_UP' : 'ON_PLAN'}`,
    );

  test('are offered in the Portfolio figures of a card, each beside where its boxes are', async () => {
    api({ isStandard: true, updatedAt: null, visuals: [] });
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await screen.findByRole('heading', { level: 1, name: en('dashboards.designer.title') });
    await user.click(
      await waitFor(() => {
        const tile = screen.getByRole('button', { name: en('dashboards.designer.type.CARD') });
        expect(tile.hasAttribute('disabled')).toBe(false);
        return tile;
      }),
    );
    for (const [measure, label] of NEW_FIGURES) {
      expect(en(`dashboards.designer.measure.${measure}`)).toBe(en(label));
      expect(
        within(settings()).getByRole('checkbox', {
          name: (name) =>
            name.replace(/\s/g, '') === `${en(label)}${source(measure)}`.replace(/\s/g, ''),
        }),
      ).toBeTruthy();
    }
  });

  test('are offered in a table’s Add column', async () => {
    api({ isStandard: true, updatedAt: null, visuals: [] });
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await screen.findByRole('heading', { level: 1, name: en('dashboards.designer.title') });
    await user.click(
      await waitFor(() => {
        const tile = screen.getByRole('button', { name: en('dashboards.designer.type.TABLE') });
        expect(tile.hasAttribute('disabled')).toBe(false);
        return tile;
      }),
    );
    await user.click(
      within(settings()).getByRole('button', { name: en('dashboards.designer.addColumn') }),
    );
    for (const [measure, label] of NEW_FIGURES) {
      expect(
        within(settings()).getByRole('button', {
          name: (name) =>
            plain(name).startsWith(plain(en(label))) && plain(name).includes(source(measure)),
        }),
      ).toBeTruthy();
    }
  });

  test.each([
    ['LATE', '/projects?registered=true&onPlan=LATE'],
    ['NOT_MEASURED', '/projects?registered=true&onPlan=NOT_MEASURED'],
    ['FOLLOW_UP_ACTION_OVERDUE', '/projects/follow-up?mainReason=ACTION_OVERDUE'],
  ] as const)(
    'a card of %s on the dashboard draws its count and opens its records',
    async (measure, href) => {
      const cardOf: VisualResult = {
        title: { ar: 'Box', en: 'Box' },
        visualType: 'CARD',
        measures: [measure],
        groupBy: 'NONE',
        layoutColumn: 0,
        layoutRow: 0,
        layoutWidth: 3,
        layoutHeight: 2.5,
        groups: [group([4])],
        children: null,
      };
      api({
        isStandard: false,
        updatedAt: '2026-11-20T09:00:00Z',
        visuals: [
          {
            title: { ar: 'Boxes', en: 'Boxes' },
            visualType: 'CONTAINER',
            measures: [],
            groupBy: 'NONE',
            layoutColumn: 0,
            layoutRow: 0,
            layoutWidth: 6,
            layoutHeight: 4,
            groups: [],
            children: [cardOf],
          },
        ],
      });
      renderApp({ path: DASHBOARD, session: session() });

      const frame = await screen.findByRole('region', { name: 'Boxes' });
      expect(within(frame).getByText('4')).toBeTruthy();
      const link = within(frame).getByRole('link', { name: 'Box' });
      expect(link.getAttribute('href')).toBe(href);
    },
  );
});
