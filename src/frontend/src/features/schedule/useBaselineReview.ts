import { useCallback } from 'react';

import { approvalTasksApi } from '@/features/approvals/api/approvalsApi.ts';
import {
  type ApprovalInboxItem,
  type ApprovalInstanceSummary,
} from '@/features/approvals/api/types.ts';
import { type ApiResource, useApiResource } from '@/shared/api/useApiResource.ts';

import { decidesBaseline } from './access.ts';
import { SCHEDULE_PAGE_SIZE, scheduleApi } from './api/scheduleApi.ts';
import { type ProjectBaselineDetail } from './api/types.ts';
import { isForbidden } from './problems.ts';

/** A submitted candidate's WF-11 review as far as the person may see it. */
export interface BaselineReview {
  /** The inbox task deciding it, when the person may decide it now (APPROVAL_DECIDE). */
  task: ApprovalInboxItem | null;
  /** The pending run, when the person may read runs (APPROVAL_VIEW): its history is one link away. */
  run: ApprovalInstanceSummary | null;
}

const NONE: BaselineReview = { task: null, run: null };

/** Null when the read is refused: a person without the permission has no part in the review (TASK-035 D-10). */
async function unlessForbidden<T>(read: Promise<T>): Promise<T | null> {
  try {
    return await read;
  } catch (error) {
    if (isForbidden(error)) {
      return null;
    }
    throw error;
  }
}

/** The person's inbox task for this baseline revision, reading the inbox page after page until it is found or ends. */
async function decidingTask(
  baseline: Pick<ProjectBaselineDetail, 'id' | 'revisionNo'>,
  signal: AbortSignal,
): Promise<ApprovalInboxItem | null> {
  for (let page = 1; ; page += 1) {
    const inbox = await approvalTasksApi.listInbox(page, SCHEDULE_PAGE_SIZE, signal);
    const found = inbox.items.find((item) => decidesBaseline(item, baseline));
    if (found !== undefined) {
      return found;
    }
    if (inbox.items.length === 0 || page * SCHEDULE_PAGE_SIZE >= inbox.totalCount) {
      return null;
    }
  }
}

/**
 * MOD-018 finds its task in the inbox, which needs only the permission deciding needs; reading the run itself needs
 * APPROVAL_VIEW, which an approver may not hold. Nothing is read unless the candidate is with WF-11.
 */
export function useBaselineReview(
  candidate: ProjectBaselineDetail | null,
): ApiResource<BaselineReview> {
  const id = candidate?.status === 'SUBMITTED' ? candidate.id : null;
  const revisionNo = candidate?.revisionNo ?? 0;
  const load = useCallback(
    async (signal: AbortSignal): Promise<BaselineReview> => {
      if (id === null) {
        return NONE;
      }
      const [task, runs] = await Promise.all([
        unlessForbidden(decidingTask({ id, revisionNo }, signal)),
        unlessForbidden(scheduleApi.approvalRuns(id, signal)),
      ]);
      return {
        task,
        run: runs?.items.find((run) => run.status === 'PENDING') ?? null,
      };
    },
    [id, revisionNo],
  );
  return useApiResource(load);
}
