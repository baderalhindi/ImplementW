import { apiRequest } from '@/shared/api/httpClient.ts';

// The header search (GlobalSearchController, docs/architecture/global-search.md).

export type GlobalSearchRecordType =
  'PROJECT' | 'PROJECT_TASK' | 'RISK' | 'MANAGEMENT_CONCERN' | 'CHANGE_REQUEST' | 'DOCUMENT';

export interface GlobalSearchHit {
  type: GlobalSearchRecordType;
  id: string;
  projectId: string | null;
  title: { text: string };
  projectTitle: { text: string } | null;
  reference: string | null;
}

/** The API finds nothing under this many characters, so the SPA does not ask. */
export const MINIMUM_RECORD_SEARCH_LENGTH = 2;

export async function searchRecords(q: string, signal: AbortSignal): Promise<GlobalSearchHit[]> {
  const { data } = await apiRequest<{ items: GlobalSearchHit[] }>('/search', {
    query: { q },
    signal,
  });
  return data.items;
}

/** The page that opens a record. A task has no page of its own: it opens on its project's board. */
export function recordPath(hit: GlobalSearchHit): string {
  switch (hit.type) {
    case 'PROJECT':
      return `/projects/${hit.id}`;
    case 'PROJECT_TASK':
      return `/projects/${hit.projectId ?? ''}/tasks`;
    case 'RISK':
      return `/projects/${hit.projectId ?? ''}/risks/${hit.id}`;
    case 'MANAGEMENT_CONCERN':
      return `/projects/${hit.projectId ?? ''}/issues-challenges/${hit.id}`;
    case 'CHANGE_REQUEST':
      return `/change-requests/${hit.id}`;
    case 'DOCUMENT':
      return `/documents/${hit.id}`;
  }
}
