import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { type RoleSummary } from '@/features/identity-access/api/types.ts';
import { translate } from '@/shared/i18n/i18n.ts';
import { roles, sessionFor, withIdentityAccessData } from '@/test/identityAccessFixtures.ts';
import { mockApi } from '@/test/mockApi.ts';
import { renderApp } from '@/test/renderApp.tsx';

// Client review, item 8 on ADM-006: every role is edited from its row; only a role created here is deleted, never R01–R08.

const en = (key: string) => translate('en', key);
const created: RoleSummary = {
  id: 'role-TEMP',
  isExternalEligible: false,
  isActive: true,
  allowedScopes: ['PROJECT'],
  landingDashboardCode: null,
  code: 'TEMP',
  name: { ar: 'دور مؤقت', en: 'Temporary role' },
  isSystem: false,
};

describe('role rows', () => {
  test('a built-in role has Edit only; a created role has Delete too, which is confirmed and sent', async () => {
    const api = withIdentityAccessData(mockApi()).on('GET', /^\/roles$/, {
      body: [...roles, created],
    });
    renderApp({ path: '/admin/roles', session: sessionFor(['R01']) });

    const builtIn = (await screen.findByRole('link', { name: 'System Administrator' })).closest(
      'tr',
    ) as HTMLElement;
    expect(within(builtIn).getByRole('link', { name: /^Edit/ }).getAttribute('href')).toBe(
      '/admin/roles/role-R01/edit',
    );
    expect(within(builtIn).queryByRole('button', { name: /^Delete/ })).toBeNull();

    const own = screen.getByRole('link', { name: 'Temporary role' }).closest('tr') as HTMLElement;
    const user = userEvent.setup();
    await user.click(within(own).getByRole('button', { name: /^Delete/ }));
    api
      .on('POST', /\/roles\/role-TEMP\/delete$|^\/roles\/role-TEMP$/, { status: 204 })
      .on('DELETE', /^\/roles\/role-TEMP$/, { status: 204 });
    const dialog = await screen.findByRole('dialog', { name: en('common.rowActions.deleteTitle') });
    await user.click(within(dialog).getByRole('button', { name: en('common.rowActions.delete') }));
    await waitFor(() => {
      expect([
        ...api.requestsTo('DELETE', /role-TEMP/),
        ...api.requestsTo('POST', /role-TEMP/),
      ]).toHaveLength(1);
    });
  });
});
