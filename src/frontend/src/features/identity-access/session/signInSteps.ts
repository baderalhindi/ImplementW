import { ApiError } from '@/shared/api/httpClient.ts';

import { type ProblemDescriber, problemMessage } from '../problems.ts';
import {
  isMultiFactorPending,
  type MultiFactorChallenge,
  type MultiFactorPending,
  type Session,
  type SignInResult,
  sessionApi,
} from './sessionApi.ts';
import { credentialJson, isPasskeyCancelled, requestOptions } from './webAuthn.ts';

export interface PendingSecondFactor {
  pending: MultiFactorPending;
  challenge: MultiFactorChallenge;
}

/**
 * The API answers every failed sign-in step with the same 401 AUTHENTICATION_REQUIRED, which elsewhere means a session ended. Here no
 * session has begun, so each step says what its own failure means.
 */
export const describeRefusal =
  (
    key:
      | 'identityAccess.signIn.wrongCredentials'
      | 'identityAccess.signIn.wrongCode'
      | 'identityAccess.signIn.passwordChangeExpired'
      | 'identityAccess.passwordSelfService.linkInvalid'
      | 'identityAccess.signIn.sso.refused',
  ): ProblemDescriber =>
  (error, t) =>
    error instanceof ApiError && error.code === 'AUTHENTICATION_REQUIRED'
      ? t(key)
      : problemMessage(error, t);

/** What a password-less step answered: the session, or the second factor still to come. */
export async function nextStep(
  result: SignInResult,
): Promise<{ session: Session } | { secondFactor: PendingSecondFactor }> {
  if (!isMultiFactorPending(result)) {
    return { session: result };
  }
  const challenge = await sessionApi.beginMultiFactor(result.mfaToken);
  return { secondFactor: { pending: result, challenge } };
}

export type SignInStep = Awaited<ReturnType<typeof nextStep>>;

/** A passkey step's failure: cancelled at the device, or a key no account knows. */
export const describePasskeyRefusal: ProblemDescriber = (error, t) =>
  isPasskeyCancelled(error)
    ? t('identityAccess.signIn.passwordless.passkeyCancelled')
    : error instanceof ApiError && error.code === 'AUTHENTICATION_REQUIRED'
      ? t('identityAccess.signIn.passwordless.refused')
      : problemMessage(error, t);

/**
 * Signs in with the passkey this device holds (Face ID / Touch ID). With a National ID only its owner's verifies; without one,
 * whoever the device's passkey belongs to signs in.
 */
export async function signInWithPasskey(nationalId: string | null): Promise<SignInStep> {
  const { options, transaction } = await sessionApi.beginPasskeySignIn(nationalId);
  const credential = (await navigator.credentials.get({
    publicKey: requestOptions(options),
  })) as PublicKeyCredential | null;
  if (credential === null) {
    throw new DOMException('Nothing was chosen.', 'NotAllowedError');
  }
  return nextStep(await sessionApi.signInWithPasskey(transaction, credentialJson(credential)));
}
