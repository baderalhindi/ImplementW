import { type ReactElement, useCallback, useState } from 'react';
import { Link } from 'react-router';

import { useCan } from '@/features/identity-access/permissions/permissions.ts';
import { apiRequest } from '@/shared/api/httpClient.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { SelectField } from '@/shared/ui/FormFields.tsx';
import { TableContainer } from '@/shared/ui/Layout.tsx';
import { SelectAllHeader, SelectRowCell } from '@/shared/ui/rowSelection.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';
import { useRowSelection } from '@/shared/ui/useRowSelection.ts';

import { PARTICIPATION_MONITOR_PATH } from './paths.ts';
import { participationProblemMessage } from './problems.ts';
import { useEntityNames } from './useExternalParticipationData.ts';

// Items 9 and 10 of review round 5: one definition, so an entity and AHDA read the same number. The API does the counting.

const PERIODS = ['ThisMonth', 'LastThreeMonths', 'ThisYear'] as const;
type Period = (typeof PERIODS)[number];

interface EntityCompletionRow {
  externalEntityId: string;
  issued: number;
  submitted: number;
  accepted: number;
  overdue: number;
  completionPercent: number;
}

interface EntityCompletionPage {
  items: EntityCompletionRow[];
  averagePercent: number | null;
}

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

/** Whole points when it is a whole number, else the two places the API keeps. */
function percent(value: number): string {
  return `${String(Number(value.toFixed(2)))}%`;
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
        <EmptyState title={t('externalParticipation.completion.ownEmpty')} />
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

/** AHDA's dashboard: every entity it may see, lowest rate first, each opening the participation monitor filtered to it. */
export function EntityCompletionPanel(): ReactElement | null {
  return useCan('EXTERNAL_REQUEST_VIEW') ? <EntityCompletionTable /> : null;
}

function EntityCompletionTable(): ReactElement {
  const { t } = useI18n();
  const entityName = useEntityNames();
  const [period, setPeriod] = useState<Period>('ThisMonth');
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
  return (
    <section className="completion" aria-label={t('externalParticipation.completion.title')}>
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
        <EmptyState title={t('externalParticipation.completion.empty')} />
      ) : (
        <>
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
                  ids={rates.data.items.map((row) => row.externalEntityId)}
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
              {rates.data.items.map((row) => (
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
