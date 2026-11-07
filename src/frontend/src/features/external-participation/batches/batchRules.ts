import { type ProjectSummary } from '@/features/projects/api/types.ts';
import { type AssigneeValue, assigneeOf } from '@/features/tasks/assignee.ts';
import { ApiError } from '@/shared/api/httpClient.ts';
import { type TranslationKey } from '@/shared/i18n/i18n.ts';
import { type StatusTone } from '@/shared/ui/StatusBadge.tsx';

import { type BatchRowState, type UpdateRequestBatchItem } from '../api/monthlyUpdateTypes.ts';
import { isRequestableProjectStatus } from '../rules.ts';

// The three-step "طلب تحديث" (the 5 Nov 2026 review §2.2). The API is the authority: it answers every project that cannot be sent to.

/** Why a project cannot be chosen, or null when it can. */
export type Ineligibility = 'STATUS' | 'NO_ENTITY';

export function ineligibilityOf(
  project: Pick<ProjectSummary, 'status' | 'externalEntityId'>,
): Ineligibility | null {
  if (!isRequestableProjectStatus(project.status)) {
    return 'STATUS';
  }
  return project.externalEntityId === null ? 'NO_ENTITY' : null;
}

export function entityCountOf(projects: readonly ProjectSummary[]): number {
  return new Set(projects.map((project) => project.externalEntityId)).size;
}

/** What the API refused about one project of the batch. */
export interface RowIssue {
  field: 'projectId' | 'responsibleUserId';
  code: string;
}

/** Whether the project has someone to answer it: its entity contact, unless refused, or the person named here. */
export function responderOf(
  project: Pick<ProjectSummary, 'entityContactUserId'>,
  chosen: AssigneeValue | undefined,
): string | null {
  const named = chosen === undefined ? null : assigneeOf(chosen).userId;
  return named ?? project.entityContactUserId ?? null;
}

/** A row Send waits for: no responder, or a refusal the person has not yet acted on. */
export function isFlagged(
  project: Pick<ProjectSummary, 'entityContactUserId'>,
  chosen: AssigneeValue | undefined,
  issue: RowIssue | undefined,
): boolean {
  return issue !== undefined || responderOf(project, chosen) === null;
}

/** The items to send, in the order the 422's `items[i]` then refers to. */
export function batchItemsOf(
  projects: readonly ProjectSummary[],
  responders: Readonly<Record<string, AssigneeValue>>,
): UpdateRequestBatchItem[] {
  return projects.map((project) => {
    const chosen = responders[project.id];
    const named = chosen === undefined ? null : assigneeOf(chosen).userId;
    return named === null
      ? { projectId: project.id }
      : { projectId: project.id, responsibleUserId: named };
  });
}

const ITEM_ISSUE = /^items\[(\d+)\]\.(projectId|responsibleUserId)$/;

/** The 422's issues split between the projects they name, by the index sent, and the rest of the form. */
export function batchIssuesOf(
  error: unknown,
  items: readonly UpdateRequestBatchItem[],
): { rows: Record<string, RowIssue>; form: Record<string, string> } {
  const rows: Record<string, RowIssue> = {};
  const form: Record<string, string> = {};
  if (error instanceof ApiError) {
    for (const issue of error.fieldIssues) {
      const match = ITEM_ISSUE.exec(issue.field);
      const projectId = match === null ? undefined : items[Number(match[1])]?.projectId;
      if (projectId === undefined) {
        form[issue.field] ??= issue.code;
      } else {
        rows[projectId] = { field: match?.[2] as RowIssue['field'], code: issue.code };
      }
    }
  }
  return { rows, form };
}

const ROW_ISSUE_MESSAGES: Partial<Record<string, TranslationKey>> = {
  REQUIRED: 'externalParticipation.batch.rowIssue.responderRequired',
  NOT_ALLOWED: 'externalParticipation.batch.rowIssue.responderIneligible',
  NOT_FOUND: 'externalParticipation.batch.rowIssue.notReachable',
  PROJECT_STATE_NOT_PERMITTED: 'externalParticipation.batch.rowIssue.status',
  PROJECT_HAS_NO_ENTITY: 'externalParticipation.batch.rowIssue.noEntity',
};

export function rowIssueMessage(issue: RowIssue): TranslationKey {
  return ROW_ISSUE_MESSAGES[issue.code] ?? 'externalParticipation.batch.rowIssue.other';
}

export const BATCH_STATE_TONES: Record<BatchRowState, StatusTone> = {
  SENT: 'neutral',
  OPENED: 'info',
  DRAFT: 'info',
  SUBMITTED: 'warning',
  ACCEPTED: 'positive',
  RETURNED: 'warning',
  REJECTED: 'negative',
  WITHDRAWN: 'neutral',
};
