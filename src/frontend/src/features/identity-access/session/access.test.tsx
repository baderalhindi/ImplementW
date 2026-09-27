import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import { sessionFor, withIdentityAccessData } from '@/test/identityAccessFixtures.ts';
import { mockApi } from '@/test/mockApi.ts';
import { renderApp } from '@/test/renderApp.tsx';

import { identityAccessNavigation } from '../routes.tsx';

// Acceptance criterion 1: the FG-03 screens are reachable only for R01. The guard is navigation; the API refuses
// every other role on its own (TASK-031 EveryRoleButR01IsRefusedAdministration).

const en = (key: string) => translate('en', key);
const ADMIN_PATHS = identityAccessNavigation.map((item) => item.to);

describe('R01 only', () => {
  test.each(ADMIN_PATHS)('%s without a session goes to sign-in and calls no API', async (path) => {
    const api = mockApi();
    const { router } = renderApp({ path });

    expect(
      await screen.findByRole('heading', { level: 1, name: en('identityAccess.signIn.title') }),
    ).toBeTruthy();
    expect(router.state.location.pathname).toBe('/sign-in');
    expect(api.requests).toHaveLength(0);
  });

  test.each(['R02', 'R03', 'R04', 'R05', 'R06', 'R07', 'R08'])(
    '%s is denied every administration screen and shown no administration navigation',
    async (roleCode) => {
      const api = mockApi();
      for (const path of ADMIN_PATHS) {
        const { unmount } = renderApp({ path, session: sessionFor([roleCode]) });
        expect(
          await screen.findByRole('heading', { level: 1, name: en('common.forbidden.title') }),
        ).toBeTruthy();
        expect(
          screen.queryByRole('navigation', { name: en('identityAccess.nav.label') }),
        ).toBeNull();
        unmount();
      }
      expect(api.requests).toHaveLength(0);
    },
  );

  test('R01 sees the administration navigation and lands on the user list', async () => {
    withIdentityAccessData(mockApi());
    renderApp({ path: '/admin', session: sessionFor(['R01']) });

    expect(
      await screen.findByRole('heading', { level: 1, name: en('identityAccess.users.list.title') }),
    ).toBeTruthy();
    const navigation = screen.getByRole('navigation', { name: en('identityAccess.nav.label') });
    expect(navigation.querySelectorAll('a')).toHaveLength(ADMIN_PATHS.length);
  });
});

describe('sign-in', () => {
  test('an administrator signs in with the second factor and returns to the page they asked for', async () => {
    const session = sessionFor(['R01']);
    const api = withIdentityAccessData(mockApi())
      .on('POST', /^\/sessions$/, {
        body: {
          mfaToken: 'mfa-token',
          mfaTokenExpiresAt: '2099-01-01T00:00:00Z',
          enrolmentRequired: false,
        },
      })
      .on('POST', /^\/sessions\/mfa-challenge$/, {
        body: { challengeId: 'challenge-1', expiresAt: null, provisioningUri: null },
      })
      .on('POST', /^\/sessions\/mfa$/, { status: 201, body: session });
    const { router } = renderApp({ path: '/admin/roles' });
    const user = userEvent.setup();

    await user.type(await screen.findByRole('textbox', { name: /Username/ }), 'test.admin');
    await user.type(screen.getByLabelText(/Password/), 'local-test-password');
    await user.click(screen.getByRole('button', { name: en('identityAccess.signIn.submit') }));
    await user.type(await screen.findByRole('textbox', { name: /Verification code/ }), '654321');
    await user.click(screen.getByRole('button', { name: en('identityAccess.signIn.verify') }));

    expect(
      await screen.findByRole('heading', { level: 1, name: en('identityAccess.roles.list.title') }),
    ).toBeTruthy();
    expect(router.state.location.pathname).toBe('/admin/roles');
    expect(api.requestsTo('POST', /^\/sessions$/)[0]?.headers.get('Authorization')).toBeNull();
    expect(api.requestsTo('POST', /^\/sessions\/mfa$/)[0]?.body).toEqual({
      mfaToken: 'mfa-token',
      challengeId: 'challenge-1',
      code: '654321',
    });
    expect(api.requestsTo('GET', /^\/roles$/)[0]?.headers.get('Authorization')).toBe(
      'Bearer test-access-token',
    );
  });
});
