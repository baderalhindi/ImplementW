import { useCallback } from 'react';

import { useSession } from '@/features/identity-access/session/useSession.ts';
import { type ApiResource, useApiResource } from '@/shared/api/useApiResource.ts';

import { auditActivityApi } from './api/auditActivityApi.ts';
import { type AuditAccessView } from './api/types.ts';

/**
 * What the signed-in person may read of FG-06, asked of the API each time a screen that needs it opens, so a grant
 * changed meanwhile is reflected (Step 16: "refetch only with current authority"). Null with no session: nothing is
 * asked.
 */
export function useAuditAccess(): ApiResource<AuditAccessView | null> {
  const { session } = useSession();
  const userId = session?.user.id ?? null;
  const load = useCallback(
    (signal: AbortSignal) =>
      userId === null ? Promise.resolve(null) : auditActivityApi.access(signal),
    [userId],
  );
  return useApiResource(load);
}
