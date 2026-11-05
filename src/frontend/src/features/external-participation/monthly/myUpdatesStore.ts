import { useSyncExternalStore } from 'react';

import { UNREAD_POLL_INTERVAL_MS } from '@/features/notifications/unreadCount.ts';

import { myUpdateRequestsApi } from '../api/monthlyUpdatesApi.ts';
import { type MyUpdateRequest, type MyUpdateRequestPage } from '../api/monthlyUpdateTypes.ts';

// The monthly updates waiting on the signed-in person (the 5 Nov 2026 review §2.1), outside React because the window, the bell's pinned
// item and the Home read one answer. Read again on the unread badge's cadence and right after a submit; the counts are the API's.

export interface MyUpdatesSnapshot {
  /** The API's last answer, or null before the first one. */
  page: MyUpdateRequestPage | null;
  /** What this person submitted since signing in: the window's progress, kept once the API no longer lists them. */
  submitted: MyUpdateRequest[];
  windowOpen: boolean;
}

const INITIAL: MyUpdatesSnapshot = { page: null, submitted: [], windowOpen: false };

let snapshot = INITIAL;
const listeners = new Set<() => void>();
let inFlight: AbortController | null = null;

function publish(next: Partial<MyUpdatesSnapshot>): void {
  snapshot = { ...snapshot, ...next };
  listeners.forEach((listener) => {
    listener();
  });
}

export const myUpdatesStore = {
  subscribe: (listener: () => void): (() => void) => {
    listeners.add(listener);
    return () => {
      listeners.delete(listener);
    };
  },

  getSnapshot: (): MyUpdatesSnapshot => snapshot,

  /** Reads the list now. A read still in flight is abandoned; a failed read keeps the last answer until the next poll. */
  refresh: async (): Promise<void> => {
    inFlight?.abort();
    const controller = new AbortController();
    inFlight = controller;
    try {
      const page = await myUpdateRequestsApi.listAll(controller.signal);
      if (inFlight === controller) {
        publish({ page });
      }
    } catch {
      // Aborted, offline or refused: the window and the bell keep the last answer.
    } finally {
      if (inFlight === controller) {
        inFlight = null;
      }
    }
  },

  openWindow: (): void => {
    publish({ windowOpen: true });
  },

  closeWindow: (): void => {
    publish({ windowOpen: false });
  },

  /** Moves a project off the window's list at once; the counts stay the API's, read again by the caller. */
  markSubmitted: (request: MyUpdateRequest): void => {
    publish({
      submitted: [...snapshot.submitted, request],
      page:
        snapshot.page === null
          ? null
          : {
              ...snapshot.page,
              items: snapshot.page.items.filter((item) => item.requestId !== request.requestId),
            },
    });
  },

  /** Polls while the returned function has not been called; called when the session's shell mounts and unmounts. */
  watch: (): (() => void) => {
    const refreshIfVisible = () => {
      if (document.visibilityState !== 'hidden') {
        void myUpdatesStore.refresh();
      }
    };
    const timer = setInterval(refreshIfVisible, UNREAD_POLL_INTERVAL_MS);
    document.addEventListener('visibilitychange', refreshIfVisible);
    void myUpdatesStore.refresh();
    return () => {
      clearInterval(timer);
      document.removeEventListener('visibilitychange', refreshIfVisible);
      inFlight?.abort();
      inFlight = null;
      snapshot = INITIAL;
      listeners.forEach((listener) => {
        listener();
      });
    };
  },
};

export function useMyUpdates(): MyUpdatesSnapshot {
  return useSyncExternalStore(myUpdatesStore.subscribe, myUpdatesStore.getSnapshot);
}
