import { useEffect, useRef, useSyncExternalStore } from 'react';

import { notificationsApi } from './api/notificationsApi.ts';

// The unread badge (TASK-040). WF-15 has no push channel (TASK-039 F-17): the count is read again every poll interval
// while someone is signed in, when the tab becomes visible again, and right after this person reads something. The
// count shown is always the API's last answer; nothing is counted or adjusted in the browser.

/** How often the badge asks the API. The worker routes every 15 s (TASK-039 `Notifications:Worker`). */
export const UNREAD_POLL_INTERVAL_MS = 30_000;

export interface UnreadSnapshot {
  /** The API's last answer, or null before the first one. */
  count: number | null;
  /** Moves each time a later answer differs from the one before, so an open inbox knows to read its page again. */
  revision: number;
}

const INITIAL: UnreadSnapshot = { count: null, revision: 0 };

let snapshot = INITIAL;
const listeners = new Set<() => void>();
let inFlight: AbortController | null = null;

function publish(next: UnreadSnapshot): void {
  snapshot = next;
  listeners.forEach((listener) => {
    listener();
  });
}

function accept(count: number): void {
  if (count === snapshot.count) {
    return;
  }
  publish({
    count,
    revision: snapshot.count === null ? snapshot.revision : snapshot.revision + 1,
  });
}

export const unreadCountStore = {
  subscribe: (listener: () => void): (() => void) => {
    listeners.add(listener);
    return () => {
      listeners.delete(listener);
    };
  },

  getSnapshot: (): UnreadSnapshot => snapshot,

  /**
   * Reads the count now. A read still in flight is abandoned, so the latest question's answer is the one shown. A failed
   * read keeps the last count; the next poll tries again.
   */
  refresh: async (): Promise<void> => {
    inFlight?.abort();
    const controller = new AbortController();
    inFlight = controller;
    try {
      const { unreadCount } = await notificationsApi.countUnread(controller.signal);
      if (inFlight === controller) {
        accept(unreadCount);
      }
    } catch {
      // Aborted, offline or refused: the badge keeps its last answer until the next poll.
    } finally {
      if (inFlight === controller) {
        inFlight = null;
      }
    }
  },

  /** Polls while the returned function has not been called; called when the session's shell mounts and unmounts. */
  watch: (): (() => void) => {
    const refreshIfVisible = () => {
      if (document.visibilityState !== 'hidden') {
        void unreadCountStore.refresh();
      }
    };
    const timer = setInterval(refreshIfVisible, UNREAD_POLL_INTERVAL_MS);
    document.addEventListener('visibilitychange', refreshIfVisible);
    void unreadCountStore.refresh();
    return () => {
      clearInterval(timer);
      document.removeEventListener('visibilitychange', refreshIfVisible);
      inFlight?.abort();
      inFlight = null;
      publish(INITIAL);
    };
  },
};

export function useUnreadCount(): UnreadSnapshot {
  return useSyncExternalStore(unreadCountStore.subscribe, unreadCountStore.getSnapshot);
}

/** Calls `reload` when the count moves after the screen opened: a notification arrived or was read elsewhere. */
export function useReloadOnUnreadChange(reload: () => void): void {
  const { revision } = useUnreadCount();
  const seen = useRef(revision);
  useEffect(() => {
    if (seen.current !== revision) {
      seen.current = revision;
      reload();
    }
  }, [revision, reload]);
}
