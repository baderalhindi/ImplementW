import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import { sessionFor } from '@/test/identityAccessFixtures.ts';
import { mockApi, problem } from '@/test/mockApi.ts';
import { renderApp } from '@/test/renderApp.tsx';

import { sessionStore } from './sessionStore.ts';

const en = (key: string) => translate('en', key);
const NEW_PASSWORD = 'a-new-password-12';

function localSession() {
  const session = sessionFor(['R02']);
  return { ...session, user: { ...session.user, authenticationMethod: 'LOCAL_PASSWORD' as const } };
}

async function fillNewPassword(
  user: ReturnType<typeof userEvent.setup>,
  confirmation = NEW_PASSWORD,
) {
  await user.type(screen.getByLabelText(/^New password/), NEW_PASSWORD);
  await user.type(screen.getByLabelText(/^Confirm the new password/), confirmation);
}

describe('the account menu', () => {
  test('offers Change password to every signed-in person', async () => {
    mockApi();
    const user = userEvent.setup();
    const { router } = renderApp({ path: '/help', session: sessionFor(['R02']) });

    await user.click(await screen.findByRole('button', { name: /^Account:/ }));
    await user.click(
      screen.getByRole('link', { name: en('identityAccess.passwordSelfService.changeMenu') }),
    );

    expect(router.state.location.pathname).toBe('/account/password');
  });
});

describe('changing a password while signed in', () => {
  test('a local account proves its current password, then is signed out', async () => {
    const api = mockApi().on('POST', /^\/sessions\/current\/password-change$/, { status: 204 });
    const user = userEvent.setup();
    const { router } = renderApp({ path: '/account/password', session: localSession() });

    await user.type(await screen.findByLabelText(/^Current password/), 'the-old-password-1');
    await fillNewPassword(user);
    await user.click(
      screen.getByRole('button', { name: en('identityAccess.passwordSelfService.changeSubmit') }),
    );

    expect(
      await screen.findByRole('heading', {
        name: en('identityAccess.passwordSelfService.updatedTitle'),
      }),
    ).toBeTruthy();
    expect(router.state.location.pathname).toBe('/password-updated');
    expect(sessionStore.getSnapshot().session).toBeNull();
    expect(api.requestsTo('POST', /^\/sessions\/current\/password-change$/)[0]?.body).toEqual({
      currentPassword: 'the-old-password-1',
      newPassword: NEW_PASSWORD,
    });
  });

  test('a wrong current password stays on the form and names the field', async () => {
    mockApi().on(
      'POST',
      /^\/sessions\/current\/password-change$/,
      problem(400, 'VALIDATION_FAILED', [{ field: 'currentPassword', code: 'PASSWORD_INCORRECT' }]),
    );
    const user = userEvent.setup();
    const { router } = renderApp({ path: '/account/password', session: localSession() });

    await user.type(await screen.findByLabelText(/^Current password/), 'the-old-password-1');
    await fillNewPassword(user);
    await user.click(
      screen.getByRole('button', { name: en('identityAccess.passwordSelfService.changeSubmit') }),
    );

    expect(
      await screen.findByText(en('identityAccess.fieldErrors.passwordIncorrect')),
    ).toBeTruthy();
    expect(router.state.location.pathname).toBe('/account/password');
    expect(sessionStore.getSnapshot().session).not.toBeNull();
  });

  test('two different new passwords never reach the API', async () => {
    const api = mockApi();
    const user = userEvent.setup();
    renderApp({ path: '/account/password', session: localSession() });

    await user.type(await screen.findByLabelText(/^Current password/), 'the-old-password-1');
    await fillNewPassword(user, 'something-else-34');
    await user.click(
      screen.getByRole('button', { name: en('identityAccess.passwordSelfService.changeSubmit') }),
    );

    expect(await screen.findByText(en('identityAccess.fieldErrors.passwordMismatch'))).toBeTruthy();
    expect(api.requestsTo('POST', /password-change/)).toHaveLength(0);
  });

  test('a directory account changes its own password here and is told it changes everywhere', async () => {
    const api = mockApi().on('POST', /^\/sessions\/current\/password-change$/, { status: 204 });
    const user = userEvent.setup();
    renderApp({ path: '/account/password', session: sessionFor(['R02']) });

    expect(
      await screen.findByText(en('identityAccess.passwordSelfService.changeDescriptionDirectory')),
    ).toBeTruthy();
    await user.type(await screen.findByLabelText(/^Current password/), 'the-old-password-1');
    await fillNewPassword(user);
    await user.click(
      screen.getByRole('button', { name: en('identityAccess.passwordSelfService.changeSubmit') }),
    );

    expect(
      await screen.findByRole('heading', {
        name: en('identityAccess.passwordSelfService.updatedTitle'),
      }),
    ).toBeTruthy();
    expect(api.requestsTo('POST', /^\/sessions\/current\/password-change$/)).toHaveLength(1);
  });

  test("the directory's refusal of a new password is shown on the field", async () => {
    mockApi().on(
      'POST',
      /^\/sessions\/current\/password-change$/,
      problem(400, 'VALIDATION_FAILED', [
        { field: 'newPassword', code: 'PASSWORD_REJECTED_BY_DIRECTORY' },
      ]),
    );
    const user = userEvent.setup();
    renderApp({ path: '/account/password', session: sessionFor(['R02']) });

    await user.type(await screen.findByLabelText(/^Current password/), 'the-old-password-1');
    await fillNewPassword(user);
    await user.click(
      screen.getByRole('button', { name: en('identityAccess.passwordSelfService.changeSubmit') }),
    );

    expect(
      await screen.findByText(en('identityAccess.fieldErrors.passwordRejectedByDirectory')),
    ).toBeTruthy();
  });

  test('a Microsoft account is told where its password changes, with no form', async () => {
    mockApi();
    const session = sessionFor(['R02']);
    renderApp({
      path: '/account/password',
      session: {
        ...session,
        user: { ...session.user, authenticationMethod: 'SINGLE_SIGN_ON' as const },
      },
    });

    expect(
      await screen.findByText(en('identityAccess.passwordSelfService.managedBody')),
    ).toBeTruthy();
    expect(screen.getByText(en('identityAccess.passwordSelfService.managedContact'))).toBeTruthy();
    expect(screen.queryByLabelText(/^Current password/)).toBeNull();
  });
});

describe('a forgotten password', () => {
  test('the sign-in page links to the reset where the environment can send one', async () => {
    mockApi().on('GET', /^\/sessions\/sign-in-options$/, {
      body: { singleSignOnAvailable: false, passwordResetAvailable: true },
    });
    const user = userEvent.setup();
    const { router } = renderApp({ path: '/sign-in' });

    await user.click(
      await screen.findByRole('link', { name: en('identityAccess.signIn.forgotPassword') }),
    );

    expect(router.state.location.pathname).toBe('/forgot-password');
  });

  test('otherwise the sign-in page says to contact the service desk', async () => {
    mockApi();
    renderApp({ path: '/sign-in' });

    expect(await screen.findByText(en('identityAccess.signIn.forgotPasswordContact'))).toBeTruthy();
    expect(
      screen.queryByRole('link', { name: en('identityAccess.signIn.forgotPassword') }),
    ).toBeNull();
  });

  test('asking for a link answers the same way whatever the username', async () => {
    const api = mockApi().on('POST', /^\/sessions\/password-reset-requests$/, { status: 204 });
    const user = userEvent.setup();
    renderApp({ path: '/forgot-password' });

    await user.type(await screen.findByLabelText(/^Username/), ' someone ');
    await user.click(
      screen.getByRole('button', { name: en('identityAccess.passwordSelfService.forgotSubmit') }),
    );

    expect(
      await screen.findByText(en('identityAccess.passwordSelfService.forgotSent')),
    ).toBeTruthy();
    expect(api.requestsTo('POST', /password-reset-requests/)[0]?.body).toEqual({
      username: 'someone',
    });
  });

  test('the link sets a new password and sends the person to sign in', async () => {
    const api = mockApi().on('POST', /^\/sessions\/password-reset$/, { status: 204 });
    const user = userEvent.setup();
    const { router } = renderApp({ path: '/reset-password#token=reset-token' });

    await screen.findByLabelText(/^New password/);
    await fillNewPassword(user);
    await user.click(
      screen.getByRole('button', { name: en('identityAccess.passwordSelfService.resetSubmit') }),
    );

    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/password-updated');
    });
    expect(api.requestsTo('POST', /^\/sessions\/password-reset$/)[0]?.body).toEqual({
      resetToken: 'reset-token',
      newPassword: NEW_PASSWORD,
    });
    expect(
      screen.getByRole('link', { name: en('identityAccess.passwordSelfService.updatedSignIn') }),
    ).toBeTruthy();
  });

  test('a used or expired link says so and offers a new one', async () => {
    mockApi().on('POST', /^\/sessions\/password-reset$/, problem(401, 'AUTHENTICATION_REQUIRED'));
    const user = userEvent.setup();
    renderApp({ path: '/reset-password#token=used-token' });

    await screen.findByLabelText(/^New password/);
    await fillNewPassword(user);
    await user.click(
      screen.getByRole('button', { name: en('identityAccess.passwordSelfService.resetSubmit') }),
    );

    expect(
      await screen.findByText(en('identityAccess.passwordSelfService.linkInvalid')),
    ).toBeTruthy();
    expect(
      screen.getByRole('link', { name: en('identityAccess.passwordSelfService.requestNewLink') }),
    ).toBeTruthy();
  });

  test('a link without a token offers a new one and no form', async () => {
    mockApi();
    renderApp({ path: '/reset-password' });

    expect(
      await screen.findByText(en('identityAccess.passwordSelfService.linkInvalid')),
    ).toBeTruthy();
    expect(screen.queryByLabelText(/^New password/)).toBeNull();
  });
});
