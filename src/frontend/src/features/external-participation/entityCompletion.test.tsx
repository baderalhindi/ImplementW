import { screen, within } from '@testing-library/react';
import { describe, expect, test } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import { mockApi } from '@/test/mockApi.ts';
import { renderApp } from '@/test/renderApp.tsx';
import { catalogueFor, withDashboards } from '@/test/dashboardFixtures.ts';
import { sessionFor } from '@/test/identityAccessFixtures.ts';
import { entitySession } from '@/test/projectFixtures.ts';

const en = (key: string, params?: Record<string, string | number>) =>
  translate('en', key, params).replace(/[\u2068\u2069]/g, '');

const entityApi = () => withDashboards(mockApi(), { catalogue: catalogueFor(['R08'], true) });
const entity = entitySession;

describe('an entity against the average (review round 5, item 9)', () => {
  test('its rate, the marked average, and a sentence when it is below', async () => {
    const api = entityApi().on('GET', /^\/entity-completion-rates\/own$/, {
      body: { issued: 20, accepted: 17, completionPercent: 73, averagePercent: 85 },
    });
    renderApp({ path: '/dashboards', session: entity() });

    expect(await screen.findByText('73%')).toBeTruthy();
    expect(screen.getByRole('img').getAttribute('aria-label')).toContain('85%');
    expect(
      (document.querySelector('.notice--warning')?.textContent ?? '').replace(
        /[\u2068\u2069]/g,
        '',
      ),
    ).toBe(en('externalParticipation.completion.below', { points: '12' }));
    expect(api.requestsTo('GET', /^\/entity-completion-rates$/)).toEqual([]);
  });

  test('above the average, no sentence; nothing issued is not 0%', async () => {
    entityApi().on('GET', /^\/entity-completion-rates\/own$/, {
      body: { issued: 0, accepted: 0, completionPercent: null, averagePercent: 85 },
    });
    renderApp({ path: '/dashboards', session: entity() });

    expect(await screen.findByText(en('externalParticipation.completion.ownEmpty'))).toBeTruthy();
    expect(screen.queryByText('0%')).toBeNull();
  });
});

describe('every entity for AHDA (review round 5, item 10)', () => {
  test('rows in the API\u2019s order, each opening the monitor filtered to it', async () => {
    const ENTITY = '6f6f6f6f-0000-4000-8000-000000000107';
    withDashboards(mockApi(), { catalogue: catalogueFor(['R02']) }).on(
      'GET',
      /^\/entity-completion-rates$/,
      {
        body: {
          items: [
            {
              externalEntityId: ENTITY,
              issued: 4,
              submitted: 3,
              accepted: 2,
              overdue: 1,
              completionPercent: 50,
            },
          ],
          averagePercent: 62.5,
          page: 1,
          pageSize: 100,
          totalCount: 1,
        },
      },
    );
    renderApp({ path: '/dashboards?dashboard=PORTFOLIO', session: sessionFor(['R02']) });

    const row = (await screen.findByText('50%')).closest('tr');
    expect(row).toBeTruthy();
    expect(
      within(row as HTMLElement)
        .getByRole('link')
        .getAttribute('href'),
    ).toBe(`/external-requests/monitor?externalEntityId=${ENTITY}`);
    expect(screen.getByText(/62\.5%/)).toBeTruthy();
  });
});
