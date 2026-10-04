import { type ReactElement } from 'react';
import { Link } from 'react-router';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { periodLabel } from '@/features/progress/components/Period.tsx';
import { SemanticStateBadge } from '@/features/progress/components/ProgressBadges.tsx';
import { usePeriods } from '@/features/progress/usePeriods.ts';
import { type ProjectDetail } from '@/features/projects/api/types.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { TableContainer } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import {
  FinancialStatusBadge,
  SensitiveNote,
  UpdateStatusBadge,
  VersionStatusBadge,
} from './components/Badges.tsx';
import { AmountFigure, ProvenanceLine, ValueStateFlag } from './components/Figures.tsx';
import { FinancialStates } from './components/FinancialStates.tsx';
import { figureOf } from './financialKpiRules.ts';
import { financialKpiProblemMessage, isForbidden } from './problems.ts';
import { peopleOf, useFinancialOverview } from './useFinancialData.ts';

/**
 * SCR-071 Financial Progress: the project's financial record over time. The official and the live position head it
 * (M-12); then every published period, fixed as published; every revision of every period with its review; and every
 * version of the Approved Budget. Each figure carries its source and as-of date (ADR-008 extended) and each Unknown one
 * its state in words (acceptance criterion 1). Open commitments are not shown (gate decision).
 */
export function FinancialHistory({ project }: { project: ProjectDetail }): ReactElement {
  const { t, formatDateTime } = useI18n();
  const overview = useFinancialOverview(project.id);
  const period = usePeriods(project.id);
  const personName = usePersonNames(peopleOf(overview.data));
  const labelOf = (cycleId: string) => periodLabel(period(cycleId), cycleId, t);
  const when = (at: string | null, by: string | null) =>
    at === null ? t('common.values.none') : `${formatDateTime(at)} · ${personName(by)}`;

  const heading = (
    <p>
      <Link to={`/projects/${project.id}/financials`}>{t('financialKpi.actions.back')}</Link>
    </p>
  );

  if (overview.data === undefined) {
    return (
      <>
        {heading}
        {overview.loading ? (
          <LoadingState />
        ) : isForbidden(overview.error) ? (
          <p className="state">{t('financialKpi.financials.forbidden')}</p>
        ) : (
          <ErrorState
            message={financialKpiProblemMessage(overview.error, t)}
            onRetry={overview.reload}
          />
        )}
      </>
    );
  }

  const data = overview.data;
  const budgets = data.commitments.filter((c) => c.commitmentType !== 'OPEN_COMMITMENT');
  return (
    <div className="progress">
      {heading}
      <h2>{t('financialKpi.history.title')}</h2>
      <SensitiveNote />
      <FinancialStates overview={data} periodLabel={labelOf} personName={personName} />

      <section className="section" aria-labelledby="financial-snapshots">
        <h3 id="financial-snapshots">{t('financialKpi.history.snapshots')}</h3>
        {data.snapshots.length === 0 ? (
          <EmptyState title={t('financialKpi.financials.official.none')} />
        ) : (
          <TableContainer caption={t('financialKpi.history.snapshotsCaption')}>
            <thead>
              <tr>
                <th scope="col">{t('financialKpi.figures.period')}</th>
                <th scope="col">{t('financialKpi.figures.financialStatus')}</th>
                <th scope="col">{t('financialKpi.figures.approvedBudget')}</th>
                <th scope="col">{t('financialKpi.figures.actual')}</th>
                <th scope="col">{t('financialKpi.figures.forecast')}</th>
                <th scope="col">{t('financialKpi.figures.source')}</th>
                <th scope="col">{t('financialKpi.history.published')}</th>
              </tr>
            </thead>
            <tbody>
              {data.snapshots.map((snapshot) => (
                <tr key={snapshot.id}>
                  <td>
                    <span className="progress__period" dir="ltr">
                      {labelOf(snapshot.reportingCycleId)}
                    </span>
                    <span className="details__aside">
                      <SemanticStateBadge state="official" />
                    </span>
                  </td>
                  <td>
                    <FinancialStatusBadge status={snapshot.financialStatus} />
                  </td>
                  <td>
                    <AmountFigure
                      state={figureOf(snapshot, 'approvedBudgetSar', null)}
                      absent="financialKpi.value.noBudget"
                    />
                  </td>
                  <td>
                    <AmountFigure
                      state={figureOf(snapshot, 'actualExpenditureToDateSar', snapshot.valueStatus)}
                      absent="financialKpi.value.MISSING"
                    />
                  </td>
                  <td>
                    <AmountFigure
                      state={figureOf(snapshot, 'forecastAtCompletionSar', snapshot.valueStatus)}
                      absent="financialKpi.value.noForecast"
                    />
                  </td>
                  <td>
                    <ProvenanceLine
                      provenance={snapshot}
                      maskedFields={snapshot.maskedFields}
                      personName={personName}
                    />
                  </td>
                  <td>{when(snapshot.publishedAt, snapshot.publishedByUserId)}</td>
                </tr>
              ))}
            </tbody>
          </TableContainer>
        )}
      </section>

      <section className="section" aria-labelledby="financial-updates">
        <h3 id="financial-updates">{t('financialKpi.history.updates')}</h3>
        {data.updates.length === 0 ? (
          <EmptyState title={t('financialKpi.history.noUpdates')} />
        ) : (
          <TableContainer caption={t('financialKpi.history.updatesCaption')}>
            <thead>
              <tr>
                <th scope="col">{t('financialKpi.figures.period')}</th>
                <th scope="col">{t('financialKpi.history.revision')}</th>
                <th scope="col">{t('financialKpi.history.status')}</th>
                <th scope="col">{t('financialKpi.figures.valueStatus')}</th>
                <th scope="col">{t('financialKpi.figures.actual')}</th>
                <th scope="col">{t('financialKpi.figures.forecast')}</th>
                <th scope="col">{t('financialKpi.figures.source')}</th>
                <th scope="col">{t('financialKpi.history.submitted')}</th>
                <th scope="col">{t('financialKpi.history.reviewed')}</th>
              </tr>
            </thead>
            <tbody>
              {data.updates.map((update) => (
                <tr key={update.id}>
                  <td>
                    <span className="progress__period" dir="ltr">
                      {labelOf(update.reportingCycleId)}
                    </span>
                  </td>
                  <td>{update.revisionNo}</td>
                  <td>
                    <UpdateStatusBadge status={update.status} />
                    {update.status === 'RETURNED' && update.returnReason !== null && (
                      <p className="cell__aside" dir="auto">
                        {t('financialKpi.history.returnReason', {
                          reason: update.returnReason.text,
                        })}
                      </p>
                    )}
                  </td>
                  <td>
                    {update.valueStatus === 'MEASURED' ? (
                      t('financialKpi.value.MEASURED')
                    ) : (
                      <ValueStateFlag status={update.valueStatus} />
                    )}
                  </td>
                  <td>
                    <AmountFigure
                      state={figureOf(update, 'actualExpenditureToDateSar', update.valueStatus)}
                      absent="financialKpi.value.MISSING"
                    />
                  </td>
                  <td>
                    <AmountFigure
                      state={figureOf(update, 'forecastAtCompletionSar', update.valueStatus)}
                      absent="financialKpi.value.noForecast"
                    />
                  </td>
                  <td>
                    <ProvenanceLine
                      provenance={update}
                      maskedFields={update.maskedFields}
                      personName={personName}
                    />
                  </td>
                  <td>{when(update.submittedAt, update.submittedByUserId)}</td>
                  <td>{when(update.reviewedAt, update.reviewedByUserId)}</td>
                </tr>
              ))}
            </tbody>
          </TableContainer>
        )}
      </section>

      <section className="section" aria-labelledby="financial-budgets">
        <h3 id="financial-budgets">{t('financialKpi.history.budgets')}</h3>
        {budgets.length === 0 ? (
          <EmptyState title={t('financialKpi.value.noBudget')} />
        ) : (
          <TableContainer caption={t('financialKpi.history.budgetsCaption')}>
            <thead>
              <tr>
                <th scope="col">{t('financialKpi.history.version')}</th>
                <th scope="col">{t('financialKpi.history.status')}</th>
                <th scope="col">{t('financialKpi.figures.approvedBudget')}</th>
                <th scope="col">{t('financialKpi.history.effectiveFrom')}</th>
                <th scope="col">{t('financialKpi.figures.source')}</th>
                <th scope="col">{t('financialKpi.history.activatedAt')}</th>
              </tr>
            </thead>
            <tbody>
              {budgets.map((commitment) => (
                <tr key={commitment.id}>
                  <td>
                    {t('financialKpi.history.versionRevision', {
                      version: commitment.versionNo,
                      revision: commitment.revisionNo,
                    })}
                    {commitment.commitmentType === 'DECLARED_BUDGET' && (
                      <span className="details__aside">{t('financialKpi.history.declared')}</span>
                    )}
                  </td>
                  <td>
                    <VersionStatusBadge status={commitment.status} />
                  </td>
                  <td>
                    <AmountFigure
                      state={figureOf(commitment, 'amountSar', null)}
                      absent="financialKpi.value.noBudget"
                    />
                  </td>
                  <td>{commitment.effectiveFrom ?? t('common.values.none')}</td>
                  <td>
                    <ProvenanceLine
                      provenance={commitment}
                      maskedFields={commitment.maskedFields}
                      personName={personName}
                    />
                  </td>
                  <td>
                    {commitment.activatedAt === null
                      ? t('common.values.none')
                      : formatDateTime(commitment.activatedAt)}
                  </td>
                </tr>
              ))}
            </tbody>
          </TableContainer>
        )}
      </section>
    </div>
  );
}
