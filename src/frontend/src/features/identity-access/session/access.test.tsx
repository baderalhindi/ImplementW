import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test, vi } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import { sessionFor, withIdentityAccessData } from '@/test/identityAccessFixtures.ts';
import { mockApi, problem } from '@/test/mockApi.ts';
import { renderApp } from '@/test/renderApp.tsx';
import { menuLink, openMenuTargets } from '@/test/shell.ts';

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
        expect(menuLink('common.menu.administration')).toBeNull();
        unmount();
      }
      // The shell's unread badge (TASK-040) and audit navigation (TASK-074) are the only calls besides its approval
      // count (kept apart by mockApi); nothing administrative is asked for.
      expect(
        api.requests.filter(
          (request) =>
            request.path !== '/notifications/unread-count' && request.path !== '/audit-access',
        ),
      ).toHaveLength(0);
    },
  );

  test('R01 sees the administration navigation and lands on the user list', async () => {
    withIdentityAccessData(mockApi());
    renderApp({ path: '/admin', session: sessionFor(['R01']) });

    expect(
      await screen.findByRole('heading', { level: 1, name: en('identityAccess.users.list.title') }),
    ).toBeTruthy();
    expect(openMenuTargets('common.menu.administration')).toEqual(
      expect.arrayContaining(ADMIN_PATHS),
    );
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
    // The page renders its heading before it reads the list, so wait for the read rather than assume it was sent.
    await waitFor(() => {
      expect(api.requestsTo('GET', /^\/roles$/)[0]?.headers.get('Authorization')).toBe(
        'Bearer test-access-token',
      );
    });
  });

  test('a throttled sign-in says to wait, not that something went wrong (TASK-078)', async () => {
    const api = mockApi().on('POST', /^\/sessions$/, problem(429, 'RATE_LIMITED'));
    renderApp({ path: '/sign-in' });
    const user = userEvent.setup();

    await user.type(await screen.findByRole('textbox', { name: /Username/ }), 'local.r02');
    await user.type(screen.getByLabelText(/Password/), 'local-test-password');
    await user.click(screen.getByRole('button', { name: en('identityAccess.signIn.submit') }));

    expect(await screen.findByText(en('common.problems.rateLimited'))).toBeTruthy();
    expect(api.requestsTo('POST', /^\/sessions$/)).toHaveLength(1);
    expect(screen.getByRole('textbox', { name: /Username/ })).toBeTruthy();
  });

  test("a forgotten password leads to AHDA's reset page when the build names it", async () => {
    vi.stubEnv('VITE_PASSWORD_RESET_URL', 'https://reset.example.test/');
    try {
      mockApi();
      renderApp({ path: '/sign-in' });

      const link = await screen.findByRole('link', {
        name: en('identityAccess.signIn.forgotPassword'),
      });
      expect(link.getAttribute('href')).toBe('https://reset.example.test/');
    } finally {
      vi.unstubAllEnvs();
    }
  });

  test.each(['', ['javascript', 'alert(1)'].join(':'), 'http://reset.example.test/', '/reset'])(
    'without an https reset page (%j), a forgotten password is sent to the IT service desk',
    async (value) => {
      vi.stubEnv('VITE_PASSWORD_RESET_URL', value);
      try {
        mockApi();
        renderApp({ path: '/sign-in' });

        expect(
          await screen.findByText(en('identityAccess.signIn.forgotPasswordContact')),
        ).toBeTruthy();
        expect(
          screen.queryByRole('link', { name: en('identityAccess.signIn.forgotPassword') }),
        ).toBeNull();
      } finally {
        vi.unstubAllEnvs();
      }
    },
  );
});
