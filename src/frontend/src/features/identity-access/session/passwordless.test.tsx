import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import { sessionFor, withIdentityAccessData } from '@/test/identityAccessFixtures.ts';
import { type MockApi, mockApi, problem } from '@/test/mockApi.ts';
import { renderApp } from '@/test/renderApp.tsx';

import { hasDeviceUnlock, rememberDeviceUnlock } from './deviceUnlockMemory.ts';
import { deviceUnlock } from './webAuthn.ts';

// Sign-in with biometrics (13 Nov 2026 review): the National ID, then the passkey on this device or a code by SMS. The
// device is a stub; what is checked is what the page asks of it and what it sends the API.

const en = (key: string, values?: Record<string, string | number>) => translate('en', key, values);
const bytes = (...values: number[]) => new Uint8Array(values).buffer;
const NATIONAL_ID = '1000000032';

const offerPasswordless = (api: MockApi, options: { passkey?: boolean; sms?: boolean } = {}) =>
  api.on('GET', /^\/sessions\/sign-in-options$/, {
    body: {
      singleSignOnAvailable: false,
      passwordResetAvailable: false,
      passkeySignInAvailable: options.passkey ?? true,
      smsSignInAvailable: options.sms ?? true,
    },
  });

const credentials = { get: vi.fn(), create: vi.fn() };

beforeEach(() => {
  Object.defineProperty(window, 'isSecureContext', { value: true, configurable: true });
  Object.defineProperty(navigator, 'credentials', { value: credentials, configurable: true });
  vi.stubGlobal('PublicKeyCredential', {
    isUserVerifyingPlatformAuthenticatorAvailable: () => Promise.resolve(true),
  });
});

afterEach(() => {
  localStorage.clear();
  vi.unstubAllGlobals();
  credentials.get.mockReset();
  credentials.create.mockReset();
});

const startButton = () =>
  screen.findByRole('button', { name: en('identityAccess.signIn.passwordless.submit') });

/** Presses Sign in with biometrics, then types the National ID and continues, as the person does on a new device. */
async function giveNationalId(user: ReturnType<typeof userEvent.setup>, nationalId = NATIONAL_ID) {
  await user.click(await startButton());
  await user.type(await screen.findByLabelText(/^National ID \/ Iqama number/), nationalId);
  await user.click(
    screen.getByRole('button', { name: en('identityAccess.signIn.passwordless.continue') }),
  );
}

const deviceAnswer = {
  id: 'cred',
  rawId: bytes(1, 2, 3),
  type: 'public-key',
  response: {
    clientDataJSON: bytes(4),
    authenticatorData: bytes(5),
    signature: bytes(6, 7),
    userHandle: bytes(8),
  },
};

const newDeviceKey = {
  id: 'new',
  rawId: bytes(9),
  type: 'public-key',
  response: {
    clientDataJSON: bytes(1),
    attestationObject: bytes(2),
    getTransports: () => ['internal'],
  },
};

/** The API for a registered National ID whose owner signs in by SMS and saves Face ID / Touch ID. */
function registeredPerson(api: MockApi, session = sessionFor(['R03'])) {
  return api
    .on('POST', /^\/sessions\/national-id-check$/, { body: { registered: true } })
    .on('POST', /^\/sessions\/sms-code-challenge$/, {
      body: { challengeId: 'challenge-1', expiresAt: '2099-01-01T00:00:00Z' },
    })
    .on('POST', /^\/sessions\/sms$/, { status: 201, body: session })
    .on('GET', /^\/users\/current\/passkeys\/registration-options$/, {
      body: {
        options: {
          challenge: 'AQ',
          user: { id: 'Ag', name: 'local.r03', displayName: 'Local R03' },
        },
        transaction: 'sealed',
      },
    })
    .on('POST', /^\/users\/current\/passkeys$/, {
      status: 201,
      body: { id: 'p1', name: 'Mac', createdAt: '2026-11-13T10:00:00Z', lastUsedAt: null },
    });
}

/** Waits until the browser has said whether Face ID / Touch ID can be used here: the button is enabled then. */
async function deviceChecked() {
  const button = await startButton();
  await waitFor(() => {
    expect((button as HTMLButtonElement).disabled).toBe(false);
  });
}

async function verifyByMobile(
  user: ReturnType<typeof userEvent.setup>,
  mobileNumber = '0501234567',
) {
  await user.type(await screen.findByLabelText(/^Mobile number/), mobileNumber);
  await user.click(
    screen.getByRole('button', { name: en('identityAccess.signIn.passwordless.sendCode') }),
  );
  await user.type(await screen.findByLabelText(/^Verification code/), '123456');
  await user.click(
    screen.getByRole('button', { name: en('identityAccess.signIn.passwordless.verifyCode') }),
  );
}

describe('sign-in with biometrics', () => {
  test('asks for a valid National ID before anything else', async () => {
    const api = offerPasswordless(mockApi());
    const user = userEvent.setup();
    renderApp({ path: '/sign-in' });

    await giveNationalId(user, '1234567890');

    expect(
      (await screen.findByLabelText(/^National ID \/ Iqama number/)).getAttribute('aria-invalid'),
    ).toBe('true');
    expect(api.requests.filter((request) => request.method === 'POST')).toHaveLength(0);
  });

  test('says so when the National ID is not registered, and goes no further', async () => {
    const api = offerPasswordless(mockApi()).on('POST', /^\/sessions\/national-id-check$/, {
      body: { registered: false },
    });
    const user = userEvent.setup();
    renderApp({ path: '/sign-in' });
    await deviceChecked();

    await giveNationalId(user);

    expect(
      await screen.findByText(en('identityAccess.fieldErrors.nationalIdNotRegistered')),
    ).toBeTruthy();
    expect(screen.queryByLabelText(/^Mobile number/)).toBeNull();
    expect(api.requestsTo('POST', /^\/sessions\/national-id-check$/)[0]?.body).toEqual({
      nationalId: NATIONAL_ID,
    });
  });

  test('where this device has Face ID / Touch ID, the one button asks for it at once: no National ID', async () => {
    await rememberDeviceUnlock(NATIONAL_ID);
    credentials.get.mockResolvedValue(deviceAnswer);
    const api = offerPasswordless(withIdentityAccessData(mockApi()))
      .on('POST', /^\/sessions\/national-id-check$/, { body: { registered: true } })
      .on('POST', /^\/sessions\/passkey-challenge$/, {
        body: {
          options: { challenge: 'AQID', allowCredentials: [], extensions: null },
          transaction: 'sealed',
        },
      })
      .on('POST', /^\/sessions\/passkey$/, { status: 201, body: sessionFor(['R01']) });
    const user = userEvent.setup();
    const { router } = renderApp({ path: '/sign-in' });
    await deviceChecked();

    await user.click(await startButton());

    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/');
    });
    expect(screen.queryByLabelText(/^National ID \/ Iqama number/)).toBeNull();
    expect(api.requestsTo('POST', /^\/sessions\/national-id-check$/)).toHaveLength(0);
    expect(api.requestsTo('POST', /^\/sessions\/passkey-challenge$/)[0]?.body).toEqual({});
    const asked = credentials.get.mock.calls[0]?.[0] as {
      publicKey: PublicKeyCredentialRequestOptions;
    };
    expect(new Uint8Array(asked.publicKey.challenge as ArrayBuffer)).toEqual(
      new Uint8Array([1, 2, 3]),
    );
    expect('extensions' in asked.publicKey).toBe(false);
    expect(api.requestsTo('POST', /^\/sessions\/passkey$/)[0]?.body).toEqual({
      transaction: 'sealed',
      credential: {
        id: 'cred',
        rawId: 'AQID',
        type: 'public-key',
        response: {
          clientDataJson: 'BA',
          authenticatorData: 'BQ',
          signature: 'Bgc',
          userHandle: 'CA',
        },
      },
    });
  });

  test('when Face ID / Touch ID is cancelled, the National ID and the mobile number follow, with no second prompt', async () => {
    await rememberDeviceUnlock(NATIONAL_ID);
    credentials.get.mockRejectedValue(new DOMException('cancelled', 'NotAllowedError'));
    offerPasswordless(mockApi())
      .on('POST', /^\/sessions\/national-id-check$/, { body: { registered: true } })
      .on('POST', /^\/sessions\/passkey-challenge$/, {
        body: { options: { challenge: 'AQID' }, transaction: 'sealed' },
      });
    const user = userEvent.setup();
    renderApp({ path: '/sign-in' });
    await deviceChecked();

    await giveNationalId(user);

    expect(await screen.findByLabelText(/^Mobile number/)).toBeTruthy();
    expect(credentials.get).toHaveBeenCalledTimes(1);
  });

  test('when the key on this device was removed from the account, says so and asks for the National ID', async () => {
    await rememberDeviceUnlock(NATIONAL_ID);
    credentials.get.mockResolvedValue(deviceAnswer);
    offerPasswordless(mockApi())
      .on('POST', /^\/sessions\/passkey-challenge$/, {
        body: { options: { challenge: 'AQID' }, transaction: 'sealed' },
      })
      .on('POST', /^\/sessions\/passkey$/, problem(401, 'AUTHENTICATION_REQUIRED'));
    const user = userEvent.setup();
    renderApp({ path: '/sign-in' });
    await deviceChecked();

    await user.click(await startButton());

    expect(await screen.findByLabelText(/^National ID \/ Iqama number/)).toBeTruthy();
    expect(
      await screen.findByText(
        /^(Face ID|Touch ID|Windows Hello|your fingerprint|this device's unlock) on this device is no longer linked to an account/,
      ),
    ).toBeTruthy();
    expect(screen.queryByText(en('identityAccess.signIn.passwordless.refused'))).toBeNull();
    expect(await hasDeviceUnlock(NATIONAL_ID)).toBe(false);
  });

  test('without Face ID / Touch ID saved here, the one button asks for the National ID first', async () => {
    const api = offerPasswordless(mockApi());
    const user = userEvent.setup();
    renderApp({ path: '/sign-in' });
    await deviceChecked();

    await user.click(await startButton());

    expect(await screen.findByLabelText(/^National ID \/ Iqama number/)).toBeTruthy();
    expect(credentials.get).not.toHaveBeenCalled();
    expect(api.requests.filter((request) => request.method === 'POST')).toHaveLength(0);
  });

  test('the first time on a device: mobile number, code, then Face ID / Touch ID is saved before signing in', async () => {
    credentials.create.mockResolvedValue(newDeviceKey);
    const session = sessionFor(['R03']);
    const api = registeredPerson(offerPasswordless(withIdentityAccessData(mockApi())), session);
    const user = userEvent.setup();
    const { router } = renderApp({ path: '/sign-in' });
    await deviceChecked();

    await giveNationalId(user);
    await verifyByMobile(user);
    await user.click(
      await screen.findByRole('button', {
        name: /^Save (Face ID|Touch ID|Windows Hello|your fingerprint|this device's unlock)$/,
      }),
    );

    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/');
    });
    expect(credentials.get).not.toHaveBeenCalled();
    expect(api.requestsTo('POST', /^\/sessions\/sms-code-challenge$/)[0]?.body).toEqual({
      nationalId: NATIONAL_ID,
      mobileNumber: '+966501234567',
    });
    expect(api.requestsTo('POST', /^\/sessions\/sms$/)[0]?.body).toEqual({
      challengeId: 'challenge-1',
      code: '123456',
    });
    expect(
      api.requestsTo('POST', /^\/users\/current\/passkeys$/)[0]?.headers.get('Authorization'),
    ).toBe(`Bearer ${session.accessToken}`);
    expect(await hasDeviceUnlock(NATIONAL_ID)).toBe(true);
  });

  test('the first time on a device, Not now signs in without saving anything', async () => {
    const api = registeredPerson(offerPasswordless(withIdentityAccessData(mockApi())));
    const user = userEvent.setup();
    const { router } = renderApp({ path: '/sign-in' });
    await deviceChecked();

    await giveNationalId(user);
    await verifyByMobile(user);
    await user.click(
      await screen.findByRole('button', {
        name: en('identityAccess.signIn.passwordless.setup.notNow'),
      }),
    );

    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/');
    });
    expect(credentials.create).not.toHaveBeenCalled();
    expect(api.requestsTo('POST', /passkeys$/)).toHaveLength(0);
  });

  test('a mobile number that is not the registered one is refused on the field', async () => {
    offerPasswordless(mockApi())
      .on('POST', /^\/sessions\/national-id-check$/, { body: { registered: true } })
      .on(
        'POST',
        /^\/sessions\/sms-code-challenge$/,
        problem(422, 'IDENTITY_ACCESS_MOBILE_NUMBER_MISMATCH', [
          { field: 'mobileNumber', code: 'MISMATCH' },
        ]),
      );
    const user = userEvent.setup();
    renderApp({ path: '/sign-in' });
    await deviceChecked();

    await giveNationalId(user);
    await user.type(await screen.findByLabelText(/^Mobile number/), '0509999999');
    await user.click(
      screen.getByRole('button', { name: en('identityAccess.signIn.passwordless.sendCode') }),
    );

    expect(
      await screen.findAllByText(en('identityAccess.fieldErrors.mobileNumberMismatch')),
    ).not.toHaveLength(0);
    expect(screen.queryByLabelText(/^Verification code/)).toBeNull();
  });

  test('a wrong code says the code is wrong, not the National ID', async () => {
    offerPasswordless(mockApi())
      .on('POST', /^\/sessions\/national-id-check$/, { body: { registered: true } })
      .on('POST', /^\/sessions\/sms-code-challenge$/, {
        body: { challengeId: 'challenge-1', expiresAt: '2099-01-01T00:00:00Z' },
      })
      .on('POST', /^\/sessions\/sms$/, problem(401, 'AUTHENTICATION_REQUIRED'));
    const user = userEvent.setup();
    renderApp({ path: '/sign-in' });
    await deviceChecked();

    await giveNationalId(user);
    await verifyByMobile(user);

    expect(await screen.findByText(en('identityAccess.signIn.wrongCode'))).toBeTruthy();
    expect(screen.queryByText(en('identityAccess.signIn.passwordless.refused'))).toBeNull();
  });

  test('a person who requires MFA gives the authenticator code after the SMS code', async () => {
    offerPasswordless(withIdentityAccessData(mockApi()))
      .on('POST', /^\/sessions\/national-id-check$/, { body: { registered: true } })
      .on('POST', /^\/sessions\/sms-code-challenge$/, {
        body: { challengeId: 'challenge-1', expiresAt: '2099-01-01T00:00:00Z' },
      })
      .on('POST', /^\/sessions\/sms$/, {
        body: {
          mfaToken: 'mfa',
          mfaTokenExpiresAt: '2099-01-01T00:00:00Z',
          enrolmentRequired: false,
        },
      })
      .on('POST', /^\/sessions\/mfa-challenge$/, {
        body: { challengeId: 'mfa-challenge', expiresAt: null, provisioningUri: null },
      });
    const user = userEvent.setup();
    renderApp({ path: '/sign-in' });
    await deviceChecked();

    await giveNationalId(user);
    await verifyByMobile(user);

    expect(await screen.findByText(en('identityAccess.signIn.codeInstructions'))).toBeTruthy();
  });

  test('on a device without Face ID or Touch ID, the mobile number signs in and nothing is saved', async () => {
    vi.stubGlobal('PublicKeyCredential', {
      isUserVerifyingPlatformAuthenticatorAvailable: () => Promise.resolve(false),
    });
    const api = registeredPerson(offerPasswordless(withIdentityAccessData(mockApi())));
    const user = userEvent.setup();
    const { router } = renderApp({ path: '/sign-in' });
    expect(
      await screen.findByText(en('identityAccess.signIn.passwordless.noDeviceUnlock')),
    ).toBeTruthy();
    await giveNationalId(user);
    expect(
      await screen.findByText(en('identityAccess.signIn.passwordless.mobileOnly')),
    ).toBeTruthy();
    await verifyByMobile(user);

    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/');
    });
    expect(api.requestsTo('POST', /passkeys$/)).toHaveLength(0);
  });

  test('is disabled and marked Coming soon where neither way is configured', async () => {
    offerPasswordless(mockApi(), { passkey: false, sms: false });
    renderApp({ path: '/sign-in' });

    const submit = await screen.findByRole('button', {
      name: en('identityAccess.signIn.passwordless.submit'),
    });
    expect((submit as HTMLButtonElement).disabled).toBe(true);
    expect(await screen.findAllByText(en('identityAccess.signIn.comingSoon'))).not.toHaveLength(0);
  });
});

describe('the sign-in methods page', () => {
  test('adds a passkey for this device and lists it', async () => {
    credentials.create.mockResolvedValue({
      id: 'new',
      rawId: bytes(9),
      type: 'public-key',
      response: {
        clientDataJSON: bytes(1),
        attestationObject: bytes(2),
        getTransports: () => ['internal'],
      },
    });
    let added = false;
    const passkey = {
      id: 'p1',
      name: 'My phone',
      createdAt: '2026-11-13T10:00:00Z',
      lastUsedAt: null,
    };
    const api = mockApi()
      .on('GET', /^\/users\/current\/passkeys$/, () => ({
        body: { items: added ? [passkey] : [] },
      }))
      .on('GET', /^\/users\/current\/passkeys\/registration-options$/, {
        body: {
          options: {
            challenge: 'AQ',
            user: { id: 'Ag', name: 'local.r03', displayName: 'Local R03' },
            excludeCredentials: [{ id: 'Aw', type: 'public-key' }],
          },
          transaction: 'sealed',
        },
      })
      .on('POST', /^\/users\/current\/passkeys$/, () => {
        added = true;
        return { status: 201, body: passkey };
      })
      .on('GET', /^\/users\/current\/mobile-number$/, {
        body: { mobileNumber: null, verifiedAt: null, verificationAvailable: true },
      });
    const user = userEvent.setup();
    renderApp({ path: '/account/sign-in-methods', session: sessionFor(['R03']) });
    expect(await screen.findByText(en('identityAccess.signInMethods.noPasskeys'))).toBeTruthy();

    const name = await screen.findByLabelText(/^Device name/);
    await user.clear(name);
    await user.type(name, 'My phone');
    const add = await screen.findByRole('button', {
      name: /^Add (Face ID|Touch ID|Windows Hello|fingerprint|a passkey) for this device$/,
    });
    await waitFor(() => {
      expect((add as HTMLButtonElement).disabled).toBe(false);
    });
    await user.click(add);

    expect(await screen.findByText('My phone')).toBeTruthy();
    const asked = credentials.create.mock.calls[0]?.[0] as {
      publicKey: PublicKeyCredentialCreationOptions;
    };
    expect(new Uint8Array(asked.publicKey.user.id as ArrayBuffer)).toEqual(new Uint8Array([2]));
    expect(asked.publicKey.excludeCredentials?.[0]?.type).toBe('public-key');
    expect(api.requestsTo('POST', /^\/users\/current\/passkeys$/)[0]?.body).toEqual({
      transaction: 'sealed',
      name: 'My phone',
      credential: {
        id: 'new',
        rawId: 'CQ',
        type: 'public-key',
        response: { clientDataJson: 'AQ', attestationObject: 'Ag', transports: ['internal'] },
      },
    });
  });

  test('verifies the mobile number with the code sent to it', async () => {
    let verified = false;
    const api = mockApi()
      .on('GET', /^\/users\/current\/passkeys$/, { body: { items: [] } })
      .on('GET', /^\/users\/current\/mobile-number$/, () => ({
        body: {
          mobileNumber: '+966501234567',
          verifiedAt: verified ? '2026-11-13T10:00:00Z' : null,
          verificationAvailable: true,
        },
      }))
      .on('POST', /^\/users\/current\/mobile-verification-challenge$/, {
        body: { challengeId: 'c1', expiresAt: null },
      })
      .on('POST', /^\/users\/current\/mobile-verification$/, () => {
        verified = true;
        return {
          body: { mobileNumber: '+966501234567', mobileVerifiedAt: '2026-11-13T10:00:00Z' },
        };
      });
    const user = userEvent.setup();
    renderApp({ path: '/account/sign-in-methods', session: sessionFor(['R03']) });

    await user.click(
      await screen.findByRole('button', {
        name: en('identityAccess.signInMethods.sendVerificationCode'),
      }),
    );
    await user.type(await screen.findByLabelText(/^Verification code/), '654321');
    await user.click(
      screen.getByRole('button', { name: en('identityAccess.signInMethods.verify') }),
    );

    await waitFor(() => {
      expect(
        screen.queryByText(en('identityAccess.signInMethods.notVerified'), { exact: false }),
      ).toBeNull();
    });
    expect(
      screen.queryByRole('button', { name: en('identityAccess.signInMethods.verify') }),
    ).toBeNull();
    expect(api.requestsTo('POST', /mobile-verification$/)[0]?.body).toEqual({
      challengeId: 'c1',
      code: '654321',
    });
  });
});

describe('the helpers', () => {
  test('the button names what unlocks a passkey on the device', () => {
    expect(deviceUnlock('Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X)', 5)).toBe(
      'faceId',
    );
    expect(deviceUnlock('Mozilla/5.0 (Macintosh; Intel Mac OS X 15_0)', 0)).toBe('touchId');
    expect(deviceUnlock('Mozilla/5.0 (Macintosh; Intel Mac OS X 15_0)', 5)).toBe('faceId');
    expect(deviceUnlock('Mozilla/5.0 (Windows NT 10.0; Win64; x64)', 0)).toBe('windowsHello');
    expect(deviceUnlock('Mozilla/5.0 (Linux; Android 15)', 5)).toBe('fingerprint');
    expect(deviceUnlock('Mozilla/5.0 (X11; Linux x86_64)', 0)).toBe('deviceUnlock');
  });
});
