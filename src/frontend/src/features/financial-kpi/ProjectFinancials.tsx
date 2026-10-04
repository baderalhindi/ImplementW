import { type ReactElement, useState } from 'react';
import { Link } from 'react-router';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { periodLabel } from '@/features/progress/components/Period.tsx';
import { usePeriods } from '@/features/progress/usePeriods.ts';
import { type ProjectDetail } from '@/features/projects/api/types.ts';
import { Detail } from '@/features/projects/components/Detail.tsx';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
import { EmptyState, ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';

import { canReportFinancials, canReviewFinancials } from './access.ts';
import { financialKpiApi } from './api/financialKpiApi.ts';
import {
  type FinancialField,
  type FinancialSourceModeDetail,
  type FinancialUpdateStatus,
} from './api/types.ts';
import { SensitiveNote, UpdateStatusBadge } from './components/Badges.tsx';
import { FinancialStates } from './components/FinancialStates.tsx';
import { UpdateFigures } from './components/UpdateFigures.tsx';
import { FinancialUpdateDialog } from './dialogs/FinancialUpdateDialog.tsx';
import { ReviewFinancialDialog } from './dialogs/ReviewFinancialDialog.tsx';
import { financialKpiProblemMessage, isForbidden, isRefusal } from './problems.ts';
import { peopleOf, useFinancialOverview } from './useFinancialData.ts';

/** Whose move it is from each state of the update in progress. */
const NEXT_STEPS: Record<FinancialUpdateStatus, TranslationKey> = {
  DRAFT: 'financialKpi.current.next.DRAFT',
  SUBMITTED: 'financialKpi.current.next.SUBMITTED',
  UNDER_REVIEW: 'financialKpi.current.next.UNDER_REVIEW',
  RETURNED: 'financialKpi.current.next.RETURNED',
  PUBLISHED: 'financialKpi.current.next.PUBLISHED',
};

/** The fields a source mode is shown for. OPEN_COMMITMENT is not in use and the gate hides it. */
const SOURCE_FIELDS: Exclude<FinancialField, 'OPEN_COMMITMENT'>[] = [
  'APPROVED_BUDGET',
  'ACTUAL_EXPENDITURE',
  'FORECAST_AT_COMPLETION',
];

function modeOf(modes: FinancialSourceModeDetail[], field: FinancialField) {
  return modes.find((mode) => mode.fieldCode === field)?.sourceMode ?? 'MANUAL';
}

type OpenDialog = 'update' | 'review' | null;

/**
 * SCR-049 Project Financials tab: the official and the live position side by side (M-12), every figure with its
 * source and as-of date (ADR-008 extended) and every Unknown one in words (acceptance criterion 1); the period being
 * reported, which its Project Manager enters (MOD-023) and AHDA reviews; where each figure comes from.
 */
export function ProjectFinancials({
  project,
  user,
}: {
  project: ProjectDetail;
  user: SessionUser;
}): ReactElement {
  const { t } = useI18n();
  const overview = useFinancialOverview(project.id);
  const period = usePeriods(project.id);
  const personName = usePersonNames(peopleOf(overview.data));
  const [notice, setNotice] = useState<Notice | null>(null);
  const [dialog, setDialog] = useState<OpenDialog>(null);
  const [starting, setStarting] = useState(false);
  const [startError, setStartError] = useState<string | null>(null);

  const labelOf = (cycleId: string) => periodLabel(period(cycleId), cycleId, t);
  const done = (message: TranslationKey) => {
    setDialog(null);
    setNotice({ tone: 'success', message: t(message) });
    overview.reload();
  };
  const stale = () => {
    setDialog(null);
    setNotice({ tone: 'warning', message: t('financialKpi.done.stale') });
    overview.reload();
  };

  const start = async () => {
    setStarting(true);
    setStartError(null);
    try {
      await financialKpiApi.startUpdate(project.id);
      setDialog('update');
    } catch (error) {
      // A retried start whose first attempt succeeded, or one started meanwhile: the update is read again.
      if (isRefusal(error, 'FINANCIAL_UPDATE_EXISTS')) {
        setNotice({ tone: 'warning', message: t('financialKpi.problems.updateExists') });
      } else {
        setStartError(financialKpiProblemMessage(error, t));
      }
    } finally {
      setStarting(false);
      overview.reload();
    }
  };

  if (overview.data === undefined) {
    return overview.loading ? (
      <LoadingState />
    ) : isForbidden(overview.error) ? (
      <p className="state">{t('financialKpi.financials.forbidden')}</p>
    ) : (
      <ErrorState
        message={financialKpiProblemMessage(overview.error, t)}
        onRetry={overview.reload}
      />
    );
  }

  const data = overview.data;
  const current = data.current;
  const reporter = canReportFinancials(user, project);
  const reviewer = current !== null && canReviewFinancials(user, project, current.update);
  const label = current === null ? '' : labelOf(current.update.reportingCycleId);
  const integrated = {
    actual: modeOf(data.sourceModes, 'ACTUAL_EXPENDITURE') === 'INTEGRATED',
    forecast: modeOf(data.sourceModes, 'FORECAST_AT_COMPLETION') === 'INTEGRATED',
  };

  return (
    <div className="progress">
      <PageNotice notice={notice} />
      <SensitiveNote />
      <FinancialStates overview={data} periodLabel={labelOf} personName={personName} />

      <section className="section" aria-labelledby="financial-current">
        <h2 id="financial-current">{t('financialKpi.current.title')}</h2>
        {current === null ? (
          <EmptyState title={t('financialKpi.current.none')}>
            {project.status !== 'ACTIVE' && <p>{t('financialKpi.current.notActive')}</p>}
            {reporter && (
              <>
                <p>{t('financialKpi.current.startHint')}</p>
                <FormAlert message={startError} />
                <button
                  type="button"
                  className="button button--primary"
                  disabled={starting}
                  onClick={() => void start()}
                >
                  {starting ? t('common.states.saving') : t('financialKpi.actions.start')}
                </button>
              </>
            )}
          </EmptyState>
        ) : (
          <>
            <p className="workspace__status">
              <UpdateStatusBadge status={current.update.status} />
              <span>
                {t('financialKpi.update.period', {
                  period: label,
                  revision: current.update.revisionNo,
                })}
              </span>
            </p>
            <p className="workspace__next">{t(NEXT_STEPS[current.update.status])}</p>
            {current.returnReason !== null && (
              <div className="returned-reason" role="status">
                <h3 className="returned-reason__title">{t('financialKpi.current.returned')}</h3>
                <p className="returned-reason__text" dir="auto">
                  {current.returnReason}
                </p>
              </div>
            )}
            <UpdateFigures update={current.update} personName={personName} />
            <div className="form__actions">
              {reporter && current.update.status === 'DRAFT' && (
                <button
                  type="button"
                  className="button button--primary"
                  onClick={() => {
                    setDialog('update');
                  }}
                >
                  {t('financialKpi.actions.update')}
                </button>
              )}
              {reviewer && (
                <button
                  type="button"
                  className="button button--primary"
                  onClick={() => {
                    setDialog('review');
                  }}
                >
                  {t('financialKpi.actions.review')}
                </button>
              )}
            </div>
          </>
        )}
      </section>

      <section className="section" aria-labelledby="financial-sources">
        <h2 id="financial-sources">{t('financialKpi.sources.title')}</h2>
        <dl className="details">
          {SOURCE_FIELDS.map((field) => {
            const mode = modeOf(data.sourceModes, field);
            return (
              <Detail key={field} term={t(`financialKpi.sources.field.${field}`)}>
                <span className="figure-group">
                  <span>{t(`financialKpi.sources.mode.${mode}`)}</span>
                  {mode !== 'MANUAL' && (
                    <span className="details__aside">{t('financialKpi.sources.notConnected')}</span>
                  )}
                </span>
              </Detail>
            );
          })}
        </dl>
      </section>

      <p>
        <Link to={`/projects/${project.id}/financials/history`}>
          {t('financialKpi.actions.history')}
        </Link>
      </p>

      {current !== null && (
        <>
          <FinancialUpdateDialog
            open={dialog === 'update'}
            update={current.update}
            etag={current.etag}
            periodLabel={label}
            integrated={integrated}
            onClose={() => {
              setDialog(null);
            }}
            onDone={done}
            onStale={stale}
          />
          <ReviewFinancialDialog
            open={dialog === 'review'}
            update={current.update}
            etag={current.etag}
            periodLabel={label}
            personName={personName}
            onClose={() => {
              setDialog(null);
            }}
            onDone={done}
            onStale={stale}
          />
        </>
      )}
    </div>
  );
}
