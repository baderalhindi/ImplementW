import { type ReactElement, type ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router';

import { useSession } from './useSession.ts';

/** Shows a route only to a signed-in person; otherwise sends them to sign in and back to where they were going. */
export function RequireSession({ children }: { children: ReactNode }): ReactElement {
  const { session } = useSession();
  const location = useLocation();
  if (session === null) {
    return (
      <Navigate to="/sign-in" replace state={{ from: `${location.pathname}${location.search}` }} />
    );
  }
  return <>{children}</>;
}
