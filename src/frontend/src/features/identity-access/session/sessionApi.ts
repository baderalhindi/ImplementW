import { apiRequest } from '@/shared/api/httpClient.ts';

import { type LanguageCode, type UserType } from '../api/types.ts';

// TASK-028 and TASK-029 sessions (SessionsController). All of them are anonymous calls: the credential is in the body.

export interface SessionRoleAssignment {
  roleCode: string;
  permissionProfileVersionId: string;
  departmentId: string | null;
  externalEntityId: string | null;
  projectId: string | null;
}

export type AuthenticationMethod =
  'DIRECTORY' | 'SINGLE_SIGN_ON' | 'LOCAL_PASSWORD' | 'PASSKEY' | 'SMS_CODE';

export interface SessionUser {
  id: string;
  userType: UserType;
  username: string;
  displayName: string;
  preferredLanguage: LanguageCode;
  authenticationMethod: AuthenticationMethod;
  multiFactorAuthenticated: boolean;
  authenticatedAt: string;
  roleAssignments: SessionRoleAssignment[];
}

export interface Session {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  sessionExpiresAt: string;
  user: SessionUser;
}

export interface MultiFactorPending {
  mfaToken: string;
  mfaTokenExpiresAt: string;
  enrolmentRequired: boolean;
}

export interface MultiFactorChallenge {
  challengeId: string;
  expiresAt: string | null;
  /** Set for an enrolment only: what the person loads into their authenticator app. */
  provisioningUri: string | null;
}

/** A local account signed in with a temporary password: no session until a new password is set. */
export interface PasswordChangePending {
  passwordChangeToken: string;
  expiresAt: string;
}

export type SignInResult = Session | MultiFactorPending;

/** The sign-in methods this environment offers beyond the password. */
export interface SignInOptions {
  singleSignOnAvailable: boolean;
  /** False where no reset e-mail could be sent, or local accounts are off. */
  passwordResetAvailable: boolean;
  /** Face ID / Touch ID: the environment knows its WebAuthn relying party. The device must also have a passkey. */
  passkeySignInAvailable: boolean;
  /** A code by SMS: an SMS gateway (or its test-mailbox stand-in) is configured. */
  smsSignInAvailable: boolean;
}

/** The start of a passkey ceremony: options for the browser, and the sealed transaction to send back with its answer. */
export interface PasskeyOptions {
  options: Record<string, unknown>;
  transaction: string;
}

/** The same whether or not the number is anyone's; a code went only if it is a verified number. */
export interface SmsCodeChallenge {
  challengeId: string;
  expiresAt: string;
}

export interface SsoAuthorization {
  authorizationUrl: string;
  /** Sealed by the API: kept until the identity provider redirects back, then returned with the code. */
  transaction: string;
}

export function isMultiFactorPending(
  result: SignInResult | PasswordChangePending,
): result is MultiFactorPending {
  return 'mfaToken' in result;
}

export function isPasswordChangePending(
  result: SignInResult | PasswordChangePending,
): result is PasswordChangePending {
  return 'passwordChangeToken' in result;
}

async function post<T>(path: string, body: unknown): Promise<T> {
  return (await apiRequest<T>(path, { method: 'POST', body, anonymous: true })).data;
}

export const sessionApi = {
  signIn: (username: string, password: string) =>
    post<SignInResult | PasswordChangePending>('/sessions', { username, password }),
  getSignInOptions: async (signal: AbortSignal) =>
    (await apiRequest<SignInOptions>('/sessions/sign-in-options', { anonymous: true, signal }))
      .data,
  beginSso: async () =>
    (await apiRequest<SsoAuthorization>('/sessions/sso-authorization', { anonymous: true })).data,
  completeSso: (code: string, state: string, transaction: string) =>
    post<SignInResult>('/sessions/sso', { code, state, transaction }),
  /** Sign-in with biometrics: options for the passkey of the National ID's owner (the same answer for any number). */
  beginPasskeySignIn: (nationalId: string) =>
    post<PasskeyOptions>('/sessions/passkey-challenge', { nationalId }),
  signInWithPasskey: (transaction: string, credential: unknown) =>
    post<SignInResult>('/sessions/passkey', { transaction, credential }),
  /** Sign-in with biometrics: a code to the verified mobile number of the National ID's owner. */
  requestSmsCode: (nationalId: string) =>
    post<SmsCodeChallenge>('/sessions/sms-code-challenge', { nationalId }),
  signInWithSmsCode: (challengeId: string, code: string) =>
    post<SignInResult>('/sessions/sms', { challengeId, code }),
  changePassword: (passwordChangeToken: string, newPassword: string) =>
    post<SignInResult>('/sessions/password-change', { passwordChangeToken, newPassword }),
  /** The one call here made as the signed-in person: the access token says whose password it is. */
  changeOwnPassword: async (currentPassword: string, newPassword: string) => {
    await apiRequest<undefined>('/sessions/current/password-change', {
      method: 'POST',
      body: { currentPassword, newPassword },
    });
  },
  requestPasswordReset: (username: string) =>
    post<undefined>('/sessions/password-reset-requests', { username }),
  completePasswordReset: (resetToken: string, newPassword: string) =>
    post<undefined>('/sessions/password-reset', { resetToken, newPassword }),
  beginMultiFactor: (mfaToken: string) =>
    post<MultiFactorChallenge>('/sessions/mfa-challenge', { mfaToken }),
  completeMultiFactor: (mfaToken: string, challengeId: string, code: string) =>
    post<SignInResult>('/sessions/mfa', { mfaToken, challengeId, code }),
  refresh: (refreshToken: string) => post<Session>('/sessions/current/refresh', { refreshToken }),
  beginStepUp: (refreshToken: string) =>
    post<MultiFactorChallenge>('/sessions/current/step-up-challenge', { refreshToken }),
  completeStepUp: (refreshToken: string, challengeId: string, code: string) =>
    post<Session>('/sessions/current/step-up', { refreshToken, challengeId, code }),
};
