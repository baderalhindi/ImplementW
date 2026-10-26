import { useSyncExternalStore } from 'react';

import { type SessionSnapshot, sessionStore } from './sessionStore.ts';

export const SYSTEM_ADMINISTRATOR_ROLE = 'R01';

export function useSession(): SessionSnapshot {
  return useSyncExternalStore(sessionStore.subscribe, sessionStore.getSnapshot);
}

export function useHasRole(roleCode: string): boolean {
  const { session } = useSession();
  return (
    session?.user.roleAssignments.some((assignment) => assignment.roleCode === roleCode) ?? false
  );
}
