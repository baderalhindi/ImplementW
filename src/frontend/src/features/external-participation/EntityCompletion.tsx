import { type ReactElement, type ReactNode, useCallback, useEffect, useRef, useState } from 'react';
import { Link, useLocation } from 'react-router';

import { useCan } from '@/features/identity-access/permissions/permissions.ts';
import { apiRequest } from '@/shared/api/httpClient.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { SelectField } from '@/shared/ui/FormFields.tsx';
import { TableContainer } from '@/shared/ui/Layout.tsx';
import { SelectAllHeader, SelectRowCell } from '@/shared/ui/rowSelection.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';
import { useRowSelection } from '@/shared/ui/useRowSelection.ts';

import {
  bandOf,
  COMPLETION_BAND_PARAM,
  CompletionPeriodContext,
  ENTITY_COMPLETION_ANCHOR,
  isCompletionBand,
  type EntityCompletionPage,
  type Period,
  PERIODS,
  percent,
  useCompletionPeriod,
} from './entityCompletion.ts';
import { PARTICIPATION_MONITOR_PATH } from './paths.ts';
import { participationProblemMessage } from './problems.ts';
import { useEntityNames } from './useExternalParticipationData.ts';

// Items 9 and 10 of review round 5: one definition, so an entity and AHDA read the same number. The API does the counting.

interface OwnEntityCompletion {
  issued: number;
  accepted: number;
  completionPercent: number | null;
  averagePercent: number | null;
}

function PeriodField({
  value,
  onChange,
}: {
  value: Period;
  onChange: (period: Period) => void;
}): ReactElement {
  const { t } = useI18n();
  return (
    <SelectField
      label={t('externalParticipation.completion.period')}
      name="completionPeriod"
      value={value}
      options={PERIODS.map((period) => ({
        value: period,
        label: t(`externalParticipation.completion.periods.${period}`),
      }))}
      onChange={(next) => {
        onChange(PERIODS.find((period) => period === next) ?? 'ThisMonth');
      }}
    />
  );
}

export function CompletionPeriodProvider({ children }: { children: ReactNode }): ReactElement {
  const [period, setPeriod] = useState<Period>('ThisMonth');
  return (
    <CompletionPeriodContext.Provider value={{ period, setPeriod }}>
      {children}
    </CompletionPeriodContext.Provider>
  );
}

/** The entity's dashboard: its rate in one chart with the all-entity average as a marked line, and no other entity. */
export function OwnEntityCompletionCard(): ReactElement {
  const { t } = useI18n();
  const [period, setPeriod] = useState<Period>('ThisMonth');
  const load = useCallback(
    async (signal: AbortSignal) =>
      (
        await apiRequest<OwnEntityCompletion>('/entity-completion-rates/own', {
          query: { period },
          signal,
        })
      ).data,
    [period],
  );
  const rate = useApiResource(load);
  const own = rate.data?.completionPercent ?? null;
  const average = rate.data?.averagePercent ?? null;
  const below = own !== null && average !== null ? Number((average - own).toFixed(2)) : 0;

  return (
    <section className="completion" aria-label={t('externalParticipation.completion.ownTitle')}>
      <div className="section__header">
        <h2>{t('externalParticipation.completion.ownTitle')}</h2>
        <PeriodField value={period} onChange={setPeriod} />
      </div>
      {rate.data === undefined ? (
        rate.loading ? (
          <LoadingState />
        ) : (
          <ErrorState
            error={rate.error}
            message={participationProblemMessage(rate.error, t)}
            onRetry={rate.reload}
          />
        )
      ) : own === null ? (
        <EmptyState title={t('externalParticipation.completion.ownEmpty')} reason="period" />
      ) : (
        <>
          <p className="completion__figure">
            <strong dir="ltr">{percent(own)}</strong>{' '}
            {t('externalParticipation.completion.counts', {
              accepted: rate.data.accepted,
              issued: rate.data.issued,
            })}
          </p>
          <div
            className="completion__chart"
            role="img"
            aria-label={t('externalParticipation.completion.chartLabel', {
              own: percent(own),
              average: average === null ? '—' : percent(average),
            })}
          >
            <span className="completion__fill" style={{ inlineSize: `${String(own)}%` }} />
            {average !== null && (
              <span
                className="completion__marker"
                style={{ insetInlineStart: `${String(average)}%` }}
              />
            )}
          </div>
          {average !== null && (
            <p className="completion__legend">
              {t('externalParticipation.completion.average', { average: percent(average) })}
            </p>
          )}
          {below > 0 && (
            <p className="notice notice--warning">
              {t('externalParticipation.completion.below', { points: String(below) })}
            </p>
          )}
        </>
      )}
    </section>
  );
}

/**
 * AHDA's dashboard: every entity it may see, lowest rate first, each opening the participation monitor filtered to it. The donut's bands and its
 * "see more" open it here (review round 8, point 03): a band in the address shows that band's entities only, with a way back to all of them.
 */
export function EntityCompletionPanel(): ReactElement | null {
  return useCan('EXTERNAL_REQUEST_VIEW') ? <EntityCompletionTable /> : null;
}

function EntityCompletionTable(): ReactElement {
  const { t } = useI18n();
  const entityName = useEntityNames();
  const { period, setPeriod } = useCompletionPeriod();
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
  const rates = useApiResource(load);
  const selection = useRowSelection();
  const location = useLocation();
  const section = useRef<HTMLElement>(null);
  const bandParam = new URLSearchParams(location.search).get(COMPLETION_BAND_PARAM);
  const band = isCompletionBand(bandParam) ? bandParam : null;
  const rows = (rates.data?.items ?? []).filter(
    (row) => band === null || bandOf(row.completionPercent) === band,
  );
  const allEntities = (() => {
    const query = new URLSearchParams(location.search);
    query.delete(COMPLETION_BAND_PARAM);
    return `${location.pathname}?${query.toString()}#${ENTITY_COMPLETION_ANCHOR}`;
  })();
  const loaded = rates.data !== undefined;
  // A link to the table names its anchor: the dashboard above it is long, so the page moves to the table once it has rows to show.
  useEffect(() => {
    if (loaded && location.hash === `#${ENTITY_COMPLETION_ANCHOR}`) {
      section.current?.scrollIntoView({ block: 'start' });
    }
  }, [loaded, location.hash, location.search]);
  return (
    <section
      ref={section}
      id={ENTITY_COMPLETION_ANCHOR}
      className="completion"
      aria-label={t('externalParticipation.completion.title')}
    >
      <div className="section__header">
        <h2>{t('externalParticipation.completion.title')}</h2>
        <PeriodField value={period} onChange={setPeriod} />
      </div>
      {rates.data === undefined ? (
        rates.loading ? (
          <LoadingState />
        ) : (
          <ErrorState
            error={rates.error}
            message={participationProblemMessage(rates.error, t)}
            onRetry={rates.reload}
          />
        )
      ) : rates.data.items.length === 0 ? (
        <EmptyState title={t('externalParticipation.completion.empty')} reason="period" />
      ) : (
        <>
          {band !== null && (
            <p className="notice" role="status">
              {t('externalParticipation.completion.bandFilter', {
                band: t(`dashboards.insights.completionBands.${band}`),
                count: rows.length,
              })}{' '}
              <Link to={allEntities}>{t('externalParticipation.completion.showAll')}</Link>
            </p>
          )}
          {rates.data.averagePercent !== null && (
            <p className="completion__legend">
              {t('externalParticipation.completion.average', {
                average: percent(rates.data.averagePercent),
              })}
            </p>
          )}
          <TableContainer
            caption={t('externalParticipation.completion.title')}
            selection={{ selection, exportName: 'entity-completion' }}
          >
            <thead>
              <tr>
                <SelectAllHeader
                  ids={rows.map((row) => row.externalEntityId)}
                  selection={selection}
                />
                <th scope="col">{t('externalParticipation.table.entity')}</th>
                <th scope="col">{t('externalParticipation.completion.issued')}</th>
                <th scope="col">{t('externalParticipation.completion.submitted')}</th>
                <th scope="col">{t('externalParticipation.completion.accepted')}</th>
                <th scope="col">{t('externalParticipation.completion.overdue')}</th>
                <th scope="col">{t('externalParticipation.completion.percent')}</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={row.externalEntityId}>
                  <SelectRowCell
                    id={row.externalEntityId}
                    name={entityName(row.externalEntityId)}
                    selection={selection}
                  />
                  <td>
                    <Link
                      to={`${PARTICIPATION_MONITOR_PATH}?externalEntityId=${row.externalEntityId}`}
                    >
                      {entityName(row.externalEntityId)}
                    </Link>
                  </td>
                  <td dir="ltr">{row.issued}</td>
                  <td dir="ltr">{row.submitted}</td>
                  <td dir="ltr">{row.accepted}</td>
                  <td dir="ltr">{row.overdue}</td>
                  <td dir="ltr">{percent(row.completionPercent)}</td>
                </tr>
              ))}
            </tbody>
          </TableContainer>
        </>
      )}
    </section>
  );
}
