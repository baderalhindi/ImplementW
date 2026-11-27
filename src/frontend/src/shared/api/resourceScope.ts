import { createContext, useContext, useMemo } from 'react';

import { type ApiResource, type ResourceLoader, useApiResource } from './useApiResource.ts';

// Reference data that many parts of one screen read (master data, organisation units, the governance profile): inside a ResourceScope
// each loader runs once and every reader shares its answer, so a dashboard of twenty visuals sends one request per resource, not twenty
// (stage QA F-05). Outside a scope each reader runs its own load, as useApiResource does.

/** The answers of one screen's shared loaders, keyed by the loader itself (a module-level function). */
export class ResourceScopeStore {
  private controller = new AbortController();
  private readonly runs = new Map<ResourceLoader<unknown>, Promise<unknown>>();

  /** The loader's answer, started on the first read; a reader's own signal only stops that reader waiting. */
  read<T>(load: ResourceLoader<T>, signal: AbortSignal): Promise<T> {
    let run = this.runs.get(load) as Promise<T> | undefined;
    if (run === undefined) {
      const started = load(this.controller.signal);
      run = started;
      this.runs.set(load, started);
      // A failed read is not kept: the next reader to mount tries again.
      started.catch(() => {
        if (this.runs.get(load) === started) {
          this.runs.delete(load);
        }
      });
    }
    return untilAborted(run, signal);
  }

  /** Stops the runs under way and forgets every answer (the screen has gone). */
  dispose(): void {
    this.controller.abort();
    this.controller = new AbortController();
    this.runs.clear();
  }
}

function untilAborted<T>(run: Promise<T>, signal: AbortSignal): Promise<T> {
  return new Promise<T>((resolve, reject) => {
    if (signal.aborted) {
      reject(abortReason(signal));
      return;
    }
    signal.addEventListener(
      'abort',
      () => {
        reject(abortReason(signal));
      },
      { once: true },
    );
    run.then(resolve, reject);
  });
}

/** Why the reader stopped: the signal's own reason when it is an Error, as fetch rejects with, else an AbortError. */
function abortReason(signal: AbortSignal): Error {
  return signal.reason instanceof Error ? signal.reason : new DOMException('Aborted', 'AbortError');
}

export const ResourceScopeContext = createContext<ResourceScopeStore | null>(null);

/**
 * `useApiResource` for a loader whose answer is the same for every reader on the screen: inside a ResourceScope it is read once and shared
 * (pass a module-level function, which is the key). `reload` gives the shared answer again rather than reading anew.
 */
export function useScopedResource<T>(load: ResourceLoader<T>): ApiResource<T> {
  const scope = useContext(ResourceScopeContext);
  const scoped = useMemo<ResourceLoader<T>>(
    () => (scope === null ? load : (signal) => scope.read(load, signal)),
    [scope, load],
  );
  return useApiResource(scoped);
}
