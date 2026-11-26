import { describe, expect, test } from 'vitest';

import { sessionFor } from '@/test/identityAccessFixtures.ts';
import { mockApi, problem } from '@/test/mockApi.ts';

import { sessionStore } from './sessionStore.ts';

const SIGN_OUT = /^\/sessions\/current\/sign-out$/;
const REFRESH = /^\/sessions\/current\/refresh$/;

describe('signing out (SR-10)', () => {
  test('ends the session on the server with the access token, then forgets it', async () => {
    const api = mockApi().on('POST', SIGN_OUT, { status: 204 });
    sessionStore.setSession(sessionFor(['R02']));

    await sessionStore.endSession();

    const [request] = api.requestsTo('POST', SIGN_OUT);
    expect(request?.headers.get('Authorization')).toBe('Bearer test-access-token');
    expect(sessionStore.getSnapshot().session).toBeNull();
    expect(sessionStorage.getItem('pmplatform.session')).toBeNull();
  });

  test('forgets the session even when the server cannot end it', async () => {
    const api = mockApi()
      .on('POST', SIGN_OUT, problem(503, 'UNAVAILABLE'))
      .on('POST', REFRESH, problem(401, 'AUTHENTICATION_REQUIRED'));
    sessionStore.setSession(sessionFor(['R02']));

    await sessionStore.endSession();

    expect(api.requestsTo('POST', SIGN_OUT)).toHaveLength(1);
    expect(sessionStore.getSnapshot().session).toBeNull();
  });
});

describe('refreshing (SR-10)', () => {
  test('keeps the rotated refresh token, so the next refresh presents it and not the used one', async () => {
    const first = { ...sessionFor(['R02']), refreshToken: 'refresh-1' };
    const second = { ...first, accessToken: 'access-2', refreshToken: 'refresh-2' };
    const third = { ...first, accessToken: 'access-3', refreshToken: 'refresh-3' };
    const replies = [second, third];
    const api = mockApi().on('POST', REFRESH, () => ({ body: replies.shift() }));
    sessionStore.setSession(first);

    expect(await sessionStore.refresh()).toBe(true);
    expect(await sessionStore.refresh()).toBe(true);

    expect(api.requestsTo('POST', REFRESH).map((request) => request.body)).toEqual([
      { refreshToken: 'refresh-1' },
      { refreshToken: 'refresh-2' },
    ]);
    expect(sessionStore.getSnapshot().session?.refreshToken).toBe('refresh-3');
    sessionStore.signOut();
  });
});
