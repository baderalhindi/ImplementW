import { type ReactElement, useState } from 'react';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { type ProjectDetail } from '@/features/projects/api/types.ts';
import { todayUtc } from '@/features/tasks/taskRules.ts';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { canPublishKpi, canRecordKpi } from './access.ts';
import { KpiTable } from './components/KpiTable.tsx';
import { KpiValueDialog } from './dialogs/KpiValueDialog.tsx';
import { PublishMeasurementDialog } from './dialogs/PublishMeasurementDialog.tsx';
import { activeTarget, latestInStatus } from './financialKpiRules.ts';
import { financialKpiProblemMessage, isForbidden } from './problems.ts';
import { type KpiEntry, useKpiLookups, useProjectKpis } from './useKpiData.ts';

type OpenDialog =
  | { kind: 'value'; entry: KpiEntry; measurementId: string | null }
  | { kind: 'publish'; entry: KpiEntry; measurementId: string }
  | null;

/**
 * SCR-050 Project KPIs tab: every KPI assigned to the project, the target in force, and where each stands for the
 * period today falls in — its value, or "No data for this period" (acceptance criterion 1). The Project Manager records
 * a value (MOD-024); AHDA publishes a submitted one. Each KPI opens its history and trend (SCR-073).
 */
export function ProjectKpis({
  project,
  user,
}: {
  project: ProjectDetail;
  user: SessionUser;
}): ReactElement {
  const { t } = useI18n();
  const today = todayUtc();
  const kpis = useProjectKpis(project.id);
  const lookups = useKpiLookups();
  const personName = usePersonNames(
    (kpis.data ?? []).flatMap((entry) => [
      entry.assignment.ownerUserId,
      ...entry.measurements.map((measurement) => measurement.recordedByUserId),
    ]),
  );
  const [notice, setNotice] = useState<Notice | null>(null);
  const [dialog, setDialog] = useState<OpenDialog>(null);

  const done = (message: TranslationKey) => {
    setDialog(null);
    setNotice({ tone: 'success', message: t(message) });
    kpis.reload();
  };
  const stale = () => {
    setDialog(null);
    setNotice({ tone: 'warning', message: t('financialKpi.done.stale') });
    kpis.reload();
  };
  const close = () => {
    setDialog(null);
  };

  if (kpis.data === undefined) {
    return kpis.loading ? (
      <LoadingState />
    ) : isForbidden(kpis.error) ? (
      <p className="state">{t('financialKpi.kpis.forbidden')}</p>
    ) : (
      <ErrorState message={financialKpiProblemMessage(kpis.error, t)} onRetry={kpis.reload} />
    );
  }

  // A new value is offered while no DRAFT is open and a target is approved; its period is chosen in MOD-024, and a
  // period that already has one is refused by the API (KPI_MEASUREMENT_EXISTS). A value submitted for an earlier period
  // does not hold it up.
  const actions = (entry: KpiEntry) => {
    const draft = latestInStatus(entry.measurements, 'DRAFT');
    const submitted = latestInStatus(entry.measurements, 'SUBMITTED');
    const recorder = canRecordKpi(user, project, entry.assignment);
    const canStart = recorder && draft === null && activeTarget(entry.targets) !== null;
    return (
      <span className="figure-group">
        {canStart && (
          <button
            type="button"
            className="button"
            onClick={() => {
              setDialog({ kind: 'value', entry, measurementId: null });
            }}
          >
            {t('financialKpi.actions.recordValue')}
          </button>
        )}
        {recorder && draft !== null && (
          <button
            type="button"
            className="button"
            onClick={() => {
              setDialog({ kind: 'value', entry, measurementId: draft.id });
            }}
          >
            {t('financialKpi.actions.editValue')}
          </button>
        )}
        {submitted !== null && canPublishKpi(user, project, submitted) && (
          <button
            type="button"
            className="button button--primary"
            onClick={() => {
              setDialog({ kind: 'publish', entry, measurementId: submitted.id });
            }}
          >
            {t('financialKpi.actions.publishValue')}
          </button>
        )}
      </span>
    );
  };

  const kpiName = (entry: KpiEntry) => lookups.kpiName(entry.assignment.kpiDefinitionId);
  const unitLabel = (entry: KpiEntry) => lookups.itemLabel(entry.assignment.unitItemId);

  return (
    <div className="progress">
      <PageNotice notice={notice} />
      <h2>{t('financialKpi.kpis.title')}</h2>
      {kpis.data.length === 0 ? (
        <EmptyState title={t('financialKpi.kpis.empty')}>
          <p>{t('financialKpi.kpis.emptyBody')}</p>
        </EmptyState>
      ) : (
        <KpiTable
          caption={t('financialKpi.kpis.caption')}
          rows={kpis.data.map((entry) => ({ entry }))}
          today={today}
          lookups={lookups}
          showTarget
          personName={personName}
          actions={actions}
        />
      )}

      {dialog?.kind === 'value' && (
        <KpiValueDialog
          open
          kpiAssignmentId={dialog.entry.assignment.id}
          measurementId={dialog.measurementId}
          target={activeTarget(dialog.entry.targets)}
          kpiName={kpiName(dialog.entry)}
          unitLabel={unitLabel(dialog.entry)}
          onClose={close}
          onDone={done}
          onStale={stale}
        />
      )}
      {dialog?.kind === 'publish' && (
        <PublishMeasurementDialog
          open
          measurementId={dialog.measurementId}
          kpiName={kpiName(dialog.entry)}
          unitLabel={unitLabel(dialog.entry)}
          personName={personName}
          onClose={close}
          onDone={done}
          onStale={stale}
        />
      )}
    </div>
  );
}
