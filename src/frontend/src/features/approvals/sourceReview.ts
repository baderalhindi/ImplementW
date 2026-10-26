import { ApiError } from '@/shared/api/httpClient.ts';
import { MAX_PAGE_SIZE } from '@/shared/api/paging.ts';

import { approvalTasksApi } from './api/approvalsApi.ts';
import { type ApprovalInboxItem, type ApprovalSubject } from './api/types.ts';

// How a source module's screen (a schedule baseline, a change request) finds its part in a WF-11 review: the person's
// inbox task deciding the record, and the runs that reviewed it. Deciding needs only APPROVAL_DECIDE and reading the
// inbox; reading a run needs APPROVAL_VIEW over it, which an approver or a requester may not hold (TASK-035 D-10).

/** Null when the read is refused (403): a person without the permission has no part in that side of the review. */
export async function unlessForbidden<T>(read: Promise<T>): Promise<T | null> {
  try {
    return await read;
  } catch (error) {
    if (error instanceof ApiError && error.status === 403) {
      return null;
    }
    throw error;
  }
}

/** Whether an inbox task decides this revision of this record: the subject its source module started WF-11 with. */
export function decidesSubject(
  item: ApprovalInboxItem,
  { module, type, id, revisionNo }: ApprovalSubject,
): boolean {
  const { subject } = item.instance;
  return (
    subject.module === module &&
    subject.type === type &&
    subject.id === id &&
    subject.revisionNo === revisionNo
  );
}

/** The person's inbox task for the subject, reading the inbox page after page until it is found or ends. */
export async function findInboxTask(
  subject: ApprovalSubject,
  signal: AbortSignal,
): Promise<ApprovalInboxItem | null> {
  for (let page = 1; ; page += 1) {
    const inbox = await approvalTasksApi.listInbox(page, MAX_PAGE_SIZE, signal);
    const found = inbox.items.find((item) => decidesSubject(item, subject));
    if (found !== undefined) {
      return found;
    }
    if (inbox.items.length === 0 || page * MAX_PAGE_SIZE >= inbox.totalCount) {
      return null;
    }
  }
}
