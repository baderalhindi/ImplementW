import { apiRequest } from '@/shared/api/httpClient.ts';

/** The kinds of change an authorisation permits that a screen applies (ChangeAuthorizationScope). */
export type AuthorizationScope = 'REBASELINE' | 'COMMITMENT_CHANGE' | 'STAGE_PLAN_CHANGE';

export interface IssuedAuthorization {
  id: string;
  issuedAt: string;
  /** The title of the change request it came from, which names it to a person (review round 5, item 5: no field asks for an ID). */
  title: string;
}

/** The project's ISSUED authorisations of one kind, each named by the change request it came from. */
export async function readIssuedAuthorizations(
  projectId: string,
  authorizationScope: AuthorizationScope,
  signal: AbortSignal,
): Promise<IssuedAuthorization[]> {
  const { items } = (
    await apiRequest<{ items: { id: string; changeRequestId: string; issuedAt: string }[] }>(
      '/change-authorizations',
      { query: { projectId, authorizationScope, status: 'ISSUED', pageSize: 100 }, signal },
    )
  ).data;
  return Promise.all(
    items.map(async (item) => ({
      id: item.id,
      issuedAt: item.issuedAt,
      title: (
        await apiRequest<{ title: { text: string } }>(`/change-requests/${item.changeRequestId}`, {
          signal,
        })
      ).data.title.text,
    })),
  );
}
