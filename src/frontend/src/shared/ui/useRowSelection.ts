import { useCallback, useMemo, useState } from 'react';

export interface RowSelection {
  selected: ReadonlySet<string>;
  toggle: (id: string, on: boolean) => void;
  setAll: (ids: readonly string[], on: boolean) => void;
  clear: () => void;
}

export function useRowSelection(): RowSelection {
  const [selected, setSelected] = useState<ReadonlySet<string>>(new Set());
  const toggle = useCallback((id: string, on: boolean) => {
    setSelected((current) => {
      const next = new Set(current);
      if (on) {
        next.add(id);
      } else {
        next.delete(id);
      }
      return next;
    });
  }, []);
  const setAll = useCallback((ids: readonly string[], on: boolean) => {
    setSelected((current) => {
      const next = new Set(current);
      ids.forEach((id) => (on ? next.add(id) : next.delete(id)));
      return next;
    });
  }, []);
  const clear = useCallback(() => {
    setSelected(new Set());
  }, []);
  return useMemo(() => ({ selected, toggle, setAll, clear }), [clear, selected, setAll, toggle]);
}
