import { waitFor } from '@testing-library/react';
import { describe, expect, test } from 'vitest';

import { dashboardView, withDashboards } from '@/test/dashboardFixtures.ts';
import { sessionFor } from '@/test/identityAccessFixtures.ts';
import { type MockApi, mockApi } from '@/test/mockApi.ts';
import {
  designFieldsFixture,
  fullInsights,
  standardDesignOf,
} from '@/test/portfolioInsightFixtures.ts';
import { CATALOGUES, withProjectLookups } from '@/test/projectFixtures.ts';
import { renderApp } from '@/test/renderApp.tsx';

// Stage QA F-05: every visual on the Portfolio dashboard and in its designer names master data, units and profiles. Each of those
// is read once per screen (ResourceScope), not once per visual: the stage build sent 198 requests on one load, 126 of them for
// master data items.

function api(): MockApi {
  const mock = mockApi();
  withProjectLookups(mock);
  withDashboards(mock, { views: { PORTFOLIO: dashboardView('PORTFOLIO') } });
  return mock
    .on('GET', /^\/portfolio-insights$/, { body: fullInsights() })
    .on('GET', /^\/portfolio-insights\/design$/, { body: standardDesignOf(fullInsights()) })
    .on('GET', /^\/portfolio-insights\/design\/fields$/, { body: designFieldsFixture() });
}

const LOOKUPS = [
  /^\/master-data-catalogues$/,
  /^\/organization-units$/,
  /^\/configuration-resolutions\/GOVERNANCE_PROFILE$/,
];

describe.each([
  ['the Portfolio dashboard', '/dashboards?dashboard=PORTFOLIO'],
  ['the dashboard designer', '/dashboards/design'],
])('%s', (_name, path) => {
  test('reads each shared lookup once, however many visuals name them', async () => {
    const mock = api();
    renderApp({ path, session: sessionFor(['R02']) });

    // The standard design draws many visuals; every catalogue's items arrive once they all have mounted.
    await waitFor(() => {
      expect(document.querySelectorAll('.visual__body').length).toBeGreaterThan(5);
      expect(mock.requestsTo('GET', /^\/master-data-items$/).length).toBeGreaterThanOrEqual(
        CATALOGUES.length,
      );
    });
    await new Promise((resolve) => setTimeout(resolve, 100));

    LOOKUPS.forEach((pattern) => {
      expect(mock.requestsTo('GET', pattern), String(pattern)).toHaveLength(1);
    });
    const catalogueIds = mock
      .requestsTo('GET', /^\/master-data-items$/)
      .map((request) => request.query.get('catalogueId'));
    expect(catalogueIds).toHaveLength(new Set(catalogueIds).size);
    expect(catalogueIds).toHaveLength(CATALOGUES.length);
  });
});
