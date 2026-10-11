import { useCallback } from 'react';
import { useSearchParams } from 'react-router';

import { type Translate } from '@/features/identity-access/problems.ts';
import { useSession } from '@/features/identity-access/session/useSession.ts';
import { type ApiResponse } from '@/shared/api/httpClient.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { type Notice } from '@/shared/ui/PageNotice.tsx';

import { reportsApi } from './api/reportsApi.ts';
import { type SavedViewDetail, type SavedViewRequest } from './api/types.ts';
import { type SaveViewValues } from './dialogs/SaveViewDialog.tsx';

export interface OpenedView {
  /** The view named by `?view=`, null when none is named. */
  id: string | null;
  response: ApiResponse<SavedViewDetail> | undefined;
  error: unknown;
  loading: boolean;
  /** The person owns it: they may save changes to it. A view shared with them is saved as a new one. */
  isOwn: boolean;
}

/**
 * The saved view a report or the explorer is opened with (`?view=<id>`), read as the person may see it now: the API
 * says whether it still fits the report version and the allowlist in force, and a view neither theirs nor shared with
 * them does not exist (404).
 */
export function useOpenedView(): OpenedView {
  const [params] = useSearchParams();
  const { session } = useSession();
  const id = params.get('view');
  const load = useCallback(
    (signal: AbortSignal) =>
      id === null ? Promise.resolve(undefined) : reportsApi.savedView(id, signal),
    [id],
  );
  const { data, error, loading } = useApiResource(load);
  return {
    id,
    response: data,
    error,
    loading: id !== null && loading,
    isOwn: data !== undefined && data.data.ownerUserId === session?.user.id,
  };
}

/** A view written whole from what the person composed and what MOD-062 asked (its owner's sharing with it). */
export function savedViewRequest(
  values: SaveViewValues,
  content: Pick<SavedViewRequest, 'viewType' | 'reportCode' | 'parameters' | 'columns' | 'filters'>,
): SavedViewRequest {
  return {
    ...content,
    name: { text: values.name, language: values.language },
    sharedWithRoleCodes: values.sharedWithRoleCodes,
  };
}

/** What a save hands to the page it reopens with the view: the name it was saved under, for the confirmation. */
export interface SavedState {
  savedViewName: string;
}

export function savedNotice(state: unknown, t: Translate): Notice | null {
  const name = (state as Partial<SavedState> | null)?.savedViewName;
  return typeof name === 'string'
    ? { tone: 'success', message: t('reports.saveView.saved', { name }) }
    : null;
}
