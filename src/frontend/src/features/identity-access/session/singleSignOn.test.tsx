import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import { sessionFor, withIdentityAccessData } from '@/test/identityAccessFixtures.ts';
import { type MockApi, mockApi, problem } from '@/test/mockApi.ts';
import { renderApp } from '@/test/renderApp.tsx';

import { identityProviderRedirect, ssoTransaction } from './ssoTransaction.ts';

// Single sign-on in the SPA (sso-directory-integration.md §3, D-4): the page offers Microsoft only where the API says it is
// configured, keeps the sealed transaction across the redirect, and redeems the code once.

const en = (key: string) => translate('en', key);
const microsoftButton = (name = en('identityAccess.signIn.microsoft.submit')) =>
  screen.findByRole('button', { name: new RegExp(name) });
const offerSingleSignOn = (api: MockApi) =>
  api.on('GET', /^\/sessions\/sign-in-options$/, { body: { singleSignOnAvailable: true } });

const CALLBACK = '/auth/callback?code=the-code&state=the-state';

afterEach(() => {
  sessionStorage.clear();
  vi.restoreAllMocks();
});

describe('the Microsoft button', () => {
  let redirect: ReturnType<typeof vi.spyOn>;
  beforeEach(() => {
    redirect = vi.spyOn(identityProviderRedirect, 'go').mockImplementation(() => undefined);
  });

  test('stays disabled and says "Coming soon" where single sign-on is not configured', async () => {
    const api = mockApi();
    renderApp({ path: '/sign-in' });

    expect(await screen.findByText(en('identityAccess.signIn.comingSoon'))).toBeTruthy();
    expect(((await microsoftButton()) as HTMLButtonElement).disabled).toBe(true);
    expect(api.requestsTo('GET', /sso-authorization/)).toHaveLength(0);
  });

  test('keeps the transaction and leaves for the identity provider', async () => {
    const api = offerSingleSignOn(mockApi()).on('GET', /^\/sessions\/sso-authorization$/, {
      body: { authorizationUrl: 'https://idp.example.test/authorize?x=1', transaction: 'sealed' },
    });
    renderApp({ path: '/sign-in' });
    const button = await microsoftButton();
    await waitFor(() => {
      expect((button as HTMLButtonElement).disabled).toBe(false);
    });
    expect(button.textContent).not.toContain(en('identityAccess.signIn.comingSoon'));

    await userEvent.click(button);

    await waitFor(() => {
      expect(redirect).toHaveBeenCalledWith('https://idp.example.test/authorize?x=1');
    });
    expect(api.requestsTo('GET', /sso-authorization/)).toHaveLength(1);
    expect(ssoTransaction.read()).toEqual({ transaction: 'sealed', returnTo: '/' });
  });

  test('says so and stays on the page when the identity provider cannot be reached', async () => {
    offerSingleSignOn(mockApi()).on(
      'GET',
      /^\/sessions\/sso-authorization$/,
      problem(503, 'UNAVAILABLE'),
    );
    renderApp({ path: '/sign-in' });
    const button = await microsoftButton();
    await waitFor(() => {
      expect((button as HTMLButtonElement).disabled).toBe(false);
    });

    await userEvent.click(button);

    expect(await screen.findByText(en('common.problems.unavailable'))).toBeTruthy();
    expect(redirect).not.toHaveBeenCalled();
    expect(ssoTransaction.read()).toBeNull();
  });
});

describe('the Face ID / Touch ID sign-in through Microsoft', () => {
  let redirect: ReturnType<typeof vi.spyOn>;
  beforeEach(() => {
    redirect = vi.spyOn(identityProviderRedirect, 'go').mockImplementation(() => undefined);
  });
  const biometricButton = () =>
    screen.findByRole('button', { name: en('identityAccess.signIn.biometric.submit') });
  const emailField = () => screen.findByLabelText(en('identityAccess.signIn.biometric.email'));

  test('stays disabled where single sign-on is not configured', async () => {
    const api = mockApi();
    renderApp({ path: '/sign-in' });

    expect(((await biometricButton()) as HTMLButtonElement).disabled).toBe(true);
    expect(((await emailField()) as HTMLInputElement).disabled).toBe(true);
    expect(api.requestsTo('GET', /sso-authorization/)).toHaveLength(0);
  });

  test('sends the account to Microsoft and asks it to authenticate afresh', async () => {
    const api = offerSingleSignOn(mockApi()).on('GET', /^\/sessions\/sso-authorization$/, {
      body: { authorizationUrl: 'https://idp.example.test/authorize?y=1', transaction: 'sealed' },
    });
    renderApp({ path: '/sign-in' });
    const button = await biometricButton();
    await waitFor(() => {
      expect((button as HTMLButtonElement).disabled).toBe(false);
    });

    await userEvent.type(await emailField(), ' person@ahda.gov.sa ');
    await userEvent.click(button);

    await waitFor(() => {
      expect(redirect).toHaveBeenCalledWith('https://idp.example.test/authorize?y=1');
    });
    const [started] = api.requestsTo('GET', /sso-authorization/);
    expect(started?.query.get('loginHint')).toBe('person@ahda.gov.sa');
    expect(started?.query.get('reauthenticate')).toBe('true');
    expect(ssoTransaction.read()).toEqual({ transaction: 'sealed', returnTo: '/' });
  });

  test('asks for a valid e-mail before leaving the page', async () => {
    const api = offerSingleSignOn(mockApi());
    renderApp({ path: '/sign-in' });
    const button = await biometricButton();
    await waitFor(() => {
      expect((button as HTMLButtonElement).disabled).toBe(false);
    });

    await userEvent.type(await emailField(), 'not-an-email');
    await userEvent.click(button);

    const field = await emailField();
    await waitFor(() => {
      expect(field.getAttribute('aria-invalid')).toBe('true');
    });
    expect(api.requestsTo('GET', /sso-authorization/)).toHaveLength(0);
    expect(redirect).not.toHaveBeenCalled();
  });
});

describe('the return from the identity provider', () => {
  beforeEach(() => {
    ssoTransaction.save({ transaction: 'sealed', returnTo: '/admin/users' });
  });

  test('redeems the code once, forgets the transaction and opens the route the person asked for', async () => {
    const api = withIdentityAccessData(mockApi()).on('POST', /^\/sessions\/sso$/, {
      status: 201,
      body: sessionFor(['R01']),
    });
    const { router } = renderApp({ path: CALLBACK });

    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/admin/users');
    });
    const redeemed = api.requestsTo('POST', /^\/sessions\/sso$/);
    expect(redeemed).toHaveLength(1);
    expect(redeemed[0]?.body).toEqual({
      code: 'the-code',
      state: 'the-state',
      transaction: 'sealed',
    });
    expect(ssoTransaction.read()).toBeNull();
  });

  test('continues to the second factor when the person requires one', async () => {
    const session = sessionFor(['R01']);
    const api = withIdentityAccessData(mockApi())
      .on('POST', /^\/sessions\/sso$/, {
        body: {
          mfaToken: 'mfa-token',
          mfaTokenExpiresAt: '2099-01-01T00:00:00Z',
          enrolmentRequired: false,
        },
      })
      .on('POST', /^\/sessions\/mfa-challenge$/, {
        body: { challengeId: 'challenge', expiresAt: null, provisioningUri: null },
      })
      .on('POST', /^\/sessions\/mfa$/, { status: 201, body: session });
    const { router } = renderApp({ path: CALLBACK });

    await userEvent.type(
      await screen.findByRole('textbox', { name: new RegExp(en('identityAccess.signIn.code')) }),
      '123456',
    );
    await userEvent.click(screen.getByRole('button', { name: en('identityAccess.signIn.verify') }));

    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/admin/users');
    });
    expect(api.requestsTo('POST', /^\/sessions\/mfa$/)[0]?.body).toEqual({
      mfaToken: 'mfa-token',
      challengeId: 'challenge',
      code: '123456',
    });
  });

  test('a person the platform does not admit reads why, with the way back to sign-in', async () => {
    mockApi().on('POST', /^\/sessions\/sso$/, problem(401, 'AUTHENTICATION_REQUIRED'));
    renderApp({ path: CALLBACK });

    expect(await screen.findByText(en('identityAccess.signIn.sso.refused'))).toBeTruthy();
    expect(
      screen
        .getByRole('link', { name: en('identityAccess.signIn.backToSignIn') })
        .getAttribute('href'),
    ).toBe('/sign-in');
  });

  test('a refusal at the identity provider redeems nothing and does not repeat its text', async () => {
    const api = mockApi();
    renderApp({
      path: '/auth/callback?error=access_denied&error_description=Call+this+number',
    });

    expect(await screen.findByText(en('identityAccess.signIn.sso.notCompleted'))).toBeTruthy();
    expect(screen.queryByText(/Call this number/)).toBeNull();
    expect(api.requestsTo('POST', /^\/sessions\/sso$/)).toHaveLength(0);
    expect(ssoTransaction.read()).toBeNull();
  });

  test('a code this tab did not ask for is not redeemed', async () => {
    sessionStorage.clear();
    const api = mockApi();
    renderApp({ path: CALLBACK });

    expect(await screen.findByText(en('identityAccess.signIn.sso.notCompleted'))).toBeTruthy();
    expect(api.requestsTo('POST', /^\/sessions\/sso$/)).toHaveLength(0);
  });
});
