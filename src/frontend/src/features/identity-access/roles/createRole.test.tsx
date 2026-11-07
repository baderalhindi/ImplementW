import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import { roles, sessionFor, withIdentityAccessData } from '@/test/identityAccessFixtures.ts';
import { type MockApi, mockApi, page, problem } from '@/test/mockApi.ts';
import { renderApp } from '@/test/renderApp.tsx';

// Created roles (the 5 Nov 2026 review §6.2): the New role screen, a role's settings, and retiring or deleting a created one.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);
const R01 = sessionFor(['R01']);
const source = {
  ...roles[2],
  id: 'role-R04',
  allowedScopes: ['PROJECT'],
  landingDashboardCode: 'GOVERNANCE',
};
const created = {
  id: 'role-R09',
  code: 'R09',
  name: { ar: 'مراجع', en: 'Reviewer' },
  isSystem: false,
  isExternalEligible: false,
  isActive: true,
  allowedScopes: ['DEPARTMENT'],
  landingDashboardCode: 'PORTFOLIO',
  updatedAt: '2026-11-05T08:00:00Z',
  updatedBy: 'someone',
  profiles: [
    {
      id: 'profile-R09',
      code: 'R09-DEFAULT',
      name: { ar: 'مراجع', en: 'Reviewer' },
      baseRoleId: 'role-R09',
      baseRoleCode: 'R09',
      isShippedDefault: false,
    },
  ],
};

function api(): MockApi {
  return withIdentityAccessData(mockApi())
    .on('GET', /^\/roles$/, { body: [roles[0], source] })
    .on('GET', /^\/roles\/role-R09$/, { body: created, headers: { ETag: '"4"' } });
}

async function fillRole(user: ReturnType<typeof userEvent.setup>, code = 'R09') {
  await user.type(await screen.findByRole('textbox', { name: /^Code/ }), code);
  await user.type(screen.getByRole('textbox', { name: /Name in Arabic/ }), 'مراجع');
  await user.type(screen.getByRole('textbox', { name: /Name in English/ }), 'Reviewer');
}

describe('New role', () => {
  test("the list offers it to a holder of ROLE_MANAGE, with each role's state, scopes and landing", async () => {
    api();
    renderApp({ path: '/admin/roles', session: R01 });

    expect(
      await screen.findByRole('link', { name: en('identityAccess.roles.list.create') }),
    ).toBeTruthy();
    const row = (await screen.findByRole('link', { name: source.name?.en ?? '' })).closest('tr');
    const cells = within(row as HTMLElement);
    expect(cells.getByText(en('identityAccess.roles.state.active'))).toBeTruthy();
    expect(cells.getByText(en('identityAccess.roles.scope.PROJECT'))).toBeTruthy();
    expect(cells.getByText(en('identityAccess.roles.landing.GOVERNANCE'))).toBeTruthy();
  });

  test('copying a role prefills its scopes, landing and external flag, then opens the draft profile', async () => {
    const mock = api()
      .on('POST', /^\/roles$/, { status: 201, body: created })
      .on('GET', /^\/permission-profiles\/profile-R09$/, {
        body: { ...created.profiles[0], versions: [{ id: 'version-R09-1' }] },
      })
      .on('GET', /^\/permission-profile-versions\/version-R09-1$/, problem(404, 'NOT_FOUND'));
    const { router } = renderApp({ path: '/admin/roles/new', session: R01 });
    const user = userEvent.setup();

    await fillRole(user);
    await user.selectOptions(
      screen.getByRole('combobox', { name: /Copy permissions from/ }),
      'role-R04',
    );
    expect(
      screen.getByRole<HTMLInputElement>('checkbox', {
        name: en('identityAccess.roles.scope.PROJECT'),
      }).checked,
    ).toBe(true);
    expect(
      screen.getByRole<HTMLInputElement>('checkbox', {
        name: en('identityAccess.roles.fields.externalEligible'),
      }).checked,
    ).toBe(true);
    expect(
      screen.getByRole<HTMLSelectElement>('combobox', { name: /Landing dashboard/ }).value,
    ).toBe('GOVERNANCE');
    await user.click(
      screen.getByRole('button', { name: en('identityAccess.roles.create.submit') }),
    );

    await waitFor(() => {
      expect(router.state.location.pathname).toBe(
        '/admin/permission-profile-versions/version-R09-1',
      );
    });
    expect(mock.requestsTo('POST', /^\/roles$/)[0]?.body).toEqual({
      code: 'R09',
      nameAr: 'مراجع',
      nameEn: 'Reviewer',
      isExternalEligible: true,
      allowedScopes: ['PROJECT'],
      sourceRoleId: 'role-R04',
      landingDashboardCode: 'GOVERNANCE',
    });
  });

  test('a malformed code and no scope are refused before the API is called', async () => {
    const mock = api();
    renderApp({ path: '/admin/roles/new', session: R01 });
    const user = userEvent.setup();

    await fillRole(user, '9bad');
    await user.click(
      screen.getByRole('button', { name: en('identityAccess.roles.create.submit') }),
    );

    expect(screen.getByRole('textbox', { name: /^Code/ }).getAttribute('aria-invalid')).toBe(
      'true',
    );
    expect(screen.getByText(en('common.fieldErrors.required'))).toBeTruthy();
    expect(mock.requestsTo('POST', /^\/roles$/)).toHaveLength(0);
  });

  test('a taken code is shown on its field', async () => {
    api().on(
      'POST',
      /^\/roles$/,
      problem(409, 'IDENTITY_ACCESS_DUPLICATE_KEY', [{ field: 'code', code: 'DUPLICATE' }]),
    );
    renderApp({ path: '/admin/roles/new', session: R01 });
    const user = userEvent.setup();

    await fillRole(user);
    await user.click(screen.getByRole('checkbox', { name: en('identityAccess.roles.scope.ALL') }));
    await user.click(
      screen.getByRole('button', { name: en('identityAccess.roles.create.submit') }),
    );

    expect(await screen.findByText(en('identityAccess.fieldErrors.duplicate'))).toBeTruthy();
  });
});

describe("a role's settings and retirement", () => {
  async function openCreated() {
    const mock = api();
    renderApp({ path: '/admin/roles/role-R09', session: R01 });
    await screen.findByRole('heading', { level: 1, name: /R09/ });
    return { mock, user: userEvent.setup() };
  }

  test('settings are saved with the ETag', async () => {
    const { mock, user } = await openCreated();
    mock.on('POST', /\/update-settings$/, { body: created });

    await user.click(
      screen.getByRole('button', { name: en('identityAccess.roles.settings.open') }),
    );
    const dialog = screen.getByRole('dialog');
    await user.click(
      within(dialog).getByRole('checkbox', { name: en('identityAccess.roles.scope.PROJECT') }),
    );
    await user.click(within(dialog).getByRole('button', { name: en('common.actions.save') }));

    expect(await screen.findByText(en('identityAccess.roles.settings.saved'))).toBeTruthy();
    const [request] = mock.requestsTo('POST', /\/update-settings$/);
    expect(request?.body).toEqual({
      allowedScopes: ['DEPARTMENT', 'PROJECT'],
      landingDashboardCode: 'PORTFOLIO',
      showCreateInHeader: true,
    });
    expect(request?.headers.get('If-Match')).toBe('"4"');
  });

  test('deactivating sends the ETag and rereads the role', async () => {
    const { mock, user } = await openCreated();
    mock.on('POST', /\/deactivate$/, { body: { ...created, isActive: false } });
    mock.on('GET', /^\/roles\/role-R09$/, {
      body: { ...created, isActive: false },
      headers: { ETag: '"5"' },
    });

    await user.click(
      screen.getByRole('button', { name: en('identityAccess.roles.actions.deactivate') }),
    );
    const dialog = screen.getByRole('dialog');
    await user.click(
      within(dialog).getByRole('button', { name: en('identityAccess.roles.actions.deactivate') }),
    );

    expect(
      await screen.findByRole('button', { name: en('identityAccess.roles.actions.reactivate') }),
    ).toBeTruthy();
    expect(mock.requestsTo('POST', /\/deactivate$/)[0]?.headers.get('If-Match')).toBe('"4"');
  });

  test('deleting a role someone holds says to deactivate instead', async () => {
    const { mock, user } = await openCreated();
    mock.on('DELETE', /^\/roles\/role-R09$/, problem(422, 'IDENTITY_ACCESS_ROLE_IN_USE'));

    await user.click(
      screen.getByRole('button', { name: en('identityAccess.roles.actions.delete') }),
    );
    const dialog = screen.getByRole('dialog');
    await user.click(
      within(dialog).getByRole('button', { name: en('identityAccess.roles.actions.delete') }),
    );

    expect(await within(dialog).findByText(en('identityAccess.problems.roleInUse'))).toBeTruthy();
  });

  test('deleting a role nobody held returns to the list', async () => {
    const { mock, user } = await openCreated();
    mock.on('DELETE', /^\/roles\/role-R09$/, { status: 204 });

    await user.click(
      screen.getByRole('button', { name: en('identityAccess.roles.actions.delete') }),
    );
    const dialog = screen.getByRole('dialog');
    await user.click(
      within(dialog).getByRole('button', { name: en('identityAccess.roles.actions.delete') }),
    );

    expect(
      await screen.findByRole('heading', { level: 1, name: en('identityAccess.roles.list.title') }),
    ).toBeTruthy();
    expect(mock.requestsTo('DELETE', /^\/roles\/role-R09$/)[0]?.headers.get('If-Match')).toBe(
      '"4"',
    );
  });

  test('a built-in role offers its settings but no retirement', async () => {
    api().on('GET', /^\/roles\/role-R01$/, {
      body: { ...roles[0], profiles: [], updatedAt: '2026-09-01T08:00:00Z', updatedBy: 'x' },
    });
    renderApp({ path: '/admin/roles/role-R01', session: R01 });
    await screen.findByRole('heading', { level: 1, name: /R01/ });

    expect(
      screen.getByRole('button', { name: en('identityAccess.roles.settings.open') }),
    ).toBeTruthy();
    expect(
      screen.queryByRole('button', { name: en('identityAccess.roles.actions.deactivate') }),
    ).toBeNull();
    expect(
      screen.queryByRole('button', { name: en('identityAccess.roles.actions.delete') }),
    ).toBeNull();
  });
});

describe('the assign dialog follows the role', () => {
  const profile = (roleId: string, code: string) => ({
    id: `profile-${code}`,
    code: `${code}-DEFAULT`,
    name: { ar: code, en: code },
    baseRoleId: roleId,
    baseRoleCode: code,
    isShippedDefault: false,
  });

  function withRoles(extra: object[], profiles: object[]) {
    return withIdentityAccessData(mockApi())
      .on('GET', /^\/roles$/, { body: [...roles, ...extra] })
      .on('GET', /^\/permission-profiles$/, { body: page(profiles, 200) })
      .on('GET', /^\/permission-profiles\/profile-R10$/, {
        body: {
          ...profile('role-R10', 'R10'),
          versions: [
            { id: 'version-R10-1', versionNo: 1, lifecycleState: 'PUBLISHED', grants: [] },
          ],
        },
      });
  }

  test('an inactive role is not offered; a created one is', async () => {
    withRoles(
      [
        { ...created, id: 'role-R10', code: 'R10', allowedScopes: ['PROJECT'] },
        { ...created, id: 'role-R11', code: 'R11', isActive: false },
      ],
      [profile('role-R10', 'R10'), profile('role-R11', 'R11')],
    );
    renderApp({ path: '/admin/role-assignments', session: R01 });
    const user = userEvent.setup();
    await user.click(
      await screen.findByRole('button', { name: en('identityAccess.assignRole.open') }),
    );
    const dialog = await screen.findByRole('dialog', {
      name: en('identityAccess.assignRole.title'),
    });

    const select = await within(dialog).findByRole('combobox', {
      name: /Role and permission profile/,
    });
    expect(within(select).queryByRole('option', { name: /^R11/ })).toBeNull();
    expect(within(select).getByRole('option', { name: /^R10/ })).toBeTruthy();
  });
});
