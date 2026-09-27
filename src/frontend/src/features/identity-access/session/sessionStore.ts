import { configureAuthentication } from '@/shared/api/httpClient.ts';

import { type Session, sessionApi } from './sessionApi.ts';

// The signed-in session, outside React so the HTTP client can read it on every request (TASK-032 D-3). Kept in
// sessionStorage: it survives a reload of the tab and ends when the tab closes; nothing is written to localStorage.

const STORAGE_KEY = 'pmplatform.session';

export interface SessionSnapshot {
  session: Session | null;
  /** True while a privileged action waits for the administrator to confirm a fresh second factor (ADR-010). */
  stepUpPending: boolean;
}

type Listener = () => void;

function readStored(): Session | null {
  try {
    const stored = sessionStorage.getItem(STORAGE_KEY);
    return stored === null ? null : (JSON.parse(stored) as Session);
  } catch {
    return null;
  }
}

function writeStored(session: Session | null): void {
  try {
    if (session === null) {
      sessionStorage.removeItem(STORAGE_KEY);
    } else {
      sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session));
    }
  } catch {
    // Storage unavailable: the session then lasts until the page is reloaded.
  }
}

let snapshot: SessionSnapshot = { session: readStored(), stepUpPending: false };
const listeners = new Set<Listener>();
let refreshInFlight: Promise<boolean> | null = null;
let resolveStepUp: ((steppedUp: boolean) => void) | null = null;

function update(next: Partial<SessionSnapshot>): void {
  snapshot = { ...snapshot, ...next };
  listeners.forEach((listener) => {
    listener();
  });
}

export const sessionStore = {
  subscribe: (listener: Listener): (() => void) => {
    listeners.add(listener);
    return () => {
      listeners.delete(listener);
    };
  },

  getSnapshot: (): SessionSnapshot => {
    return snapshot;
  },

  setSession: (session: Session | null): void => {
    writeStored(session);
    update({ session });
  },

  signOut: (): void => {
    resolveStepUp?.(false);
    resolveStepUp = null;
    writeStored(null);
    update({ session: null, stepUpPending: false });
  },

  /** One refresh at a time: requests that fail together with 401 share it. */
  refresh: (): Promise<boolean> => {
    const refreshToken = snapshot.session?.refreshToken;
    if (refreshToken === undefined) {
      return Promise.resolve(false);
    }
    refreshInFlight ??= sessionApi
      .refresh(refreshToken)
      .then(
        (session) => {
          sessionStore.setSession(session);
          return true;
        },
        () => {
          sessionStore.signOut();
          return false;
        },
      )
      .finally(() => {
        refreshInFlight = null;
      });
    return refreshInFlight;
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

  completeStepUp: (session: Session | null): void => {
    if (session !== null) {
      sessionStore.setSession(session);
    }
    resolveStepUp?.(session !== null);
    resolveStepUp = null;
    update({ stepUpPending: false });
  },
};

configureAuthentication({
  accessToken: () => snapshot.session?.accessToken ?? null,
  refresh: sessionStore.refresh,
  stepUp: sessionStore.requestStepUp,
});
