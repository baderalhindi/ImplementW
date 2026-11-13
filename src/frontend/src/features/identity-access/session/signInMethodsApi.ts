import { apiRequest } from '@/shared/api/httpClient.ts';

import { type PasskeyOptions } from './sessionApi.ts';

// The signed-in person's own sign-in methods (13 Nov 2026 review): their passkeys, and the mobile number SMS codes go to.

export interface PasskeySummary {
  id: string;
  name: string;
  createdAt: string;
  lastUsedAt: string | null;
}

export interface PasskeyList {
  items: PasskeySummary[];
}

export interface MobileNumberStatus {
  mobileNumber: string | null;
  verifiedAt: string | null;
  /** False where no SMS gateway is configured, so no code can be sent. */
  verificationAvailable: boolean;
}

export interface MobileVerificationChallenge {
  challengeId: string;
  expiresAt: string | null;
}

const PASSKEYS = '/users/current/passkeys';

export const signInMethodsApi = {
  listPasskeys: async (signal: AbortSignal) =>
    (await apiRequest<PasskeyList>(PASSKEYS, { signal })).data,
  getRegistrationOptions: async () =>
    (await apiRequest<PasskeyOptions>(`${PASSKEYS}/registration-options`)).data,
  addPasskey: async (transaction: string, credential: unknown, name: string) =>
    (
      await apiRequest<PasskeySummary>(PASSKEYS, {
        method: 'POST',
        body: { transaction, credential, name },
      })
    ).data,
  removePasskey: async (passkeyId: string) =>
    (await apiRequest<PasskeyList>(`${PASSKEYS}/${passkeyId}/delete`, { method: 'POST' })).data,
  getMobileNumber: async (signal: AbortSignal) =>
    (await apiRequest<MobileNumberStatus>('/users/current/mobile-number', { signal })).data,
  startMobileVerification: async () =>
    (
      await apiRequest<MobileVerificationChallenge>(
        '/users/current/mobile-verification-challenge',
        {
          method: 'POST',
        },
      )
    ).data,
  verifyMobileNumber: async (challengeId: string, code: string) =>
    (
      await apiRequest<{ mobileNumber: string; mobileVerifiedAt: string }>(
        '/users/current/mobile-verification',
        { method: 'POST', body: { challengeId, code } },
      )
    ).data,
};
