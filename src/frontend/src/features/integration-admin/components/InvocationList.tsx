import { type ReactElement, useCallback } from 'react';

import { PAGE_SIZE } from '@/shared/api/paging.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Pagination } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { integrationAdminApi } from '../api/integrationAdminApi.ts';
import { type InvocationQuery } from '../api/types.ts';
import { integrationProblemMessage } from '../problems.ts';
import { useRefresh } from '../useRefresh.ts';

import { InvocationTable } from './InvocationTable.tsx';

interface InvocationListProps {
  query: InvocationQuery;
  caption: string;
  emptyTitle: string;
  page: number;
  onPageChange: (page: number) => void;
  /** Read again this often, in milliseconds (ADM-047, TASK-092). */
  refreshEvery?: number;
}

/** A page of attempts the API found for `query`, newest first. */
export function InvocationList({
  query,
  caption,
  emptyTitle,
  page,
  onPageChange,
  refreshEvery,
}: InvocationListProps): ReactElement {
  const { t } = useI18n();
  // Compared by value: each screen builds its query on every render.
  const key = JSON.stringify(query);
  const load = useCallback(
    (signal: AbortSignal) =>
      integrationAdminApi.invocations(
        JSON.parse(key) as InvocationQuery,
        { page, pageSize: PAGE_SIZE },
        signal,
      ),
    [key, page],
  );
  const invocations = useApiResource(load, { keepWhileReloading: true });
  useRefresh(invocations.reload, refreshEvery);

  // A refresh keeps the rows shown until the next read lands; only a first read shows the loading state.
  if (invocations.data === undefined && invocations.error === null) {
    return <LoadingState />;
  }
  if (invocations.error !== null || invocations.data === undefined) {
    return (
      <ErrorState
        message={integrationProblemMessage(invocations.error, t)}
        onRetry={invocations.reload}
      />
    );
  }
  if (invocations.data.items.length === 0) {
    return <EmptyState title={emptyTitle} />;
  }
  return (
    <>
      <InvocationTable items={invocations.data.items} caption={caption} />
      <Pagination
        page={invocations.data.page}
        pageSize={invocations.data.pageSize}
        totalCount={invocations.data.totalCount}
        onPageChange={onPageChange}
      />
    </>
  );
}
