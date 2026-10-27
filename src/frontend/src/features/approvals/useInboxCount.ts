import { useEffect, useRef } from 'react';

import { useApiResource } from '@/shared/api/useApiResource.ts';

import { approvalTasksApi } from './api/approvalsApi.ts';

/** How often the menu's count asks again while the page stays put. */
export const INBOX_COUNT_POLL_INTERVAL_MS = 60_000;

const loadCount = (signal: AbortSignal): Promise<number | null> =>
  approvalTasksApi.listInbox(1, 1, signal).then((page) => page.totalCount);

const noCount = (): Promise<null> => Promise.resolve(null);

/**
 * The number of approval tasks the caller may decide now (SCR-100's total), for the Requests and Approvals menu. It is
 * read again whenever `revision` changes (the shell passes the page's path, so a decision shows on the next screen) and
 * every poll interval, keeping the last number while it does. Null until the first answer, and always null for someone
 * who may not decide approvals (`canDecide`): the inbox would refuse them, and each refusal is an audited access denial.
 */
export function useInboxCount(revision: string, canDecide: boolean): number | null {
  const { data, reload } = useApiResource(canDecide ? loadCount : noCount, {
    keepWhileReloading: true,
  });
  const firstRevision = useRef(revision);

  useEffect(() => {
    if (revision !== firstRevision.current) {
      reload();
    }
  }, [revision, reload]);

  useEffect(() => {
    if (!canDecide) {
      return undefined;
    }
    const timer = window.setInterval(reload, INBOX_COUNT_POLL_INTERVAL_MS);
    return () => {
      window.clearInterval(timer);
    };
  }, [canDecide, reload]);

  return data ?? null;
}
