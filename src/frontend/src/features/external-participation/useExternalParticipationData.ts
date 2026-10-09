import { useCallback } from 'react';

import { externalEntitiesApi } from '@/features/identity-access/api/identityAccessApi.ts';
import { shortId } from '@/features/projects/presentation.ts';
import { tasksApi } from '@/features/tasks/api/tasksApi.ts';
import { type ProjectTaskDetail } from '@/features/tasks/api/types.ts';
import { ApiError, type ApiResponse } from '@/shared/api/httpClient.ts';
import { readInBatches } from '@/shared/api/paging.ts';
import { type ApiResource, useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';

import {
  contributionsApi,
  type ExternalUpdateRequestQuery,
  externalRequestsApi,
  sourceApplicationsApi,
} from './api/externalParticipationApi.ts';
import {
  type ExternalContributionDetail,
  type ExternalUpdateRequestDetail,
  type ExternalUpdateRequestStatus,
  type SourceApplicationDetail,
} from './api/types.ts';
import {
  type ApplicationState,
  APPLICATION_STATES,
  applicationStateOf,
  isApplicationCase,
} from './rules.ts';

// The WF-13 reads. Revisions are listed per request and attempts per revision (`externalUpdateRequestId` and
// `externalContributionId` are required, TASK-066 D-14), so the cross-request screens read the requests first, then
// each one's revisions, six at a time.

/** The requests matching a query; kept on screen while read again, so a dialog over them is not unmounted. */
export function useRequestList(
  query: ExternalUpdateRequestQuery,
): ApiResource<ExternalUpdateRequestDetail[]> {
  const { projectId, externalEntityId, responsibleUserId, reviewerUserId } = query;
  // The set as one string, so the loader changes only when its members do.
  const statuses = query.status?.join(',') ?? '';
  const load = useCallback(
    (signal: AbortSignal) =>
      externalRequestsApi.list(
        {
          projectId,
          externalEntityId,
          responsibleUserId,
          reviewerUserId,
          status:
            statuses === '' ? undefined : (statuses.split(',') as ExternalUpdateRequestStatus[]),
        },
        signal,
      ),
    [projectId, externalEntityId, responsibleUserId, reviewerUserId, statuses],
  );
  return useApiResource(load, { keepWhileReloading: true });
}

/** One request with its ETag. */
export function useRequestRecord(
  requestId: string,
): ApiResource<ApiResponse<ExternalUpdateRequestDetail>> {
  const load = useCallback(
    (signal: AbortSignal) => externalRequestsApi.get(requestId, signal),
    [requestId],
  );
  return useApiResource(load, { keepWhileReloading: true });
}

/** A request's revisions, newest first; none is read before the request is issued (a DRAFT has none). */
export function useRevisions(requestId: string | null): ApiResource<ExternalContributionDetail[]> {
  const load = useCallback(
    async (signal: AbortSignal) =>
      requestId === null ? [] : contributionsApi.list(requestId, signal),
    [requestId],
  );
  return useApiResource(load, { keepWhileReloading: true });
}

/** One revision with its ETag. */
export function useContributionRecord(
  contributionId: string,
): ApiResource<ApiResponse<ExternalContributionDetail>> {
  const load = useCallback(
    (signal: AbortSignal) => contributionsApi.get(contributionId, signal),
    [contributionId],
  );
  return useApiResource(load, { keepWhileReloading: true });
}

/** A revision's application attempts, newest first; read only for an accepted revision of a typed source. */
export function useAttempts(contributionId: string | null): ApiResource<SourceApplicationDetail[]> {
  const load = useCallback(
    async (signal: AbortSignal) =>
      contributionId === null ? [] : sourceApplicationsApi.list(contributionId, signal),
    [contributionId],
  );
  return useApiResource(load, { keepWhileReloading: true });
}

/**
 * The source task as it stands now, for SCR-165's comparison, with its ETag (its row version). Null when the request
 * names no task, or the reviewer may not read it (403; 404 for a task out of their scope or gone).
 */
export function useSourceTask(
  request: Pick<ExternalUpdateRequestDetail, 'targetType' | 'targetId'> | undefined,
): ApiResource<ApiResponse<ProjectTaskDetail> | null> {
  const targetId = request?.targetType === 'ProjectTask' ? request.targetId : null;
  const load = useCallback(
    async (signal: AbortSignal) => {
      if (targetId === null) {
        return null;
      }
      try {
        return await tasksApi.task(targetId, signal);
      } catch (error) {
        if (error instanceof ApiError && (error.status === 403 || error.status === 404)) {
          return null;
        }
        throw error;
      }
    },
    [targetId],
  );
  return useApiResource(load);
}

/** An accepted revision of a typed source with its request, its attempts and where it stands (SCR-166). */
export interface ApplicationCase {
  request: ExternalUpdateRequestDetail;
  revision: ExternalContributionDetail;
  attempts: SourceApplicationDetail[];
  state: ApplicationState;
}

/**
 * Every accepted answer of a typed source the person may see: requests RESPONDED (accepted, pending) or CLOSED
 * (applied or failed), their accepted revisions, and each one's attempts. Ordered by what needs action first —
 * conflicts, retries, pending — then most recently changed.
 */
async function readApplicationCases(signal: AbortSignal): Promise<ApplicationCase[]> {
  const requests = (
    await externalRequestsApi.list({ status: ['RESPONDED', 'CLOSED'] }, signal)
  ).filter((request) => request.applicationMode === 'UPDATE_ALLOWED_SOURCE_FIELDS');
  const accepted = await readInBatches(requests, async (request) =>
    (await contributionsApi.list(request.id, signal))
      .filter((revision) => isApplicationCase(request, revision))
      .map((revision) => ({ request, revision })),
  );
  const cases = await readInBatches(accepted, async ({ request, revision }) => {
    const attempts = await sourceApplicationsApi.list(revision.id, signal);
    return [{ request, revision, attempts, state: applicationStateOf(revision.status, attempts) }];
  });
  return cases.sort(
    (a, b) =>
      APPLICATION_STATES.indexOf(a.state) - APPLICATION_STATES.indexOf(b.state) ||
      b.revision.updatedAt.localeCompare(a.revision.updatedAt),
  );
}

/** SCR-166. */
export function useApplicationCases(): ApiResource<ApplicationCase[]> {
  return useApiResource(readApplicationCases, { keepWhileReloading: true });
}

/** A revision with the request it answers, as the entity's history lists it. */
export interface ContributionEntry {
  request: ExternalUpdateRequestDetail;
  revision: ExternalContributionDetail;
}

export interface MyContributions {
  /** Issued to the person, no answer started yet. */
  awaiting: ExternalUpdateRequestDetail[];
  /** Their revisions, and those of requests they now answer, newest first. */
  entries: ContributionEntry[];
}

/** A request has revisions once its answer is started; an ISSUED one has none yet. */
function mayHaveRevisions(request: ExternalUpdateRequestDetail): boolean {
  return request.status !== 'DRAFT' && request.status !== 'ISSUED';
}

/** SCR-164: the person's own answers across their entity's requests (the API lists only those). */
export function useMyContributions(userId: string): ApiResource<MyContributions> {
  const load = useCallback(
    async (signal: AbortSignal): Promise<MyContributions> => {
      const requests = await externalRequestsApi.list({}, signal);
      const entries = await readInBatches(requests.filter(mayHaveRevisions), async (request) =>
        (await contributionsApi.list(request.id, signal))
          .filter(
            (revision) =>
              revision.contributorUserId === userId || request.responsibleUserId === userId,
          )
          .map((revision) => ({ request, revision })),
      );
      return {
        awaiting: requests.filter(
          (request) => request.status === 'ISSUED' && request.responsibleUserId === userId,
        ),
        entries: entries.sort((a, b) => b.revision.updatedAt.localeCompare(a.revision.updatedAt)),
      };
    },
    [userId],
  );
  return useApiResource(load, { keepWhileReloading: true });
}

async function readEntityNames(
  signal: AbortSignal,
): Promise<Map<string, { ar: string; en: string }>> {
  try {
    const entities = await externalEntitiesApi.listAll(signal);
    return new Map(entities.map((entity) => [entity.id, entity.name]));
  } catch (error) {
    if (error instanceof ApiError && error.status === 403) {
      return new Map();
    }
    throw error;
  }
}

async function readNoEntityNames(): Promise<Map<string, { ar: string; en: string }>> {
  return Promise.resolve(new Map<string, { ar: string; en: string }>());
}

/**
 * An entity's name for AHDA's screens, or "Entity 6f6f6f6f" when it cannot be read: ORGANIZATION_VIEW ships to R01 only
 * (TASK-038 F-1). An external view passes `enabled` false and reads nothing: the entity is the person's own.
 */
export function useEntityNames(enabled = true): (id: string) => string {
  const { t, language } = useI18n();
  const names = useApiResource(enabled ? readEntityNames : readNoEntityNames).data;
  return (id) =>
    names?.get(id)?.[language] ?? t('externalParticipation.entity.unknown', { id: shortId(id) });
}
