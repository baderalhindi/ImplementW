import { screen, waitFor } from '@testing-library/react';
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
});
