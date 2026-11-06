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
