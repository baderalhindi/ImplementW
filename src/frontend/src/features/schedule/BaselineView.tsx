import { type ReactElement, useCallback, useState } from 'react';
import { Link, useSearchParams } from 'react-router';

import { type ApprovalDecision } from '@/features/approvals/api/types.ts';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { type ProjectDetail } from '@/features/projects/api/types.ts';
import { Detail } from '@/features/projects/components/Detail.tsx';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { TableContainer } from '@/shared/ui/Layout.tsx';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
import { EmptyState, ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';

import { candidateOf, canDecideBaseline, canEditSchedule } from './access.ts';
import { scheduleApi } from './api/scheduleApi.ts';
import { type ProjectBaselineDetail, type ScheduleActivityDetail } from './api/types.ts';
import { DateRange } from './components/DateRange.tsx';
import { BaselineStatusBadge, Variance } from './components/ScheduleBadges.tsx';
import { ScheduleNav } from './components/ScheduleNav.tsx';
import { latestFinish } from './dependencyRules.ts';
import { ApproveBaselineDialog } from './dialogs/ApproveBaselineDialog.tsx';
import { ConfirmDialog } from './dialogs/ConfirmDialog.tsx';
import { SubmitBaselineDialog } from './dialogs/SubmitBaselineDialog.tsx';
import { activityTree } from './gantt.ts';
import { activityCode, baselineName, daysLabel } from './presentation.ts';
import { isForbidden, isRefusal, scheduleProblemMessage } from './problems.ts';
import { useBaselineReview } from './useBaselineReview.ts';
import { activeBaselineOf, useScheduleView } from './useScheduleView.ts';

type OpenDialog = 'submit' | 'delete' | 'approve' | null;

const DECISION_NOTICES: Record<ApprovalDecision, TranslationKey> = {
  approve: 'schedule.done.approved',
  return: 'schedule.done.returned',
  reject: 'schedule.done.rejected',
};

/**
 * SCR-061 Baseline View: every baseline of the project, latest first, with the candidate's workflow (MOD-017 submit,
 * MOD-018 approve), and the chosen baseline's frozen copy. Only the ACTIVE one carries the variance the API measured;
 * a superseded baseline's dates are history and nothing is measured against them here.
 */
export function BaselineView({
  project,
  user,
}: {
  project: ProjectDetail;
  user: SessionUser;
}): ReactElement {
  const { t, formatDateTime } = useI18n();
  const view = useScheduleView(project.id);
  const [params, setParams] = useSearchParams();
  const [notice, setNotice] = useState<Notice | null>(null);
  const [dialog, setDialog] = useState<OpenDialog>(null);
  const [creating, setCreating] = useState(false);
  const [createError, setCreateError] = useState<string | null>(null);
  const candidate = view.data === undefined ? null : candidateOf(view.data.baselines);
  const review = useBaselineReview(candidate);

  const reload = () => {
    view.reload();
    review.reload();
  };
  const finish = (message: TranslationKey, tone: Notice['tone'] = 'success') => {
    setDialog(null);
    setNotice({ tone, message: t(message) });
    reload();
  };

  const create = async () => {
    setCreating(true);
    setCreateError(null);
    try {
      const created = await scheduleApi.createBaseline(project.id);
      setParams({ baselineId: created.data.id });
      setNotice({ tone: 'success', message: t('schedule.done.candidateCreated') });
    } catch (error) {
      if (isRefusal(error, 'SCHEDULE_BASELINE_CANDIDATE_EXISTS')) {
        setNotice({ tone: 'warning', message: t('schedule.problems.candidateExists') });
      } else {
        setCreateError(scheduleProblemMessage(error, t));
      }
    } finally {
      setCreating(false);
      reload();
    }
  };

  if (view.loading) {
    return <LoadingState />;
  }
  if (view.data === undefined) {
    return isForbidden(view.error) ? (
      <p className="state">{t('schedule.forbidden')}</p>
    ) : (
      <ErrorState message={scheduleProblemMessage(view.error, t)} onRetry={view.reload} />
    );
  }

  const data = view.data;
  const editor = canEditSchedule(user, project);
  const active = activeBaselineOf(data);
  const task = review.data?.task ?? null;
  const run = review.data?.run ?? null;
  const decider = candidate !== null && canDecideBaseline(user, candidate, task);
  const selectedId = params.get('baselineId') ?? active?.id ?? data.baselines[0]?.id ?? null;
  const selected = data.baselines.find((baseline) => baseline.id === selectedId) ?? null;

  return (
    <div className="schedule">
      <ScheduleNav projectId={project.id} />
      <PageNotice notice={notice} />

      <section className="section" aria-labelledby="schedule-baselines">
        <div className="section__header">
          <h2 id="schedule-baselines">{t('schedule.baselines.title')}</h2>
          {editor && data.schedule !== null && candidate === null && (
            <button
              type="button"
              className="button button--primary"
              disabled={creating}
              onClick={() => void create()}
            >
              {creating ? t('common.states.saving') : t('schedule.actions.createCandidate')}
            </button>
          )}
        </div>
        <FormAlert message={createError} />
        {review.error !== null && <FormAlert message={scheduleProblemMessage(review.error, t)} />}
        {data.baselines.length === 0 ? (
          <EmptyState title={t('schedule.baselines.empty')}>
            <p>{t('schedule.baselines.emptyHint')}</p>
          </EmptyState>
        ) : (
          <TableContainer caption={t('schedule.baselines.caption')}>
            <thead>
              <tr>
                <th scope="col">{t('schedule.baselines.baseline')}</th>
                <th scope="col">{t('schedule.baselines.status')}</th>
                <th scope="col">{t('schedule.baselines.finish')}</th>
                <th scope="col">{t('schedule.baselines.activated')}</th>
                <th scope="col">{t('common.table.actions')}</th>
              </tr>
            </thead>
            <tbody>
              {data.baselines.map((baseline) => {
                const isCandidate = baseline.id === candidate?.id;
                return (
                  <tr
                    key={baseline.id}
                    aria-current={baseline.id === selectedId ? 'true' : undefined}
                  >
                    <td>
                      <Link to={`?baselineId=${baseline.id}`}>{baselineName(baseline, t)}</Link>
                    </td>
                    <td>
                      <BaselineStatusBadge baseline={baseline} />
                    </td>
                    <td className="cell--ltr">{baseline.baselineFinishDate}</td>
                    <td>
                      {baseline.activatedAt === null
                        ? t('common.values.none')
                        : formatDateTime(baseline.activatedAt)}
                      {baseline.supersededAt !== null && (
                        <span className="cell__aside">
                          {t('schedule.baselines.supersededAt', {
                            at: formatDateTime(baseline.supersededAt),
                          })}
                        </span>
                      )}
                    </td>
                    <td>
                      <span className="schedule__actions">
                        {isCandidate &&
                          editor &&
                          (baseline.status === 'DRAFT' || baseline.status === 'RETURNED') && (
                            <button
                              type="button"
                              className="button button--primary"
                              onClick={() => {
                                setDialog('submit');
                              }}
                            >
                              {t('schedule.actions.submitBaseline')}
                            </button>
                          )}
                        {isCandidate && editor && baseline.status === 'DRAFT' && (
                          <button
                            type="button"
                            className="button button--link"
                            onClick={() => {
                              setDialog('delete');
                            }}
                          >
                            {t('schedule.actions.deleteDraft')}
                          </button>
                        )}
                        {isCandidate && decider && (
                          <button
                            type="button"
                            className="button button--primary"
                            onClick={() => {
                              setDialog('approve');
                            }}
                          >
                            {t('schedule.actions.decideBaseline')}
                          </button>
                        )}
                        {isCandidate && run !== null && (
                          <Link className="cell__link" to={`/approvals/instances/${run.id}`}>
                            {t('schedule.baselines.reviewHistory')}
                          </Link>
                        )}
                      </span>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </TableContainer>
        )}
      </section>

      {selected !== null && (
        <SelectedBaseline
          key={selected.id}
          baseline={selected}
          activities={data.activities}
          isActive={selected.id === active?.id}
        />
      )}

      <SubmitBaselineDialog
        baseline={dialog === 'submit' ? candidate : null}
        active={active}
        activities={data.activities}
        onClose={() => {
          setDialog(null);
        }}
        onDone={(submitted) => {
          finish(
            submitted.status === 'ACTIVE'
              ? 'schedule.done.baselineActivated'
              : 'schedule.done.baselineSubmitted',
          );
        }}
        onStale={() => {
          finish('schedule.done.stale', 'warning');
        }}
      />
      <ApproveBaselineDialog
        baseline={dialog === 'approve' ? candidate : null}
        task={dialog === 'approve' ? task : null}
        active={active}
        plannedFinish={latestFinish(data.activities, 'plan')}
        projectId={project.id}
        onClose={() => {
          setDialog(null);
        }}
        onDone={(decision) => {
          finish(DECISION_NOTICES[decision]);
        }}
        onStale={() => {
          finish('schedule.done.stale', 'warning');
        }}
      />
      <ConfirmDialog
        open={dialog === 'delete'}
        title={t('schedule.deleteDraft.title')}
        body={t('schedule.deleteDraft.body')}
        confirmLabel={t('schedule.actions.deleteDraft')}
        danger
        action={() =>
          candidate === null ? Promise.resolve() : scheduleApi.deleteBaseline(candidate.id)
        }
        onClose={() => {
          setDialog(null);
        }}
        onDone={() => {
          setParams({});
          finish('schedule.done.draftDeleted');
        }}
        onStale={() => {
          finish('schedule.done.stale', 'warning');
        }}
      />
    </div>
  );
}

/** A baseline's details and, once it has activated, its frozen copy of the activities and dependencies. */
function SelectedBaseline({
  baseline,
  activities,
  isActive,
}: {
  baseline: ProjectBaselineDetail;
  activities: ScheduleActivityDetail[];
  isActive: boolean;
}): ReactElement {
  const { t, formatDateTime } = useI18n();
  // The copy is written when an APPROVED baseline activates (D-8); a Declared Baseline has none (D-13).
  const hasCopy =
    baseline.baselineType === 'APPROVED' &&
    (baseline.status === 'ACTIVE' || baseline.status === 'SUPERSEDED');
  const load = useCallback(
    async (signal: AbortSignal) =>
      hasCopy
        ? Promise.all([
            scheduleApi.baselineActivities(baseline.id, signal),
            scheduleApi.baselineDependencies(baseline.id, signal),
          ])
        : null,
    [baseline.id, hasCopy],
  );
  const copy = useApiResource(load);
  const byId = new Map(activities.map((activity) => [activity.id, activity]));
  // The copy in work-breakdown order, as the working schedule lists the same activities.
  const order = new Map(activityTree(activities).map((row, index) => [row.activity.id, index]));

  return (
    <section className="section" aria-labelledby="schedule-selected-baseline">
      <h2 id="schedule-selected-baseline">{baselineName(baseline, t)}</h2>
      <dl className="details">
        <Detail term={t('schedule.baselines.status')}>
          <BaselineStatusBadge baseline={baseline} />
        </Detail>
        <Detail term={t('schedule.baselines.finish')}>{baseline.baselineFinishDate}</Detail>
        {baseline.activatedAt !== null && (
          <Detail term={t('schedule.baselines.activated')}>
            {formatDateTime(baseline.activatedAt)}
          </Detail>
        )}
        {baseline.supersededAt !== null && (
          <Detail term={t('schedule.baselines.superseded')}>
            {formatDateTime(baseline.supersededAt)}
          </Detail>
        )}
        {baseline.declaredEndDate !== null && (
          <Detail term={t('schedule.baselines.declaredEnd')}>{baseline.declaredEndDate}</Detail>
        )}
        {baseline.declaredScope !== null && (
          <Detail term={t('schedule.baselines.declaredScope')}>
            <span dir="auto">{baseline.declaredScope.text}</span>
          </Detail>
        )}
        {baseline.changeAuthorizationId !== null && (
          <Detail term={t('schedule.submit.changeAuthorization')}>
            <span className="break-all">{baseline.changeAuthorizationId}</span>
          </Detail>
        )}
      </dl>
      {!hasCopy ? (
        <p className="form__note">
          {t(
            baseline.baselineType === 'DECLARED'
              ? 'schedule.baselines.declaredNoCopy'
              : baseline.status === 'REJECTED' || baseline.status === 'WITHDRAWN'
                ? 'schedule.baselines.endedNoCopy'
                : 'schedule.baselines.candidateNoCopy',
          )}
        </p>
      ) : copy.loading ? (
        <LoadingState />
      ) : copy.data === undefined || copy.data === null ? (
        <ErrorState message={scheduleProblemMessage(copy.error, t)} onRetry={copy.reload} />
      ) : (
        <>
          <h3>{t('schedule.baselines.copyTitle')}</h3>
          <TableContainer caption={t('schedule.baselines.copyCaption')}>
            <thead>
              <tr>
                <th scope="col">{t('schedule.activities.wbs')}</th>
                <th scope="col">{t('schedule.activities.activity')}</th>
                <th scope="col">{t('schedule.legend.baseline')}</th>
                <th scope="col">{t('schedule.activity.duration')}</th>
                {isActive && <th scope="col">{t('schedule.legend.forecast')}</th>}
                {isActive && <th scope="col">{t('schedule.activities.finishVariance')}</th>}
              </tr>
            </thead>
            <tbody>
              {[...copy.data[0]]
                .sort(
                  (a, b) =>
                    (order.get(a.scheduleActivityId) ?? Number.MAX_SAFE_INTEGER) -
                    (order.get(b.scheduleActivityId) ?? Number.MAX_SAFE_INTEGER),
                )
                .map((entry) => {
                  const working = byId.get(entry.scheduleActivityId);
                  return (
                    <tr key={entry.scheduleActivityId}>
                      <td className="cell--ltr">{activityCode(entry.scheduleActivityId, byId)}</td>
                      <td dir="auto">
                        {working?.name.text ?? t('common.values.none')}
                        {entry.activityKind === 'SUMMARY' && (
                          <span className="cell__aside">{t('schedule.activities.summary')}</span>
                        )}
                      </td>
                      <td>
                        <DateRange
                          start={entry.plannedStartDate}
                          finish={entry.plannedFinishDate}
                        />
                      </td>
                      <td>{daysLabel(entry.plannedDurationDays, t)}</td>
                      {isActive && (
                        <td>
                          <DateRange
                            start={working?.forecastStartDate ?? null}
                            finish={working?.forecastFinishDate ?? null}
                          />
                        </td>
                      )}
                      {isActive && (
                        <td>
                          <Variance days={working?.finishVarianceDays ?? null} />
                        </td>
                      )}
                    </tr>
                  );
                })}
            </tbody>
          </TableContainer>
          {copy.data[1].length > 0 && (
            <>
              <h3>{t('schedule.baselines.copyDependencies')}</h3>
              <ul className="link-list">
                {copy.data[1].map((dependency) => (
                  <li key={`${dependency.predecessorActivityId}-${dependency.successorActivityId}`}>
                    {t('schedule.baselines.copyDependency', {
                      predecessor: activityCode(dependency.predecessorActivityId, byId),
                      successor: activityCode(dependency.successorActivityId, byId),
                      type: t(`schedule.dependencyTypeShort.${dependency.dependencyType}`),
                      lag: daysLabel(dependency.lagDays, t),
                    })}
                  </li>
                ))}
              </ul>
            </>
          )}
        </>
      )}
    </section>
  );
}
