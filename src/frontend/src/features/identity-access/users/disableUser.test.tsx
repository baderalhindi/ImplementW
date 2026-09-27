import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import { internalUser, sessionFor, withIdentityAccessData } from '@/test/identityAccessFixtures.ts';
import { mockApi, problem } from '@/test/mockApi.ts';
import { renderApp } from '@/test/renderApp.tsx';

// MOD-080 Activate/Disable User on ADM-003, including the ADR-010 step-up the API asks for (record D-11).

const en = (key: string) => translate('en', key);
const disabledUser = { ...internalUser, status: 'DISABLED', disabledAt: '2026-09-27T10:00:00Z' };

async function openUser() {
  const api = withIdentityAccessData(mockApi());
  renderApp({ path: `/admin/users/${internalUser.id}`, session: sessionFor(['R01']) });
  await screen.findByRole('heading', { level: 1, name: internalUser.displayName });
  return api;
}

describe('MOD-080 disable user', () => {
  test('disabling sends the ETag it read and shows the new status', async () => {
    const api = await openUser();
    api.on('POST', /\/disable$/, { body: disabledUser });
    const user = userEvent.setup();

    await user.click(
      screen.getByRole('button', { name: en('identityAccess.userStatusDialog.disable') }),
    );
    const dialog = screen.getByRole('dialog', {
      name: en('identityAccess.userStatusDialog.disableTitle'),
    });
    expect(within(dialog).getByText(/will no longer be able to sign in/)).toBeTruthy();

    api.on('GET', new RegExp(`^/users/${internalUser.id}$`), {
      body: disabledUser,
      headers: { ETag: '"8"' },
    });
    await user.click(
      within(dialog).getByRole('button', { name: en('identityAccess.userStatusDialog.disable') }),
    );

    expect(
      await screen.findByText(en('identityAccess.users.detail.notice.statusChanged')),
    ).toBeTruthy();
    const [request] = api.requestsTo('POST', /\/disable$/);
    expect(request?.path).toBe(`/users/${internalUser.id}/disable`);
    expect(request?.headers.get('If-Match')).toBe('"7"');
    expect(request?.headers.get('Idempotency-Key')).not.toBeNull();
    expect(
      await screen.findByRole('button', { name: en('identityAccess.userStatusDialog.activate') }),
    ).toBeTruthy();
  });

  test('a STEP_UP_REQUIRED answer asks for a fresh code, then the same request is sent again', async () => {
    const api = await openUser();
    let attempts = 0;
    api
      .on('POST', /\/disable$/, () => {
        attempts += 1;
        return attempts === 1 ? problem(403, 'STEP_UP_REQUIRED') : { body: disabledUser };
      })
      .on('POST', /^\/sessions\/current\/step-up-challenge$/, {
        body: { challengeId: 'challenge-1', expiresAt: null, provisioningUri: null },
      })
      .on('POST', /^\/sessions\/current\/step-up$/, {
        body: { ...sessionFor(['R01']), accessToken: 'stepped-up-token' },
      });
    const user = userEvent.setup();

    await user.click(
      screen.getByRole('button', { name: en('identityAccess.userStatusDialog.disable') }),
    );
    const dialog = screen.getByRole('dialog', {
      name: en('identityAccess.userStatusDialog.disableTitle'),
    });
    await user.click(
      within(dialog).getByRole('button', { name: en('identityAccess.userStatusDialog.disable') }),
    );

    const stepUp = await screen.findByRole('dialog', { name: en('identityAccess.stepUp.title') });
    await user.type(
      await within(stepUp).findByRole('textbox', { name: /Verification code/ }),
      '123456',
    );
    await user.click(
      within(stepUp).getByRole('button', { name: en('identityAccess.stepUp.confirm') }),
    );

    expect(
      await screen.findByText(en('identityAccess.users.detail.notice.statusChanged')),
    ).toBeTruthy();
    expect(api.requestsTo('POST', /^\/sessions\/current\/step-up$/)[0]?.body).toEqual({
      refreshToken: 'test-refresh-token',
      challengeId: 'challenge-1',
      code: '123456',
    });
    const [first, retry] = api.requestsTo('POST', /\/disable$/);
    expect(retry?.headers.get('Authorization')).toBe('Bearer stepped-up-token');
    expect(retry?.headers.get('Idempotency-Key')).toBe(first?.headers.get('Idempotency-Key'));
  });

  test('an administrator disabling themselves is told why it was refused', async () => {
    const api = await openUser();
    api.on('POST', /\/disable$/, problem(422, 'IDENTITY_ACCESS_SELF_ADMINISTRATION'));
    const user = userEvent.setup();

    await user.click(
      screen.getByRole('button', { name: en('identityAccess.userStatusDialog.disable') }),
    );
    const dialog = screen.getByRole('dialog');
    await user.click(
      within(dialog).getByRole('button', { name: en('identityAccess.userStatusDialog.disable') }),
    );

    await waitFor(() => {
      expect(within(dialog).getByRole('alert').textContent).toBe(
        en('identityAccess.problems.selfAdministration'),
      );
    });
  });
});
