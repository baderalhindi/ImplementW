import { createContext, useCallback, useContext, useState } from 'react';

import { namesApi } from '@/features/identity-access/names/namesApi.ts';
import { apiRequest } from '@/shared/api/httpClient.ts';
import { useScopedResource } from '@/shared/api/resourceScope.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { type Language, useI18n } from '@/shared/i18n/i18n.ts';

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

export type CompletionBandKey = (typeof COMPLETION_BANDS)[number]['key'];

/** The band a rate falls in. */
export function bandOf(percentValue: number): CompletionBandKey {
  return (COMPLETION_BANDS.find((band) => percentValue >= band.min) ?? COMPLETION_BANDS[3]).key;
}

export function isCompletionBand(value: string | null): value is CompletionBandKey {
  return COMPLETION_BANDS.some((band) => band.key === value);
}

/** The dashboard's URL parameter that filters the entity completion table to one band (review round 8, points 01 and 03). */
export const COMPLETION_BAND_PARAM = 'completionBand';

/** The entity completion table's anchor on the dashboard, which "see more" and a band open. */
export const ENTITY_COMPLETION_ANCHOR = 'entity-completion';

export interface CompletionBandEntity {
  id: string;
  name: string;
  percent: number;
  issued: number;
  accepted: number;
}

export interface CompletionBands {
  /** The API's simple average: each entity with requests in the period counts once. Null when none had requests. */
  averagePercent: number | null;
  /** Every entity with requests in the period, lowest rate first. */
  entities: CompletionBandEntity[];
  /** The four bands of those entities; an entity with no request is in none. */
  bands: { key: CompletionBandKey; entities: CompletionBandEntity[] }[];
  /** Active entities that were issued no request in the period: under the ring, not in it. */
  noRequests: number;
}

/**
 * The donut's figures for the period the table shows (review round 8, point 03): the average of the entities that had requests, each in its band by
 * completion rate, lowest first, and how many active entities had none.
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
    async (signal: AbortSignal) =>
      (
        await apiRequest<EntityCompletionPage>('/entity-completion-rates', {
          query: { period, pageSize: 100 },
          signal,
        })
      ).data,
    [period],
  );
  const resource = useApiResource(load);
  // The entities' names are the screen's shared read (stage QA F-05), not one of this panel's own.
  const units = useScopedResource(namesApi.units);
  const raw =
    resource.data === undefined || units.data === undefined
      ? undefined
      : { page: resource.data, entities: units.data.externalEntities };
  const data =
    raw === undefined
      ? undefined
      : (() => {
          const names = new Map(raw.entities.map((entity) => [entity.id, entity.name[language]]));
          const entities: CompletionBandEntity[] = [...raw.page.items]
            .sort((a, b) => a.completionPercent - b.completionPercent)
            .map((row) => ({
              id: row.externalEntityId,
              name: names.get(row.externalEntityId) ?? row.externalEntityId.slice(0, 8),
              percent: row.completionPercent,
              issued: row.issued,
              accepted: row.accepted,
            }));
          const withRequests = new Set(entities.map((entity) => entity.id));
          return {
            averagePercent: raw.page.averagePercent,
            entities,
            bands: COMPLETION_BANDS.map((band) => ({
              key: band.key,
              entities: entities.filter((entity) => bandOf(entity.percent) === band.key),
            })),
            noRequests: raw.entities.filter(
              (entity) => entity.isActive && !withRequests.has(entity.id),
            ).length,
          };
        })();
  return {
    data,
    loading: resource.loading || units.loading,
    error: resource.error ?? units.error,
    reload: resource.reload,
  };
}

/** The months a period covers, in words: "November 2026", "September – November 2026", "2026" (review round 8, point 03). */
export function periodLabel(period: Period, language: Language, today = new Date()): string {
  const locale = language === 'ar' ? 'ar-u-ca-gregory-nu-latn' : 'en-GB';
  const year = today.getUTCFullYear();
  const month = today.getUTCMonth();
  const monthYear = new Intl.DateTimeFormat(locale, {
    month: 'long',
    year: 'numeric',
    timeZone: 'UTC',
  });
  const monthOnly = new Intl.DateTimeFormat(locale, { month: 'long', timeZone: 'UTC' });
  switch (period) {
    case 'ThisMonth':
      return monthYear.format(Date.UTC(year, month, 1));
    case 'LastThreeMonths': {
      const first = new Date(Date.UTC(year, month - 2, 1));
      const from =
        first.getUTCFullYear() === year ? monthOnly.format(first) : monthYear.format(first);
      return `${from} – ${monthYear.format(Date.UTC(year, month, 1))}`;
    }
    case 'ThisYear':
      return String(year);
  }
}

/** Whole points when it is a whole number, else the two places the API keeps. */
export function percent(value: number): string {
  return `${String(Number(value.toFixed(2)))}%`;
}
