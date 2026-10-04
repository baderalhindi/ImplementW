import { useCallback } from 'react';

import { type ApiResource, useApiResource } from '@/shared/api/useApiResource.ts';

import { financialKpiApi } from './api/financialKpiApi.ts';
import {
  type FinancialCommitmentDetail,
  type FinancialPositionDetail,
  type FinancialProgressUpdateDetail,
  type FinancialSourceModeDetail,
  type PublishedFinancialSnapshotDetail,
} from './api/types.ts';
import { isUpdateInProgress } from './presentation.ts';

/** The update in the workflow, with the ETag its commands send, and why the revision before it was returned. */
export interface CurrentUpdate {
  update: FinancialProgressUpdateDetail;
  etag: string | null;
  returnReason: string | null;
}

/**
 * A project's financial records as SCR-049 and SCR-071 read them. The official and the live view are read side by side
 * and never merged (M-12); the commitments and updates are read whole, for their history and for the provenance of
 * the figures the two views show (ADR-008 extended).
 */
export interface FinancialOverview {
  /** The latest PUBLISHED/OFFICIAL snapshot, or null before the first publication. */
  official: PublishedFinancialSnapshotDetail | null;
  /** Every snapshot, latest first. */
  snapshots: PublishedFinancialSnapshotDetail[];
  /** The CURRENT/LIVE position, or null when the API answered none. */
  live: FinancialPositionDetail | null;
  /** Every Approved Budget version, newest first. */
  commitments: FinancialCommitmentDetail[];
  /** Every revision of every period, newest first. */
  updates: FinancialProgressUpdateDetail[];
  sourceModes: FinancialSourceModeDetail[];
  current: CurrentUpdate | null;
}

/**
 * The newest revision, if it is still in the workflow, read again on its own for its ETag. A revision that follows a
 * returned one of the same period shows why it was returned.
 */
async function currentOf(
  updates: FinancialProgressUpdateDetail[],
  signal: AbortSignal,
): Promise<CurrentUpdate | null> {
  const [latest, previous] = updates;
  if (latest === undefined || !isUpdateInProgress(latest.status)) {
    return null;
  }
  const detail = await financialKpiApi.update(latest.id, signal);
  const returned =
    previous?.status === 'RETURNED' && previous.reportingCycleId === latest.reportingCycleId
      ? (previous.returnReason?.text ?? null)
      : null;
  return { update: detail.data, etag: detail.etag, returnReason: returned };
}

export function useFinancialOverview(projectId: string): ApiResource<FinancialOverview> {
  const load = useCallback(
    async (signal: AbortSignal): Promise<FinancialOverview> => {
      const [snapshots, positions, commitments, updates, sourceModes] = await Promise.all([
        financialKpiApi.snapshots(projectId, signal),
        financialKpiApi.positions(projectId, signal),
        financialKpiApi.commitments(projectId, signal),
        financialKpiApi.updates(projectId, signal),
        financialKpiApi.sourceModes(projectId, signal),
      ]);
      return {
        official: snapshots[0] ?? null,
        snapshots,
        live: positions[0] ?? null,
        commitments,
        updates,
        sourceModes,
        current: await currentOf(updates, signal),
      };
    },
    [projectId],
  );
  return useApiResource(load, { keepWhileReloading: true });
}

/** Everyone the financial screens name: who entered, submitted, reviewed and published each record. */
export function peopleOf(overview: FinancialOverview | undefined): (string | null)[] {
  if (overview === undefined) {
    return [];
  }
  return [
    ...overview.commitments.map((commitment) => commitment.enteredByUserId ?? null),
    ...overview.updates.flatMap((update) => [
      update.enteredByUserId ?? null,
      update.submittedByUserId,
      update.reviewedByUserId,
    ]),
    ...overview.snapshots.flatMap((snapshot) => [
      snapshot.publishedByUserId,
      snapshot.enteredByUserId ?? null,
    ]),
  ];
}
