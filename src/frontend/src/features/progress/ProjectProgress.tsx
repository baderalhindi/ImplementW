import { type ReactElement, useCallback, useState } from 'react';
import { Link } from 'react-router';

import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { type ProjectDetail } from '@/features/projects/api/types.ts';
import { Detail } from '@/features/projects/components/Detail.tsx';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
import { EmptyState, ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';

import { canReport, canReview } from './access.ts';
import { progressApi } from './api/progressApi.ts';
import { type ProgressSubmissionDetail, type ProgressSubmissionStatus } from './api/types.ts';
import { FigureDetails } from './components/Figures.tsx';
import { HealthComparison } from './components/HealthComparison.tsx';
import { periodLabel } from './components/Period.tsx';
import { IntakeMarker, SubmissionStatusBadge } from './components/ProgressBadges.tsx';
import { ProgressUpdateDialog, type ProgressUpdateMode } from './dialogs/ProgressUpdateDialog.tsx';
import { ReviewProgressDialog } from './dialogs/ReviewProgressDialog.tsx';
import { isInProgress } from './presentation.ts';
import { isExistingSubmission, isForbidden, progressProblemMessage } from './problems.ts';
import { useHealthView } from './useHealthView.ts';
import { usePeriods } from './usePeriods.ts';

/** The update in the workflow, with the ETag its commands send, and the reason the revision before it was returned. */
interface CurrentUpdate {
  submission: ProgressSubmissionDetail;
  etag: string | null;
  returnReason: string | null;
}

/** Whose move it is from each state of the update in progress. */
const NEXT_STEPS: Record<ProgressSubmissionStatus, TranslationKey> = {
  DRAFT: 'progress.current.next.DRAFT',
  SUBMITTED: 'progress.current.next.SUBMITTED',
  UNDER_REVIEW: 'progress.current.next.UNDER_REVIEW',
  RETURNED: 'progress.current.next.RETURNED',
  PUBLISHED: 'progress.current.next.PUBLISHED',
};

/**
 * The newest revision, if it is still in the workflow, read again on its own for its ETag. A DRAFT that follows a
 * returned revision of the same period shows why it was returned.
 */
async function loadCurrent(projectId: string, signal: AbortSignal): Promise<CurrentUpdate | null> {
  const newest = await progressApi.submissions(projectId, { pageSize: 2 }, signal);
  const [latest, previous] = newest.items;
  if (latest === undefined || !isInProgress(latest.status)) {
    return null;
  }
  const detail = await progressApi.get(latest.id, signal);
  const returned =
    previous?.status === 'RETURNED' && previous.reportingCycleId === latest.reportingCycleId
      ? (previous.returnReason?.text ?? null)
      : null;
  return { submission: detail.data, etag: detail.etag, returnReason: returned };
}

type OpenDialog = ProgressUpdateMode | 'review' | null;

/**
 * SCR-048 Project Progress tab: the official and the live value side by side, each badged (M-12), and the period
 * being reported, with what the person may do with it: start, edit or submit it as the project's Project Manager
 * (MOD-020, MOD-021), or review it as AHDA (MOD-022).
 */
export function ProjectProgress({
  project,
  user,
}: {
  project: ProjectDetail;
  user: SessionUser;
}): ReactElement {
  const { t } = useI18n();
  const health = useHealthView(project.id);
  const load = useCallback((signal: AbortSignal) => loadCurrent(project.id, signal), [project.id]);
  const current = useApiResource(load);
  const period = usePeriods(project.id);
  const [notice, setNotice] = useState<Notice | null>(null);
  const [dialog, setDialog] = useState<OpenDialog>(null);
  const [starting, setStarting] = useState(false);
  const [startError, setStartError] = useState<string | null>(null);
  const intakeDate = project.legacyIntakeDate;

  const reload = () => {
    health.reload();
    current.reload();
  };
  const done = (message: TranslationKey) => {
    setDialog(null);
    setNotice({ tone: 'success', message: t(message) });
    reload();
  };
  const stale = () => {
    setDialog(null);
    setNotice({ tone: 'warning', message: t('progress.done.stale') });
    reload();
  };

  const start = async () => {
    setStarting(true);
    setStartError(null);
    try {
      await progressApi.start(project.id);
      setDialog('submit');
    } catch (error) {
      if (isExistingSubmission(error)) {
        setNotice({ tone: 'warning', message: t('progress.problems.submissionExists') });
      } else {
        setStartError(progressProblemMessage(error, t));
      }
    } finally {
      setStarting(false);
      reload();
    }
  };

  if (health.loading || current.loading) {
    return <LoadingState />;
  }
  const error = health.error ?? current.error;
  if (error !== null || health.data === undefined || current.data === undefined) {
    return isForbidden(error) ? (
      <p className="state">{t('progress.forbidden')}</p>
    ) : (
      <ErrorState message={progressProblemMessage(error, t)} onRetry={reload} />
    );
  }

  const update = current.data;
  const reporter = canReport(user, project);
  const reviewer = update !== null && canReview(user, project, update.submission);
  const label =
    update === null
      ? ''
      : periodLabel(
          period(update.submission.reportingCycleId),
          update.submission.reportingCycleId,
          t,
        );

  return (
    <div className="progress">
      <PageNotice notice={notice} />
      {intakeDate !== null && (
        <p className="progress__intake">
          <IntakeMarker intakeDate={intakeDate} />
          <span className="details__aside">{t('progress.intake.explanation')}</span>
        </p>
      )}
      <HealthComparison view={health.data} intakeDate={intakeDate} />

      <section className="section" aria-labelledby="progress-current">
        <h2 id="progress-current">{t('progress.current.title')}</h2>
        {project.status !== 'ACTIVE' && update === null && (
          <p className="form__note">{t('progress.current.notActive')}</p>
        )}
        {update === null ? (
          <EmptyState title={t('progress.current.none')}>
            {reporter && (
              <>
                <p>{t('progress.current.startHint')}</p>
                <FormAlert message={startError} />
                <button
                  type="button"
                  className="button button--primary"
                  disabled={starting}
                  onClick={() => void start()}
                >
                  {starting ? t('common.states.saving') : t('progress.actions.start')}
                </button>
              </>
            )}
          </EmptyState>
        ) : (
          <>
            <p className="workspace__status">
              <SubmissionStatusBadge status={update.submission.status} />
              <span>
                {t('progress.update.period', {
                  period: label,
                  revision: update.submission.revisionNo,
                })}
              </span>
            </p>
            <p className="workspace__next">{t(NEXT_STEPS[update.submission.status])}</p>
            {update.returnReason !== null && (
              <div className="notice notice--warning" role="status">
                <p>{t('progress.current.returned')}</p>
                <p dir="auto">{update.returnReason}</p>
              </div>
            )}
            <dl className="details">
              <FigureDetails
                figures={{
                  ...update.submission,
                  isOpeningPosition: update.submission.projectIntakeId !== null,
                }}
                intakeDate={intakeDate}
              />
              {update.submission.narrative !== null && (
                <Detail term={t('progress.update.narrative')}>
                  <span dir="auto">{update.submission.narrative.text}</span>
                </Detail>
              )}
            </dl>
            <div className="form__actions">
              {reporter && update.submission.status === 'DRAFT' && (
                <>
                  <button
                    type="button"
                    className="button button--primary"
                    onClick={() => {
                      setDialog('submit');
                    }}
                  >
                    {t('progress.actions.submit')}
                  </button>
                  <button
                    type="button"
                    className="button"
                    onClick={() => {
                      setDialog('edit');
                    }}
                  >
                    {t('progress.actions.edit')}
                  </button>
                </>
              )}
              {reviewer && (
                <button
                  type="button"
                  className="button button--primary"
                  onClick={() => {
                    setDialog('review');
                  }}
                >
                  {t('progress.actions.review')}
                </button>
              )}
            </div>
          </>
        )}
      </section>

      <p>
        <Link to={`/projects/${project.id}/progress/history`}>{t('progress.actions.history')}</Link>
      </p>

      {update !== null && (
        <>
          <ProgressUpdateDialog
            open={dialog === 'edit' || dialog === 'submit'}
            mode={dialog === 'edit' ? 'edit' : 'submit'}
            submission={update.submission}
            etag={update.etag}
            periodLabel={label}
            intakeDate={intakeDate}
            onClose={() => {
              setDialog(null);
            }}
            onDone={done}
            onStale={stale}
          />
          <ReviewProgressDialog
            open={dialog === 'review'}
            submission={update.submission}
            etag={update.etag}
            periodLabel={label}
            intakeDate={intakeDate}
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
