import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import { dashboardView, withDashboards } from '@/test/dashboardFixtures.ts';
import { sessionFor } from '@/test/identityAccessFixtures.ts';
import { type MockApi, mockApi } from '@/test/mockApi.ts';
import { designFieldsFixture } from '@/test/portfolioInsightFixtures.ts';
import { withProjectLookups } from '@/test/projectFixtures.ts';
import { renderApp } from '@/test/renderApp.tsx';

import { type DesignView, type Visual, type VisualGroup, type VisualResult } from './designApi.ts';

// Containers (19 Nov 2026, docs/architecture/dashboard-designer-containers.md): a framed, titled box on the dashboard that holds visuals. A child's
// place counts inside its container's inner grid (the container's columns, its rows under the title bar). These tests hold the designer and the
// dashboard to the contract's API shapes and its i18n names; the pointer drags are tested through the pure helpers (containers.test.ts).

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);
const plain = (text: string | null | undefined) => (text ?? '').replace(/[⁨⁩]/g, '');

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
  projects: 3,
  measured: 3,
  valueEntered: 3,
  values,
  onPlan: null,
});

function card(
  title: string,
  measure: Visual['measures'][number],
  [layoutColumn, layoutRow, layoutWidth, layoutHeight]: [number, number, number, number],
  values: (number | null)[] = [3],
): VisualResult {
  return {
    title: { ar: title, en: title },
    visualType: 'CARD',
    measures: [measure],
    groupBy: 'NONE',
    layoutColumn,
    layoutRow,
    layoutWidth,
    layoutHeight,
    groups: [group(values)],
    children: null,
  };
}

function container(
  title: string,
  [layoutColumn, layoutRow, layoutWidth, layoutHeight]: [number, number, number, number],
  children: VisualResult[],
): VisualResult {
  return {
    title: { ar: title, en: title },
    visualType: 'CONTAINER',
    measures: [],
    groupBy: 'NONE',
    layoutColumn,
    layoutRow,
    layoutWidth,
    layoutHeight,
    children,
    groups: [],
  };
}

/** A design with one card beside a container of two cards, the second on the container's second inner row. */
const VALUE = card('Total value', 'PROJECTS', [0, 0, 2, 2]);
const PROJECTS = card('All projects', 'PROJECTS', [0, 0, 4, 2]);
const ACTIVE = card('Active projects', 'IN_PROGRESS', [4, 1, 4, 3]);
const PROGRAMME = container('Programme view', [2, 3, 8, 6], [PROJECTS, ACTIVE]);
const WITH_CONTAINER: DesignView = {
  isStandard: false,
  updatedAt: '2026-11-19T09:00:00Z',
  visuals: [VALUE, PROGRAMME],
};
const NOTHING_SAVED: DesignView = { isStandard: true, updatedAt: null, visuals: [] };

function visualsSent(body: unknown): Visual[] {
  return (body as { visuals: Visual[] }).visuals;
}

/** What the mocked API calculates: a figure per measure for a visual, the same for each child of a container. */
/**
 * What the mocked API calculates, in the backend's shape: a figure per measure; a container no groups and its children always a list (each
 * calculated as a top-level visual); every other visual `children: null`.
 */
function calculated(visual: Visual): VisualResult {
  const { children, ...rest } = visual;
  return visual.visualType === 'CONTAINER'
    ? { ...rest, groups: [], children: (children ?? []).map((child) => calculated(child)) }
    : { ...rest, groups: [group(visual.measures.map(() => 3))], children: null };
}

/**
 * The API takes `children` only on a container (even `[]` elsewhere is NOT_ALLOWED): a container sends a list, every other visual, top-level
 * or child, none.
 */
function expectChildrenOnlyOnContainers(visuals: readonly Visual[]) {
  for (const visual of visuals) {
    if (visual.visualType === 'CONTAINER') {
      expect(Array.isArray(visual.children), `${visual.title.en} sends its children`).toBe(true);
      for (const child of visual.children ?? []) {
        expect(child.visualType).not.toBe('CONTAINER');
        expect(child.children ?? null, `${child.title.en} sends no children`).toBeNull();
      }
    } else {
      expect(visual.children ?? null, `${visual.title.en} sends no children`).toBeNull();
    }
  }
}

function api(design: DesignView = NOTHING_SAVED): MockApi {
  const mock = mockApi();
  withProjectLookups(mock);
  withDashboards(mock, { views: { PORTFOLIO: dashboardView('PORTFOLIO') } });
  return mock
    .on('GET', /^\/portfolio-insights\/design$/, { body: design })
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
        updatedAt: '2026-11-19T10:00:00Z',
        visuals: visualsSent(request.body).map(calculated),
      },
    }));
}

const session = () => sessionFor(['R02']);

/** The settings pane of the selected visual. */
const settings = () => screen.getByRole('region', { name: en('dashboards.designer.properties') });

const lastPreviewed = (mock: MockApi): Visual[] =>
  visualsSent(
    mock.requestsTo('POST', /^\/portfolio-insights\/design\/preview$/).at(-1)?.body ?? {
      visuals: [],
    },
  );

async function saved(mock: MockApi, user: ReturnType<typeof userEvent.setup>): Promise<Visual[]> {
  await user.click(screen.getByRole('button', { name: en('dashboards.designer.save') }));
  await waitFor(() => {
    expect(mock.requestsTo('PUT', /^\/portfolio-insights\/design$/)).toHaveLength(1);
  });
  return visualsSent(mock.requestsTo('PUT', /^\/portfolio-insights\/design$/)[0]?.body);
}

/** A visual's place and size as sent, for comparing layouts. */
const placeOf = (visual: Visual) => ({
  title: visual.title.en,
  visualType: visual.visualType,
  layoutColumn: visual.layoutColumn,
  layoutRow: visual.layoutRow,
  layoutWidth: visual.layoutWidth,
  layoutHeight: visual.layoutHeight,
});

const byTitle = (a: { title: string }, b: { title: string }) => a.title.localeCompare(b.title);

/** Opens a visual's settings by its edit pencil. */
async function edit(user: ReturnType<typeof userEvent.setup>, title: string) {
  await user.click(
    screen.getByRole('button', {
      name: (name) => plain(name) === plain(en('dashboards.designer.editVisual', { title })),
    }),
  );
}

/** The remove button of the selected container's settings: whichever of the designer's remove words it carries. */
function removeButton(): HTMLElement {
  const words = ['remove', 'removeContainer', 'removeSection'].map((key) =>
    en(`dashboards.designer.${key}`),
  );
  return present(
    within(settings())
      .getAllByRole('button')
      .find((button) => words.includes(plain(button.textContent).trim())),
    'The remove button',
  );
}

async function openRemoveDialog(user: ReturnType<typeof userEvent.setup>) {
  renderApp({ path: DESIGNER, session: session() });
  await screen.findByRole('region', { name: 'Programme view' });
  await edit(user, 'Programme view');
  await user.click(removeButton());
  return screen.findByRole('dialog', { name: en('dashboards.designer.removeContainer') });
}

describe('the designer places containers', () => {
  test('a Container from the gallery is sent in the preview as a CONTAINER, 6 by 6, titled in both languages, with no children', async () => {
    const mock = api();
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await screen.findByRole('heading', { level: 1, name: en('dashboards.designer.title') });

    await user.click(
      await screen.findByRole('button', { name: en('dashboards.designer.type.CONTAINER') }),
    );

    await waitFor(() => {
      expect(lastPreviewed(mock).at(-1)).toMatchObject({
        visualType: 'CONTAINER',
        measures: [],
        groupBy: 'NONE',
        layoutWidth: 6,
        layoutHeight: 6,
        title: {
          en: en('dashboards.designer.containerDefaultTitle'),
          ar: translate('ar', 'dashboards.designer.containerDefaultTitle'),
        },
        children: [],
      });
    });
    const sent = present(lastPreviewed(mock).at(-1), 'The container');
    expectChildrenOnlyOnContainers(lastPreviewed(mock));
    // A container draws no figures of its own.
    expect(sent.values ?? null).toBeNull();
    expect(sent.groupField ?? null).toBeNull();
    expect(sent.section ?? null).toBeNull();
    expect(sent.widgetCode ?? null).toBeNull();
    expect(sent.axes ?? null).toBeNull();
    expect(
      await screen.findByRole('region', { name: en('dashboards.designer.containerDefaultTitle') }),
    ).toBeTruthy();
  });

  test('a design read with a container draws its two children inside the container, each at its inner column', async () => {
    api(WITH_CONTAINER);
    renderApp({ path: DESIGNER, session: session() });

    const frame = await screen.findByRole('region', { name: 'Programme view' });
    const inside = within(frame);
    const projects = inside.getByRole('region', { name: 'All projects' });
    const active = inside.getByRole('region', { name: 'Active projects' });
    // The card outside the container is not in it.
    expect(inside.queryByRole('region', { name: 'Total value' })).toBeNull();
    expect(screen.getByRole('region', { name: 'Total value' })).toBeTruthy();
    // Each child at its place on the container's own grid of columns.
    expect(projects.style.getPropertyValue('--visual-column')).toBe('1');
    expect(active.style.getPropertyValue('--visual-column')).toBe('5');
    expect(Number(active.style.getPropertyValue('--visual-row'))).toBeGreaterThan(
      Number(projects.style.getPropertyValue('--visual-row')),
    );
    // A child is selectable and editable as any visual: its own edit pencil, inside the frame.
    expect(
      inside.getByRole('button', {
        name: (name) =>
          plain(name) === plain(en('dashboards.designer.editVisual', { title: 'All projects' })),
      }),
    ).toBeTruthy();
  });

  test('Save sends the children nested under their container, at their inner places', async () => {
    const mock = api(WITH_CONTAINER);
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await screen.findByRole('region', { name: 'Programme view' });

    // A change, so the design can be saved: the container's English title.
    await edit(user, 'Programme view');
    const titleEn = within(settings()).getByLabelText(en('dashboards.designer.titleEn'));
    await user.clear(titleEn);
    await user.type(titleEn, 'Programme');

    const sent = await saved(mock, user);
    expectChildrenOnlyOnContainers(sent);
    expect(sent).toHaveLength(2);
    const box = present(
      sent.find((visual) => visual.visualType === 'CONTAINER'),
      'The container sent',
    );
    expect(placeOf(box)).toEqual({
      title: 'Programme',
      visualType: 'CONTAINER',
      layoutColumn: 2,
      layoutRow: 3,
      layoutWidth: 8,
      layoutHeight: 6,
    });
    expect((box.children ?? []).map(placeOf).sort(byTitle)).toEqual(
      [
        { ...placeOf(ACTIVE), title: 'Active projects' },
        { ...placeOf(PROJECTS), title: 'All projects' },
      ].sort(byTitle),
    );
    // Children carry their own measures, and no designer key; only the container has children.
    expect(box.children?.find((child) => child.title.en === 'Active projects')?.measures).toEqual([
      'IN_PROGRESS',
    ]);
    for (const child of box.children ?? []) {
      expect('key' in child).toBe(false);
      expect(child.children ?? null).toBeNull();
    }
    const other = present(sent.find((visual) => visual.visualType !== 'CONTAINER'));
    expect(other.children ?? null).toBeNull();
  });
});

describe('removing a container asks what becomes of its visuals', () => {
  test('the dialog offers to remove them, to keep them, or to cancel', async () => {
    api(WITH_CONTAINER);
    const user = userEvent.setup();
    const dialog = within(await openRemoveDialog(user));
    expect(
      dialog.getByRole('button', { name: en('dashboards.designer.removeWithVisuals') }),
    ).toBeTruthy();
    expect(
      dialog.getByRole('button', { name: en('dashboards.designer.keepVisuals') }),
    ).toBeTruthy();
    await user.click(dialog.getByRole('button', { name: en('dashboards.designer.cancel') }));

    await waitFor(() => {
      expect(
        screen.queryByRole('dialog', { name: en('dashboards.designer.removeContainer') }),
      ).toBeNull();
    });
    // Cancel changes nothing.
    expect(screen.getByRole('region', { name: 'Programme view' })).toBeTruthy();
    expect(screen.getByText(en('dashboards.designer.savedState'))).toBeTruthy();
  });

  test('"Remove the container and its visuals" sends neither the container nor its children', async () => {
    const mock = api(WITH_CONTAINER);
    const user = userEvent.setup();
    const dialog = within(await openRemoveDialog(user));
    await user.click(
      dialog.getByRole('button', { name: en('dashboards.designer.removeWithVisuals') }),
    );

    await waitFor(() => {
      expect(screen.queryByRole('region', { name: 'Programme view' })).toBeNull();
    });
    expect(screen.queryByRole('region', { name: 'All projects' })).toBeNull();
    expect(screen.queryByRole('region', { name: 'Active projects' })).toBeNull();

    const sent = await saved(mock, user);
    expectChildrenOnlyOnContainers(sent);
    expect(sent.map(placeOf)).toEqual([placeOf(VALUE)]);
    expect(sent[0]?.children ?? null).toBeNull();
  });

  test('"Keep its visuals on the dashboard" sends each child as a top-level visual at its place on screen, and no container', async () => {
    const mock = api(WITH_CONTAINER);
    const user = userEvent.setup();
    const dialog = within(await openRemoveDialog(user));
    await user.click(dialog.getByRole('button', { name: en('dashboards.designer.keepVisuals') }));

    await waitFor(() => {
      expect(screen.queryByRole('region', { name: 'Programme view' })).toBeNull();
    });
    expect(screen.getByRole('region', { name: 'All projects' })).toBeTruthy();
    expect(screen.getByRole('region', { name: 'Active projects' })).toBeTruthy();

    const sent = await saved(mock, user);
    expectChildrenOnlyOnContainers(sent);
    expect(sent.some((visual) => visual.visualType === 'CONTAINER')).toBe(false);
    expect(sent.every((visual) => (visual.children ?? null) === null)).toBe(true);
    // Container column + child column; container row + 1 (its title bar) + child row. Nothing else is there, so settling moves nothing.
    expect(sent.map(placeOf).sort(byTitle)).toEqual(
      [
        placeOf(VALUE),
        { ...placeOf(PROJECTS), layoutColumn: 2, layoutRow: 4 },
        { ...placeOf(ACTIVE), layoutColumn: 6, layoutRow: 5 },
      ].sort(byTitle),
    );
  });

  test('kept visuals never overlap the others, and a visual below the container stays where it was', async () => {
    // No top-level visual can stand where a kept child lands (the container held that room), so settling must leave the others in place.
    const below = card('Below', 'PROJECTS', [0, 9, 12, 2]);
    const mock = api({ ...WITH_CONTAINER, visuals: [VALUE, PROGRAMME, below] });
    const user = userEvent.setup();
    const dialog = within(await openRemoveDialog(user));
    await user.click(dialog.getByRole('button', { name: en('dashboards.designer.keepVisuals') }));
    await waitFor(() => {
      expect(screen.queryByRole('region', { name: 'Programme view' })).toBeNull();
    });

    const sent = await saved(mock, user);
    expectChildrenOnlyOnContainers(sent);
    const overlap = sent.some((a, i) =>
      sent.some(
        (b, j) =>
          j > i &&
          a.layoutColumn < b.layoutColumn + b.layoutWidth &&
          b.layoutColumn < a.layoutColumn + a.layoutWidth &&
          a.layoutRow < b.layoutRow + b.layoutHeight &&
          b.layoutRow < a.layoutRow + a.layoutHeight,
      ),
    );
    expect(overlap).toBe(false);
    expect(sent.find((visual) => visual.title.en === 'Below')).toMatchObject({
      layoutColumn: 0,
      layoutRow: 9,
    });
  });
});

describe('the dashboard draws containers', () => {
  const withheldValue = card('Value in SAR', 'VALUE_SAR', [0, 2, 4, 2], [null]);

  test('a container draws its frame and title with its children inside; a withheld child is left out', async () => {
    api({
      ...WITH_CONTAINER,
      visuals: [
        VALUE,
        container('Programme view', [2, 3, 8, 6], [PROJECTS, ACTIVE, withheldValue]),
      ],
    });
    renderApp({ path: DASHBOARD, session: session() });

    const frame = await screen.findByRole('region', { name: 'Programme view' });
    const inside = within(frame);
    expect(inside.getByText('All projects')).toBeTruthy();
    expect(inside.getByText('Active projects')).toBeTruthy();
    expect(inside.queryByText('Value in SAR')).toBeNull();
    expect(screen.queryByText('Value in SAR')).toBeNull();
    // The card outside is drawn outside.
    expect(inside.queryByText('Total value')).toBeNull();
    expect(screen.getByText('Total value')).toBeTruthy();
  });

  test('a container with no child left to show is left out', async () => {
    api({
      ...WITH_CONTAINER,
      visuals: [
        VALUE,
        container('Programme view', [2, 3, 8, 6], [PROJECTS]),
        container('Finance', [0, 9, 6, 4], [{ ...withheldValue, layoutRow: 0 }]),
        container('Empty box', [6, 9, 6, 4], []),
      ],
    });
    renderApp({ path: DASHBOARD, session: session() });

    expect(await screen.findByRole('region', { name: 'Programme view' })).toBeTruthy();
    expect(screen.queryByRole('region', { name: 'Finance' })).toBeNull();
    expect(screen.queryByText('Finance')).toBeNull();
    expect(screen.queryByRole('region', { name: 'Empty box' })).toBeNull();
    expect(screen.queryByText('Empty box')).toBeNull();
  });
});

describe('decimal sizes in the settings (20 Nov 2026, decimal layout contract)', () => {
  const overlapping = (visuals: readonly Visual[]) =>
    visuals.some((a, i) =>
      visuals.some(
        (b, j) =>
          j > i &&
          a.layoutColumn < b.layoutColumn + b.layoutWidth &&
          b.layoutColumn < a.layoutColumn + a.layoutWidth &&
          a.layoutRow < b.layoutRow + b.layoutHeight &&
          b.layoutRow < a.layoutRow + a.layoutHeight,
      ),
    );

  test('Width takes 4.5 and Height 2.7: drawn and sent so, while 4.55 is not taken', async () => {
    const mock = api(WITH_CONTAINER);
    const user = userEvent.setup();
    renderApp({ path: DESIGNER, session: session() });
    await screen.findByRole('region', { name: 'Total value' });

    await edit(user, 'Total value');
    const width = within(settings()).getByLabelText(en('dashboards.designer.width'));
    const height = within(settings()).getByLabelText(en('dashboards.designer.height'));
    expect(width.getAttribute('step')).toBe('0.1');
    expect(height.getAttribute('step')).toBe('0.1');
    await user.clear(width);
    await user.type(width, '4.5');
    await user.clear(height);
    await user.type(height, '2.7');

    await waitFor(() => {
      expect(lastPreviewed(mock).find((visual) => visual.title.en === 'Total value')).toMatchObject(
        { layoutColumn: 0, layoutRow: 0, layoutWidth: 4.5, layoutHeight: 2.7 },
      );
    });
    const drawn = screen.getByRole('region', { name: 'Total value' });
    expect(drawn.style.getPropertyValue('--visual-width')).toBe('4.5');
    expect(drawn.style.getPropertyValue('--visual-height')).toBe('2.7');

    // Two decimal places are not a size the API takes: the visual keeps 4.5, and the field shows it again once left.
    await user.clear(width);
    await user.type(width, '4.55');
    expect(drawn.style.getPropertyValue('--visual-width')).toBe('4.5');
    await user.tab();
    expect((width as HTMLInputElement).value).toBe('4.5');

    // A child 4.7 wide in its container: what it now covers moves down, nothing overlaps, and the decimals are saved.
    await edit(user, 'All projects');
    const childWidth = within(settings()).getByLabelText(en('dashboards.designer.width'));
    await user.clear(childWidth);
    await user.type(childWidth, '4.7');

    const sent = await saved(mock, user);
    expect(
      placeOf(present(sent.find((visual) => visual.title.en === 'Total value'))),
    ).toMatchObject({ layoutWidth: 4.5, layoutHeight: 2.7 });
    const box = present(sent.find((visual) => visual.visualType === 'CONTAINER'));
    expect(
      present(box.children?.find((child) => child.title.en === 'All projects')).layoutWidth,
    ).toBe(4.7);
    expect(overlapping(sent)).toBe(false);
    expect(overlapping(box.children ?? [])).toBe(false);
  });
});
