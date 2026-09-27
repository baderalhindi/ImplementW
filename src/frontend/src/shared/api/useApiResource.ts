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

/**
 * Runs `load` whenever it changes (pass a useCallback) or `reload` is called, aborting the previous run. `loading`
 * is true until the current run settles, so a screen never shows a previous filter's rows as the current result.
 */
export function useApiResource<T>(load: ResourceLoader<T>): ApiResource<T> {
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
  return {
    data: current?.data,
    error: current?.error ?? null,
    loading: current === null,
    reload,
  };
}
