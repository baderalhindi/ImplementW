import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import { dashboardView, withDashboards } from '@/test/dashboardFixtures.ts';
import { sessionFor } from '@/test/identityAccessFixtures.ts';
import { type MockApi, mockApi, problem } from '@/test/mockApi.ts';
import { designFieldsFixture } from '@/test/portfolioInsightFixtures.ts';
import { withProjectLookups } from '@/test/projectFixtures.ts';
import { renderApp } from '@/test/renderApp.tsx';

import {
  type DesignView,
  type TextBox,
  type Visual,
  type VisualGroup,
  type VisualResult,
} from './designApi.ts';

// The gallery's Text box (20 Nov 2026, docs/architecture/dashboard-designer-map-text.md; its Map is in designerMapVisual.test.tsx): a TEXT
// visual carrying `text` (and only it does), drawn in the page's language without a title bar on the dashboard. The API is mocked: it echoes
// what it is sent, as the backend does.

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

const group = (values: (number | null)[]): VisualGroup => ({
  key: null,
  label: null,
  projects: 3,
  measured: 3,
  valueEntered: 3,
  values,
  onPlan: null,
});

const CARD: VisualResult = {
  title: { ar: 'كل المشاريع', en: 'All projects' },
  visualType: 'CARD',
  measures: ['PROJECTS'],
  groupBy: 'NONE',
  layoutColumn: 0,
  layoutRow: 0,
  layoutWidth: 4,
  layoutHeight: 2,
  groups: [group([3])],
  children: null,
};

const MAP: VisualResult = {
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
  layoutWidth: 8,
  layoutHeight: 5,
  groups: [],
  children: [],
};

const design = (visuals: VisualResult[]): DesignView => ({
  isStandard: false,
  updatedAt: '2026-11-20T09:00:00Z',
  visuals,
});

function visualsSent(body: unknown): Visual[] {
  return (body as { visuals: Visual[] }).visuals;
}

/** What the mocked API calculates, in the backend's shape: a text box echoes its `text` with no groups, a container its children. */
function calculated(visual: Visual): VisualResult {
  const { children, ...rest } = visual;
  if (visual.visualType === 'CONTAINER') {
    return { ...rest, groups: [], children: (children ?? []).map(calculated) };
  }
  return visual.visualType === 'TEXT' || visual.visualType === 'SECTION'
    ? { ...rest, groups: [], children: null }
    : { ...rest, groups: [group(visual.measures.map(() => 3))], children: null };
}

function api(view: DesignView = design([CARD])): MockApi {
  const mock = mockApi();
  withProjectLookups(mock);
  withDashboards(mock, { views: { PORTFOLIO: dashboardView('PORTFOLIO') } });
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

/** Only a text box sends `text`. */
function expectTextOnlyOnTextBoxes(visuals: readonly Visual[]) {
  for (const visual of everyVisual(visuals)) {
    if (visual.visualType === 'TEXT') {
      expect(visual.text, `${visual.title.en} sends its text`).toBeTruthy();
    } else {
      expect('text' in visual, `${visual.title.en} sends no text`).toBe(false);
    }
  }
}

const textTile = () => screen.getByRole('button', { name: en('dashboards.designer.type.TEXT') });

async function openDesigner(language: 'en' | 'ar' = 'en') {
  renderApp({ path: DESIGNER, session: session(), language });
  await screen.findByRole('heading', {
    level: 1,
    name: translate(language, 'dashboards.designer.title'),
  });
}

/**
 * The designer's canvas measured as a browser would: 1200 px across at the window's top-left, so a column is 100 px and a row 72 px (the
 * designer's fallback where no style sets it). jsdom lays nothing out.
 */
function measuredCanvas(): HTMLElement {
  const canvas = present(document.querySelector<HTMLElement>('.designer__canvas'), 'The canvas');
  Object.defineProperty(canvas, 'clientWidth', { configurable: true, value: 1200 });
  canvas.getBoundingClientRect = () => ({
    left: 0,
    top: 0,
    right: 1200,
    bottom: 2000,
    width: 1200,
    height: 2000,
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

describe('the gallery places a text box', () => {
  test('a click sends a TEXT visual, 6 by 2, titled "Text box", with its text in both languages, normal, at the start, not bold, on the card colour', async () => {
    const mock = api();
    const user = userEvent.setup();
    await openDesigner();

    await user.click(textTile());

    await waitFor(() => {
      expect(lastPreviewed(mock).at(-1)).toMatchObject({
        visualType: 'TEXT',
        measures: [],
        groupBy: 'NONE',
        layoutWidth: 6,
        layoutHeight: 2,
        title: {
          en: en('dashboards.designer.textDefaultTitle'),
          ar: ar('dashboards.designer.textDefaultTitle'),
        },
        text: {
          en: en('dashboards.designer.textDefault'),
          ar: ar('dashboards.designer.textDefault'),
          size: 'NORMAL',
          align: 'START',
          bold: false,
          // A new text box is on the card colour (the user: "background to be white"), every part of its format sent.
          fontSize: null,
          italic: false,
          underline: false,
          color: null,
          background: 'CARD',
          border: false,
          borderColor: null,
          verticalAlign: 'TOP',
        } satisfies TextBox,
      });
    });
    const sent = present(lastPreviewed(mock).at(-1));
    // A text box draws no figures.
    for (const key of [
      'values',
      'groupField',
      'filters',
      'section',
      'widgetCode',
      'columns',
    ] as const) {
      expect(sent[key] ?? null, key).toBeNull();
    }
    expect(sent.axes ?? null).toBeNull();
    expect(sent.children ?? null).toBeNull();
    expectTextOnlyOnTextBoxes(lastPreviewed(mock));
    // Selected: its settings are open.
    expect(within(settings()).getByLabelText(en('dashboards.designer.textEn'))).toBeTruthy();
  });

  test('the settings edit each part of the text, which is sent and drawn so, line breaks kept', async () => {
    const mock = api();
    const user = userEvent.setup();
    await openDesigner();
    await user.click(textTile());

    const pane = within(settings());
    const english = pane.getByLabelText(en('dashboards.designer.textEn'));
    const arabic = pane.getByLabelText(en('dashboards.designer.textAr'));
    expect(english.tagName).toBe('TEXTAREA');
    expect(english.getAttribute('dir')).toBe('ltr');
    expect(arabic.getAttribute('dir')).toBe('rtl');
    await user.clear(english);
    await user.type(english, 'Quarter review{Enter}Second line');
    await user.clear(arabic);
    await user.type(arabic, 'مراجعة الربع{Enter}السطر الثاني');
    await user.click(
      pane.getByRole('button', { name: en('dashboards.designer.textSizes.HEADING') }),
    );
    await user.click(
      pane.getByRole('button', { name: en('dashboards.designer.textAligns.CENTER') }),
    );
    await user.click(pane.getByRole('button', { name: en('dashboards.designer.textBold') }));
    expect(
      pane
        .getByRole('button', { name: en('dashboards.designer.textBold') })
        .getAttribute('aria-pressed'),
    ).toBe('true');

    await waitFor(() => {
      expect(
        lastPreviewed(mock).find((visual) => visual.visualType === 'TEXT')?.text,
      ).toMatchObject({
        en: 'Quarter review\nSecond line',
        ar: 'مراجعة الربع\nالسطر الثاني',
        size: 'HEADING',
        align: 'CENTER',
        bold: true,
      });
    });
    expectTextOnlyOnTextBoxes(lastPreviewed(mock));

    // Drawn in the page's language, at its size, alignment and weight, its line break kept.
    const box = screen.getByRole('region', { name: en('dashboards.designer.textDefaultTitle') });
    const words = present(box.querySelector('.visual-text'), 'The text');
    expect(words.textContent).toBe('Quarter review\nSecond line');
    expect(words.className).toContain('visual-text--heading');
    expect(words.className).toContain('visual-text--center');
    expect(words.className).toContain('visual-text--bold');
  });

  test('an empty text says it is required', async () => {
    api();
    const user = userEvent.setup();
    await openDesigner();
    await user.click(textTile());
    const english = within(settings()).getByLabelText(en('dashboards.designer.textEn'));
    await user.clear(english);
    expect(english.getAttribute('aria-invalid')).toBe('true');
    expect(within(settings()).getByText(en('dashboards.designer.textRequired'))).toBeTruthy();
  });

  test('in Arabic the designer draws the Arabic text', async () => {
    api();
    const user = userEvent.setup();
    await openDesigner('ar');
    await user.click(
      screen.getByRole('button', { name: translate('ar', 'dashboards.designer.type.TEXT') }),
    );
    const box = await screen.findByRole('region', {
      name: ar('dashboards.designer.textDefaultTitle'),
    });
    expect(box.querySelector('.visual-text')?.textContent).toBe(
      ar('dashboards.designer.textDefault'),
    );
  });

  test('a text box dragged onto a container becomes its child, still carrying its text', async () => {
    const mock = api(design([CARD, CONTAINER]));
    await openDesigner();
    await screen.findByRole('region', { name: 'Notes' });
    measuredCanvas();

    // Into the container's inner grid: column 1, the container's second row (its first inner row) and below.
    dragTile(textTile(), 110, 3 * 72 + 30);

    await waitFor(() => {
      const box = lastPreviewed(mock).find((visual) => visual.visualType === 'CONTAINER');
      expect(box?.children?.map((child) => child.visualType)).toEqual(['TEXT']);
    });
    const box = present(lastPreviewed(mock).find((visual) => visual.visualType === 'CONTAINER'));
    expect(box.children?.[0]?.text?.en).toBe(en('dashboards.designer.textDefault'));
    expect(lastPreviewed(mock).filter((visual) => visual.visualType === 'TEXT')).toHaveLength(0);
    expectTextOnlyOnTextBoxes(lastPreviewed(mock));
    expect(
      within(screen.getByRole('region', { name: 'Notes' })).getByRole('region', {
        name: en('dashboards.designer.textDefaultTitle'),
      }),
    ).toBeTruthy();
  });

  test('Save sends text on the text box only', async () => {
    const mock = api(design([CARD, MAP]));
    const user = userEvent.setup();
    await openDesigner();
    await screen.findByRole('region', { name: 'Projects by location' });
    await user.click(textTile());
    await user.click(screen.getByRole('button', { name: en('dashboards.designer.save') }));
    await waitFor(() => {
      expect(mock.requestsTo('PUT', /^\/portfolio-insights\/design$/)).toHaveLength(1);
    });
    const sent = visualsSent(mock.requestsTo('PUT', /^\/portfolio-insights\/design$/)[0]?.body);
    expect(sent.map((visual) => visual.visualType).sort()).toEqual(['CARD', 'SECTION', 'TEXT']);
    expectTextOnlyOnTextBoxes(sent);
  });
});

describe('a text box the API would refuse', () => {
  test('English words in Arabic letters say so under the field, and the rest of the design is still previewed without the text box', async () => {
    const mock = api();
    const user = userEvent.setup();
    await openDesigner();
    await user.click(textTile());
    await waitFor(() => {
      expect(lastPreviewed(mock).some((visual) => visual.visualType === 'TEXT')).toBe(true);
    });

    const english = within(settings()).getByLabelText(en('dashboards.designer.textEn'));
    await user.clear(english);
    await user.type(english, 'مرحبا');

    expect(english.getAttribute('aria-invalid')).toBe('true');
    expect(within(settings()).getByText(en('common.fieldErrors.notEnglish'))).toBeTruthy();
    await waitFor(() => {
      expect(lastPreviewed(mock).some((visual) => visual.visualType === 'TEXT')).toBe(false);
    });
    expect(lastPreviewed(mock).map((visual) => visual.title.en)).toEqual(['All projects']);
    // The text box stays on the canvas, drawn as typed.
    expect(
      screen.getByRole('region', { name: en('dashboards.designer.textDefaultTitle') }),
    ).toBeTruthy();
  });

  test('Arabic words with no Arabic letter, a tab, and more than 2000 characters each say why', async () => {
    api();
    const user = userEvent.setup();
    await openDesigner();
    await user.click(textTile());
    const pane = within(settings());
    const arabic = pane.getByLabelText(en('dashboards.designer.textAr'));

    fireEvent.change(arabic, { target: { value: 'Hello' } });
    expect(pane.getByText(en('common.fieldErrors.notArabic'))).toBeTruthy();
    fireEvent.change(arabic, { target: { value: 'مرحبا\tبكم' } });
    expect(pane.getByText(en('dashboards.designer.textInvalidCharacter'))).toBeTruthy();
    fireEvent.change(arabic, { target: { value: 'م'.repeat(2001) } });
    expect(pane.getByText(en('dashboards.designer.textTooLong'))).toBeTruthy();
    fireEvent.change(arabic, { target: { value: 'مرحبا' } });
    expect(arabic.getAttribute('aria-invalid')).toBeNull();
  });

  test('a line break typed as CR LF is sent as LF', async () => {
    const mock = api();
    const user = userEvent.setup();
    await openDesigner();
    await user.click(textTile());
    fireEvent.change(within(settings()).getByLabelText(en('dashboards.designer.textEn')), {
      target: { value: 'First\r\nSecond' },
    });
    await waitFor(() => {
      expect(lastPreviewed(mock).find((visual) => visual.visualType === 'TEXT')?.text?.en).toBe(
        'First\nSecond',
      );
    });
  });

  test("a refusal of the preview at the text box's words shows under that field", async () => {
    const mock = api(design([CARD]));
    mock.on('POST', /^\/portfolio-insights\/design\/preview$/, (request) =>
      visualsSent(request.body).some((visual) => visual.visualType === 'TEXT')
        ? problem(400, 'VALIDATION_FAILED', [{ field: 'visuals[1].text.en', code: 'NOT_ENGLISH' }])
        : {
            body: {
              isStandard: false,
              updatedAt: null,
              visuals: visualsSent(request.body).map(calculated),
            },
          },
    );
    const user = userEvent.setup();
    await openDesigner();
    await user.click(textTile());

    const pane = within(settings());
    expect(await pane.findByText(en('common.fieldErrors.notEnglish'))).toBeTruthy();
    expect(pane.getByLabelText(en('dashboards.designer.textEn')).getAttribute('aria-invalid')).toBe(
      'true',
    );
    expect(
      pane.getByLabelText(en('dashboards.designer.textAr')).getAttribute('aria-invalid'),
    ).toBeNull();
  });
});

describe('the dashboard draws a text box', () => {
  const TEXT: VisualResult = {
    title: { ar: 'مقدمة', en: 'Introduction' },
    visualType: 'TEXT',
    measures: [],
    groupBy: 'NONE',
    layoutColumn: 4,
    layoutRow: 0,
    layoutWidth: 8,
    layoutHeight: 2,
    text: {
      ar: 'مرحبًا\nبالجميع',
      en: 'Welcome\nto all',
      size: 'LARGE',
      align: 'END',
      bold: false,
    },
    groups: [],
    children: null,
  };

  test('as its words in the page language, with no title bar', async () => {
    api(design([CARD, TEXT]));
    renderApp({ path: DASHBOARD, session: session() });

    const box = await screen.findByRole('region', { name: 'Introduction' });
    expect(box.querySelector('h3')).toBeNull();
    expect(within(box).queryByText('Introduction')).toBeNull();
    const words = present(box.querySelector('.visual-text'));
    expect(words.textContent).toBe('Welcome\nto all');
    expect(words.className).toContain('visual-text--large');
    expect(words.className).toContain('visual-text--end');
    expect(words.className).not.toContain('visual-text--bold');
    // No card round it.
    expect(box.classList.contains('widget')).toBe(false);
  });

  test('in Arabic, the Arabic words', async () => {
    api(design([CARD, TEXT]));
    renderApp({ path: DASHBOARD, session: session(), language: 'ar' });
    const box = await screen.findByRole('region', { name: 'مقدمة' });
    expect(box.querySelector('.visual-text')?.textContent).toBe('مرحبًا\nبالجميع');
  });
});

describe("a text box's format", () => {
  const sentText = (mock: MockApi) =>
    lastPreviewed(mock).find((visual) => visual.visualType === 'TEXT')?.text;
  const textBox = () =>
    screen.getByRole('region', { name: en('dashboards.designer.textDefaultTitle') });
  const words = () => present(textBox().querySelector<HTMLElement>('.visual-text'), 'The text');
  const chooser = (label: string) => within(within(settings()).getByRole('group', { name: label }));

  async function withTextBox() {
    const mock = api();
    const user = userEvent.setup();
    await openDesigner();
    await user.click(textTile());
    await waitFor(() => {
      expect(sentText(mock)).toBeTruthy();
    });
    return { mock, user, pane: within(settings()) };
  }

  test('a custom font size is sent and drawn; a size outside 8–96 says so and is not sent; a preset clears it', async () => {
    const { mock, user, pane } = await withTextBox();
    const size = pane.getByLabelText(en('dashboards.designer.textCustomSize'));

    await user.type(size, '32');
    await waitFor(() => {
      expect(sentText(mock)?.fontSize).toBe(32);
    });
    expect(words().style.fontSize).toBe('32px');
    // No preset is pressed while a size of its own is set.
    expect(
      pane
        .getByRole('button', { name: en('dashboards.designer.textSizes.NORMAL') })
        .getAttribute('aria-pressed'),
    ).toBe('false');

    // Outside 8–96: said under the field, and the size it had is kept.
    fireEvent.change(size, { target: { value: '120' } });
    expect(pane.getByText(en('dashboards.designer.textCustomSizeError'))).toBeTruthy();
    expect(size.getAttribute('aria-invalid')).toBe('true');
    expect(words().style.fontSize).toBe('32px');

    await user.click(pane.getByRole('button', { name: en('dashboards.designer.textSizes.LARGE') }));
    await waitFor(() => {
      expect(sentText(mock)).toMatchObject({ size: 'LARGE', fontSize: null });
    });
    expect((size as HTMLInputElement).value).toBe('');
    expect(words().style.fontSize).toBe('');
    expect(words().className).toContain('visual-text--large');
  });

  test('italic and underline are sent and drawn', async () => {
    const { mock, user, pane } = await withTextBox();
    await user.click(pane.getByRole('button', { name: en('dashboards.designer.textItalic') }));
    await user.click(pane.getByRole('button', { name: en('dashboards.designer.textUnderline') }));
    await waitFor(() => {
      expect(sentText(mock)).toMatchObject({ italic: true, underline: true });
    });
    expect(words().style.fontStyle).toBe('italic');
    expect(words().style.textDecoration).toBe('underline');
    expect(
      pane
        .getByRole('button', { name: en('dashboards.designer.textItalic') })
        .getAttribute('aria-pressed'),
    ).toBe('true');
  });

  test('a text colour from the swatches, a custom one, and the default', async () => {
    const { mock, user } = await withTextBox();
    const colour = () => chooser(en('dashboards.designer.textColour'));

    await user.click(colour().getByRole('button', { name: en('dashboards.designer.colours.RED') }));
    await waitFor(() => {
      expect(sentText(mock)?.color).toBe('#B8322A');
    });
    expect(words().style.color).toBe('rgb(184, 50, 42)');
    expect(colour().getByText(en('dashboards.designer.colours.RED'))).toBeTruthy();

    fireEvent.change(
      colour().getByLabelText(
        en('dashboards.designer.colourCustomLabel', {
          label: en('dashboards.designer.textColour'),
        }),
      ),
      { target: { value: '#12ab34' } },
    );
    await waitFor(() => {
      expect(sentText(mock)?.color).toBe('#12AB34');
    });

    await user.click(
      colour().getByRole('button', { name: en('dashboards.designer.colourDefault') }),
    );
    await waitFor(() => {
      expect(sentText(mock)?.color).toBeNull();
    });
    expect(words().style.color).toBe('');
  });

  test('the background: the card colour for a new box, none, or a colour', async () => {
    const { mock, user } = await withTextBox();
    const background = () => chooser(en('dashboards.designer.textBackground'));
    expect(
      background()
        .getByRole('button', { name: en('dashboards.designer.textBackgrounds.CARD') })
        .getAttribute('aria-pressed'),
    ).toBe('true');
    expect(textBox().classList.contains('visual--text-card')).toBe(true);

    await user.click(
      background().getByRole('button', { name: en('dashboards.designer.textBackgrounds.NONE') }),
    );
    await waitFor(() => {
      expect(sentText(mock)?.background).toBe('NONE');
    });
    expect(textBox().classList.contains('visual--text-card')).toBe(false);
    expect(textBox().classList.contains('visual--text-fill')).toBe(false);

    await user.click(
      background().getByRole('button', { name: en('dashboards.designer.colours.BLUE') }),
    );
    await waitFor(() => {
      expect(sentText(mock)?.background).toBe('#3B78C4');
    });
    expect(textBox().classList.contains('visual--text-fill')).toBe(true);
    expect(textBox().style.getPropertyValue('--text-box-fill')).toBe('#3B78C4');
  });

  test('a border, in the theme colour or one chosen', async () => {
    const { mock, user, pane } = await withTextBox();
    expect(
      pane.queryByRole('group', { name: en('dashboards.designer.textBorderColour') }),
    ).toBeNull();

    await user.click(
      pane.getByRole('checkbox', { name: en('dashboards.designer.textBorderShow') }),
    );
    await waitFor(() => {
      expect(sentText(mock)).toMatchObject({ border: true, borderColor: null });
    });
    expect(textBox().classList.contains('visual--text-border')).toBe(true);
    expect(textBox().style.getPropertyValue('--text-box-border')).toBe('');

    await user.click(
      chooser(en('dashboards.designer.textBorderColour')).getByRole('button', {
        name: en('dashboards.designer.colours.GREEN'),
      }),
    );
    await waitFor(() => {
      expect(sentText(mock)?.borderColor).toBe('#3FB27F');
    });
    expect(textBox().style.getPropertyValue('--text-box-border')).toBe('#3FB27F');
  });

  test('the vertical alignment is sent and drawn', async () => {
    const { mock, user, pane } = await withTextBox();
    expect(words().className).toContain('visual-text--top');
    await user.click(
      pane.getByRole('button', { name: en('dashboards.designer.textVerticalAligns.MIDDLE') }),
    );
    await waitFor(() => {
      expect(sentText(mock)?.verticalAlign).toBe('MIDDLE');
    });
    expect(words().className).toContain('visual-text--middle');
    await user.click(
      pane.getByRole('button', { name: en('dashboards.designer.textVerticalAligns.BOTTOM') }),
    );
    expect(words().className).toContain('visual-text--bottom');
  });

  test('a text colour under 3:1 on its background in the current theme says so, and is kept', async () => {
    const { mock, user } = await withTextBox();
    const colour = () => chooser(en('dashboards.designer.textColour'));
    // White on the card colour, white in the light theme.
    await user.click(
      colour().getByRole('button', { name: en('dashboards.designer.colours.WHITE') }),
    );
    expect(colour().getByText(en('dashboards.designer.textLowContrast'))).toBeTruthy();
    await waitFor(() => {
      expect(sentText(mock)?.color).toBe('#FFFFFF');
    });
    await user.click(
      colour().getByRole('button', { name: en('dashboards.designer.colours.BLACK') }),
    );
    expect(colour().queryByText(en('dashboards.designer.textLowContrast'))).toBeNull();
  });

  test('in the dark theme, red on the dark card colour says so', async () => {
    document.documentElement.dataset.theme = 'dark';
    const { user } = await withTextBox();
    const colour = () => chooser(en('dashboards.designer.textColour'));
    await user.click(colour().getByRole('button', { name: en('dashboards.designer.colours.RED') }));
    expect(colour().getByText(en('dashboards.designer.textLowContrast'))).toBeTruthy();
  });
});

describe('a text box saved before the format', () => {
  const OLD: VisualResult = {
    title: { ar: 'مقدمة', en: 'Introduction' },
    visualType: 'TEXT',
    measures: [],
    groupBy: 'NONE',
    layoutColumn: 0,
    layoutRow: 2,
    layoutWidth: 8,
    layoutHeight: 2,
    text: { ar: 'مرحبًا', en: 'Welcome', size: 'NORMAL', align: 'START', bold: false },
    groups: [],
    children: null,
  };

  test('draws as before on the dashboard: no background, no border, at the top', async () => {
    api(design([CARD, OLD]));
    renderApp({ path: DASHBOARD, session: session() });
    const box = await screen.findByRole('region', { name: 'Introduction' });
    expect(box.className).toBe('visual visual--text');
    const shown = present(box.querySelector<HTMLElement>('.visual-text'));
    expect(shown.getAttribute('style')).toBeNull();
    expect(shown.className).toContain('visual-text--top');
  });

  test('opens in the designer with no background, and is sent with every part at what it draws', async () => {
    const mock = api(design([CARD, OLD]));
    const user = userEvent.setup();
    await openDesigner();
    const box = await screen.findByRole('region', { name: 'Introduction' });
    expect(box.classList.contains('visual--text-card')).toBe(false);
    await user.click(
      screen.getByRole('button', {
        name: (name) =>
          plain(name) === plain(en('dashboards.designer.editVisual', { title: 'Introduction' })),
      }),
    );
    expect(
      within(
        within(settings()).getByRole('group', { name: en('dashboards.designer.textBackground') }),
      )
        .getByRole('button', { name: en('dashboards.designer.textBackgrounds.NONE') })
        .getAttribute('aria-pressed'),
    ).toBe('true');
    // Nothing changed: the design read is the design saved.
    expect(
      screen.getByRole('button', { name: en('dashboards.designer.save') }).hasAttribute('disabled'),
    ).toBe(true);
    await waitFor(() => {
      expect(lastPreviewed(mock).find((visual) => visual.visualType === 'TEXT')?.text).toEqual({
        ...OLD.text,
        fontSize: null,
        italic: false,
        underline: false,
        color: null,
        background: 'NONE',
        border: false,
        borderColor: null,
        verticalAlign: 'TOP',
      });
    });
  });
});

describe('the dashboard draws a formatted text box', () => {
  test('its size, styles, colour, card background, border and vertical alignment', async () => {
    api(
      design([
        CARD,
        {
          title: { ar: 'تنبيه', en: 'Notice' },
          visualType: 'TEXT',
          measures: [],
          groupBy: 'NONE',
          layoutColumn: 0,
          layoutRow: 2,
          layoutWidth: 8,
          layoutHeight: 3,
          text: {
            ar: 'تنبيه',
            en: 'Heads up',
            size: 'NORMAL',
            align: 'CENTER',
            bold: true,
            fontSize: 32,
            italic: true,
            underline: false,
            color: '#B8322A',
            background: 'CARD',
            border: true,
            borderColor: '#3FB27F',
            verticalAlign: 'MIDDLE',
          },
          groups: [],
          children: null,
        },
      ]),
    );
    renderApp({ path: DASHBOARD, session: session() });
    const box = await screen.findByRole('region', { name: 'Notice' });
    expect(box.classList.contains('visual--text-card')).toBe(true);
    expect(box.classList.contains('visual--text-border')).toBe(true);
    expect(box.classList.contains('widget')).toBe(false);
    expect(box.style.getPropertyValue('--text-box-border')).toBe('#3FB27F');
    const shown = present(box.querySelector<HTMLElement>('.visual-text'));
    expect(shown.style.fontSize).toBe('32px');
    expect(shown.style.fontStyle).toBe('italic');
    expect(shown.style.color).toBe('rgb(184, 50, 42)');
    expect(shown.className).toContain('visual-text--middle');
    expect(shown.className).toContain('visual-text--bold');
  });
});
