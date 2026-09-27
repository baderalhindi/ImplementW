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

export interface SessionUser {
  id: string;
  userType: UserType;
  username: string;
  displayName: string;
  preferredLanguage: LanguageCode;
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

export type SignInResult = Session | MultiFactorPending;

export function isMultiFactorPending(result: SignInResult): result is MultiFactorPending {
  return 'mfaToken' in result;
}

async function post<T>(path: string, body: unknown): Promise<T> {
  return (await apiRequest<T>(path, { method: 'POST', body, anonymous: true })).data;
}

export const sessionApi = {
  signIn: (username: string, password: string) =>
    post<SignInResult>('/sessions', { username, password }),
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
