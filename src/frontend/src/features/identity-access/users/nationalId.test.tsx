import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import { internalUser, sessionFor, withIdentityAccessData } from '@/test/identityAccessFixtures.ts';
import { mockApi } from '@/test/mockApi.ts';
import { renderApp } from '@/test/renderApp.tsx';

import { checkNationalId } from '../forms.ts';

// Client review, item 7: the National ID or Iqama number, masked, revealed on purpose, set with a check digit.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);
const recorded = { ...internalUser, nationalIdMasked: '******0008' };

async function openUser(user: unknown, roles: string[] = ['R01']) {
  const api = withIdentityAccessData(mockApi());
  api.on('GET', new RegExp(`^/users/${internalUser.id}$`), {
    body: user,
    headers: { ETag: '"7"' },
  });
  api.on('GET', new RegExp(`^/users/${internalUser.id}/permissions$`), {
    body: { userId: internalUser.id, items: [] },
  });
  renderApp({ path: `/admin/users/${internalUser.id}`, session: sessionFor(roles) });
  await screen.findByRole('heading', { name: en('identityAccess.users.nationalId.title') });
  return api;
}

describe('a National ID or Iqama number', () => {
  test.each([
    ['1000000008', null],
    ['2000000006', null],
    ['1000000009', 'MALFORMED'],
    ['3000000002', 'MALFORMED'],
    ['12345', 'MALFORMED'],
    ['', 'REQUIRED'],
  ])('%s checks as %s', (value, code) => {
    expect(checkNationalId(value)).toBe(code);
  });

  test('is shown masked, and in full only when asked for', async () => {
    const api = await openUser(recorded);
    expect(screen.getByText('******0008')).toBeTruthy();
    api.on('GET', new RegExp(`^/users/${internalUser.id}/national-id$`), {
      body: { nationalId: '1000000008' },
    });
    const user = userEvent.setup();

    await user.click(
      screen.getByRole('button', { name: en('identityAccess.users.nationalId.show') }),
    );
    expect(await screen.findByText('1000000008')).toBeTruthy();
    await user.click(
      screen.getByRole('button', { name: en('identityAccess.users.nationalId.hide') }),
    );
    expect(screen.queryByText('1000000008')).toBeNull();
    expect(api.requestsTo('GET', /\/national-id$/)).toHaveLength(1);
  });

  test('a wrong check digit is caught before it is sent; a right one is sent with the ETag', async () => {
    const api = await openUser({ ...internalUser, nationalIdMasked: null });
    expect(screen.getByText(en('identityAccess.users.nationalId.none'))).toBeTruthy();
    const user = userEvent.setup();
    await user.click(
      screen.getByRole('button', { name: en('identityAccess.users.nationalId.add') }),
    );
    const dialog = screen.getByRole('dialog');
    const field = within(dialog).getByLabelText(
      new RegExp(en('identityAccess.users.nationalId.field')),
    );

    await user.type(field, '1000000009');
    await user.click(within(dialog).getByRole('button', { name: en('common.actions.save') }));
    expect(api.requestsTo('PUT', /\/national-id$/)).toHaveLength(0);

    await user.clear(field);
    await user.type(field, '1000000008');
    api.on('PUT', /\/national-id$/, { body: recorded });
    await user.click(within(dialog).getByRole('button', { name: en('common.actions.save') }));
    await waitFor(() => {
      expect(api.requestsTo('PUT', /\/national-id$/)).toHaveLength(1);
    });
    const [request] = api.requestsTo('PUT', /\/national-id$/);
    expect(request?.body).toEqual({ nationalId: '1000000008' });
    expect(request?.headers.get('If-Match')).toBe('"7"');
  });

  test('the Edit form records a new number first, then saves the profile with the newer ETag', async () => {
    const api = withIdentityAccessData(mockApi());
    api.on('GET', new RegExp(`^/users/${internalUser.id}$`), {
      body: recorded,
      headers: { ETag: '"7"' },
    });
    api.on('PUT', /\/national-id$/, { body: recorded, headers: { ETag: '"8"' } });
    api.on('PUT', new RegExp(`^/users/${internalUser.id}$`), {
      body: recorded,
      headers: { ETag: '"9"' },
    });
    renderApp({ path: `/admin/users/${internalUser.id}/edit`, session: sessionFor(['R01']) });
    const field = await screen.findByLabelText(
      new RegExp(en('identityAccess.users.nationalId.field')),
    );
    expect(
      screen.getByText(
        en('identityAccess.users.form.nationalIdHintRecorded', { current: '******0008' }),
      ),
    ).toBeTruthy();
    const user = userEvent.setup();

    await user.type(field, '1000000009');
    await user.click(screen.getByRole('button', { name: en('common.actions.save') }));
    expect(api.requestsTo('PUT', /\/national-id$/)).toHaveLength(0);
    expect(api.requestsTo('PUT', new RegExp(`^/users/${internalUser.id}$`))).toHaveLength(0);

    await user.clear(field);
    await user.type(field, '2000000006');
    await user.click(screen.getByRole('button', { name: en('common.actions.save') }));
    await waitFor(() => {
      expect(api.requestsTo('PUT', new RegExp(`^/users/${internalUser.id}$`))).toHaveLength(1);
    });
    const [recordedId] = api.requestsTo('PUT', /\/national-id$/);
    expect(recordedId?.body).toEqual({ nationalId: '2000000006' });
    expect(recordedId?.headers.get('If-Match')).toBe('"7"');
    const [profile] = api.requestsTo('PUT', new RegExp(`^/users/${internalUser.id}$`));
    expect(profile?.headers.get('If-Match')).toBe('"8"');
  });

  test('the Edit form leaves the number alone when the field is blank', async () => {
    const api = withIdentityAccessData(mockApi());
    api.on('GET', new RegExp(`^/users/${internalUser.id}$`), {
      body: recorded,
      headers: { ETag: '"7"' },
    });
    api.on('PUT', new RegExp(`^/users/${internalUser.id}$`), { body: recorded });
    renderApp({ path: `/admin/users/${internalUser.id}/edit`, session: sessionFor(['R01']) });
    await screen.findByLabelText(new RegExp(en('identityAccess.users.nationalId.field')));
    await userEvent.setup().click(screen.getByRole('button', { name: en('common.actions.save') }));
    await waitFor(() => {
      expect(api.requestsTo('PUT', new RegExp(`^/users/${internalUser.id}$`))).toHaveLength(1);
    });
    expect(api.requestsTo('PUT', /\/national-id$/)).toHaveLength(0);
  });
});
