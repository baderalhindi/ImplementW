import { type ReactElement, useState } from 'react';

import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { type ProjectDetail } from '@/features/projects/api/types.ts';
import { type TranslationKey, type TranslationParams, useI18n } from '@/shared/i18n/i18n.ts';
import { TableContainer } from '@/shared/ui/Layout.tsx';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
import { EmptyState, ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { canEditSchedule, isPlanFrozen } from './access.ts';
import { scheduleApi } from './api/scheduleApi.ts';
import { type ScheduleActivityDetail, type ScheduleDependencyDetail } from './api/types.ts';
import { DateRange } from './components/DateRange.tsx';
import { ScheduleNav } from './components/ScheduleNav.tsx';
import { ScheduleSummary } from './components/ScheduleSummary.tsx';
import { Variance } from './components/ScheduleBadges.tsx';
import { isLiveLeaf } from './dependencyRules.ts';
import { ActivityDialog } from './dialogs/ActivityDialog.tsx';
import { AddDependencyDialog } from './dialogs/AddDependencyDialog.tsx';
import { ConfirmDialog } from './dialogs/ConfirmDialog.tsx';
import { ReforecastDialog } from './dialogs/ReforecastDialog.tsx';
import { activityTree } from './gantt.ts';
import { activityCode, activityLabel, daysLabel } from './presentation.ts';
import { isForbidden, isRefusal, scheduleProblemMessage } from './problems.ts';
import { useScheduleView } from './useScheduleView.ts';

type OpenDialog =
  | { kind: 'activity'; activityId: string | null }
  | { kind: 'reforecast'; activityId: string }
  | { kind: 'cancel'; activity: ScheduleActivityDetail }
  | { kind: 'dependency' }
  | { kind: 'removeDependency'; dependency: ScheduleDependencyDetail }
  | null;

function today(): string {
  return new Date().toISOString().slice(0, 10);
}

/**
 * SCR-060 Schedule Manager: the work breakdown with each activity's working plan, Current Forecast and Approved
 * Baseline side by side, with the variance the API measured (BR-SCH-002, D-9), and the dependencies between them. The
 * project's own Project Manager initializes the schedule, adds and changes activities and dependencies (MOD-015) while
 * no candidate is under review, and, once a baseline is active, updates forecasts.
 */
export function ScheduleManager({
  project,
  user,
}: {
  project: ProjectDetail;
  user: SessionUser;
}): ReactElement {
  const { t } = useI18n();
  const view = useScheduleView(project.id);
  const [notice, setNotice] = useState<Notice | null>(null);
  const [dialog, setDialog] = useState<OpenDialog>(null);
  const [initializing, setInitializing] = useState(false);
  const [initializeError, setInitializeError] = useState<string | null>(null);

  const close = () => {
    setDialog(null);
  };
  const done = (message: TranslationKey, params?: TranslationParams) => {
    setDialog(null);
    setNotice({ tone: 'success', message: t(message, params) });
    view.reload();
  };
  const stale = () => {
    setDialog(null);
    setNotice({ tone: 'warning', message: t('schedule.done.stale') });
    view.reload();
  };

  const initialize = async () => {
    setInitializing(true);
    setInitializeError(null);
    try {
      await scheduleApi.initialize(project.id);
      setNotice({ tone: 'success', message: t('schedule.done.initialized') });
    } catch (error) {
      if (isRefusal(error, 'SCHEDULE_EXISTS')) {
        setNotice({ tone: 'warning', message: t('schedule.problems.exists') });
      } else {
        setInitializeError(scheduleProblemMessage(error, t));
      }
    } finally {
      setInitializing(false);
      view.reload();
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
  const frozen = isPlanFrozen(data.baselines);
  const planEditable = editor && !frozen;
  const forecasting = data.schedule?.activeBaselineId != null;
  const byId = new Map(data.activities.map((activity) => [activity.id, activity]));
  const leaves = data.activities.filter(isLiveLeaf);

  if (data.schedule === null) {
    return (
      <div className="schedule">
        <ScheduleNav projectId={project.id} />
        <PageNotice notice={notice} />
        <EmptyState title={t('schedule.notInitialized.title')}>
          {editor ? (
            <>
              <p>{t('schedule.notInitialized.hint')}</p>
              <FormAlert message={initializeError} />
              <button
                type="button"
                className="button button--primary"
                disabled={initializing}
                onClick={() => void initialize()}
              >
                {initializing ? t('common.states.saving') : t('schedule.actions.initialize')}
              </button>
            </>
          ) : (
            <p>{t('schedule.notInitialized.waiting')}</p>
          )}
        </EmptyState>
      </div>
    );
  }

  const predecessorsOf = (activity: ScheduleActivityDetail) =>
    data.dependencies
      .filter((dependency) => dependency.successorActivityId === activity.id)
      .map(
        (dependency) =>
          `${activityCode(dependency.predecessorActivityId, byId)} ${dependency.dependencyType}${
            dependency.lagDays === 0 ? '' : `+${String(dependency.lagDays)}`
          }`,
      );
  // A cancellation needs the activity free: no live children and no dependencies (D-4).
  const isFree = (activity: ScheduleActivityDetail) =>
    !data.activities.some(
      (other) => other.parentActivityId === activity.id && other.status !== 'CANCELLED',
    ) &&
    !data.dependencies.some(
      (dependency) =>
        dependency.predecessorActivityId === activity.id ||
        dependency.successorActivityId === activity.id,
    );

  return (
    <div className="schedule">
      <ScheduleNav projectId={project.id} />
      <PageNotice notice={notice} />
      <ScheduleSummary view={data} projectId={project.id} />

      <section className="section" aria-labelledby="schedule-activities">
        <div className="section__header">
          <h2 id="schedule-activities">{t('schedule.activities.title')}</h2>
          {planEditable && (
            <button
              type="button"
              className="button button--primary"
              onClick={() => {
                setDialog({ kind: 'activity', activityId: null });
              }}
            >
              {t('schedule.actions.addActivity')}
            </button>
          )}
        </div>
        {data.activities.length === 0 ? (
          <EmptyState title={t('schedule.activities.empty')} />
        ) : (
          <TableContainer caption={t('schedule.activities.caption')}>
            <thead>
              <tr>
                <th scope="col">{t('schedule.activities.wbs')}</th>
                <th scope="col">{t('schedule.activities.activity')}</th>
                <th scope="col">{t('schedule.activities.planned')}</th>
                <th scope="col">{t('schedule.legend.forecast')}</th>
                <th scope="col">{t('schedule.legend.baseline')}</th>
                <th scope="col">{t('schedule.activities.finishVariance')}</th>
                {editor && <th scope="col">{t('common.table.actions')}</th>}
              </tr>
            </thead>
            <tbody>
              {activityTree(data.activities).map(({ activity, depth }) => {
                const cancelled = activity.status === 'CANCELLED';
                const leaf = isLiveLeaf(activity);
                const predecessors = predecessorsOf(activity);
                return (
                  <tr key={activity.id} className={cancelled ? 'row--muted' : undefined}>
                    <td className="cell--ltr">{activity.wbsCode}</td>
                    <td style={{ paddingInlineStart: `${String(0.75 + depth * 1.25)}rem` }}>
                      <span className="subject__name" dir="auto">
                        {activity.name.text}
                      </span>
                      <span className="cell__aside">
                        {activity.activityKind === 'SUMMARY'
                          ? t('schedule.activities.summary')
                          : daysLabel(activity.plannedDurationDays, t)}
                      </span>
                      {predecessors.length > 0 && (
                        <span className="cell__aside">
                          {t('schedule.activities.after', { list: predecessors.join(', ') })}
                        </span>
                      )}
                      {cancelled && (
                        <StatusBadge
                          label={t('schedule.activityStatus.CANCELLED')}
                          tone="neutral"
                        />
                      )}
                    </td>
                    <td>
                      <DateRange
                        start={activity.plannedStartDate}
                        finish={activity.plannedFinishDate}
                      />
                    </td>
                    <td>
                      <DateRange
                        start={activity.forecastStartDate}
                        finish={activity.forecastFinishDate}
                      />
                    </td>
                    <td>
                      <DateRange
                        start={activity.baselineStartDate}
                        finish={activity.baselineFinishDate}
                      />
                    </td>
                    <td>
                      <Variance days={activity.finishVarianceDays} />
                    </td>
                    {editor && (
                      <td>
                        {!cancelled && (
                          <span className="schedule__actions">
                            {!frozen && (
                              <button
                                type="button"
                                className="button button--link"
                                aria-label={t('schedule.actions.editActivity', {
                                  activity: activityLabel(activity),
                                })}
                                onClick={() => {
                                  setDialog({ kind: 'activity', activityId: activity.id });
                                }}
                              >
                                {t('common.actions.edit')}
                              </button>
                            )}
                            {forecasting && leaf && (
                              <button
                                type="button"
                                className="button button--link"
                                aria-label={t('schedule.actions.reforecastActivity', {
                                  activity: activityLabel(activity),
                                })}
                                onClick={() => {
                                  setDialog({ kind: 'reforecast', activityId: activity.id });
                                }}
                              >
                                {t('schedule.actions.reforecast')}
                              </button>
                            )}
                            {!frozen && isFree(activity) && (
                              <button
                                type="button"
                                className="button button--link"
                                aria-label={t('schedule.actions.cancelActivity', {
                                  activity: activityLabel(activity),
                                })}
                                onClick={() => {
                                  setDialog({ kind: 'cancel', activity });
                                }}
                              >
                                {t('schedule.actions.cancel')}
                              </button>
                            )}
                          </span>
                        )}
                      </td>
                    )}
                  </tr>
                );
              })}
            </tbody>
          </TableContainer>
        )}
      </section>

      <section className="section" aria-labelledby="schedule-dependencies">
        <div className="section__header">
          <h2 id="schedule-dependencies">{t('schedule.dependencies.title')}</h2>
          {planEditable && leaves.length >= 2 && (
            <button
              type="button"
              className="button"
              onClick={() => {
                setDialog({ kind: 'dependency' });
              }}
            >
              {t('schedule.actions.addDependency')}
            </button>
          )}
        </div>
        {data.dependencies.length === 0 ? (
          <EmptyState title={t('schedule.dependencies.empty')} />
        ) : (
          <TableContainer caption={t('schedule.dependencies.caption')}>
            <thead>
              <tr>
                <th scope="col">{t('schedule.dependency.predecessor')}</th>
                <th scope="col">{t('schedule.dependency.successor')}</th>
                <th scope="col">{t('schedule.dependency.type')}</th>
                <th scope="col">{t('schedule.dependency.lag')}</th>
                {planEditable && <th scope="col">{t('common.table.actions')}</th>}
              </tr>
            </thead>
            <tbody>
              {data.dependencies.map((dependency) => {
                const predecessor = byId.get(dependency.predecessorActivityId);
                const successor = byId.get(dependency.successorActivityId);
                return (
                  <tr key={dependency.id}>
                    <td dir="auto">
                      {predecessor === undefined
                        ? activityCode(dependency.predecessorActivityId, byId)
                        : activityLabel(predecessor)}
                    </td>
                    <td dir="auto">
                      {successor === undefined
                        ? activityCode(dependency.successorActivityId, byId)
                        : activityLabel(successor)}
                    </td>
                    <td>{t(`schedule.dependencyTypeShort.${dependency.dependencyType}`)}</td>
                    <td>{daysLabel(dependency.lagDays, t)}</td>
                    {planEditable && (
                      <td>
                        <button
                          type="button"
                          className="button button--link"
                          onClick={() => {
                            setDialog({ kind: 'removeDependency', dependency });
                          }}
                        >
                          {t('schedule.actions.removeDependency')}
                        </button>
                      </td>
                    )}
                  </tr>
                );
              })}
            </tbody>
          </TableContainer>
        )}
      </section>

      <ActivityDialog
        open={dialog?.kind === 'activity'}
        activityId={dialog?.kind === 'activity' ? dialog.activityId : null}
        projectId={project.id}
        activities={data.activities}
        dependencies={data.dependencies}
        defaultStart={project.plannedStartDate ?? today()}
        onClose={close}
        onDone={(created) => {
          done(created ? 'schedule.done.activityCreated' : 'schedule.done.activityUpdated');
        }}
        onStale={stale}
      />
      <ReforecastDialog
        activityId={dialog?.kind === 'reforecast' ? dialog.activityId : null}
        activities={data.activities}
        dependencies={data.dependencies}
        onClose={close}
        onDone={() => {
          done('schedule.done.reforecast');
        }}
        onStale={stale}
      />
      <AddDependencyDialog
        open={dialog?.kind === 'dependency'}
        activities={data.activities}
        dependencies={data.dependencies}
        onClose={close}
        onDone={() => {
          done('schedule.done.dependencyAdded');
        }}
        onStale={stale}
      />
      <ConfirmDialog
        open={dialog?.kind === 'cancel'}
        title={t('schedule.cancelActivity.title')}
        body={
          dialog?.kind === 'cancel'
            ? t('schedule.cancelActivity.body', { activity: activityLabel(dialog.activity) })
            : ''
        }
        confirmLabel={t('schedule.cancelActivity.confirm')}
        danger
        action={() =>
          dialog?.kind === 'cancel'
            ? scheduleApi.cancelActivity(dialog.activity.id, null)
            : Promise.resolve()
        }
        onClose={close}
        onDone={() => {
          done('schedule.done.activityCancelled');
        }}
        onStale={stale}
      />
      <ConfirmDialog
        open={dialog?.kind === 'removeDependency'}
        title={t('schedule.removeDependency.title')}
        body={t('schedule.removeDependency.body')}
        confirmLabel={t('schedule.actions.removeDependency')}
        danger
        action={() =>
          dialog?.kind === 'removeDependency'
            ? scheduleApi.deleteDependency(dialog.dependency.id)
            : Promise.resolve()
        }
        onClose={close}
        onDone={() => {
          done('schedule.done.dependencyRemoved');
        }}
        onStale={stale}
      />
    </div>
  );
}
