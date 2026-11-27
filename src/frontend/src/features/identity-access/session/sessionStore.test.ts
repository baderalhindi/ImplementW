import { describe, expect, test } from 'vitest';

import { sessionFor } from '@/test/identityAccessFixtures.ts';
import { mockApi, problem } from '@/test/mockApi.ts';

import { type Session } from './sessionApi.ts';
import {
  createSessionStore,
  type SessionStoreEnvironment,
  sessionStore,
  type TabChannel,
  type TabMessage,
} from './sessionStore.ts';

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

// DEV-D1-01: tabs of one browser, each with its own sessionStorage, one BroadcastChannel and one lock manager.
function browser() {
  const channels = new Set<TabChannel>();
  const locks = new Map<string, Promise<unknown>>();
  const exclusive: SessionStoreEnvironment['exclusive'] = (name, task) => {
    const run = (locks.get(name) ?? Promise.resolve()).then(task);
    locks.set(
      name,
      run.catch(() => undefined),
    );
    return run;
  };
  const tab = (storage: Map<string, string> = new Map()) => {
    const channel: TabChannel = {
      onmessage: null,
      postMessage: (message: TabMessage) => {
        for (const other of channels) {
          if (other !== channel) {
            const data = structuredClone(message);
            setTimeout(() => other.onmessage?.({ data } as MessageEvent<TabMessage>), 0);
          }
        }
      },
    };
    channels.add(channel);
    const store = createSessionStore({
      storage: {
        getItem: (key) => storage.get(key) ?? null,
        setItem: (key, value) => storage.set(key, value),
        removeItem: (key) => storage.delete(key),
      },
      channel,
      exclusive,
      peerAnswerMs: 20,
    });
    return {
      store,
      /** Chrome's "Duplicate tab": a new tab with a copy of this one's sessionStorage. */
      duplicate: () => tab(new Map(storage)),
      close: () => channels.delete(channel),
    };
  };
  return { tab };
}

function refreshTokenOf(sessionId: string, serial: number): string {
  const payload = btoa(JSON.stringify({ sid: sessionId, jti: String(serial) })).replace(/=+$/, '');
  return `header.${payload}.signature`;
}

function pair(sessionId: string, serial: number, accessTokenExpiresAt: string): Session {
  return {
    ...sessionFor(['R07']),
    accessToken: `access-${String(serial)}`,
    accessTokenExpiresAt,
    refreshToken: refreshTokenOf(sessionId, serial),
  };
}

const EXPIRED = '2000-01-01T00:00:00Z';
const VALID = '2099-01-01T00:00:00Z';

/** The API's SR-10 rule: the current refresh token rotates; a replaced one revokes the session. */
function sessionServer(sessionId: string) {
  let serial = 1;
  let revoked = false;
  const api = mockApi().on('POST', REFRESH, (request) => {
    const presented = (request.body as { refreshToken: string }).refreshToken;
    if (revoked || presented !== refreshTokenOf(sessionId, serial)) {
      revoked = true;
      return problem(401, 'AUTHENTICATION_REQUIRED');
    }
    serial += 1;
    return { body: pair(sessionId, serial, VALID) };
  });
  return {
    api,
    revoked: () => revoked,
    refreshes: () => api.requestsTo('POST', REFRESH).length,
  };
}

describe('two tabs holding one session (DEV-D1-01)', () => {
  const SID = '47a7806c-0000-7000-8000-000000000001';

  test('a duplicated tab takes the pair the first tab rotated to, without presenting the replaced token', async () => {
    const server = sessionServer(SID);
    const first = browser().tab();
    first.store.setSession(pair(SID, 1, EXPIRED));
    const second = first.duplicate();

    expect(await first.store.refresh()).toBe(true);
    await new Promise((resolve) => setTimeout(resolve, 5));
    // The second tab's request went out with the access token both tabs started with, and got 401.
    expect(await second.store.refresh('access-1')).toBe(true);

    expect(server.refreshes()).toBe(1);
    expect(server.revoked()).toBe(false);
    expect(second.store.getSnapshot().session?.accessToken).toBe('access-2');
    expect(second.store.getSnapshot().session?.refreshToken).toBe(refreshTokenOf(SID, 2));
  });

  test('tabs that refresh at the same moment send one refresh and both adopt its pair', async () => {
    const server = sessionServer(SID);
    const first = browser().tab();
    first.store.setSession(pair(SID, 1, EXPIRED));
    const second = first.duplicate();

    expect(await Promise.all([first.store.refresh(), second.store.refresh()])).toEqual([
      true,
      true,
    ]);

    expect(server.refreshes()).toBe(1);
    expect(server.revoked()).toBe(false);
    expect(second.store.getSnapshot().session?.refreshToken).toBe(
      first.store.getSnapshot().session?.refreshToken,
    );
  });

  test('a tab that missed a rotation takes the newest pair from its peers before refreshing', async () => {
    const server = sessionServer(SID);
    const tabs = browser();
    const first = tabs.tab();
    first.store.setSession(pair(SID, 1, EXPIRED));
    const staleCopy = new Map([
      ['pmplatform.session', JSON.stringify(pair(SID, 1, EXPIRED))],
      ['pmplatform.session.generation', '0'],
    ]);
    expect(await first.store.refresh()).toBe(true);

    // Restored from the copy taken before the rotation, so it never heard of it: it holds the replaced token.
    const woken = tabs.tab(staleCopy);
    expect(await woken.store.refresh()).toBe(true);

    expect(server.refreshes()).toBe(1);
    expect(server.revoked()).toBe(false);
    expect(woken.store.getSnapshot().session?.refreshToken).toBe(refreshTokenOf(SID, 2));
    expect(staleCopy.get('pmplatform.session.generation')).toBe('1');
  });

  test('a stolen replaced token presented elsewhere still revokes the session, and every tab is told', async () => {
    const server = sessionServer(SID);
    const first = browser().tab();
    first.store.setSession(pair(SID, 1, EXPIRED));
    const second = first.duplicate();
    expect(await first.store.refresh()).toBe(true);
    await new Promise((resolve) => setTimeout(resolve, 5));

    // The thief's copy is not a tab of this browser: it presents the replaced token straight to the API.
    const { sessionApi } = await import('./sessionApi.ts');
    await expect(sessionApi.refresh(refreshTokenOf(SID, 1))).rejects.toMatchObject({ status: 401 });
    expect(server.revoked()).toBe(true);

    expect(await first.store.refresh()).toBe(false);
    await new Promise((resolve) => setTimeout(resolve, 5));

    expect(first.store.getSnapshot()).toMatchObject({ session: null, endReason: 'ENDED' });
    expect(second.store.getSnapshot()).toMatchObject({ session: null, endReason: 'ENDED' });
  });

  test('signing out in one tab signs the other tabs of the session out, saying where', async () => {
    mockApi().on('POST', SIGN_OUT, { status: 204 });
    const first = browser().tab();
    first.store.setSession(pair(SID, 1, VALID));
    const second = first.duplicate();

    await first.store.endSession();
    await new Promise((resolve) => setTimeout(resolve, 5));

    expect(first.store.getSnapshot()).toMatchObject({ session: null, endReason: null });
    expect(second.store.getSnapshot()).toMatchObject({
      session: null,
      endReason: 'SIGNED_OUT_ELSEWHERE',
    });
  });

  test("a tab of another session ignores this session's pairs and endings", async () => {
    const server = sessionServer(SID);
    const tabs = browser();
    const first = tabs.tab();
    first.store.setSession(pair(SID, 1, EXPIRED));
    const other = tabs.tab();
    const otherSession = pair('99999999-0000-7000-8000-000000000002', 7, VALID);
    other.store.setSession(otherSession);

    expect(await first.store.refresh()).toBe(true);
    await new Promise((resolve) => setTimeout(resolve, 5));

    expect(server.refreshes()).toBe(1);
    expect(other.store.getSnapshot().session).toEqual(otherSession);
  });
});
