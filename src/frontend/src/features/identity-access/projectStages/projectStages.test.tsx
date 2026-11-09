import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import { sessionFor } from '@/test/identityAccessFixtures.ts';
import { mockApi } from '@/test/mockApi.ts';
import { renderApp } from '@/test/renderApp.tsx';

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);

function first<T>(items: readonly T[]): T {
  const [item] = items;
  if (item === undefined) {
    throw new Error('Nothing found.');
  }
  return item;
}

const VERSION_ID = '00000000-0000-4000-8000-0000000000a1';
const NEW_ID = '00000000-0000-4000-8000-0000000000a2';

function open() {
  const api = mockApi()
    .on('GET', /^\/configuration-families$/, {
      body: [{ id: 'family', code: 'PROJECT_STAGE' }],
    })
    .on('GET', /^\/configuration-versions$/, {
      body: {
        items: [
          {
            id: VERSION_ID,
            versionNo: 1,
            lifecycleState: 'PUBLISHED',
            effectiveFrom: '2026-01-01T00:00:00Z',
          },
        ],
      },
    })
    .on('GET', new RegExp(`^/configuration-versions/${VERSION_ID}$`), {
      body: {
        id: VERSION_ID,
        versionNo: 1,
        lifecycleState: 'PUBLISHED',
        effectiveFrom: '2026-01-01T00:00:00Z',
        content: {
          projectStages: [
            {
              code: 'A',
              name: { ar: 'تخطيط', en: 'Plan' },
              sortOrder: 1,
              weightPercent: 40,
              isActive: true,
            },
            {
              code: 'B',
              name: { ar: 'تنفيذ', en: 'Build' },
              sortOrder: 2,
              weightPercent: 60,
              isActive: true,
            },
          ],
        },
      },
    })
    .on('POST', /^\/configuration-versions$/, { status: 201, body: { id: NEW_ID } })
    .on('PUT', new RegExp(`^/configuration-versions/${NEW_ID}$`), { body: {} })
    .on('POST', new RegExp(`^/configuration-versions/${NEW_ID}/validate$`), { body: {} })
    .on('POST', new RegExp(`^/configuration-versions/${NEW_ID}/publish$`), { body: {} });
  renderApp({ path: '/admin/project-stages', session: sessionFor(['R01']) });
  return api;
}

describe('Project stages (review round 5, item 7)', () => {
  test('weights that do not total 100 are not saved', async () => {
    const api = open();
    const weight = first(await screen.findAllByRole('textbox', { name: /^Weight/ }));
    await userEvent.clear(weight);
    await userEvent.type(weight, '30');
    await userEvent.click(
      screen.getByRole('button', { name: en('identityAccess.projectStages.save') }),
    );

    expect(screen.getByRole('alert').textContent).toContain('90');
    expect(api.requestsTo('POST', /^\/configuration-versions$/)).toEqual([]);
  });

  test('a rename and a new weight become a new version; the old one is not touched', async () => {
    const api = open();
    const nameEn = first(await screen.findAllByRole('textbox', { name: /^English name/ }));
    await userEvent.clear(nameEn);
    await userEvent.type(nameEn, 'Planning');
    await userEvent.click(
      screen.getByRole('button', { name: en('identityAccess.projectStages.save') }),
    );

    await screen.findByText(en('identityAccess.projectStages.published'));
    const put = api.requestsTo('PUT', /^\/configuration-versions\//)[0];
    expect(put?.path).toBe(`/configuration-versions/${NEW_ID}`);
    expect(put?.body).toMatchObject({
      content: {
        projectStages: [{ code: 'A', name: { en: 'Planning' }, weightPercent: 40 }, { code: 'B' }],
      },
    });
    expect(api.requestsTo('PUT', new RegExp(VERSION_ID))).toEqual([]);
  });

  test('a stage is deactivated, keeping its row, and its weight no longer counts', async () => {
    open();
    const table = first(await screen.findAllByRole('table'));
    await userEvent.click(first(within(table).getAllByRole('checkbox').slice(1)));

    expect(
      screen.getByText(en('identityAccess.projectStages.total', { total: '40' })),
    ).toBeTruthy();
    expect(within(table).getAllByRole('checkbox')).toHaveLength(2);
  });

  test('Add stage, beside the heading, adds an empty row; a version reads its state in words', async () => {
    open();
    expect(await screen.findAllByRole('textbox', { name: /^English name/ })).toHaveLength(2);
    expect(screen.getByText(en('identityAccess.masterData.states.PUBLISHED'))).toBeTruthy();

    await userEvent.click(
      screen.getByRole('button', { name: en('identityAccess.projectStages.add') }),
    );

    const names = screen.getAllByRole('textbox', { name: /^English name/ });
    expect(names).toHaveLength(3);
    expect((names[2] as HTMLInputElement).value).toBe('');
  });

  test('with no stages yet, the table says how to add the first one', async () => {
    mockApi()
      .on('GET', /^\/configuration-families$/, { body: [{ id: 'family', code: 'PROJECT_STAGE' }] })
      .on('GET', /^\/configuration-versions$/, { body: { items: [] } });
    renderApp({ path: '/admin/project-stages', session: sessionFor(['R01']) });

    expect(await screen.findByText(en('identityAccess.projectStages.empty'))).toBeTruthy();
    await userEvent.click(
      screen.getByRole('button', { name: en('identityAccess.projectStages.add') }),
    );
    expect(screen.queryByText(en('identityAccess.projectStages.empty'))).toBeNull();
  });
});
