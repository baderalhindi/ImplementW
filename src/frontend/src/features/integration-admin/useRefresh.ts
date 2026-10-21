import { useEffect } from 'react';

/**
 * Calls `reload` every `refreshEvery` milliseconds while the component is mounted; nothing without an interval. ADM-047
 * passes FG-05's pass interval so its figures and lists stay live (TASK-092).
 */
export function useRefresh(reload: () => void, refreshEvery: number | undefined): void {
  useEffect(() => {
    if (refreshEvery === undefined) {
      return undefined;
    }
    const timer = window.setInterval(reload, refreshEvery);
    return () => {
      window.clearInterval(timer);
    };
  }, [refreshEvery, reload]);
}
