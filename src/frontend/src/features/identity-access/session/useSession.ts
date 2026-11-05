import { useSyncExternalStore } from 'react';

import { type SessionSnapshot, sessionStore } from './sessionStore.ts';

export function useSession(): SessionSnapshot {
  return useSyncExternalStore(sessionStore.subscribe, sessionStore.getSnapshot);
}
