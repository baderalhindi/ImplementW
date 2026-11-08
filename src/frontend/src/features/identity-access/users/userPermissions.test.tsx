import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import { internalUser, sessionFor, withIdentityAccessData } from '@/test/identityAccessFixtures.ts';
import { mockApi, problem } from '@/test/mockApi.ts';
import { renderApp } from '@/test/renderApp.tsx';

// Client review, item 5: delete a user, and adjust one user's permissions on top of their roles.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);

const riskManage = {
  permissionCode: 'RISK_MANAGE',
  name: { ar: 'إدارة المخاطر', en: 'Manage risks' },
  permissionGroup: 'RISK',
  isPrivileged: false,
  fromRoles: [],
  overrideEffect: null,
  overrideScope: null,
  overrideReason: null,
  isHeld: false,
};
const projectView = {
  ...riskManage,
  permissionCode: 'PROJECT_VIEW',
  name: { ar: 'عرض المشاريع', en: 'View projects' },
  permissionGroup: 'PROJECT',
  fromRoles: [{ roleCode: 'R04', dataScope: 'OWN' }],
  isHeld: true,
};
const permissionsPath = new RegExp(`^/users/${internalUser.id}/permissions$`);

function apiWithPermissions(items: unknown[]) {
  const api = withIdentityAccessData(mockApi());
  api.on('GET', permissionsPath, { body: { userId: internalUser.id, items } });
  return api;
}

async function openUser(items: unknown[] = [riskManage, projectView]) {
  const api = apiWithPermissions(items);
  renderApp({ path: `/admin/users/${internalUser.id}`, session: sessionFor(['R01']) });
  await screen.findByRole('heading', { level: 1, name: internalUser.displayName });
  await screen.findByRole('heading', { name: en('identityAccess.users.permissions.title') });
  return api;
}

describe('deleting a user', () => {
  test('is confirmed by name, sends the ETag it read, and returns to the list', async () => {
    const api = await openUser();
    api.on('POST', /\/delete$/, { status: 204 });
    const user = userEvent.setup();

    await user.click(
      screen.getByRole('button', { name: en('identityAccess.users.delete.confirm') }),
    );
    const dialog = screen.getByRole('dialog', { name: en('identityAccess.users.delete.title') });
    expect(within(dialog).getByText(new RegExp(internalUser.displayName))).toBeTruthy();
    await user.click(
      within(dialog).getByRole('button', { name: en('identityAccess.users.delete.confirm') }),
    );

    await waitFor(() => {
      expect(api.requestsTo('POST', /\/delete$/)).toHaveLength(1);
    });
    const [request] = api.requestsTo('POST', /\/delete$/);
    expect(request?.path).toBe(`/users/${internalUser.id}/delete`);
    expect(request?.headers.get('If-Match')).toBe('"7"');
  });
});

describe('the user list', () => {
  test('each person has Edit and Delete in their row; a service account has neither', async () => {
    const api = withIdentityAccessData(mockApi());
    renderApp({ path: '/admin/users', session: sessionFor(['R01']) });
    const link = await screen.findByRole('link', { name: internalUser.displayName });
    const row = link.closest('tr');
    expect(row).not.toBeNull();
    expect(
      within(row as HTMLElement).getByRole('link', {
        name: `${en('common.actions.edit')} ${internalUser.displayName}`,
      }),
    ).toBeTruthy();
    const user = userEvent.setup();
    await user.click(
      within(row as HTMLElement).getByRole('button', {
        name: `${en('identityAccess.users.delete.short')} ${internalUser.displayName}`,
      }),
    );
    api.on('POST', /\/delete$/, { status: 204 });
    const dialog = screen.getByRole('dialog', { name: en('identityAccess.users.delete.title') });
    await user.click(
      within(dialog).getByRole('button', { name: en('identityAccess.users.delete.confirm') }),
    );
    await waitFor(() => {
      expect(api.requestsTo('POST', /\/delete$/).map((request) => request.path)).toEqual([
        `/users/${internalUser.id}/delete`,
      ]);
    });
  });
});

describe('custom permissions', () => {
  test('nothing adjusted says so, and what the user holds is listed with where it comes from', async () => {
    await openUser();
    expect(screen.getByText(en('identityAccess.users.permissions.none'))).toBeTruthy();
    expect(
      screen.getByText(en('identityAccess.users.permissions.held', { count: 1 })),
    ).toBeTruthy();
    expect(screen.getByText(/View projects — R04/)).toBeTruthy();
  });

  test('a grant needs a reason, then is sent with its scope and shown', async () => {
    const api = await openUser();
    const user = userEvent.setup();
    await user.click(
      screen.getByRole('button', { name: en('identityAccess.users.permissions.adjust') }),
    );
    const dialog = screen.getByRole('dialog');

    await user.click(within(dialog).getByRole('button', { name: en('common.actions.save') }));
    expect(api.requestsTo('PUT', /\/permissions\//)).toHaveLength(0);

    await user.selectOptions(
      within(dialog).getByLabelText(new RegExp(en('identityAccess.users.permissions.scope'))),
      'DEPT',
    );
    await user.type(
      within(dialog).getByLabelText(new RegExp(en('identityAccess.users.permissions.reason'))),
      'Covers the risk desk.',
    );
    const granted = {
      ...riskManage,
      overrideEffect: 'GRANT',
      overrideScope: 'DEPT',
      overrideReason: { text: 'Covers the risk desk.', language: 'en' },
      isHeld: true,
    };
    api.on('PUT', /\/permissions\/RISK_MANAGE$/, {
      body: { userId: internalUser.id, items: [granted, projectView] },
    });
    api.on('GET', permissionsPath, {
      body: { userId: internalUser.id, items: [granted, projectView] },
    });
    await user.click(within(dialog).getByRole('button', { name: en('common.actions.save') }));

    expect(await screen.findByText(en('identityAccess.users.permissions.saved'))).toBeTruthy();
    const [request] = api.requestsTo('PUT', /\/permissions\/RISK_MANAGE$/);
    expect(request?.body).toMatchObject({
      effect: 'GRANT',
      dataScope: 'DEPT',
      reason: { text: 'Covers the risk desk.' },
    });
    expect(
      await screen.findByText(
        en('identityAccess.users.permissions.granted', {
          scope: en('identityAccess.dataScope.DEPT'),
        }),
      ),
    ).toBeTruthy();
  });

  test('going back to the role clears the adjustment, and a refusal is explained', async () => {
    const removed = {
      ...projectView,
      overrideEffect: 'REMOVE',
      overrideReason: { text: 'Left the team.', language: 'en' },
      isHeld: false,
    };
    const api = await openUser([riskManage, removed]);
    const user = userEvent.setup();
    await user.click(
      await screen.findByRole('button', {
        name: en('identityAccess.users.permissions.changeOf', { permission: 'View projects' }),
      }),
    );
    const dialog = screen.getByRole('dialog');
    await user.click(
      within(dialog).getByRole('radio', {
        name: en('identityAccess.users.permissions.choiceRole'),
      }),
    );
    await user.type(
      within(dialog).getByLabelText(new RegExp(en('identityAccess.users.permissions.reason'))),
      'Back on the team.',
    );
    api.on(
      'POST',
      /\/permissions\/PROJECT_VIEW\/clear$/,
      problem(422, 'IDENTITY_ACCESS_USER_HAS_NO_ROLE'),
    );
    await user.click(within(dialog).getByRole('button', { name: en('common.actions.save') }));

    expect(
      await within(dialog).findByText(en('identityAccess.problems.userHasNoRole')),
    ).toBeTruthy();
    const [request] = api.requestsTo('POST', /\/clear$/);
    expect(request?.body).toMatchObject({ reason: { text: 'Back on the team.' } });
  });
});
