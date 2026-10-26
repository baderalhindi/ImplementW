import { useCallback, useEffect, useState } from 'react';

export type ResourceLoader<T> = (signal: AbortSignal) => Promise<T>;

interface Settled<T> {
  load: ResourceLoader<T>;
  version: number;
  data: T | undefined;
  error: unknown;
}

export interface ApiResource<T> {
  data: T | undefined;
  error: unknown;
  loading: boolean;
  reload: () => void;
}

export interface ApiResourceOptions {
  /**
   * Keeps the last result of the same `load` while `reload` runs, so a screen with a dialog open over it is not
   * unmounted by a refresh. `loading` still says a run is under way; a new `load` (another filter) never shows the old.
   */
  keepWhileReloading?: boolean;
}

/**
 * Runs `load` whenever it changes (pass a useCallback) or `reload` is called, aborting the previous run. `loading`
 * is true until the current run settles, so a screen never shows a previous filter's rows as the current result.
 */
export function useApiResource<T>(
  load: ResourceLoader<T>,
  { keepWhileReloading = false }: ApiResourceOptions = {},
): ApiResource<T> {
  const [version, setVersion] = useState(0);
  const [settled, setSettled] = useState<Settled<T> | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    load(controller.signal).then(
      (data) => {
        setSettled({ load, version, data, error: null });
      },
      (error: unknown) => {
        if (!controller.signal.aborted) {
          setSettled({ load, version, data: undefined, error });
        }
      },
    );
    return () => {
      controller.abort();
    };
  }, [load, version]);

  const reload = useCallback(() => {
    setVersion((current) => current + 1);
  }, []);

  const current = settled?.load === load && settled.version === version ? settled : null;
  // A loader may resolve to null (no record yet), which is a result, not a missing one.
  const shown = current ?? (keepWhileReloading && settled?.load === load ? settled : null);
  return {
    data: shown?.data,
    error: current?.error ?? null,
    loading: current === null,
    reload,
  };
}
