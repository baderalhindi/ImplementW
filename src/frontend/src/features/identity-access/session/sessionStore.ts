import { ApiError, configureAuthentication } from '@/shared/api/httpClient.ts';

import { type Session, sessionApi } from './sessionApi.ts';

// The signed-in session, outside React so the HTTP client can read it on every request (TASK-032 D-3). Kept in
// sessionStorage: it survives a reload of the tab and ends when the tab closes; nothing is written to localStorage.
//
// DEV-D1-01: a duplicated tab (Chrome's "Duplicate tab", a window.open with an opener) starts with a copy of this
// tab's sessionStorage, so two tabs hold one session and one refresh token. Each refresh token works once and a
// replaced one presented again revokes the session (SR-10), so the tabs of one session take turns: whoever uses the
// refresh token does it under a Web Lock named by the session id, first asks the other tabs for a newer token pair,
// and hands the pair it gets to them over a BroadcastChannel. The server's reuse rule is unchanged.

const STORAGE_KEY = 'pmplatform.session';
/** How many times this tab's session has been rotated: of two pairs of one session, the higher one is the newer. */
const GENERATION_KEY = 'pmplatform.session.generation';
const CHANNEL_NAME = 'pmplatform.session';
/** How long a tab about to use the refresh token waits for the others to answer with a newer pair. */
const PEER_ANSWER_MS = 250;
/** An access token this close to its expiry is treated as expired. */
const EXPIRY_MARGIN_MS = 5_000;

/** Why the session ended without the person signing out in this tab: the sign-in page says so. */
export type SessionEndReason = 'ENDED' | 'SIGNED_OUT_ELSEWHERE';

export interface SessionSnapshot {
  session: Session | null;
  /** True while a privileged action waits for the administrator to confirm a fresh second factor (ADR-010). */
  stepUpPending: boolean;
  /** Set once the session has ended on its own, or in another tab; cleared by the next sign-in. */
  endReason: SessionEndReason | null;
}

/** What the tabs of one browser say to each other about a session they share. */
export type TabMessage =
  | { type: 'ASK'; sessionId: string }
  | { type: 'ROTATED'; sessionId: string; generation: number; session: Session }
  | { type: 'ENDED'; sessionId: string; reason: SessionEndReason };

export interface TabChannel {
  postMessage: (message: TabMessage) => void;
  onmessage: ((event: MessageEvent<TabMessage>) => void) | null;
}

/** Where a store keeps its session and how it reaches the browser's other tabs. */
export interface SessionStoreEnvironment {
  storage: Pick<Storage, 'getItem' | 'setItem' | 'removeItem'> | null;
  /** Null where the browser has no BroadcastChannel: the tab then works alone. */
  channel: TabChannel | null;
  /** Runs a task while holding the named lock, across the browser's tabs. */
  exclusive: <T>(name: string, task: () => Promise<T>) => Promise<T>;
  peerAnswerMs: number;
}

type Listener = () => void;

/** The session id (`sid`) the API wrote into the refresh token; null if it cannot be read. Not a check of the token. */
export function sessionIdOf(session: Session | null): string | null {
  const payload = session?.refreshToken.split('.')[1];
  if (payload === undefined) {
    return null;
  }
  try {
    const json = atob(payload.replace(/-/g, '+').replace(/_/g, '/'));
    const sid = (JSON.parse(json) as { sid?: unknown }).sid;
    return typeof sid === 'string' ? sid : null;
  } catch {
    return null;
  }
}

function expired(session: Session): boolean {
  return Date.parse(session.accessTokenExpiresAt) - EXPIRY_MARGIN_MS <= Date.now();
}

export function createSessionStore(environment: SessionStoreEnvironment) {
  const { storage, channel } = environment;

  function readStored(): { session: Session | null; generation: number } {
    try {
      const stored = storage?.getItem(STORAGE_KEY) ?? null;
      return {
        session: stored === null ? null : (JSON.parse(stored) as Session),
        generation: Number(storage?.getItem(GENERATION_KEY) ?? 0) || 0,
      };
    } catch {
      return { session: null, generation: 0 };
    }
  }

  function writeStored(session: Session | null, generation: number): void {
    try {
      if (session === null) {
        storage?.removeItem(STORAGE_KEY);
        storage?.removeItem(GENERATION_KEY);
      } else {
        storage?.setItem(STORAGE_KEY, JSON.stringify(session));
        storage?.setItem(GENERATION_KEY, String(generation));
      }
    } catch {
      // Storage unavailable: the session then lasts until the page is reloaded.
    }
  }

  const stored = readStored();
  let generation = stored.generation;
  let snapshot: SessionSnapshot = {
    session: stored.session,
    stepUpPending: false,
    endReason: null,
  };
  const listeners = new Set<Listener>();
  let refreshInFlight: Promise<boolean> | null = null;
  let resolveStepUp: ((steppedUp: boolean) => void) | null = null;

  function update(next: Partial<SessionSnapshot>): void {
    snapshot = { ...snapshot, ...next };
    listeners.forEach((listener) => {
      listener();
    });
  }

  function keep(session: Session | null, nextGeneration: number): void {
    generation = nextGeneration;
    writeStored(session, nextGeneration);
    update({ session });
  }

  function forget(endReason: SessionEndReason | null): void {
    resolveStepUp?.(false);
    resolveStepUp = null;
    keep(null, 0);
    update({ stepUpPending: false, endReason });
  }

  function post(message: TabMessage): void {
    try {
      channel?.postMessage(message);
    } catch {
      // A closed channel: the other tabs find out at their next refresh.
    }
  }

  if (channel !== null) {
    channel.onmessage = ({ data }) => {
      const session = snapshot.session;
      const sessionId = sessionIdOf(session);
      if (session === null || sessionId === null || data.sessionId !== sessionId) {
        return;
      }
      if (data.type === 'ASK') {
        post({ type: 'ROTATED', sessionId, generation, session });
      } else if (data.type === 'ROTATED') {
        if (data.generation > generation) {
          keep(data.session, data.generation);
        }
      } else {
        forget(data.reason);
      }
    };
  }

  /** Asks the other tabs of this session for a newer pair; one that answers in time is adopted by `onmessage`. */
  async function catchUp(sessionId: string): Promise<void> {
    if (channel === null) {
      return;
    }
    post({ type: 'ASK', sessionId });
    await new Promise((resolve) => setTimeout(resolve, environment.peerAnswerMs));
  }

  /**
   * Runs `task` with the session's newest refresh token while no other tab of the session uses it, and hands the pair
   * `task` answers to them before letting them go. Null when there is no session any more.
   */
  async function withNewestSession<T>(
    task: (session: Session) => Promise<{ result: T; next: Session | null }>,
  ): Promise<T | null> {
    const sessionId = sessionIdOf(snapshot.session);
    const run = async (): Promise<T | null> => {
      if (sessionId !== null) {
        await catchUp(sessionId);
      }
      const current = snapshot.session;
      if (current === null || sessionIdOf(current) !== sessionId) {
        return null;
      }
      const { result, next } = await task(current);
      if (next !== null) {
        keep(next, generation + 1);
        if (sessionId !== null) {
          post({ type: 'ROTATED', sessionId, generation, session: next });
        }
      }
      return result;
    };
    return sessionId === null
      ? run()
      : environment.exclusive(`${CHANNEL_NAME}.refresh.${sessionId}`, run);
  }

  return {
    subscribe: (listener: Listener): (() => void) => {
      listeners.add(listener);
      return () => {
        listeners.delete(listener);
      };
    },

    getSnapshot: (): SessionSnapshot => {
      return snapshot;
    },

    /** A new sign-in, or none. */
    setSession: (session: Session | null): void => {
      keep(session, 0);
      if (session !== null) {
        update({ endReason: null });
      }
    },

    /**
     * The person signs out: the API ends the session (SR-10), then this tab and the others holding the session forget
     * it. Best effort: if the API cannot be reached or refuses, the tabs still forget the session, which then ends on
     * the server at its idle timeout.
     */
    endSession: async (): Promise<void> => {
      const sessionId = sessionIdOf(snapshot.session);
      if (snapshot.session !== null) {
        try {
          await sessionApi.signOut();
        } catch {
          // Signed out here regardless.
        }
      }
      if (sessionId !== null) {
        post({ type: 'ENDED', sessionId, reason: 'SIGNED_OUT_ELSEWHERE' });
      }
      forget(null);
    },

    /** Forgets the session in this tab only: once the API has already ended it. */
    signOut: (): void => {
      forget(null);
    },

    /**
     * After a 401 to `refusedAccessToken` (by default the current one): exchanges the refresh token for a new pair. Requests that fail together share one refresh, and
     * the tabs of one session one refresh per rotation: a tab that finds a newer, unexpired pair at another tab takes
     * it instead. A refused refresh ends the session in every tab that holds it.
     */
    refresh: (refusedAccessToken?: string | null): Promise<boolean> => {
      if (snapshot.session === null) {
        return Promise.resolve(false);
      }
      const refused = refusedAccessToken ?? snapshot.session.accessToken;
      refreshInFlight ??= withNewestSession(async (current) => {
        if (current.accessToken !== refused && !expired(current)) {
          return { result: true, next: null };
        }
        try {
          return { result: true, next: await sessionApi.refresh(current.refreshToken) };
        } catch (error) {
          const sessionId = sessionIdOf(current);
          if (error instanceof ApiError && error.status === 401 && sessionId !== null) {
            post({ type: 'ENDED', sessionId, reason: 'ENDED' });
          }
          forget('ENDED');
          return { result: false, next: null };
        }
      })
        .then((refreshed) => refreshed === true)
        .finally(() => {
          refreshInFlight = null;
        });
      return refreshInFlight;
    },

    /** Starts a step-up with the session's newest refresh token, which the API checks but does not replace. */
    beginStepUp: async <T>(call: (refreshToken: string) => Promise<T>): Promise<T> => {
      const result = await withNewestSession(async (current) => ({
        result: { value: await call(current.refreshToken) },
        next: null,
      }));
      if (result === null) {
        throw new ApiError(401, 'AUTHENTICATION_REQUIRED', [], null);
      }
      return result.value;
    },

    /** Completes a step-up with the session's newest refresh token; the pair it answers replaces it in every tab. */
    completeStepUp: async (call: (refreshToken: string) => Promise<Session>): Promise<void> => {
      const done = await withNewestSession(async (current) => ({
        result: true,
        next: await call(current.refreshToken),
      }));
      if (done === null) {
        throw new ApiError(401, 'AUTHENTICATION_REQUIRED', [], null);
      }
    },

    /** Asks the StepUpDialog for a fresh second factor; resolves once it is confirmed or cancelled. */
    requestStepUp: (): Promise<boolean> => {
      if (snapshot.session === null) {
        return Promise.resolve(false);
      }
      resolveStepUp?.(false);
      update({ stepUpPending: true });
      return new Promise<boolean>((resolve) => {
        resolveStepUp = resolve;
      });
    },

    /** The StepUpDialog closes: confirmed (after `completeStepUp`) or cancelled. */
    finishStepUp: (steppedUp: boolean): void => {
      resolveStepUp?.(steppedUp);
      resolveStepUp = null;
      update({ stepUpPending: false });
    },
  };
}

function browserEnvironment(): SessionStoreEnvironment {
  let storage: Storage | null = null;
  try {
    storage = sessionStorage;
  } catch {
    // Storage blocked: the session lasts until the page is reloaded.
  }
  const locks = typeof navigator !== 'undefined' ? navigator.locks : undefined;
  return {
    storage,
    channel: typeof BroadcastChannel === 'function' ? new BroadcastChannel(CHANNEL_NAME) : null,
    // Web Locks need a secure context; without them, asking the other tabs first still covers most turns.
    exclusive: async <T>(name: string, task: () => Promise<T>): Promise<T> => {
      if (locks === undefined) {
        return task();
      }
      const result: T = await locks.request(name, task);
      return result;
    },
    peerAnswerMs: PEER_ANSWER_MS,
  };
}

export const sessionStore = createSessionStore(browserEnvironment());

configureAuthentication({
  accessToken: () => sessionStore.getSnapshot().session?.accessToken ?? null,
  refresh: sessionStore.refresh,
  stepUp: sessionStore.requestStepUp,
});
