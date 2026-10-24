import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import { sessionFor, withIdentityAccessData } from '@/test/identityAccessFixtures.ts';
import { mockApi } from '@/test/mockApi.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';

import { IMPORT_REGISTERS } from './registers.ts';

// TASK-102: the Data migration page lists every register in load order, each with its import, for R01 alone.

describe.each(['en', 'ar'] as const)('Data migration in %s', (language) => {
  test('every register is listed in load order with its import, and no axe violation', async () => {
    withIdentityAccessData(mockApi());
    renderApp({ path: '/admin/data-migration', language, session: sessionFor(['R01']) });

    expect((await screen.findByRole('heading', { level: 1 })).textContent).toBe(
      translate(language, 'dataImport.page.title'),
    );
    const rows = within(screen.getByRole('table')).getAllByRole('row').slice(1);
    expect(rows.map((row) => within(row).getByRole('rowheader').textContent)).toEqual(
      IMPORT_REGISTERS.map((register) => translate(language, register.name)),
    );
    for (const row of rows) {
      expect(
        within(row).getByRole('button', {
          name: new RegExp(translate(language, 'dataImport.action')),
        }),
      ).toBeTruthy();
    }
    const violations = await accessibilityViolations();
    expect(violations, describeViolations(violations)).toEqual([]);
  });
});

describe('Data migration access', () => {
  test('the page is in the administration menu for R01 and opens a register import', async () => {
    withIdentityAccessData(mockApi());
    renderApp({ path: '/admin/data-migration', session: sessionFor(['R01']) });
    const user = userEvent.setup();

    expect(
      await screen.findByRole('link', { name: translate('en', 'dataImport.nav') }),
    ).toBeTruthy();
    await user.click(screen.getByRole('button', { name: /^Import\s+Users$/ }));

    expect(
      screen.getByRole('dialog', {
        name: translate('en', 'dataImport.title', {
          register: translate('en', 'dataImport.registers.USER.name'),
        }),
      }),
    ).toBeTruthy();
  });

  test('another role is refused the page', async () => {
    withIdentityAccessData(mockApi());
    renderApp({ path: '/admin/data-migration', session: sessionFor(['R02']) });

    expect(
      await screen.findByRole('heading', {
        level: 1,
        name: translate('en', 'common.forbidden.title'),
      }),
    ).toBeTruthy();
    expect(screen.queryByRole('button', { name: /^Import/ })).toBeNull();
    expect(screen.queryByRole('link', { name: translate('en', 'dataImport.nav') })).toBeNull();
  });
});
