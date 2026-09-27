import { useCallback } from 'react';

import { useApiResource } from '@/shared/api/useApiResource.ts';

import { usersApi } from '../api/identityAccessApi.ts';

/**
 * Display names for the user ids an assignment page shows (the assigned users and their sponsors). An assignment
 * carries ids only, and a page holds at most 25 rows, so each distinct id is read once.
 */
export function useUserNames(ids: (string | null)[]): Map<string, string> {
  const key = [...new Set(ids.filter((id): id is string => id !== null))].sort().join(',');
  const load = useCallback(
    async (signal: AbortSignal) => {
      const unique = key === '' ? [] : key.split(',');
      const users = await Promise.all(
        unique.map((id) =>
          usersApi.get(id, signal).then(
            (response) => response.data,
            () => null,
          ),
        ),
      );
      return new Map(
        users.filter((user) => user !== null).map((user) => [user.id, user.displayName]),
      );
    },
    [key],
  );
  return useApiResource(load).data ?? new Map<string, string>();
}
