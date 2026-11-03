import { type ReactElement, useCallback, useState } from 'react';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { isInternal } from '../access.ts';
import { followUpApi } from '../api/followUpApi.ts';
import { type ProjectDetail } from '../api/types.ts';
import { Detail } from '../components/Detail.tsx';
import { GapValue, ImportedLabel, OnPlanBadge } from '../components/FollowUpBadges.tsx';
import { FollowUpDialog } from '../dialogs/FollowUpDialog.tsx';
import { formatPoints } from '../presentation.ts';

import { useWorkspace } from './workspaceContext.ts';

/** The states from which RPMO keeps a project's follow-up: once it is approved, until it is closed. */
const FOLLOWED_UP: ProjectDetail['status'][] = [
  'APPROVED_PLANNED',
  'ACTIVE',
  'SUSPENDED',
  'COMPLETED',
];

/**
 * RPMO's follow-up of the project (the 2 Nov 2026 review, R3-3): the on-plan indicator and the other calculated columns, the obstacle and next
 * action, progress and spend as declared — "Not available" when blank, never 0 — the entity's update compliance and the dated notes. AHDA
 * updates it (PROJECT_FOLLOW_UP); everyone who sees the project reads it.
 */
export function FollowUpPanel(): ReactElement | null {
  const { t, formatDateTime } = useI18n();
  const { project, etag, access, user, lookups, reload, notify } = useWorkspace();
  const [editing, setEditing] = useState(false);
  const load = useCallback(
    (signal: AbortSignal) => followUpApi.notes(project.id, signal),
    [project.id],
  );
  const notes = useApiResource(load);
  const authorName = usePersonNames((notes.data?.items ?? []).map((note) => note.recordedBy));
  const ownerName = usePersonNames([project.followUpOwnerUserId ?? null]);

  if (!FOLLOWED_UP.includes(project.status) && project.status !== 'CLOSED') {
    return null;
  }

  const followUp = project.followUp;
  const indicators = project.indicators;
  const none = t('projects.followUp.notAvailable');
  const percent = (value: number | null | undefined) =>
    value === null || value === undefined ? (
      <span className="value--unavailable">{none}</span>
    ) : (
      <span dir="ltr">{t('projects.followUp.percent', { value: formatPoints(value) })}</span>
    );
  const mayEdit =
    FOLLOWED_UP.includes(project.status) &&
    isInternal(user) &&
    access.permissions.can('PROJECT_FOLLOW_UP');

  return (
    <section className="section" aria-labelledby="follow-up-heading">
      <div className="section__header">
        <h2 id="follow-up-heading">
          {t('projects.followUp.title')} <ImportedLabel batchId={project.projectImportBatchId} />
        </h2>
        {mayEdit && (
          <button
            type="button"
            className="button"
            onClick={() => {
              setEditing(true);
            }}
          >
            {t('projects.followUp.edit')}
          </button>
        )}
      </div>
      {indicators !== undefined && indicators.followUpReasons.length > 0 && (
        <p className="notice notice--warning" role="status">
          {t('projects.followUp.reasons')}{' '}
          {indicators.followUpReasons.map((reason) => (
            <StatusBadge
              key={reason}
              label={t(`projects.followUpReason.${reason}`)}
              tone="warning"
            />
          ))}
        </p>
      )}
      <dl className="details">
        <Detail term={t('projects.followUp.onPlan')}>
          <OnPlanBadge status={indicators?.onPlan} />
        </Detail>
        <Detail term={t('projects.followUp.gap')}>
          <GapValue points={indicators?.gapPoints} />
        </Detail>
        <Detail term={t('projects.followUp.daysRemaining')}>
          {indicators?.daysRemaining === null || indicators?.daysRemaining === undefined
            ? none
            : t('projects.followUp.daysValue', { days: indicators.daysRemaining })}
        </Detail>
        <Detail term={t('projects.followUp.declaredPlanned')}>
          {percent(followUp?.declaredPlannedPercent)}
        </Detail>
        <Detail term={t('projects.followUp.declaredActual')}>
          {percent(followUp?.declaredActualPercent)}
          {followUp?.declaredAsOf !== null &&
            followUp?.declaredAsOf !== undefined &&
            followUp.declaredSource !== null && (
              <span className="details__aside">
                {t('projects.followUp.declaredFrom', {
                  date: followUp.declaredAsOf,
                  source: t(`projects.followUp.source.${followUp.declaredSource}`),
                })}
              </span>
            )}
        </Detail>
        <Detail term={t('projects.followUp.declaredPlannedSpend')}>
          {percent(followUp?.declaredPlannedSpendPercent)}
        </Detail>
        <Detail term={t('projects.followUp.declaredActualSpend')}>
          {percent(followUp?.declaredActualSpendPercent)}
        </Detail>
        <Detail term={t('projects.followUp.obstacle')}>
          {followUp?.currentObstacle?.text ?? (
            <span className="value--unavailable">{t('common.values.none')}</span>
          )}
        </Detail>
        <Detail term={t('projects.followUp.nextAction')}>
          {followUp?.nextAction?.text ?? (
            <span className="value--unavailable">{t('common.values.none')}</span>
          )}
          {followUp?.nextActionOwner !== null && followUp?.nextActionOwner !== undefined && (
            <span className="details__aside">{followUp.nextActionOwner}</span>
          )}
          {followUp?.nextActionDueDate !== null && followUp?.nextActionDueDate !== undefined && (
            <span className="details__aside">
              <span dir="ltr">{followUp.nextActionDueDate}</span>{' '}
              {indicators?.actionDue === 'OVERDUE' && (
                <StatusBadge label={t('projects.followUp.actionOverdue')} tone="negative" />
              )}
            </span>
          )}
        </Detail>
        <Detail term={t('projects.followUp.compliance')}>
          {indicators?.entityUpdates?.effective === null ||
          indicators?.entityUpdates?.effective === undefined
            ? none
            : t(
                indicators.entityUpdates.effective
                  ? 'projects.followUp.yes'
                  : 'projects.followUp.no',
              )}
          {indicators?.entityUpdates !== null && indicators?.entityUpdates !== undefined && (
            <span className="details__aside">
              {indicators.entityUpdates.counted === 0
                ? t('projects.followUp.complianceNoRequests')
                : t('projects.followUp.complianceCounted', {
                    onTime: indicators.entityUpdates.onTime,
                    counted: indicators.entityUpdates.counted,
                    percent: formatPoints(indicators.entityUpdates.onTimePercent ?? 0),
                  })}
              {indicators.entityUpdates.overridden &&
                ` · ${t('projects.followUp.complianceOverridden')}`}
            </span>
          )}
        </Detail>
        <Detail term={t('projects.fields.strategicPortfolio')}>
          {lookups.itemLabel(project.strategicPortfolioItemId ?? null) ?? t('common.values.none')}
          {project.strategicInitiativeItemId !== null &&
            project.strategicInitiativeItemId !== undefined && (
              <span className="details__aside">
                {lookups.itemLabel(project.strategicInitiativeItemId)}
              </span>
            )}
        </Detail>
        <Detail term={t('projects.fields.followUpOwner')}>
          {project.followUpOwnerUserId === null || project.followUpOwnerUserId === undefined
            ? t('common.values.none')
            : ownerName(project.followUpOwnerUserId)}
        </Detail>
        {project.legacyReference !== null && project.legacyReference !== undefined && (
          <Detail term={t('projects.fields.legacyReference')}>
            <span dir="ltr">{project.legacyReference}</span>
          </Detail>
        )}
      </dl>
      <h3>{t('projects.followUp.notes')}</h3>
      {notes.data?.items.length === 0 && <p>{t('projects.followUp.noNotes')}</p>}
      {notes.data !== undefined && notes.data.items.length > 0 && (
        <ul className="notes">
          {notes.data.items.map((note) => (
            <li key={note.id}>
              <p dir="auto">{note.note.text}</p>
              <p className="details__aside">
                {t('projects.followUp.noteBy', {
                  name: authorName(note.recordedBy),
                  date: formatDateTime(note.recordedAt),
                })}
              </p>
            </li>
          ))}
        </ul>
      )}
      <FollowUpDialog
        open={editing}
        project={project}
        etag={etag}
        onClose={() => {
          setEditing(false);
        }}
        onSaved={() => {
          setEditing(false);
          notify({ tone: 'success', message: t('projects.followUp.saved') });
          reload();
          notes.reload();
        }}
      />
    </section>
  );
}
