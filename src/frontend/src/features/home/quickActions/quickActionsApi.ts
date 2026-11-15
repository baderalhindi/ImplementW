import { apiRequest } from '@/shared/api/httpClient.ts';

/** The person's quick actions as the server keeps them (review round 8, point 08): the codes in order, or null while they keep their role's set. */
export interface QuickActionsPreference {
  actions: string[] | null;
}

export const quickActionsApi = {
  read: async (signal?: AbortSignal) =>
    (await apiRequest<QuickActionsPreference>('/users/current/quick-actions', { signal })).data,
  /** Saves the codes in order; null goes back to the role's set. */
  save: async (actions: string[] | null) =>
    (
      await apiRequest<QuickActionsPreference>('/users/current/quick-actions', {
        method: 'PUT',
        body: { actions },
      })
    ).data,
};
