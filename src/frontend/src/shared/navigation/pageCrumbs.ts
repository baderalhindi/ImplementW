import { useEffect, useSyncExternalStore } from 'react';

/** A step the page adds to the shell's breadcrumb: a link, or the page itself when `to` is null. */
export interface PageCrumb {
  label: string;
  to: string | null;
}

// The steps below the menu that only the page knows (review round 8, point 16): a project's name and its open tab. The shell's breadcrumb reads them;
// the page sets them while it is shown and clears them when it goes.

let crumbs: readonly PageCrumb[] = [];
const listeners = new Set<() => void>();

function publish(next: readonly PageCrumb[]): void {
  crumbs = next;
  listeners.forEach((listener) => {
    listener();
  });
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

/** The steps the page shown has set, for the breadcrumb. */
export function usePageCrumbs(): readonly PageCrumb[] {
  return useSyncExternalStore(subscribe, () => crumbs);
}

/** Sets the breadcrumb's steps under the menu while the calling page is shown. `key` stands for the steps: they are set again when it changes. */
export function useSetPageCrumbs(next: readonly PageCrumb[], key: string): void {
  useEffect(() => {
    publish(next);
    return () => {
      publish([]);
    };
    // key stands for next, rebuilt on every render.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key]);
}
