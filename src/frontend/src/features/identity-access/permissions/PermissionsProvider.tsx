import { type ReactElement, type ReactNode, useCallback, useMemo } from 'react';

import { useApiResource } from '@/shared/api/useApiResource.ts';

import { useSession } from '../session/useSession.ts';
import { PermissionsContext, permissionsOf } from './permissions.ts';
import { permissionsApi } from './permissionsApi.ts';

/** Asks the API what the signed-in person holds, once per signed-in user, for every screen below it. */
export function PermissionsProvider({ children }: { children: ReactNode }): ReactElement {
  const { session } = useSession();
  const userId = session?.user.id ?? null;
  const load = useCallback(
    (signal: AbortSignal) =>
      userId === null ? Promise.resolve(null) : permissionsApi.current(signal),
    [userId],
  );
  const { data } = useApiResource(load);
  const value = useMemo(() => permissionsOf(data), [data]);
  return <PermissionsContext.Provider value={value}>{children}</PermissionsContext.Provider>;
}
