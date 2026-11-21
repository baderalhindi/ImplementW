import { act, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import { dashboardView, withDashboards } from '@/test/dashboardFixtures.ts';
import { sessionFor } from '@/test/identityAccessFixtures.ts';
import { type MockApi, mockApi } from '@/test/mockApi.ts';
import { designFieldsFixture } from '@/test/portfolioInsightFixtures.ts';
import { withProjectLookups } from '@/test/projectFixtures.ts';
import { renderApp } from '@/test/renderApp.tsx';

import { type DesignView, type Visual, type VisualResult } from './designApi.ts';

// Several visuals selected and moved together (21 Nov 2026: "the user can select more than one visual, so he can drag them down or up at
// once, not visual by visual"). Ctrl, ⌘ or Shift on a visual's handle adds it to the selection; the arrow keys on any selected handle move
// them all. The pointer drag takes the same offset (shiftedTogether, visuals.test.ts).

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);
const plain = (text: string | null | undefined) => (text ?? '').replace(/[⁨⁩]/g, '');

function card(title: string, [layoutColumn, layoutRow]: [number, number]): VisualResult {
  return {
    title: { ar: title, en: title },
    visualType: 'CARD',
    measures: ['PROJECTS'],
    groupBy: 'NONE',
    layoutColumn,
    layoutRow,
    layoutWidth: 4,
    layoutHeight: 2,
    groups: [
      {
        key: null,
        label: null,
        projects: 3,
        measured: 3,
        valueEntered: 3,
        values: [3],
        onPlan: null,
      },
    ],
    children: null,
  };
}

const DESIGN: DesignView = {
  isStandard: false,
  updatedAt: '2026-11-21T09:00:00Z',
  visuals: [card('First', [0, 0]), card('Second', [6, 0]), card('Third', [0, 4])],
};

const visualsSent = (body: unknown): Visual[] => (body as { visuals: Visual[] }).visuals;

function api(): MockApi {
  const mock = mockApi();
  withProjectLookups(mock);
  withDashboards(mock, { views: { PORTFOLIO: dashboardView('PORTFOLIO') } });
  return mock
    .on('GET', /^\/portfolio-insights\/design$/, { body: DESIGN })
    .on('GET', /^\/portfolio-insights\/design\/fields$/, { body: designFieldsFixture() })
    .on('POST', /^\/portfolio-insights\/design\/preview$/, (request) => ({
      body: {
        isStandard: false,
        updatedAt: null,
        visuals: visualsSent(request.body).map((visual) => ({
          ...visual,
          groups: DESIGN.visuals[0]?.groups ?? [],
          children: null,
        })),
      },
    }));
}

const rowsPreviewed = (mock: MockApi) =>
  Object.fromEntries(
    visualsSent(
      mock.requestsTo('POST', /^\/portfolio-insights\/design\/preview$/).at(-1)?.body ?? {
        visuals: [],
      },
    ).map((visual) => [visual.title.en, visual.layoutRow]),
  );

const moveHandle = (title: string) =>
  screen.getByRole('button', {
    name: (name) => plain(name) === plain(en('dashboards.designer.moveHandle', { title })),
  });

const isSelected = (title: string) =>
  screen.getByRole('region', { name: title }).classList.contains('is-selected');

async function openDesigner() {
  renderApp({ path: '/dashboards/design', session: sessionFor(['R02']) });
  await screen.findByRole('region', { name: 'Third' });
}

describe('several visuals selected together', () => {
  test('Ctrl on two handles selects both, and the Properties pane says two are selected', async () => {
    api();
    const user = userEvent.setup();
    await openDesigner();

    await user.keyboard('{Control>}');
    await user.click(moveHandle('First'));
    await user.click(moveHandle('Second'));
    await user.keyboard('{/Control}');

    expect([isSelected('First'), isSelected('Second'), isSelected('Third')]).toEqual([
      true,
      true,
      false,
    ]);
    expect(
      plain(screen.getByRole('region', { name: en('dashboards.designer.properties') }).textContent),
    ).toContain(plain(en('dashboards.designer.severalSelected', { count: 2 })));
  });

  test('the arrow keys on one selected handle move them all, and what they land on moves down below them', async () => {
    const mock = api();
    const user = userEvent.setup();
    await openDesigner();
    await user.keyboard('{Shift>}');
    await user.click(moveHandle('First'));
    await user.click(moveHandle('Second'));
    await user.keyboard('{/Shift}');

    act(() => {
      moveHandle('Second').focus();
    });
    await user.keyboard('{ArrowDown}{ArrowDown}{ArrowDown}');

    // First and Second (two rows tall) go from row 0 to row 3, onto Third at row 4: Third moves to row 5, under them.
    await waitFor(() => {
      expect(rowsPreviewed(mock)).toEqual({ First: 3, Second: 3, Third: 5 });
    });
  });

  test('Ctrl on a selected handle takes it out, and a plain press selects one visual alone again', async () => {
    api();
    const user = userEvent.setup();
    await openDesigner();
    await user.keyboard('{Control>}');
    await user.click(moveHandle('First'));
    await user.click(moveHandle('Second'));
    await user.click(moveHandle('Third'));
    await user.click(moveHandle('Second'));
    await user.keyboard('{/Control}');

    expect([isSelected('First'), isSelected('Second'), isSelected('Third')]).toEqual([
      true,
      false,
      true,
    ]);

    await user.click(moveHandle('Second'));

    expect([isSelected('First'), isSelected('Second'), isSelected('Third')]).toEqual([
      false,
      true,
      false,
    ]);
  });
});
