import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import {
  ADMIN_ID,
  internalUser,
  sessionFor,
  withIdentityAccessData,
} from '@/test/identityAccessFixtures.ts';
import { mockApi } from '@/test/mockApi.ts';
import { renderApp } from '@/test/renderApp.tsx';

import { sessionStore } from '../session/sessionStore.ts';

// The Home banner names the signed-in person from the session, which holds the name it signed in with.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);
const REFRESH = /^\/sessions\/current\/refresh$/;

async function saveRenamed(userId: string) {
  const session = sessionFor(['R01']);
  const before = { ...internalUser, id: userId, displayName: 'Test Administrator' };
  const api = withIdentityAccessData(mockApi());
  api.on('GET', new RegExp(`^/users/${userId}$`), { body: before, headers: { ETag: '"7"' } });
  api.on('PUT', new RegExp(`^/users/${userId}$`), {
    body: { ...before, displayName: 'System Administrator' },
    headers: { ETag: '"8"' },
  });
  api.on('POST', REFRESH, {
    body: {
      ...session,
      accessToken: 'renewed-access-token',
      user: { ...session.user, displayName: 'System Administrator' },
    },
  });
  renderApp({ path: `/admin/users/${userId}/edit`, session });
  const name = await screen.findByLabelText(
    new RegExp(en('identityAccess.users.fields.displayName')),
  );
  const user = userEvent.setup();
  await user.clear(name);
  await user.type(name, 'System Administrator');
  await user.click(screen.getByRole('button', { name: en('common.actions.save') }));
  await waitFor(() => {
    expect(api.requestsTo('PUT', new RegExp(`^/users/${userId}$`))).toHaveLength(1);
  });
  return api;
}

describe('saving a user record', () => {
  test('of the signed-in person renews the session, so it carries the new name', async () => {
    const api = await saveRenamed(ADMIN_ID);
    await waitFor(() => {
      expect(sessionStore.getSnapshot().session?.user.displayName).toBe('System Administrator');
    });
    expect(api.requestsTo('POST', REFRESH)).toHaveLength(1);
  });

  test('of someone else leaves the session alone', async () => {
    const api = await saveRenamed(internalUser.id);
    await screen.findByText(en('identityAccess.users.detail.notice.updated'));
    expect(api.requestsTo('POST', REFRESH)).toHaveLength(0);
    expect(sessionStore.getSnapshot().session?.user.displayName).toBe('Test Administrator');
  });
});
