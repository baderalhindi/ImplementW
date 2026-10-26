import { useEffect, useRef } from 'react';

import { useApiResource } from '@/shared/api/useApiResource.ts';

import { approvalTasksApi } from './api/approvalsApi.ts';

/** How often the menu's count asks again while the page stays put. */
export const INBOX_COUNT_POLL_INTERVAL_MS = 60_000;

const loadCount = (signal: AbortSignal): Promise<number> =>
  approvalTasksApi.listInbox(1, 1, signal).then((page) => page.totalCount);

/**
 * The number of approval tasks the caller may decide now (SCR-100's total), for the Requests and Approvals menu. It is
 * read again whenever `revision` changes (the shell passes the page's path, so a decision shows on the next screen) and
 * every poll interval, keeping the last number while it does. Null until the first answer.
 */
export function useInboxCount(revision: string): number | null {
  const { data, reload } = useApiResource(loadCount, { keepWhileReloading: true });
  const firstRevision = useRef(revision);

  useEffect(() => {
    if (revision !== firstRevision.current) {
      reload();
    }
  }, [revision, reload]);

  useEffect(() => {
    const timer = window.setInterval(reload, INBOX_COUNT_POLL_INTERVAL_MS);
    return () => {
      window.clearInterval(timer);
    };
  }, [reload]);

  return data ?? null;
}
