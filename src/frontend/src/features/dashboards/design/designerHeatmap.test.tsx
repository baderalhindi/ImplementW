import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { dashboardView, withDashboards } from '@/test/dashboardFixtures.ts';
import { sessionFor } from '@/test/identityAccessFixtures.ts';
import { type MockApi, mockApi } from '@/test/mockApi.ts';
import { designFieldsFixture } from '@/test/portfolioInsightFixtures.ts';
import { withProjectLookups } from '@/test/projectFixtures.ts';
import { renderApp } from '@/test/renderApp.tsx';

import { type DesignView, type Visual } from './designApi.ts';

// A saved heatmap (22 Nov 2026) keeps its columns when the designer reads it back: without its columnField every preview of the design was refused
// (visuals[n].columnField REQUIRED) and each visual read "No data" (24 Nov 2026).

const HEATMAP = {
  title: {
    ar: 'عدد المخاطر حسب الاحتمالية والأثر',
    en: 'Number of risks by Probability and Impact',
  },
  visualType: 'HEATMAP',
  source: 'RISKS',
  measures: [],
  values: [{ field: 'RISK.RISK', aggregation: 'COUNT' }],
  groupBy: 'NONE',
  groupField: 'RISK.PROBABILITY',
  columnField: 'RISK.IMPACT',
  layoutColumn: 0,
  layoutRow: 0,
  layoutWidth: 6,
  layoutHeight: 6,
  groups: [],
  children: null,
} as const;

const DESIGN = {
  isStandard: false,
  updatedAt: '2026-11-23T09:00:00Z',
  visuals: [HEATMAP],
} as unknown as DesignView;

// The risk fields the designer reads for a visual of risks: probability and impact list their five levels.
const level = (key: string) => ({
  key,
  projectionCode: 'RISK',
  sourceDomain: 'WF-06',
  fieldCode: key.split('.')[1],
  type: 'CODE',
  isSensitive: false,
  values: ['1', '2', '3', '4', '5'],
  aggregations: ['COUNT'],
  grain: 'PROJECT',
});
const RISK_FIELDS = [
  { ...level('RISK.RISK'), type: 'COUNT', values: [] },
  level('RISK.PROBABILITY'),
  level('RISK.IMPACT'),
];

const visualsSent = (body: unknown): Visual[] => (body as { visuals: Visual[] }).visuals;

function api(): MockApi {
  const mock = mockApi();
  withProjectLookups(mock);
  withDashboards(mock, { views: { PORTFOLIO: dashboardView('PORTFOLIO') } });
  return mock
    .on('GET', /^\/portfolio-insights\/design$/, { body: DESIGN })
    .on('GET', /^\/portfolio-insights\/design\/fields$/, (request) => ({
      body:
        request.query.get('source') === 'RISKS' ? { fields: RISK_FIELDS } : designFieldsFixture(),
    }))
    .on('POST', /^\/portfolio-insights\/design\/preview$/, (request) => ({
      body: {
        isStandard: false,
        updatedAt: null,
        visuals: visualsSent(request.body).map((v) => ({ ...v, groups: [], children: null })),
      },
    }));
}

describe('a saved heatmap', () => {
  test('is previewed with its rows and its columns, as it was saved', async () => {
    const mock = api();
    renderApp({ path: '/dashboards/design', session: sessionFor(['R02']) });
    await screen.findByRole('region', { name: HEATMAP.title.en });

    await waitFor(() => {
      expect(
        mock.requestsTo('POST', /^\/portfolio-insights\/design\/preview$/).length,
      ).toBeGreaterThan(0);
    });
    const sent = visualsSent(
      mock.requestsTo('POST', /^\/portfolio-insights\/design\/preview$/).at(-1)?.body,
    )[0];
    expect(sent).toMatchObject({
      visualType: 'HEATMAP',
      groupField: 'RISK.PROBABILITY',
      columnField: 'RISK.IMPACT',
    });
  });

  test('its settings name each level, the guide’s names first; a name changed is sent with the design', async () => {
    const mock = api();
    const user = userEvent.setup();
    renderApp({ path: '/dashboards/design', session: sessionFor(['R02']) });
    await screen.findByRole('region', { name: HEATMAP.title.en });
    await user.click(
      screen.getByRole('button', {
        name: (name) => name.replace(/[\u2068\u2069]/g, '') === `Edit ${HEATMAP.title.en}`,
      }),
    );

    const english = await screen.findByRole('textbox', {
      name: (name) => name.replace(/[\u2068\u2069]/g, '') === 'English name of Probability 5',
    });
    expect((english as HTMLInputElement).value).toBe('Almost certain');
    await user.clear(english);
    await user.type(english, 'Near certain');

    await waitFor(() => {
      const sent = visualsSent(
        mock.requestsTo('POST', /^\/portfolio-insights\/design\/preview$/).at(-1)?.body,
      )[0];
      expect(sent?.labels?.rows).toEqual([
        { key: '5', label: { ar: 'مُرجّح', en: 'Near certain' } },
      ]);
    });
    expect(
      screen.getByRole('group', {
        name: (name) => name.replace(/[\u2068\u2069]/g, '') === 'Row names: Probability',
      }),
    ).toBeTruthy();
  });
});
