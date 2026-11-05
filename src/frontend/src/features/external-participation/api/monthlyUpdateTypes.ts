import { type NarrativeText } from '@/features/projects/api/types.ts';

import { type ExternalContributionStatus, type ExternalUpdateRequestStatus } from './types.ts';

// The monthly update (the 5 Nov 2026 review §2): `/my-update-requests` for whoever answers, `/update-request-batches` for whoever sends.

/** A monthly update waiting on its responder, with what the form starts from: the open draft's values, or last month's. */
export interface MyUpdateRequest {
  requestId: string;
  requestVersion: number;
  projectId: string;
  formalProjectId: string | null;
  legacyReference: string | null;
  projectTitle: NarrativeText;
  dueDate: string | null;
  overdue: boolean;
  status: ExternalUpdateRequestStatus;
  instructions: NarrativeText;
  contributionId: string | null;
  contributionVersion: number | null;
  contributionStatus: ExternalContributionStatus | null;
  returnReason: NarrativeText | null;
  previouslyReturned: boolean;
  plannedProgressPercent: number | null;
  prefill: Record<string, string>;
  prefillLanguages: Record<string, 'AR' | 'EN'>;
}

export interface MyUpdateRequestPage {
  items: MyUpdateRequest[];
  page: number;
  pageSize: number;
  totalCount: number;
  overdueCount: number;
  earliestDueDate: string | null;
}

/** Where a project of a batch stands. */
export type BatchRowState =
  'SENT' | 'OPENED' | 'DRAFT' | 'SUBMITTED' | 'ACCEPTED' | 'RETURNED' | 'REJECTED' | 'WITHDRAWN';

export const BATCH_ROW_STATES: readonly BatchRowState[] = [
  'SENT',
  'OPENED',
  'DRAFT',
  'SUBMITTED',
  'ACCEPTED',
  'RETURNED',
  'REJECTED',
  'WITHDRAWN',
];

export interface UpdateRequestBatchRow {
  requestId: string;
  projectId: string;
  formalProjectId: string | null;
  legacyReference: string | null;
  projectTitle: NarrativeText;
  externalEntityId: string;
  responsibleUserId: string | null;
  state: BatchRowState;
  overdue: boolean;
  requestVersion: number;
}

export interface UpdateRequestBatchSummary {
  id: string;
  seriesId: string;
  dueDate: string;
  repeatMonthly: boolean;
  repeatDay: number | null;
  nextRunOn: string | null;
  withdrawnAt: string | null;
  reviewerUserId: string;
  createdBy: string;
  createdAt: string;
  projectCount: number;
  entityCount: number;
  openCount: number;
}

export interface UpdateRequestBatchDetail {
  batch: UpdateRequestBatchSummary;
  instructions: NarrativeText;
  rows: UpdateRequestBatchRow[];
}

export interface UpdateRequestBatchPage {
  items: UpdateRequestBatchSummary[];
  page: number;
  pageSize: number;
  totalCount: number;
}

/** A responder given here is for a project with no entity contact, and becomes it. */
export interface UpdateRequestBatchItem {
  projectId: string;
  responsibleUserId?: string;
}

export interface UpdateRequestBatchRequest {
  items: UpdateRequestBatchItem[];
  dueDate: string;
  repeatMonthly: boolean;
  repeatDay: number | null;
  instructions: string;
  reviewerUserId: string;
}
