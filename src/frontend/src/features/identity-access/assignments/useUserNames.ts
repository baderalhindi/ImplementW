import { useCallback } from 'react';

import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';

import { usersApi } from '../api/identityAccessApi.ts';
import { useSession } from '../session/useSession.ts';

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

const SHORT_ID_LENGTH = 8;

/**
 * A name for each person a screen shows by id. Reading a user needs USER_VIEW, which many callers do not hold: an id
 * that cannot be read is shown shortened ("User b2b2b2b2"), and the signed-in person is always "You".
 */
export function usePersonNames(ids: (string | null)[]): (id: string | null) => string {
  const { t } = useI18n();
  const { session } = useSession();
  const selfId = session?.user.id ?? null;
  const names = useUserNames(ids.filter((id) => id !== selfId));
  return (id) => {
    if (id === null) {
      return '—';
    }
    if (id === selfId) {
      return t('common.people.you');
    }
    return names.get(id) ?? t('common.people.unknownUser', { id: id.slice(0, SHORT_ID_LENGTH) });
  };
}
