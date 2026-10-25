import { useCallback } from 'react';

import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';

import { MAX_NAMES_PER_CALL, namesApi } from '../names/namesApi.ts';
import { useSession } from '../session/useSession.ts';

/**
 * Display names for the user ids a screen shows, read in one call per hundred ids from `GET /display-names`, which every signed-in
 * person may ask (the 24 Oct 2026 review, H1). An id the API does not name is left out, and shown by its short form.
 */
export function useUserNames(ids: (string | null)[]): Map<string, string> {
  const key = [...new Set(ids.filter((id): id is string => id !== null))].sort().join(',');
  const load = useCallback(
    async (signal: AbortSignal) => {
      const unique = key === '' ? [] : key.split(',');
      const batches = Array.from(
        { length: Math.ceil(unique.length / MAX_NAMES_PER_CALL) },
        (_, index) => unique.slice(index * MAX_NAMES_PER_CALL, (index + 1) * MAX_NAMES_PER_CALL),
      );
      const people = (
        await Promise.all(batches.map((batch) => namesApi.people(batch, signal)))
      ).flat();
      return new Map(people.map((person) => [person.id, person.displayName]));
    },
    [key],
  );
  return useApiResource(load).data ?? new Map<string, string>();
}

const SHORT_ID_LENGTH = 8;

/**
 * A name for each person a screen shows by id: an id the API does not name — a person the caller may not know of, as an
 * external user may not know AHDA's — is shown shortened ("User b2b2b2b2"), and the signed-in person is always "You".
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
