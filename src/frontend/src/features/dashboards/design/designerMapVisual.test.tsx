import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test, vi } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import { dashboardView, withDashboards } from '@/test/dashboardFixtures.ts';
import { sessionFor } from '@/test/identityAccessFixtures.ts';
import { type MockApi, mockApi } from '@/test/mockApi.ts';
import { designFieldsFixture, FIELD } from '@/test/portfolioInsightFixtures.ts';
import { withProjectLookups } from '@/test/projectFixtures.ts';
import { renderApp } from '@/test/renderApp.tsx';

import { type DashboardPlace } from '../api/types.ts';
import { type DesignView, type Visual, type VisualGroup, type VisualResult } from './designApi.ts';
import { fieldName } from './fields.ts';
import { formatValue } from './visuals.ts';

// The Map visual (20 Nov 2026, docs/architecture/dashboard-designer-map-visual.md): each Map tile adds a new MAP visual (the Projects by location
// section stays a section), its figure per city or region drawn on the dashboard's map, with its filters and options. MapLibre is faked, as
// in LocationsMap.test.tsx: a marker is its button in the map's container. The API is mocked: it echoes what it is sent, with groups per place.

const fake = vi.hoisted(() => {
  class FakeMap {
    canvas = document.createElement('div');
    constructor(options: { container: HTMLElement }) {
      options.container.append(this.canvas);
    }
    addControl = vi.fn();
    on = vi.fn();
    flyTo = vi.fn();
    fitBounds = vi.fn();
    remove = vi.fn();
    getZoom(): number {
      return 6;
    }
  }
  class FakeMarker {
    element: HTMLElement;
    constructor({ element }: { element: HTMLElement }) {
      this.element = element;
    }
    setLngLat(): this {
      return this;
    }
    addTo(map: FakeMap): this {
      map.canvas.append(this.element);
      return this;
    }
    remove(): void {
      this.element.remove();
    }
  }
  class FakePopup {
    setLngLat(): this {
      return this;
    }
    setText(): this {
      return this;
    }
    addTo(): this {
      return this;
    }
    remove(): void {
      // Nothing is drawn.
    }
  }
  class FakeBounds {
    extend(): this {
      return this;
    }
  }
  class FakeNavigationControl {
    position = 'top-left';
  }
  return { FakeMap, FakeMarker, FakePopup, FakeBounds, FakeNavigationControl };
});

vi.mock('maplibre-gl', () => ({
  Map: fake.FakeMap,
  Marker: fake.FakeMarker,
  Popup: fake.FakePopup,
  LngLatBounds: fake.FakeBounds,
  NavigationControl: fake.FakeNavigationControl,
  setWorkerUrl: vi.fn(),
}));

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);
const ar = (key: string) => translate('ar', key);
const plain = (text: string | null | undefined) => (text ?? '').replace(/[⁨⁩]/g, '');

const DESIGNER = '/dashboards/design';
const DASHBOARD = '/dashboards?dashboard=PORTFOLIO';

function present<T>(value: T | null | undefined, what = 'The element'): T {
  if (value === null || value === undefined) {
    throw new Error(`${what} is not there`);
  }
  return value;
}

const EASTERN = 'region-eastern';
const RIYADH_REGION = 'region-riyadh';
const HOFUF = 'city-hofuf';
const DAMMAM = 'city-dammam';
const RIYADH = 'city-riyadh';

const PLACES: DashboardPlace[] = [
  { id: EASTERN, label: { ar: 'الشرقية', en: 'Eastern' }, parentItemId: null },
  { id: RIYADH_REGION, label: { ar: 'الرياض', en: 'Riyadh Region' }, parentItemId: null },
  {
    id: HOFUF,
    label: { ar: 'الهفوف', en: 'Hofuf' },
    parentItemId: EASTERN,
    latitude: 25.38,
    longitude: 49.59,
  },
  {
    id: DAMMAM,
    label: { ar: 'الدمام', en: 'Dammam' },
    parentItemId: EASTERN,
    latitude: 26.43,
    longitude: 50.1,
  },
  {
    id: RIYADH,
    label: { ar: 'الرياض', en: 'Riyadh' },
    parentItemId: RIYADH_REGION,
    latitude: 24.71,
    longitude: 46.68,
  },
];

const group = (key: string | null, value: number, projects = value): VisualGroup => ({
  key,
  label: null,
  projects,
  measured: projects,
  valueEntered: projects,
  values: [value],
  onPlan: null,
});

/** A map's groups as the API calculates them: per city (one project with none), or per region. */
const mapGroups = (groupBy: string, measure: string | undefined): VisualGroup[] => {
  const scale = measure === 'VALUE_SAR' ? 1_000_000 : 1;
  return groupBy === 'REGION'
    ? [group(EASTERN, 7 * scale, 7), group(RIYADH_REGION, 2 * scale, 2)]
    : [
        group(HOFUF, 5 * scale, 5),
        group(DAMMAM, 2 * scale, 2),
        group(RIYADH, 2 * scale, 2),
        group(null, 1 * scale, 1),
      ];
};

const CARD: VisualResult = {
  title: { ar: 'كل المشاريع', en: 'All projects' },
  visualType: 'CARD',
  measures: ['PROJECTS'],
  groupBy: 'NONE',
  layoutColumn: 0,
  layoutRow: 0,
  layoutWidth: 4,
  layoutHeight: 2,
  groups: [group(null, 10)],
  children: null,
};

const SECTION_MAP: VisualResult = {
  title: { ar: 'المشاريع حسب الموقع', en: 'Projects by location' },
  visualType: 'SECTION',
  measures: [],
  groupBy: 'NONE',
  section: 'PROJECT_LOCATIONS',
  layoutColumn: 0,
  layoutRow: 2,
  layoutWidth: 12,
  layoutHeight: 7,
  groups: [],
  children: null,
};

const CONTAINER: VisualResult = {
  title: { ar: 'ملاحظات', en: 'Notes' },
  visualType: 'CONTAINER',
  measures: [],
  groupBy: 'NONE',
  layoutColumn: 0,
  layoutRow: 2,
  layoutWidth: 12,
  layoutHeight: 9,
  groups: [],
  children: [],
};

const MAP_VISUAL: VisualResult = {
  title: { ar: 'القيمة حسب الموقع', en: 'Value by place' },
  visualType: 'MAP',
  measures: ['VALUE_SAR'],
  groupBy: 'CITY',
  layoutColumn: 0,
  layoutRow: 2,
  layoutWidth: 12,
  layoutHeight: 7,
  map: { showList: true, showCounts: true },
  groups: mapGroups('CITY', 'VALUE_SAR'),
  children: null,
};

const design = (visuals: VisualResult[]): DesignView => ({
  isStandard: false,
  updatedAt: '2026-11-20T09:00:00Z',
  visuals,
});

function visualsSent(body: unknown): Visual[] {
  return (body as { visuals: Visual[] }).visuals;
}

/** What the mocked API calculates: a map its groups per place, a container its children, a section none. */
function calculated(visual: Visual): VisualResult {
  const { children, ...rest } = visual;
  if (visual.visualType === 'CONTAINER') {
    return { ...rest, groups: [], children: (children ?? []).map(calculated) };
  }
  if (visual.visualType === 'MAP') {
    return { ...rest, groups: mapGroups(visual.groupBy, visual.measures[0]), children: null };
  }
  return visual.visualType === 'SECTION'
    ? { ...rest, groups: [], children: null }
    : { ...rest, groups: [group(null, 3)], children: null };
}

function api(view: DesignView = design([CARD, SECTION_MAP])): MockApi {
  const mock = mockApi();
  withProjectLookups(mock);
  withDashboards(mock, {
    views: {
      PORTFOLIO: dashboardView('PORTFOLIO', { places: PLACES }),
    },
  });
  return mock
    .on('GET', /^\/portfolio-insights\/design$/, { body: view })
    .on('GET', /^\/portfolio-insights\/design\/fields$/, { body: designFieldsFixture() })
    .on('POST', /^\/portfolio-insights\/design\/preview$/, (request) => ({
      body: {
        isStandard: false,
        updatedAt: null,
        visuals: visualsSent(request.body).map(calculated),
      },
    }))
    .on('PUT', /^\/portfolio-insights\/design$/, (request) => ({
      body: {
        isStandard: false,
        updatedAt: '2026-11-20T10:00:00Z',
        visuals: visualsSent(request.body).map(calculated),
      },
    }));
}

const session = () => sessionFor(['R02']);

const settings = () => screen.getByRole('region', { name: en('dashboards.designer.properties') });

const lastPreviewed = (mock: MockApi): Visual[] =>
  visualsSent(
    mock.requestsTo('POST', /^\/portfolio-insights\/design\/preview$/).at(-1)?.body ?? {
      visuals: [],
    },
  );

/** Every visual sent, a container's children included. */
const everyVisual = (visuals: readonly Visual[]): Visual[] =>
  visuals.flatMap((visual) => [visual, ...(visual.children ?? [])]);

const mapsIn = (visuals: readonly Visual[]) =>
  everyVisual(visuals).filter((visual) => visual.visualType === 'MAP');

/** Only a map sends `map`. */
function expectMapOnlyOnMaps(visuals: readonly Visual[]) {
  for (const visual of everyVisual(visuals)) {
    if (visual.visualType === 'MAP') {
      expect(visual.map, `${visual.title.en} sends its options`).toBeTruthy();
    } else {
      expect('map' in visual, `${visual.title.en} sends no map options`).toBe(false);
    }
  }
}

const mapTile = () => screen.getByRole('button', { name: en('dashboards.designer.type.MAP') });

async function openDesigner(language: 'en' | 'ar' = 'en') {
  renderApp({ path: DESIGNER, session: session(), language });
  await screen.findByRole('heading', {
    level: 1,
    name: translate(language, 'dashboards.designer.title'),
  });
}

/** The designer's canvas measured as a browser would: 1200 px across, a column 100 px and a row 72 px. jsdom lays nothing out. */
function measuredCanvas(): HTMLElement {
  const canvas = present(document.querySelector<HTMLElement>('.designer__canvas'), 'The canvas');
  Object.defineProperty(canvas, 'clientWidth', { configurable: true, value: 1200 });
  canvas.getBoundingClientRect = () => ({
    left: 0,
    top: 0,
    right: 1200,
    bottom: 3000,
    width: 1200,
    height: 3000,
    x: 0,
    y: 0,
    toJSON: () => ({}),
  });
  return canvas;
}

/** Drags a gallery tile to the canvas point (x, y): pressed, moved past the drag threshold, released there. */
function dragTile(tile: HTMLElement, x: number, y: number) {
  fireEvent.pointerDown(tile, { button: 0, clientX: 1500, clientY: 100 });
  fireEvent.pointerMove(window, { clientX: 1400, clientY: 120 });
  fireEvent.pointerMove(window, { clientX: x, clientY: y });
  fireEvent.pointerUp(window, { clientX: x, clientY: y });
  fireEvent.click(tile);
}

/** The markers drawn in a visual: their figures, by the place they name. */
function markers(visual: HTMLElement): { name: string; figure: string }[] {
  return Array.from(visual.querySelectorAll<HTMLElement>('.map-bubble')).map((bubble) => ({
    name: bubble.getAttribute('aria-label') ?? '',
    figure: bubble.textContent,
  }));
}

describe('the Map tile adds a new map', () => {
  test('Map sits after Gauge and the Heatmap (22 Nov 2026), and before Text box and Container', async () => {
    api();
    await openDesigner();
    const names = within(screen.getByRole('region', { name: en('dashboards.designer.gallery') }))
      .getAllByRole('button')
      .map((button) => plain(button.textContent).trim());
    const at = (key: string) => names.indexOf(en(`dashboards.designer.type.${key}`));
    expect(at('GAUGE')).toBeGreaterThanOrEqual(0);
    expect(at('HEATMAP')).toBe(at('GAUGE') + 1);
    expect(at('MAP')).toBe(at('HEATMAP') + 1);
    expect(at('TEXT')).toBe(at('MAP') + 1);
    expect(at('CONTAINER')).toBe(at('TEXT') + 1);
  });

  test('each click sends a new MAP, 12 by 7, of the projects per city with the list and counts; the section stays as it was', async () => {
    const mock = api();
    const user = userEvent.setup();
    await openDesigner();
    await screen.findByRole('region', { name: 'Projects by location' });

    await user.click(mapTile());
    await user.click(mapTile());

    await waitFor(() => {
      expect(mapsIn(lastPreviewed(mock))).toHaveLength(2);
    });
    for (const map of mapsIn(lastPreviewed(mock))) {
      expect(map).toMatchObject({
        visualType: 'MAP',
        measures: ['PROJECTS'],
        groupBy: 'CITY',
        layoutWidth: 12,
        layoutHeight: 7,
        map: { showList: true, showCounts: true },
        title: { en: en('dashboards.locations.title'), ar: ar('dashboards.locations.title') },
      });
      expect(map.section ?? null).toBeNull();
    }
    const sections = lastPreviewed(mock).filter((visual) => visual.section === 'PROJECT_LOCATIONS');
    expect(sections).toEqual([
      expect.objectContaining({ visualType: 'SECTION', layoutRow: 2, layoutWidth: 12 }),
    ]);
    // Never disabled, and no word of a map already placed.
    expect((mapTile() as HTMLButtonElement).disabled).toBe(false);
    expect(mapTile().getAttribute('title')).toBeNull();
    expect(mapTile().getAttribute('aria-describedby')).toBeNull();
    expectMapOnlyOnMaps(lastPreviewed(mock));
  });

  test('each drag places a new map at the cell it is dropped on, the section left where it is', async () => {
    const mock = api();
    await openDesigner();
    await screen.findByRole('region', { name: 'Projects by location' });
    measuredCanvas();

    // Row 9, under the section (rows 2 to 9), then row 16 under that map.
    dragTile(mapTile(), 30, 9 * 72 + 2);
    dragTile(mapTile(), 30, 16 * 72 + 2);

    await waitFor(() => {
      expect(mapsIn(lastPreviewed(mock)).map((map) => map.layoutRow)).toEqual([9, 16]);
    });
    expect(
      lastPreviewed(mock).find((visual) => visual.section === 'PROJECT_LOCATIONS'),
    ).toMatchObject({ layoutRow: 2, layoutColumn: 0 });
  });

  test('a map dragged onto a container becomes its child, with its options', async () => {
    const mock = api(design([CARD, CONTAINER]));
    await openDesigner();
    await screen.findByRole('region', { name: 'Notes' });
    measuredCanvas();

    dragTile(mapTile(), 110, 3 * 72 + 30);

    await waitFor(() => {
      const box = lastPreviewed(mock).find((visual) => visual.visualType === 'CONTAINER');
      expect(box?.children).toEqual([
        expect.objectContaining({
          visualType: 'MAP',
          groupBy: 'CITY',
          map: { showList: true, showCounts: true },
        }),
      ]);
    });
    expect(lastPreviewed(mock).filter((visual) => visual.visualType === 'MAP')).toHaveLength(0);
    expectMapOnlyOnMaps(lastPreviewed(mock));
  });
});

describe("a map's settings", () => {
  test('the figure, the places, a filter and the options are sent; the automatic title follows the figure', async () => {
    const mock = api(design([CARD]));
    const user = userEvent.setup();
    await openDesigner();
    await user.click(mapTile());
    const pane = () => within(settings());
    // No type to change, no axes.
    expect(
      pane().queryByRole('combobox', { name: en('dashboards.designer.typeLabel') }),
    ).toBeNull();
    // Averages and the gap are not offered: a map's figure adds up.
    expect(
      pane().queryByRole('checkbox', { name: en('dashboards.designer.measure.AVERAGE_ACTUAL') }),
    ).toBeNull();

    await user.click(
      pane().getByRole('checkbox', { name: en('dashboards.designer.measure.VALUE_SAR') }),
    );
    await user.selectOptions(
      pane().getByRole('combobox', { name: en('dashboards.designer.mapPlaces') }),
      'REGION',
    );
    await user.click(pane().getByRole('button', { name: en('dashboards.designer.addFilter') }));
    await user.selectOptions(
      pane().getByRole('combobox', { name: en('dashboards.designer.filterField') }),
      'PROJECT.STATUS',
    );
    await user.click(
      await waitFor(() => pane().getByRole('checkbox', { name: en('projects.status.ACTIVE') })),
    );
    await user.click(pane().getByRole('checkbox', { name: en('dashboards.designer.mapShowList') }));
    await user.click(
      pane().getByRole('checkbox', { name: en('dashboards.designer.mapShowCounts') }),
    );

    await waitFor(() => {
      expect(mapsIn(lastPreviewed(mock))).toEqual([
        expect.objectContaining({
          measures: ['VALUE_SAR'],
          groupBy: 'REGION',
          filters: [{ field: 'PROJECT.STATUS', values: ['ACTIVE'] }],
          map: { showList: false, showCounts: false },
          title: {
            en: plain(
              en('dashboards.designer.titleByGroup', {
                measure: en('dashboards.designer.measure.VALUE_SAR'),
                group: en('dashboards.designer.mapLocation'),
              }),
            ),
            ar: expect.any(String) as string,
          },
        }),
      ]);
    });
  });

  test('a field value is a figure too, with a calculation that adds up, and keeps the places and filters', async () => {
    const mock = api(design([CARD]));
    const user = userEvent.setup();
    await openDesigner();
    await user.click(mapTile());
    const pane = () => within(settings());
    await user.click(pane().getByRole('button', { name: en('dashboards.designer.addFilter') }));
    await user.selectOptions(
      pane().getByRole('combobox', { name: en('dashboards.designer.filterField') }),
      'PROJECT.STATUS',
    );
    await user.click(
      await waitFor(() => pane().getByRole('checkbox', { name: en('projects.status.ACTIVE') })),
    );

    await user.click(
      pane().getByRole('checkbox', {
        name: (name) => name.startsWith(fieldName(FIELD.ACTUAL_EXPENDITURE, 'en')),
      }),
    );

    await waitFor(() => {
      expect(mapsIn(lastPreviewed(mock))[0]).toMatchObject({
        measures: [],
        values: [{ field: FIELD.ACTUAL_EXPENDITURE, aggregation: 'COUNT' }],
        groupBy: 'CITY',
        filters: [{ field: 'PROJECT.STATUS', values: ['ACTIVE'] }],
      });
    });
    const calculation = pane()
      .getAllByRole('combobox')
      .find((select) =>
        Array.from((select as HTMLSelectElement).options).some((option) => option.value === 'SUM'),
      ) as HTMLSelectElement;
    expect(Array.from(calculation.options).map((option) => option.value)).toEqual(['COUNT', 'SUM']);
  });
});

describe('a map draws its groups', () => {
  test('in the designer: a marker per placed city with its figure formatted, the list of regions and cities, and the projects with no place', async () => {
    api(design([CARD, MAP_VISUAL]));
    await openDesigner();
    const visual = await screen.findByRole('region', { name: 'Value by place' });
    const sar = (value: number) => formatValue('VALUE_SAR', value, en);

    await waitFor(() => {
      expect(markers(visual)).toEqual([
        { name: `Hofuf: ${sar(5_000_000)}`, figure: sar(5_000_000) },
        { name: `Dammam: ${sar(2_000_000)}`, figure: sar(2_000_000) },
        { name: `Riyadh: ${sar(2_000_000)}`, figure: sar(2_000_000) },
      ]);
    });
    const side = present(visual.querySelector<HTMLElement>('.locations__side'), 'The list');
    const regions = Array.from(side.querySelectorAll('.locations__region')).map((region) =>
      plain(region.querySelector('.locations__region-header')?.textContent),
    );
    expect(regions).toEqual([
      plain(`Eastern${sar(7_000_000)}`),
      plain(`Riyadh Region${sar(2_000_000)}`),
      plain(`${en('dashboards.locations.noRegion')}${sar(1_000_000)}`),
    ]);
    expect(within(side).getByText(en('dashboards.locations.noCity'))).toBeTruthy();
    expect(plain(visual.querySelector('.insights__coverage')?.textContent)).toBe(
      plain(en('dashboards.locations.unplaced', { count: 1 })),
    );
  });

  test('by region, a marker per region in the middle of its cities, and no city in the list', async () => {
    api(
      design([
        CARD,
        {
          ...MAP_VISUAL,
          measures: ['PROJECTS'],
          groupBy: 'REGION',
          groups: mapGroups('REGION', 'PROJECTS'),
        },
      ]),
    );
    await openDesigner();
    const visual = await screen.findByRole('region', { name: 'Value by place' });
    await waitFor(() => {
      expect(markers(visual)).toEqual([
        { name: 'Eastern: 7', figure: '7' },
        { name: 'Riyadh Region: 2', figure: '2' },
      ]);
    });
    expect(visual.querySelectorAll('.bars__item')).toHaveLength(0);
  });

  test('without the counts the markers hold no figure; without the list the map takes the row', async () => {
    api(design([CARD, { ...MAP_VISUAL, map: { showList: false, showCounts: false } }]));
    await openDesigner();
    const visual = await screen.findByRole('region', { name: 'Value by place' });
    await waitFor(() => {
      expect(markers(visual)).toHaveLength(3);
    });
    expect(markers(visual).map((marker) => marker.figure)).toEqual(['', '', '']);
    // Still named to a screen reader.
    expect(markers(visual)[0]?.name).toBe(`Hofuf: ${formatValue('VALUE_SAR', 5_000_000, en)}`);
    expect(visual.querySelector('.locations__side')).toBeNull();
    expect(visual.querySelector('.locations__map-row--wide')).not.toBeNull();
  });

  test('on the dashboard, each place opening the register filtered by it', async () => {
    api(design([CARD, MAP_VISUAL]));
    renderApp({ path: DASHBOARD, session: session() });
    const map = await screen.findByRole('region', {
      name: (name) =>
        plain(name) === plain(en('dashboards.designer.mapLabel', { title: 'Value by place' })),
    });
    const visual = present(map.closest<HTMLElement>('.visual'), 'The map visual');
    expect(visual.dataset.visual).toBe('MAP');
    await waitFor(() => {
      expect(markers(visual)).toHaveLength(3);
    });
    const hofuf = within(visual).getByRole('link', { name: 'Hofuf' });
    expect(hofuf.getAttribute('href')).toContain(`cityItemId=${HOFUF}`);
    expect(within(visual).getByRole('link', { name: 'Eastern' }).getAttribute('href')).toContain(
      `regionItemId=${EASTERN}`,
    );
  });
});

describe('saving a design with maps', () => {
  test('`map` is sent on a map only, a child map included', async () => {
    const mock = api(
      design([
        CARD,
        SECTION_MAP,
        MAP_VISUAL,
        {
          ...CONTAINER,
          layoutRow: 9,
          children: [{ ...MAP_VISUAL, layoutRow: 0, layoutColumn: 0, map: null }],
        },
      ]),
    );
    const user = userEvent.setup();
    await openDesigner();
    await screen.findByRole('region', { name: 'Notes' });
    await user.click(mapTile());
    await user.click(screen.getByRole('button', { name: en('dashboards.designer.save') }));

    await waitFor(() => {
      expect(mock.requestsTo('PUT', /^\/portfolio-insights\/design$/)).toHaveLength(1);
    });
    const sent = visualsSent(mock.requestsTo('PUT', /^\/portfolio-insights\/design$/)[0]?.body);
    expect(mapsIn(sent)).toHaveLength(3);
    // A map saved without options is sent with both at their default.
    expect(sent.find((visual) => visual.visualType === 'CONTAINER')?.children?.[0]?.map).toEqual({
      showList: true,
      showCounts: true,
    });
    expectMapOnlyOnMaps(sent);
  });
});
