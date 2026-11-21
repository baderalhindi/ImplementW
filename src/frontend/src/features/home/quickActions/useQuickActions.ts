import { useCallback, useState } from 'react';

import { useApiResource } from '@/shared/api/useApiResource.ts';

import { quickActionsApi } from './quickActionsApi.ts';

export interface QuickActionsState {
  /** The codes chosen, in order; null while the person keeps their role's set, or until the choice is read. */
  saved: string[] | null;
  /** Whether the choice has been read, so the role's set is not shown in place of a choice still on its way. */
  loaded: boolean;
  /** Saves a choice, or null for the role's set; resolves false when the server refuses it. */
  save: (actions: string[] | null) => Promise<boolean>;
}

/** This person's quick actions, read from the server and saved there, so they follow the person across browsers and sign-ins. */
export function useQuickActions(): QuickActionsState {
  const read = useApiResource(
    useCallback((signal: AbortSignal) => quickActionsApi.read(signal), []),
  );
  // A choice saved here; until then, the choice read.
  const [stored, setStored] = useState<{ actions: string[] | null } | null>(null);

  const save = async (actions: string[] | null) => {
    try {
      setStored(await quickActionsApi.save(actions));
      return true;
    } catch {
      return false;
    }
  };

  const current = stored ?? read.data ?? null;
  // A read that failed leaves the role's set showing, as before this point.
  return { saved: current?.actions ?? null, loaded: current !== null || !read.loading, save };
}
