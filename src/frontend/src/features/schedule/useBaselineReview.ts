import { useCallback } from 'react';

import {
  type ApprovalInboxItem,
  type ApprovalInstanceSummary,
} from '@/features/approvals/api/types.ts';
import { findInboxTask, unlessForbidden } from '@/features/approvals/sourceReview.ts';
import { type ApiResource, useApiResource } from '@/shared/api/useApiResource.ts';

import { baselineSubject } from './access.ts';
import { scheduleApi } from './api/scheduleApi.ts';
import { type ProjectBaselineDetail } from './api/types.ts';

/** A submitted candidate's WF-11 review as far as the person may see it. */
export interface BaselineReview {
  /** The inbox task deciding it, when the person may decide it now (APPROVAL_DECIDE). */
  task: ApprovalInboxItem | null;
  /** The pending run, when the person may read runs (APPROVAL_VIEW): its history is one link away. */
  run: ApprovalInstanceSummary | null;
}

const NONE: BaselineReview = { task: null, run: null };

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
        unlessForbidden(findInboxTask(baselineSubject({ id, revisionNo }), signal)),
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
