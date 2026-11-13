import { createContext, useCallback, useContext, useState } from 'react';

import { namesApi } from '@/features/identity-access/names/namesApi.ts';
import { apiRequest } from '@/shared/api/httpClient.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';

// Update completion by entity (review round 5, items 9 and 10; round 7, item 5): the period, the rows the API counts, and the
// donut's bands. The table and the donut read them from here, so both describe the same requests.

export const PERIODS = ['ThisMonth', 'LastThreeMonths', 'ThisYear'] as const;
export type Period = (typeof PERIODS)[number];

export interface EntityCompletionRow {
  externalEntityId: string;
  issued: number;
  submitted: number;
  accepted: number;
  overdue: number;
  completionPercent: number;
}

export interface EntityCompletionPage {
  items: EntityCompletionRow[];
  averagePercent: number | null;
}

/**
 * The period the dashboard's completion figures cover. The table chooses it; the donut near the top of the dashboard (round 7,
 * item 5) reads the same one, so both always describe the same requests. Without a provider each user keeps its own.
 */
export const CompletionPeriodContext = createContext<{
  period: Period;
  setPeriod: (period: Period) => void;
} | null>(null);

export function useCompletionPeriod(): { period: Period; setPeriod: (period: Period) => void } {
  const shared = useContext(CompletionPeriodContext);
  const [period, setPeriod] = useState<Period>('ThisMonth');
  return shared ?? { period, setPeriod };
}

/** The donut's bands, highest first; the lower bound of each, in percent. Bands for AHDA to confirm (round 7, item 5). */
export const COMPLETION_BANDS = [
  { key: 'from90', min: 90 },
  { key: 'from70', min: 70 },
  { key: 'from50', min: 50 },
  { key: 'below50', min: 0 },
] as const;

export type CompletionBandKey = (typeof COMPLETION_BANDS)[number]['key'] | 'noRequests';

export interface CompletionBandEntity {
  id: string;
  name: string;
  /** Null for an entity with no request in the period. */
  percent: number | null;
}

export interface CompletionBands {
  /** All entities together: requests accepted ÷ requests issued, in percent; null when none were issued. */
  overallPercent: number | null;
  bands: { key: CompletionBandKey; entities: CompletionBandEntity[] }[];
}

/**
 * The donut's figures for the period the table shows: each entity in its band by completion rate (lowest rate first in each
 * band), and the active entities that were issued no request in the period. The overall rate is accepted over issued across
 * every entity, not the mean of their rates.
 */
export function useEntityCompletionBands(): {
  data: CompletionBands | undefined;
  loading: boolean;
  error: unknown;
  reload: () => void;
} {
  const { language } = useI18n();
  const { period } = useCompletionPeriod();
  const load = useCallback(
    async (signal: AbortSignal) => {
      const [rates, units] = await Promise.all([
        apiRequest<EntityCompletionPage>('/entity-completion-rates', {
          query: { period, pageSize: 100 },
          signal,
        }),
        namesApi.units(signal),
      ]);
      return { rows: rates.data.items, entities: units.externalEntities };
    },
    [period],
  );
  const resource = useApiResource(load);
  const raw = resource.data;
  const data =
    raw === undefined
      ? undefined
      : (() => {
          const names = new Map(raw.entities.map((entity) => [entity.id, entity.name[language]]));
          const nameOf = (id: string) => names.get(id) ?? id.slice(0, 8);
          const issued = raw.rows.reduce((sum, row) => sum + row.issued, 0);
          const accepted = raw.rows.reduce((sum, row) => sum + row.accepted, 0);
          const byRate = [...raw.rows].sort((a, b) => a.completionPercent - b.completionPercent);
          const withRequests = new Set(raw.rows.map((row) => row.externalEntityId));
          const bands: CompletionBands['bands'] = COMPLETION_BANDS.map((band, index) => {
            const upper =
              index === 0 ? Number.POSITIVE_INFINITY : (COMPLETION_BANDS[index - 1]?.min ?? 100);
            return {
              key: band.key,
              entities: byRate
                .filter((row) => row.completionPercent >= band.min && row.completionPercent < upper)
                .map((row) => ({
                  id: row.externalEntityId,
                  name: nameOf(row.externalEntityId),
                  percent: row.completionPercent,
                })),
            };
          });
          bands.push({
            key: 'noRequests',
            entities: raw.entities
              .filter((entity) => entity.isActive && !withRequests.has(entity.id))
              .map((entity) => ({ id: entity.id, name: entity.name[language], percent: null }))
              .sort((a, b) => a.name.localeCompare(b.name, language)),
          });
          return {
            overallPercent: issued === 0 ? null : Number(((accepted * 100) / issued).toFixed(2)),
            bands,
          };
        })();
  return { data, loading: resource.loading, error: resource.error, reload: resource.reload };
}

/** Whole points when it is a whole number, else the two places the API keeps. */
export function percent(value: number): string {
  return `${String(Number(value.toFixed(2)))}%`;
}
